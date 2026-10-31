#include "WiiUAchievementsHost.h"
#include "WiiUAchievementsPacket.h"

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "Cafe/CafeSystem.h"
#include "Cafe/HW/MMU/MMU.h"
#include <Windows.h>
#include <metahost.h>
#include <mscoree.h>
#include <atomic>
#include <chrono>
#include <cstring>
#include <cwchar>
#include <filesystem>
#include <mutex>
#include <string>

namespace WiiUAchievements
{
    namespace
    {
        using Callback = int (__cdecl *)(int command, const void* bytes, int length);
        using Clock = std::chrono::steady_clock;
        constexpr std::uint64_t SupportedTitle = 0x0005000010143600ull;
        constexpr std::uint32_t SupportedRpx = 0xcdc68acd;
        constexpr std::uint32_t StageAddress = 0x104741e4;
        constexpr std::uint32_t PlayersAddress = 0x10474bdc;
        constexpr std::uint32_t SaveAddress = 0x145b7b80;
        constexpr auto Cadence = std::chrono::milliseconds(100);

        // The lifecycle mutex prevents a capture racing title-memory teardown.
        // The managed callback only copies one bounded packet into its mailbox.
        std::mutex s_mutex;
        std::atomic<Callback> s_callback{nullptr};
        const auto s_clockOrigin = Clock::now();
        auto s_lastSample = Clock::time_point::min();
        std::uint64_t s_generation = 0;
        std::uint64_t s_lastPacketMilliseconds = 0;
        bool s_active = false;
        bool s_paused = false;
        std::atomic_bool s_shutdown{false};
        bool s_initializationAttempted = false; // GUI-thread-owned
        std::wstring s_error;

        std::uint64_t Milliseconds()
        {
            const auto elapsed = static_cast<std::uint64_t>(
                std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now() - s_clockOrigin).count());
            // Lifecycle boundaries and an immediate first frame can share one
            // host millisecond. Keep transport ordering strictly increasing.
            s_lastPacketMilliseconds = (elapsed > s_lastPacketMilliseconds) ? elapsed : s_lastPacketMilliseconds + 1;
            return s_lastPacketMilliseconds;
        }

        void __cdecl Register(Callback callback)
        {
            s_callback.store(callback, std::memory_order_release);
        }

        void SendStateLocked(std::uint32_t state)
        {
            auto callback = s_callback.load(std::memory_order_acquire);
            if (!callback || s_shutdown)
                return;
            Packet packet;
            packet.state = state;
            packet.generation = s_generation;
            packet.milliseconds = Milliseconds();
            if (s_active)
            {
                packet.titleId = SupportedTitle;
                packet.rpxHash = SupportedRpx;
            }
            if (callback(1, &packet, sizeof(packet)) < 0)
                s_callback.store(nullptr, std::memory_order_release);
        }

        std::filesystem::path ManagedAssemblyPath()
        {
            std::wstring path(32768, L'\0');
            DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
            if (!length || length >= path.size())
                return {};
            path.resize(length);
            return std::filesystem::path(path).parent_path() / L"WiiUAchievements" / L"WiiUAchievements.exe";
        }

        bool StartManagedRuntime(const std::filesystem::path& assembly)
        {
            // Use the installed .NET Framework v4 runtime, not .NET/CoreCLR.
            // mscoree/runtime intentionally stay loaded for the Cemu lifetime.
            HMODULE shim = LoadLibraryExW(L"mscoree.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
            if (!shim)
                return false;
            auto create = reinterpret_cast<HRESULT (STDAPICALLTYPE *)(REFCLSID, REFIID, LPVOID*)>(
                GetProcAddress(shim, "CLRCreateInstance"));
            if (!create)
                return false;

            ICLRMetaHost* meta = nullptr;
            ICLRRuntimeInfo* info = nullptr;
            ICLRRuntimeHost* host = nullptr;
            HRESULT hr = create(CLSID_CLRMetaHost, IID_ICLRMetaHost, reinterpret_cast<void**>(&meta));
            if (SUCCEEDED(hr))
                hr = meta->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, reinterpret_cast<void**>(&info));
            if (SUCCEEDED(hr))
                hr = info->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, reinterpret_cast<void**>(&host));
            if (SUCCEEDED(hr))
                hr = host->Start();

            DWORD result = 1;
            if (SUCCEEDED(hr))
            {
                wchar_t argument[32]{};
                swprintf_s(argument, L"%llX", static_cast<unsigned long long>(reinterpret_cast<std::uintptr_t>(&Register)));
                hr = host->ExecuteInDefaultAppDomain(assembly.c_str(), L"WiiUAchievements.NativeCemuBridge",
                    L"Initialize", argument, &result);
            }
            if (host) host->Release();
            if (info) info->Release();
            if (meta) meta->Release();

            if (FAILED(hr) || result != 0 || !s_callback.load(std::memory_order_acquire))
            {
                s_callback.store(nullptr, std::memory_order_release);
                wchar_t message[160]{};
                swprintf_s(message, L"Impossibile avviare Wii U Achievements (HRESULT 0x%08lX, codice %lu).",
                    static_cast<unsigned long>(hr), static_cast<unsigned long>(result));
                s_error = message;
                return false;
            }
            return true;
        }
    }

    void Initialize()
    {
        if (s_initializationAttempted)
            return;
        s_initializationAttempted = true;
        const auto assembly = ManagedAssemblyPath();
        std::error_code pathError;
        if (assembly.empty() || !std::filesystem::is_regular_file(assembly, pathError))
        {
            s_error = L"Modulo mancante: copia la cartella WiiUAchievements accanto all'eseguibile Cemu del fork.";
            return;
        }
        if (!StartManagedRuntime(assembly) && s_error.empty())
            s_error = L".NET Framework 4.x non disponibile: il modulo achievement non e stato avviato.";
    }

    void Show()
    {
        Initialize();
        Callback callback = nullptr;
        {
            std::scoped_lock lock(s_mutex);
            if (!s_shutdown.load(std::memory_order_acquire))
                callback = s_callback.load(std::memory_order_acquire);
        }
        // GUI callbacks and modal diagnostics must never hold the capture
        // mutex: a dismissed dialog must not be needed to advance emulation.
        // The managed delegate is statically rooted until process exit.
        if (callback)
            callback(2, nullptr, 0);
        else
            MessageBoxW(nullptr, s_error.empty() ? L"Wii U Achievements non disponibile." : s_error.c_str(),
                L"Wii U Achievements", MB_OK | MB_ICONINFORMATION);
    }

    bool IsAvailable() noexcept
    {
        return !s_shutdown.load(std::memory_order_acquire) &&
            s_callback.load(std::memory_order_acquire) != nullptr;
    }

    void TitleStarted()
    {
        std::scoped_lock lock(s_mutex);
        if (s_shutdown)
            return;
        ++s_generation;
        s_paused = false;
        // Require both base and actually loaded RPX hashes, excluding updates.
        s_active = CafeSystem::GetForegroundTitleId() == SupportedTitle &&
            CafeSystem::GetForegroundTitleVersion() == 0 &&
            CafeSystem::GetRPXHashBase() == SupportedRpx &&
            CafeSystem::GetRPXHashUpdated() == SupportedRpx;
        s_lastSample = Clock::time_point::min();
        SendStateLocked(0); // clear prior engine state before any game frame
    }

    void TitleStopped()
    {
        std::scoped_lock lock(s_mutex);
        ++s_generation;
        s_active = false;
        s_paused = false;
        SendStateLocked(0);
    }

    void SetPaused(bool paused)
    {
        std::scoped_lock lock(s_mutex);
        if (!s_active || s_paused == paused)
            return;
        s_paused = paused;
        ++s_generation;
        s_lastSample = Clock::time_point::min();
        SendStateLocked(paused ? 2 : 0);
    }

    void FrameSubmitted()
    {
        if (!s_callback.load(std::memory_order_acquire))
            return;
        std::scoped_lock lock(s_mutex);
        auto callback = s_callback.load(std::memory_order_acquire);
        if (!callback || !s_active || s_paused || s_shutdown)
            return;
        const auto now = Clock::now();
        if (s_lastSample != Clock::time_point::min() && now - s_lastSample < Cadence)
            return;
        s_lastSample = now;

        Packet packet;
        packet.generation = s_generation;
        packet.milliseconds = Milliseconds();
        packet.titleId = SupportedTitle;
        packet.rpxHash = SupportedRpx;
        // Check mapping metadata before obtaining any guest pointer. These
        // ranges are read only while title teardown is excluded by s_mutex.
        if (!memory_isAddressRangeAccessible(StageAddress, sizeof(packet.beforeStage)) ||
            !memory_isAddressRangeAccessible(PlayersAddress, sizeof(packet.beforePlayers)) ||
            !memory_isAddressRangeAccessible(SaveAddress, sizeof(packet.save)))
        {
            SendStateLocked(0);
            return;
        }

        packet.state = 1;
        std::memcpy(packet.beforeStage, memory_getPointerFromVirtualOffset(StageAddress), sizeof(packet.beforeStage));
        std::memcpy(packet.beforePlayers, memory_getPointerFromVirtualOffset(PlayersAddress), sizeof(packet.beforePlayers));
        std::memcpy(packet.save, memory_getPointerFromVirtualOffset(SaveAddress), sizeof(packet.save));
        std::memcpy(packet.afterStage, memory_getPointerFromVirtualOffset(StageAddress), sizeof(packet.afterStage));
        std::memcpy(packet.afterPlayers, memory_getPointerFromVirtualOffset(PlayersAddress), sizeof(packet.afterPlayers));
        // Other PPC cores may still run. The managed adapter compares both
        // guards and rejects changed context; this is not a global CPU stop.
        if (callback(1, &packet, sizeof(packet)) < 0)
            s_callback.store(nullptr, std::memory_order_release);
    }

    void Shutdown()
    {
        std::scoped_lock lock(s_mutex);
        if (s_shutdown)
            return;
        ++s_generation;
        s_active = false;
        SendStateLocked(0);
        s_shutdown = true;
        auto callback = s_callback.exchange(nullptr, std::memory_order_acq_rel);
        if (callback)
            callback(3, nullptr, 0);
        // Never unload CLR or revoke reverse-P/Invoke delegates while managed
        // work may still be finishing on its independent STA thread.
    }
}
#else
namespace WiiUAchievements
{
    void Initialize() {}
    bool IsAvailable() noexcept { return false; }
    void Show() {}
    void Shutdown() {}
    void TitleStarted() {}
    void TitleStopped() {}
    void SetPaused(bool) {}
    void FrameSubmitted() {}
}
#endif
