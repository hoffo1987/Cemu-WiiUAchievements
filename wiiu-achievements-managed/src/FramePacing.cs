using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace WiiUAchievements
{
    // Frame pacing for animations. A WinForms timer cannot tick faster than 10 ms and runs on a
    // 15.6 ms system clock: on a 60 Hz display updates beat against the refresh, and on 120/144 Hz
    // frames alternate between two and three refreshes. Both read as micro-stutter. Instead, while
    // something moves and no input is waiting, the window paints a frame and then waits for the
    // start of the next display refresh, read from the compositor's timing (refresh period and
    // last vertical blank). The wait never exceeds one refresh; DwmFlush is not used because it
    // can block for hundreds of milliseconds when the compositor skips a frame. The timer stays
    // as a fallback and as a slow poll for the banner video.
    static class FramePacing
    {
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
        [DllImport("dwmapi.dll")] static extern int DwmGetCompositionTimingInfo(IntPtr window, IntPtr timing);
        [StructLayout(LayoutKind.Sequential)] struct NativeMessage { public IntPtr Window; public uint Message; public IntPtr WParam, LParam; public uint Time; public int X, Y; }
        [DllImport("user32.dll")] static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint first, uint last, uint remove);

        // DWM_TIMING_INFO is byte-packed: 292 bytes, refresh period (QPC ticks) at offset 12 and
        // the last vertical blank (QPC time) at offset 28.
        const int TimingSize = 292, PeriodOffset = 12, VBlankOffset = 28;
        static IntPtr timing = IntPtr.Zero;
        static bool fine, noTiming;
        static readonly bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        static long lastWake;

        // Fine timer resolution only while something moves; released as soon as the app is idle.
        public static void Begin()
        {
            if (fine || !windows) return;
            try { fine = timeBeginPeriod(1) == 0; } catch (Exception) { fine = false; }
        }
        public static void End()
        {
            if (!fine) return;
            try { timeEndPeriod(1); } catch (Exception) { }
            fine = false;
        }
        public static bool Available { get { return windows; } }
        // True when input or other messages wait: the frame loop yields to them.
        public static bool MessagePending()
        {
            if (!windows) return true;
            NativeMessage message;
            try { return PeekMessage(out message, IntPtr.Zero, 0, 0, 0); } catch (Exception) { return true; }
        }
        static bool Refresh(out long period, out long vblank)
        {
            period = vblank = 0;
            if (noTiming) return false;
            try
            {
                if (timing == IntPtr.Zero) timing = Marshal.AllocHGlobal(TimingSize);
                for (int i = 0; i < TimingSize; i += 4) Marshal.WriteInt32(timing, i, 0);
                Marshal.WriteInt32(timing, 0, TimingSize);
                if (DwmGetCompositionTimingInfo(IntPtr.Zero, timing) != 0) return false;
                period = Marshal.ReadInt64(timing, PeriodOffset);
                vblank = Marshal.ReadInt64(timing, VBlankOffset);
                // Plausible refresh: 30-500 Hz.
                return period >= Stopwatch.Frequency / 500 && period <= Stopwatch.Frequency / 30 && vblank > 0;
            }
            catch (Exception) { noTiming = true; return false; }
        }
        // Waits until just after the next display refresh. Returns false if no timing is known
        // (the caller then relies on its timer).
        public static bool WaitForFrame()
        {
            if (!windows) return false;
            long period, vblank, now = Stopwatch.GetTimestamp();
            long target;
            if (Refresh(out period, out vblank))
            {
                long next = now < vblank ? vblank : vblank + ((now - vblank) / period + 1) * period;
                target = next + Stopwatch.Frequency / 2000; // half a millisecond into the refresh
            }
            else
            {
                // No compositor timing: keep a steady 60 Hz from the previous wake-up.
                period = Stopwatch.Frequency / 60;
                target = Math.Max(now, lastWake + period);
            }
            // Sleep most of the wait (1 ms timer resolution while animating), spin the rest.
            while (true)
            {
                long remaining = target - Stopwatch.GetTimestamp();
                if (remaining <= 0) break;
                double ms = remaining * 1000.0 / Stopwatch.Frequency;
                if (ms > 1.6) Thread.Sleep((int)(ms - 1.1)); else Thread.SpinWait(40);
            }
            lastWake = Stopwatch.GetTimestamp();
            return true;
        }
        public static void Reset() { }
    }
}
