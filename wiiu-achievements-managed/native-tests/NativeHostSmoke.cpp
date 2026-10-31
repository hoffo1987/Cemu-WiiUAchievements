#include "WiiUAchievementsHost.h"
#include "WiiUAchievementsPacket.h"
#include "Cafe/CafeSystem.h"
#include "Cafe/HW/MMU/MMU.h"
#include <Windows.h>
#include <chrono>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <stdexcept>
#include <string>
#include <thread>

namespace
{
    using Clock = std::chrono::steady_clock;
    std::filesystem::path UserData()
    {
        wchar_t path[32768]{};
        const auto length = GetEnvironmentVariableW(L"LOCALAPPDATA", path, 32768);
        if (length == 0 || length >= 32768) throw std::runtime_error("LOCALAPPDATA unavailable");
        return std::filesystem::path(path) / L"WiiUAchievements";
    }
    void Require(bool condition, const char* text)
    {
        if (!condition) throw std::runtime_error(text);
        std::cout << "PASS " << text << std::endl;
    }
    void Stage(const char* name)
    {
        std::memset(NativeSmokeMemory::stage, 0, sizeof(NativeSmokeMemory::stage));
        std::memcpy(NativeSmokeMemory::stage, name, std::strlen(name));
        std::memcpy(NativeSmokeMemory::stage + 12, name, std::strlen(name));
    }
    void Frames(int milliseconds)
    {
        const auto end = Clock::now() + std::chrono::milliseconds(milliseconds);
        do
        {
            WiiUAchievements::FrameSubmitted();
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        } while (Clock::now() < end);
    }
    int Awards(const char* id)
    {
        const auto directory = UserData() / L"progress";
        if (!std::filesystem::is_directory(directory)) return 0;
        int result = 0;
        const std::string needle = std::string("id=\"") + id + "\"";
        for (const auto& entry : std::filesystem::directory_iterator(directory))
        {
            if (entry.path().extension() != L".xml") continue;
            std::ifstream file(entry.path(), std::ios::binary);
            const std::string text((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
            std::size_t position = 0;
            while ((position = text.find(needle, position)) != std::string::npos)
            { ++result; position += needle.size(); }
        }
        return result;
    }
}

int main()
{
    try
    {
        // The runner script permits this executable only in a clean CI account.
        Require(!std::filesystem::exists(UserData()), "clean CI user archive");
        WiiUAchievements::Initialize();
        Require(WiiUAchievements::IsAvailable(), "real CLR loads the managed achievement module");
        const auto readyDeadline = Clock::now() + std::chrono::seconds(15);
        while (!std::filesystem::is_regular_file(UserData() / L"settings.xml") && Clock::now() < readyDeadline)
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        Require(std::filesystem::is_regular_file(UserData() / L"settings.xml"), "managed hidden UI and progress engine started");

        NativeSmokeMemory::save[1] = 12;
        NativeSmokeMemory::save[3] = 12;
        NativeSmokeMemory::save[5] = 10;
        NativeSmokeMemory::players[0] = NativeSmokeMemory::players[8] = 0x20;
        NativeSmokeMemory::players[2] = NativeSmokeMemory::players[10] = 0x10;
        Stage("Name");
        WiiUAchievements::TitleStarted();
        Frames(1500);
        Require(Awards("wallet-10") == 0, "file-select frames do not award");

        Stage("sea");
        Frames(350);
        Require(Awards("wallet-10") == 0, "ready frames require stabilization");
        WiiUAchievements::SetPaused(true);
        Frames(1200);
        Require(Awards("wallet-10") == 0, "native pause prevents frozen-frame awards");
        WiiUAchievements::SetPaused(false);
        Frames(350);
        Require(Awards("wallet-10") == 0, "resume starts a fresh stability window");
        Frames(2500);
        Require(Awards("wallet-10") == 1, "native frame callback reaches real engine and durable progress");

        WiiUAchievements::TitleStopped();
        NativeSmokeMemory::save[5] = 100;
        Frames(1600);
        Require(Awards("wallet-100") == 0, "title-stop blocks later memory observations");
        CafeSystem::testTitleVersion = 1;
        WiiUAchievements::TitleStarted();
        Frames(1600);
        Require(Awards("wallet-100") == 0, "unsupported title update is rejected");
        CafeSystem::testTitleVersion = 0;
        WiiUAchievements::TitleStarted();
        Frames(2500);
        Require(Awards("wallet-100") == 1 && Awards("wallet-10") == 1, "new supported lifecycle resumes without duplicate awards");
        WiiUAchievements::Shutdown();
        Require(!WiiUAchievements::IsAvailable(), "shutdown unregisters callbacks");
        std::this_thread::sleep_for(std::chrono::milliseconds(300));
        std::cout << "Native CLR smoke checks passed (synthetic memory only)." << std::endl;
        return 0;
    }
    catch (const std::exception& error)
    {
        WiiUAchievements::Shutdown();
        std::cerr << "FAIL " << error.what() << std::endl;
        return 1;
    }
}
