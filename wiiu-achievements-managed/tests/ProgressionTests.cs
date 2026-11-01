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
    static void Word(byte[] b, int offset, int bit) { b[offset + 4 * (bit / 32) + 3 - bit % 32 / 8] |= (byte)(1 << (bit % 8)); }
    static void Flag(byte[] b, int flag) { b[0x624 + (flag >> 8)] |= (byte)(flag & 255); }
    static void Box(byte[] b, int stage, int bit) { Word(b, 0x380 + 0x24 * stage, bit); }
    static void RemainingChecks(Action<bool, string> check, string root)
    {
        var b = Empty(); b[0x145] = 0xEA;
        check(At(b, 0).Value("deciphered_triforce_charts") == 0, "The five HD default deciphering bits award no obtainable Triforce charts");
        b[0xD7] = 0x15;
        check(At(b, 0).Value("deciphered_triforce_charts") == 0, "Owning three Triforce charts does not imply deciphering");
        b[0x145] = 255;
        check(At(b, 0).Value("deciphered_triforce_charts") == 3 && At(b, 0).Value("treasure_charts") == 0, "The three deciphered Triforce charts stay outside the 46 treasure charts");
        // Item IDs CC..FE, excluding the two special charts and the three
        // Triforce charts; item function collectmapNN uses index FE-item.
        int normal = 0;
        for (int item = 0xC2; item <= 0xFE; item++)
        {
            b = Empty(); Word(b, 0xD4, 0xFE - item);
            bool treasure = item >= 0xCC && item != 0xDB && item != 0xDC && item != 0xFA && item != 0xFC && item != 0xFE;
            check(At(b, 0).Value("treasure_charts") == (treasure ? 1 : 0), "Chart item " + item.ToString("X2") + " selects its actual HD pack bit");
            if (treasure) normal++;
        }
        check(normal == 46, "Exactly 46 normal HD chart items exist in the reconstructed dispatch range");
        b = Empty(); for (int i = 0; i < 16; i++) b[0xD4 + i] = 255;
        check(At(b, 0).Value("treasure_charts") == 46 && At(b, 0).Value("chart_salvages") == 0, "Owned and opened charts alone cannot award a salvaged treasure");
        for (int i = 0; i < 16; i++) b[0xF4 + i] = 255;
        check(At(b, 0).Value("chart_salvages") == 46, "Special, Triforce and unused completed-map bits cannot inflate the 46 salvages");
        b = Empty(); b[0xF7] = 128;
        check(At(b, 0).Value("chart_salvages") == 0, "A completed-map bit without its owned chart is not counted");
        b[0xD7] = 128;
        check(At(b, 0).Value("chart_salvages") == 1 && At(b, 0).Value("teacher_chart_45") == 0, "HD chart 46 is distinct from the teacher's chart 45");
        b[0xD7] |= 64; check(At(b, 0).Value("teacher_chart_45") == 1, "Teacher reward uses item F8, collect-map 7");
        b = Empty(); b[0xBF] = 7; check(At(b, 0).Value("tower_pearls_placed") == 0, "Possession of pearls does not raise the tower");
        Flag(b, 0x14C0); check(At(b, 0).Value("tower_pearls_placed") == 0, "Two placed pearls cannot satisfy the tower milestone");
        Flag(b, 0x1410); check(At(b, 0).Value("tower_pearls_placed") == 1, "All three placement flags satisfy the tower milestone");
        b = Empty(); Flag(b, 0x2920);
        check(At(b, 0).Value("medli_temple") == 1 && At(b, 0).Value("makar_temple") == 0, "The two sages have independent persistent events");
        b = Empty(); Flag(b, 0x3401);
        check(At(b, 0).Value("gallery_first") == 0 && At(b, 0).Value("gallery_complete") == 0, "Submitting a pictograph does not create or complete the gallery");
        Flag(b, 0x3A01); check(At(b, 0).Value("gallery_first") == 1 && At(b, 0).Value("gallery_complete") == 0, "First completed figurine is separate from HD gallery completion");
        Flag(b, 0x3D08); check(At(b, 0).Value("gallery_complete") == 1, "Gallery completion uses Carlov's HD completion event");
        b = Empty(); Flag(b, 0x3B02); Flag(b, 0x4002);
        check(At(b, 0).Value("puppet_ganon_defeated") == 0 && !At(b, 0).HasValue("ganondorf_defeated"), "Pre-battle cutscenes cannot stand in for either final victory");
        Flag(b, 0x3F10); b[0xBE] = 255; b[0xB4] = 15;
        Func<int, int, int, GameSnapshot> finale = (point, room, layer) => WindWakerProgression.Parse(b,
            new GameplayContext(GameplayState.Ready, "GTower", 0x15000000, false, point, room, layer));
        check(finale(2, 0, 8).Value("ganondorf_defeated") == 0 && finale(1, 0, 9).Value("ganondorf_defeated") == 0 && finale(2, 1, 9).Value("ganondorf_defeated") == 0,
            "Tower, layer, spawn and room must identify the final-blow transition together");
        check(finale(2, 0, 9).Value("ganondorf_defeated") == 1 && At(b, 0).Value("puppet_ganon_defeated") == 1, "The HD post-duel destination completes the finale independently of Puppet Ganon");
        var finalRule = new Achievement { Id = "story-ganondorf", Title = "Finale", Description = "Finale", Field = "ganondorf_defeated", Threshold = 1, Points = 25, Experimental = true };
        string finalPath = Path.Combine(root, "decoded-finale");
        var finalStore = new ProgressStore(finalPath, "synthetic", "synthetic");
        var finalEngine = new AchievementEngine(new List<Achievement> { finalRule }, finalStore, AchievementRecognition.IncludeExistingSave);
        DateTime epoch = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int frame = 0; frame < 13; frame++) { var s = finale(2, 0, 8); s.Utc = epoch.AddMilliseconds(frame * 100); finalEngine.Update(s); }
        check(!finalStore.Contains(finalRule.Id), "A stabilized pre-victory tower never awards the final achievement");
        var loading = finale(2, 0, 9); loading.Utc = epoch.AddMilliseconds(1300);
        loading.Context = new GameplayContext(GameplayState.Loading, "GTower", 0);
        finalEngine.Update(loading); finalEngine.Reset();
        int finalAwards = 0;
        for (int frame = 14; frame < 32; frame++) { var s = finale(2, 0, 9); s.Utc = epoch.AddMilliseconds(frame * 100); finalAwards += finalEngine.Update(s).Count; }
        var restoredFinal = new ProgressStore(finalPath, "synthetic", "synthetic");
        check(finalAwards == 1 && restoredFinal.Contains(finalRule.Id) && restoredFinal.Get(finalRule.Id).Utc >= epoch.AddMilliseconds(2100),
            "The decoded finale survives a transition reset, gameplay stabilization, confirmation and archive restart exactly once");
        b = Empty(); Flag(b, 0x0102); check(At(b, 0).Value("forest_water_reward") == 0, "All trees watered cannot substitute for collecting their heart reward");
        Flag(b, 0x2E20); check(At(b, 0).Value("forest_water_reward") == 1, "The forest-water reward needs its pickup flag");
        b = Empty(); b[0x624 + 0x88] = 3;
        check(At(b, 0).Value("ghost_ship_reward") == 0, "Ghost Ship progression without the exact chest is insufficient");
        Box(b, 10, 23); check(At(b, 0).Value("ghost_ship_reward") == 1, "The cleared Ghost Ship and chest 23 jointly identify its reward");
        b = Empty(); Box(b, 13, 11);
        check(At(b, 0).Value("savage_30") == 1 && At(b, 0).Value("savage_50") == 0, "The two Savage Labyrinth rewards use different chests");
        Box(b, 13, 12); b[0xB7] = 1;
        check(At(b, 0).Value("savage_50") == 0, "The Pirate's Charm cannot substitute for the HD Hero's Charm");
        b[0xB8] = 1; check(At(b, 0).Value("savage_50") == 1, "Floor 50 requires its chest and Hero's Charm");
        b = Empty(); for (int i = 3; i < 7; i++) Box(b, i, 15);
        check(At(b, 0).Value("tingle_statues") == 4, "Four different Tingle chests do not complete the five-statue set");
        Box(b, 7, 15); check(At(b, 0).Value("tingle_statues") == 5, "All five HD statue chests count exactly once");
        b = Empty(); Word(b, 0x384, 52); b[0x5C0 + 6 * 2 + 1] = 1;
        check(At(b, 0).Value("big_octos") == 0, "The fairy reward and salvaged Octo loot cannot substitute for a defeated Octo");
        for (int bit = 13; bit <= 18; bit++) Word(b, 0x384, bit);
        check(At(b, 0).Value("big_octos") == 6, "All six actual EUR Octo defeat switches count independently");
        b = Empty(); b[0x624 + 0xBE] = 20; Flag(b, 0x0E04);
        check(At(b, 0).Value("squid_20_reward") == 0, "A Squid-Hunt record without chart 23 does not count the prize");
        b[0xD6] = 0x20; check(At(b, 0).Value("squid_20_reward") == 1, "A winning 20-shot record plus the correct prize completes Squid-Hunt");
        b[0x624 + 0xBE] = 21; check(At(b, 0).Value("squid_20_reward") == 0, "A 21-shot record cannot complete the at-most-20 objective");
        b = Empty(); b[0x92] = 0x80; b[0x86] = 0x9B;
        check(At(b, 0).Value("maggie_reward") == 0, "Possessing Moe's letter is not delivering it");
        b[0x86] = 255; b[0x624 + 0xCC] = 1;
        check(At(b, 0).Value("maggie_reward") == 0, "An unfinished Maggie reward event cannot satisfy delivery");
        b[0x624 + 0xCC] = 0; check(At(b, 0).Value("maggie_reward") == 1, "Maggie's persistent letter ownership, consumed letter and ended counter identify completion");
        b = Empty(); Flag(b, 0xAE01); check(At(b, 0).Value("hoskit_letter_read") == 0, "Sending Hoskit's reward letter is distinct from reading it");
        Flag(b, 0xAE02); check(At(b, 0).Value("hoskit_letter_read") == 1, "Hoskit completion requires the READ letter state");
        b = Empty(); b[1] = 80; for (int i = 2; i <= 7; i++) b[0x380 + i * 0x24 + 0x21] = 24;
        check(At(b, 0).Value("heart_pieces") == 0, "Twenty hearts and six boss containers alone cannot invent even one specific heart piece");
        Box(b, 13, 12); b[0xB8] = 1;
        check(At(b, 0).Value("heart_pieces") == 0, "The HD floor-50 charm is excluded from the heart-piece catalog");
        b = Empty(); Flag(b, 0x1340);
        check(!At(b, 0).HasValue("heart_pieces"), "A reward flag before its health animation leaves the piece count unavailable");
        b[1] = 13; check(At(b, 0).Value("heart_pieces") == 1, "The first specific piece becomes available after its health gain");
        b = Empty(); b[1] = 80;
        int[,] boxes = { {0,10}, {0,6}, {2,3}, {0,3}, {0,0}, {0,20}, {12,26}, {12,5}, {12,6}, {10,1}, {10,0} };
        for (int i = 0; i < boxes.GetLength(0); i++) Box(b, boxes[i,0], boxes[i,1]);
        Word(b, 0x394, 2); Word(b, 0x394, 8);
        foreach (int room in new[] {6,16,17}) b[0x5C0 + room * 2 + 1] = 1;
        foreach (int chart in new[] {9,11,17,18,13,12,14,30,15,8,10,16}) { Word(b, 0xD4, chart - 1); Word(b, 0xF4, chart - 1); }
        check(At(b, 0).Value("heart_pieces") == 28, "HD has 28 distinct field/chest/salvage heart pieces including the new sea-room-46 salvage");
        foreach (int flag in new[] {0x0F10,0x1340,0x1B20,0x1020,0x1B10,0x2280,0xAE03,0xAC03,0xB503,0x3E04,0x2E20,0x2010,0x2B40}) Flag(b, flag);
        b[0x624 + 0xFE] = 1; b[0x624 + 0xB7] = 1; b[0x92] = 0x80;
        check(At(b, 0).Value("heart_pieces") == 44, "The 16 distinct NPC/minigame/mail rewards complete exactly 44 HD heart-piece sources");
        b[0x624 + 0xFE] = 7; check(!At(b, 0).HasValue("heart_pieces"), "Invalid prize counters suppress the composite piece count");
    }
    public static void Run(Action<bool, string> check, string root)
    {
        RemainingChecks(check, root);
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
