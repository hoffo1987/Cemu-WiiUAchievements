using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using WiiUAchievements;

static class ProgressionTests
{
    static byte[] Empty()
    {
        var bytes = new byte[WindWakerProgression.ReadSize]; bytes[1] = 12; bytes[3] = 12;
        for (int i = 0x4A; i <= 0x4D; i++) bytes[i] = 255;
        return bytes;
    }
    static GameSnapshot At(byte[] bytes, int frame)
    {
        var s = WindWakerProgression.Parse(bytes);
        s.Utc = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(100 * frame); return s;
    }
    static bool Rejects(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } catch (InvalidDataException) { return true; } }
    public static void Run(Action<bool, string> check, string root)
    {
        var b = Empty();
        check(At(b, 0).Value("telescope") == 0 && At(b, 0).Value("bottle_count") == 0, "A reconstructed initial save decodes known absence separately from unavailable data");
        b[0x51] = 5; b[0xB4] = 15; b[0xB5] = 3;
        check(At(b, 0).Value("telescope") == 1 && At(b, 0).Value("hero_sword") == 1 && At(b, 0).Value("master_sword") == 1 && At(b, 0).Value("mirror_shield") == 1,
            "Cumulative inventory bits preserve early equipment after upgrades");
        b[0x5D] = 1; var bow = At(b, 0); b[0x5D] = 3; var elemental = At(b, 1); b[0x5D] = 7; var light = At(b, 2);
        check(bow.Value("bow") == 1 && bow.Value("elemental_arrows") == 0 && elemental.Value("elemental_arrows") == 1 && elemental.Value("light_arrows") == 0 && light.Value("light_arrows") == 1,
            "Bow, elemental arrows and light arrows are separate permanent upgrades");
        b[0x5D] = 255;
        check(!At(b, 0).HasValue("bow"), "Unexpected inventory flag bytes become unavailable, never a completed objective");
        b = Empty(); b[0xBD] = 32;
        check(At(b, 0).Value("song_count") == 1 && At(b, 0).Value("song_passing") == 1 && At(b, 0).Value("winds_requiem") == 0,
            "The highest song bit counts as one song, not six or thirty-two");
        b[0xBD] = 63; b[0xBE] = 128;
        check(At(b, 0).Value("song_count") == 6 && At(b, 0).Value("triforce_count") == 1, "Full song set and sparse Triforce masks use population counts");
        b = Empty(); b[0xBF] = 2;
        check(At(b, 0).Value("gohma_and_din") == 0 && At(b, 0).Value("nayru_pearl") == 0, "A pearl alone cannot stand in for its defeated boss or a different pearl");
        b[0x380 + 3 * 0x24 + 0x21] = 8;
        check(At(b, 0).Value("gohma_and_din") == 1 && At(b, 0).Value("kalle_and_farore") == 0, "Dungeon completion selects the exact save-stage flag");
        b = Empty(); b[0x380 + 6 * 0x24 + 0x21] = 8;
        check(At(b, 0).Value("jalhalla_and_sword") == 0, "Jalhalla also requires the sword restoration, not only a boss flag");
        b[0xB4] = 7; check(At(b, 0).Value("jalhalla_and_sword") == 1 && At(b, 0).Value("molgera_and_sword") == 0, "First sword restoration cannot complete the wind temple target");
        b = Empty(); b[0x6F] = 99; b[0x70] = 60; b[0x13] = 32; b[0x12] = 2;
        check(At(b, 0).Value("arrow_capacity") == 99 && At(b, 0).Value("bomb_capacity") == 60 && At(b, 0).Value("wallet_capacity") == 5000 && At(b, 0).Value("magic_capacity") == 32,
            "Capacity decoders use maxima independently of current rupees or ammunition");
        b[0x4A] = 0x50; b[0x4B] = 0x57; b[0x4C] = 0x55; b[0x4D] = 0x56;
        check(At(b, 0).Value("bottle_count") == 4, "Four bottle slots count despite different contents");
        b[0x64E] = 0x20;
        check(At(b, 0).Value("grandma_and_soup") == 1 && At(b, 0).HeroClothes == 0, "Healing Grandma and soup do not imply the clothing bit in the same byte");
        b = Empty(); for (int i = 0; i < 49; i++) b[0x104 + i] = (byte)(i % 2 == 0 ? 1 : 3);
        check(At(b, 0).Value("sea_sectors") == 49, "All 49 fishman sectors count both observed mapped encodings");
        b[0x104] = 255; check(!At(b, 0).HasValue("sea_sectors"), "Invalid sector encodings suppress that counter");
        check(Rejects(() => WindWakerProgression.Parse(new byte[6])) && Rejects(() => At(Empty(), 0).SetProgression("invented", 1)),
            "Truncated reads and invented progression fields are rejected");
        check(!WindWakerProgression.ValidThreshold("arrow_capacity", 98) && !WindWakerProgression.ValidThreshold("magic_capacity", 31) &&
            WindWakerProgression.ValidThreshold("wallet_capacity", 5000), "Capacity conditions require attainable upgrade steps");
        var metadata = XDocument.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "achievements-story.xml"));
        metadata.Root.Elements().First(x => (string)x.Attribute("experimental") == "true").Attribute("experimental").Remove();
        string badMetadata = Path.Combine(root, "missing-experimental.xml"); metadata.Save(badMetadata);
        check(Rejects(() => AchievementSet.Load(badMetadata)), "Unverified progression mappings cannot silently lose their experimental marker");

        var rule = new Achievement { Id = "telescope", Field = "telescope", Threshold = 1, Title = "telescope", Description = "telescope", Points = 5, Experimental = true };
        var store = new ProgressStore(Path.Combine(root, "decoded-flow"), "test", "synthetic");
        var engine = new AchievementEngine(new List<Achievement> { rule }, store); b = Empty();
        for (int i = 0; i < 3; i++) engine.Update(At(b, i));
        b[0x51] = 5; engine.Update(At(b, 3)); b[0x51] = 0; engine.Update(At(b, 4));
        check(!store.Contains(rule.Id) && engine.Status(rule.Id).Phase == AchievementPhase.Stabilizing, "Permanent-state rollback clears pending awards and starts a fresh baseline");
        engine.Update(At(b, 5)); engine.Update(At(b, 6)); b[0x51] = 5;
        engine.Update(At(b, 7)); engine.Update(At(b, 8)); engine.Update(At(b, 9));
        check(new ProgressStore(Path.Combine(root, "decoded-flow"), "test", "synthetic").Contains(rule.Id), "Actual decoder output crosses the engine and persists one stable acquisition");
        var mutationStore = new ProgressStore(Path.Combine(root, "snapshot-reuse"), "test", "synthetic");
        engine = new AchievementEngine(new List<Achievement> { rule }, mutationStore); b = Empty();
        for (int i = 0; i < 3; i++) engine.Update(At(b, i));
        b[0x51] = 5; var mutable = At(b, 3); engine.Update(mutable);
        mutable.SetProgression("telescope", 0); mutable.Utc = mutable.Utc.AddMilliseconds(100); engine.Update(mutable);
        check(engine.Status(rule.Id).Phase == AchievementPhase.Stabilizing, "Reusing and mutating a caller snapshot cannot hide a permanent-state rollback");

        var catalog = GameCatalog.Default.LoadRules(AppDomain.CurrentDomain.BaseDirectory);
        var journeyStore = new ProgressStore(Path.Combine(root, "journey"), "test", "synthetic");
        var journey = new AchievementEngine(catalog, journeyStore); var bytes = Empty(); int frame = 0;
        for (int i = 0; i < 3; i++) journey.Update(At(bytes, frame++));
        Action<Action<byte[]>, string[]> step = (change, expected) => {
            change(bytes); var awards = new List<string>();
            for (int i = 0; i < 3; i++) awards.AddRange(journey.Update(At(bytes, frame++)).Select(a => a.Id));
            check(expected.OrderBy(id => id).SequenceEqual(awards.OrderBy(id => id)), "Decoded playthrough event: " + String.Join(", ", expected));
        };
        step(x => x[0x64E] |= 0x80, new[] { "story-heros-clothes" });
        step(x => x[0x51] = 5, new[] { "story-telescope" });
        step(x => x[0xB4] = 1, new[] { "story-hero-sword" });
        step(x => x[0xB5] = 1, new[] { "story-hero-shield" });
        step(x => x[0x55] = 1, new[] { "story-spoils-bag" });
        step(x => x[0x633] |= 0x80, new[] { "story-fortress-escape" });
        step(x => x[0x52] = 1, new[] { "story-sail" });
        step(x => x[0x53] = 1, new[] { "story-wind-waker" });
        step(x => x[0xBD] = 1, new[] { "collect-winds-requiem" });
        step(x => x[0x54] = 1, new[] { "gear-grappling-hook" });
        step(x => { x[0xBF] = 2; x[0x380 + 3 * 0x24 + 0x21] |= 8; }, new[] { "story-din-pearl" });
        step(x => { x[0x57] = 1; x[0x13] = 16; }, new[] { "gear-deku-leaf" });
        step(x => x[0x56] = 1, new[] { "gear-boomerang" });
        step(x => { x[0xBF] = 6; x[0x380 + 4 * 0x24 + 0x21] |= 8; }, new[] { "story-farore-pearl" });
        step(x => { x[0x5E] = 1; x[0x70] = 30; }, new[] { "gear-bombs" });
        step(x => x[0xBF] = 7, new[] { "story-nayru-pearl" });
        step(x => x[0xBD] |= 4, new[] { "collect-command-melody" });
        step(x => { x[0x5D] = 1; x[0x6F] = 30; }, new[] { "gear-bow" });
        step(x => x[0x380 + 5 * 0x24 + 0x21] |= 8, new[] { "story-gohdan" });
        step(x => x[0xB4] = 3, new[] { "story-master-sword" });
        step(x => x[0x65] = 1, new[] { "gear-skull-hammer" });
        step(x => x[0x380 + 2 * 0x24 + 0x21] |= 8, new[] { "story-helmaroc" });
        step(x => x[0x651] |= 2, new[] { "story-zelda" });
        step(x => x[0x5D] = 3, new[] { "gear-elemental-arrows" });
        step(x => x[0xB5] = 3, new[] { "gear-mirror-shield" });
        step(x => { x[0xB4] = 7; x[0x380 + 6 * 0x24 + 0x21] |= 8; }, new[] { "story-jalhalla" });
        step(x => x[0x64] = 1, new[] { "gear-hookshot" });
        step(x => { x[0xB4] = 15; x[0x380 + 7 * 0x24 + 0x21] |= 8; }, new[] { "story-molgera" });
        step(x => x[0xBE] = 128, new[] { "story-triforce-first" });
        step(x => x[0xBE] = 255, new[] { "story-triforce-complete" });
        step(x => x[0x650] |= 2, new[] { "story-hyrule-barrier" });
        step(x => x[0x5D] = 7, new[] { "story-light-arrows" });
        check(journey.Update(At(bytes, frame++)).Count == 0 && new ProgressStore(Path.Combine(root, "journey"), "test", "synthetic").Contains("story-light-arrows"),
            "The decoded playthrough persists through the finale without duplicate notifications");
    }
}
