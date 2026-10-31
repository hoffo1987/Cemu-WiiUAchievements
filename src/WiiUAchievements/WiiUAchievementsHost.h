#pragma once

// This extension consumes guest state only. It never writes guest memory,
// opens another process, patches game instructions or uses EmuHook.
namespace WiiUAchievements
{
    // GUI thread only. A missing managed module leaves ordinary Cemu usable.
    void Initialize();
    // Reports successful CLR callback registration without opening a dialog.
    bool IsAvailable() noexcept;
    void Show();
    void Shutdown();

    // Cemu title lifecycle and PPC GX2SwapScanBuffers entry points.
    void TitleStarted();
    void TitleStopped();
    void SetPaused(bool paused);
    void FrameSubmitted();
}
