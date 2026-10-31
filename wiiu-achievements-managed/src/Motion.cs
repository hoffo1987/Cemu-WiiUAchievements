using System;
using System.Runtime.InteropServices;

namespace WiiUAchievements
{
    // Reduced motion: follows the Windows "Show animations" setting unless the user chooses in
    // Settings. When on, panels fade instead of sliding/zooming, the grid appears without a
    // staggered entrance and unlocks keep their glow and colour change without flips or bursts.
    public static class Motion
    {
        public static bool Reduced;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(int action, int parameter, ref bool value, int flags);
        const int SPI_GETCLIENTAREAANIMATION = 0x1042;

        public static bool SystemPrefersReduced()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
            try
            {
                bool animations = true;
                return SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref animations, 0) && !animations;
            }
            catch (Exception) { return false; }
        }
    }
}
