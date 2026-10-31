using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WiiUAchievements
{
    // All release folders use one per-Windows-user archive. Explicit test roots
    // bypass discovery and never touch the player's shared data.
    public static class UserData
    {
        public static string DefaultDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiiUAchievements"); }
        }
        public static string[] LegacyDirectories(string applicationDirectory)
        {
            var result = new List<string> { Path.Combine(Path.GetFullPath(applicationDirectory), "data") };
            var parent = new DirectoryInfo(applicationDirectory);
            for (int depth = 0; parent != null && depth < 6; depth++, parent = parent.Parent)
            {
                if (!File.Exists(Path.Combine(parent.FullName, "HANDOFF.md")) || !File.Exists(Path.Combine(parent.FullName, "build.ps1"))) continue;
                result.Add(Path.Combine(parent.FullName, "data"));
                string builds = Path.Combine(parent.FullName, "build-check");
                if (Directory.Exists(builds))
                {
                    result.Add(Path.Combine(builds, "data"));
                    // Retired builds are moved to build-check/archive/ and still count as sources.
                    string archive = Path.Combine(builds, "archive");
                    var candidates = Directory.GetDirectories(builds).Concat(Directory.Exists(archive) ? Directory.GetDirectories(archive) : new string[0]);
                    foreach (string build in candidates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                        if (File.Exists(Path.Combine(build, "WiiUAchievements.exe"))) result.Add(Path.Combine(build, "data"));
                }
                break;
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public static void ImportSettings(string destination, IEnumerable<string> legacyDirectories)
        {
            Directory.CreateDirectory(destination);
            // Never replace a shared account/settings file or copy credential backups.
            foreach (string name in new[] { "settings.xml", "cloud.xml" })
            {
                string target = Path.Combine(destination, name);
                if (File.Exists(target)) continue;
                foreach (string directory in legacyDirectories)
                {
                    string source = Path.Combine(directory, name);
                    if (!File.Exists(source)) continue;
                    File.Copy(source, target, false);
                    break;
                }
            }
        }
        public static int ImportProgress(ProgressStore target, IEnumerable<string> legacyDirectories, IEnumerable<string> allowedIds)
        {
            int count = target.Snapshot().Length;
            foreach (string directory in legacyDirectories) target.ImportLegacy(Path.Combine(directory, "progress"), allowedIds);
            return target.Snapshot().Length - count;
        }
    }
}
