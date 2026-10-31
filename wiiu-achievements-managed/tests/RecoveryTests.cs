using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using WiiUAchievements;

static class RecoveryTests
{
    static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static GameSnapshot Loaded(int frame, GameplayState state = GameplayState.Ready, int rupees = 137)
    {
        var sample = new GameSnapshot { Utc = Start.AddMilliseconds(frame * 100), Rupees = rupees,
            Health = 12, MaxHealth = 12, HeroClothes = 1,
            Context = new GameplayContext(state, state == GameplayState.Menu ? "Name" : "sea", 0x20001000) };
        sample.SetProgression("telescope", 1); return sample;
    }
    static List<Achievement> Rules()
    {
        return new List<Achievement> {
            new Achievement { Id = "story-heros-clothes", Field = "hero_clothes", Threshold = 1 },
            new Achievement { Id = "story-telescope", Field = "telescope", Threshold = 1, Experimental = true },
            new Achievement { Id = "wallet-100", Field = "rupees", Threshold = 100 },
            new Achievement { Id = "pending", Requirement = "Not implemented" }
        };
    }
    public static void Run(Action<bool, string> check, string temporary)
    {
        string root = Path.Combine(temporary, "recovery");
        string repo = Path.Combine(root, "repo"), old = Path.Combine(repo, "build-check", "old");
        Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(repo, "HANDOFF.md"), "fixture");
        File.WriteAllText(Path.Combine(repo, "build.ps1"), "fixture");
        File.WriteAllText(Path.Combine(old, "WiiUAchievements.exe"), "fixture, never executed");
        string retired = Path.Combine(repo, "build-check", "archive", "2026-10-28-ui");
        Directory.CreateDirectory(retired);
        File.WriteAllText(Path.Combine(retired, "WiiUAchievements.exe"), "fixture, never executed");
        var directories = UserData.LegacyDirectories(old);
        check(directories[0] == Path.Combine(old, "data") && directories.Contains(Path.Combine(repo, "data")), "Migration prefers active legacy build and discovers the source archive");
        check(directories.Contains(Path.Combine(retired, "data")) && directories.Distinct(StringComparer.OrdinalIgnoreCase).Count() == directories.Length,
            "Builds moved to build-check/archive remain legacy progress sources");
        check(Path.IsPathRooted(UserData.DefaultDirectory) && !UserData.DefaultDirectory.StartsWith(repo, StringComparison.OrdinalIgnoreCase), "Production archive lives outside version-specific build folders");
        string legacy = Path.Combine(old, "data"), shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.xml"), "<settings><player>original</player></settings>");
        File.WriteAllText(Path.Combine(legacy, "cloud.xml"), "<cloud><email>synthetic@example.invalid</email></cloud>");
        File.WriteAllText(Path.Combine(legacy, "cloud.xml.bak"), "must not migrate");
        UserData.ImportSettings(shared, directories);
        check(File.ReadAllText(Path.Combine(shared, "settings.xml")).Contains("original") && File.Exists(Path.Combine(shared, "cloud.xml")) && !File.Exists(Path.Combine(shared, "cloud.xml.bak")), "Settings/account migrate without credential backups");
        File.WriteAllText(Path.Combine(shared, "settings.xml"), "<settings><player>current</player></settings>");
        UserData.ImportSettings(shared, directories);
        check(File.ReadAllText(Path.Combine(shared, "settings.xml")).Contains("current"), "Later builds cannot replace existing shared settings/account");
        string fingerprint = "0005000010143600|v0|cdc68acd";
        var donor = new ProgressStore(Path.Combine(legacy, "progress"), "player", fingerprint);
        donor.Award("story-heros-clothes", Start); donor.Award("story-telescope", Start.AddMinutes(1));
        donor.Award("wallet-100", Start.AddMinutes(2)); donor.Award("unknown", Start);
        string donorPath = Directory.GetFiles(Path.Combine(legacy, "progress"), "*.xml").Single();
        string before = File.ReadAllText(donorPath);
        var target = new ProgressStore(Path.Combine(shared, "progress"), "player", fingerprint);
        target.Award("wallet-100", Start.AddMinutes(-1));
        var allowed = Rules().Where(x => !x.IsPlanned).Select(x => x.Id).ToArray();
        check(UserData.ImportProgress(target, directories, allowed) == 2 && target.Contains("story-heros-clothes") && target.Contains("story-telescope"), "Historical tunic/telescope awards are recovered by exact identity");
        check(target.Get("story-heros-clothes").Utc == Start && target.Get("story-telescope").Utc == Start.AddMinutes(1) && target.Get("wallet-100").Utc == Start.AddMinutes(-1), "Migration preserves original dates and cannot overwrite an existing award");
        check(!target.Contains("unknown") && File.ReadAllText(donorPath) == before, "Migration filters unknown IDs and leaves donor files untouched");
        check(UserData.ImportProgress(target, directories, allowed) == 0, "Repeated migration is idempotent");
        var other = new ProgressStore(Path.Combine(shared, "progress"), "other player", fingerprint);
        check(UserData.ImportProgress(other, directories, allowed) == 0 && other.Snapshot().Length == 0, "Migration cannot cross local profiles");
        var revision = new ProgressStore(Path.Combine(shared, "progress"), "player", fingerprint, "different-revision");
        check(UserData.ImportProgress(revision, directories, allowed) == 0, "Migration cannot cross game/set revision identity");
        File.WriteAllText(donorPath, before.Replace("player|", "wrong-player|"));
        bool rejected = false; try { UserData.ImportProgress(target, directories, allowed); } catch (InvalidDataException) { rejected = true; }
        check(rejected && target.Snapshot().Length == 3, "Spoofed identity fails before adding any recovered entries");
        File.WriteAllText(donorPath, before);
        var reopened = new ProgressStore(Path.Combine(shared, "progress"), "player", fingerprint);
        check(reopened.Snapshot().Length == 3 && reopened.Get("story-telescope").Utc == Start.AddMinutes(1), "Recovered awards survive reopening from another application build");

        var saved = new ProgressStore(Path.Combine(root, "recognize"), "player", fingerprint);
        var engine = new AchievementEngine(Rules(), saved, AchievementRecognition.IncludeExistingSave);
        for (int i = 0; i < 20; i++) engine.Update(Loaded(i, GameplayState.Menu));
        check(saved.Snapshot().Length == 0, "Existing-save recognition never awards title/file-select data");
        for (int i = 20; i < 29; i++) engine.Update(Loaded(i));
        check(saved.Snapshot().Length == 0, "Loaded achievements wait for gameplay stabilization and consecutive confirmations");
        engine.Update(Loaded(29)); var awards = engine.Update(Loaded(30));
        check(awards.Count == 3 && saved.Contains("story-heros-clothes") && saved.Contains("story-telescope") && saved.Contains("wallet-100"), "Stable loaded tunic/telescope/137 rupees recognize all three requested objectives");
        check(awards.All(x => engine.RecognizedFromSave(x.Id)) && !saved.Contains("pending"), "Loaded recognition is identified separately and never enables pending targets");
        check(saved.Get("wallet-100").Utc == Loaded(30).Utc, "Newly recovered save conditions record observation time, not an invented acquisition date");
        check(engine.Update(Loaded(31)).Count == 0, "Existing-save recognition cannot notify twice");
        engine.Reset(); for (int i = 32; i < 46; i++) engine.Update(Loaded(i));
        check(saved.Snapshot().Length == 3 && saved.Get("wallet-100").Utc == Loaded(30).Utc, "Reconnect/reload keeps recorded achievements and dates");
        saved = new ProgressStore(Path.Combine(root, "no-context"), "player", fingerprint);
        engine = new AchievementEngine(Rules(), saved, AchievementRecognition.IncludeExistingSave);
        for (int i = 0; i < 12; i++) { var sample = Loaded(i); sample.Context = null; engine.Update(sample); }
        check(saved.Snapshot().Length == 0, "Save recognition requires an actual ready gameplay context");
        saved = new ProgressStore(Path.Combine(root, "save-glitch"), "player", fingerprint);
        engine = new AchievementEngine(new List<Achievement> { Rules()[2] }, saved, AchievementRecognition.IncludeExistingSave);
        for (int i = 0; i < 9; i++) engine.Update(Loaded(i));
        engine.Update(Loaded(9, GameplayState.Ready, 0)); engine.Update(Loaded(10));
        check(!saved.Contains("wallet-100"), "A below-threshold sample cancels loaded-condition confirmation");
        engine.Update(Loaded(11, GameplayState.Unknown));
        for (int i = 12; i < 21; i++) engine.Update(Loaded(i));
        check(!saved.Contains("wallet-100"), "An unknown-memory boundary requires a fresh stable gameplay window");
        engine.Update(Loaded(21)); engine.Update(Loaded(22));
        check(saved.Contains("wallet-100"), "Valid save recognition resumes automatically after an uncertain boundary");
    }
}
