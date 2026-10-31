using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WiiUAchievements
{
    public sealed class GameSession
    {
        public const string EurTitleId = "0005000010143600";
        public string CemuPath, LogPath, TitleId, Version, RpxHash, CemuVersion;
        public ulong GuestBase;
        public int Pid;
        public string Fingerprint { get { return TitleId + "|" + Version + "|" + RpxHash; } }
        public static GameSession FromLog(string cemuPath, int pid)
        {
            var result = new GameSession { CemuPath = cemuPath, Pid = pid };
            string[] locations = {
                Path.Combine(Path.GetDirectoryName(cemuPath), "log.txt"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cemu", "log.txt"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cemu", "log.txt") };
            foreach (var path in locations)
            {
                if (!File.Exists(path)) continue;
                string text;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
                // Parse only the final "TitleId" entry: Cemu can switch titles in one process.
                var ids = Regex.Matches(text, @"TitleId\s*:\s*([0-9a-fA-F]{8})[- ]?([0-9a-fA-F]{8})", RegexOptions.IgnoreCase);
                if (ids.Count == 0) continue;
                var id = ids[ids.Count - 1];
                var mounts = Regex.Matches(text, @"Mounting title\s+([0-9a-fA-F]{16})", RegexOptions.IgnoreCase);
                if (mounts.Count > 0 && mounts[mounts.Count - 1].Index > id.Index)
                    throw new InvalidDataException("Cemu sta caricando un titolo. Attendi che il caricamento sia terminato.");
                string tail = text.Substring(id.Index);
                result.CemuVersion = Match(text, @"Init Cemu\s+([0-9.]+)");
                result.TitleId = (id.Groups[1].Value + id.Groups[2].Value).ToLowerInvariant();
                result.Version = Match(tail, @"TitleVersion\s*:\s*([^\r\n]+)");
                result.RpxHash = Match(tail, @"RPX hash[^:\r\n]*:\s*([0-9a-fA-F]+)");
                result.LogPath = path;
                string baseText = Match(text, @"Init Wii U memory space\s*\(base:\s*0x([0-9a-fA-F]+)\)");
                if (baseText == "unknown") throw new InvalidDataException("Base di memoria assente dal log di Cemu.");
                result.GuestBase = Convert.ToUInt64(baseText, 16);
                if (File.GetLastWriteTimeUtc(path) < Process.GetProcessById(pid).StartTime.ToUniversalTime().AddSeconds(-2))
                    throw new InvalidDataException("Il log non appartiene alla sessione attuale di Cemu.");
                return result;
            }
            throw new InvalidDataException("Log attivo di Cemu non trovato. Seleziona la cartella corretta oppure controlla log.txt.");
        }
        static string Match(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "unknown";
        }
    }
}
