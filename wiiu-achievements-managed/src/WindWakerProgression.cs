using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WiiUAchievements
{
    // Independently implemented decoders. Public trainer offsets and GameCube
    // structure derivations are research evidence, NOT HD live certification.
    public sealed class ProgressionField
    {
        public string Name { get; private set; }
        public int Maximum { get; private set; }
        readonly Func<byte[], int?> decode;
        public ProgressionField(string name, int maximum, Func<byte[], int?> parser)
        { Name = name; Maximum = maximum; decode = parser; }
        public int? Decode(byte[] bytes) { return decode(bytes); }
    }
    public static class WindWakerProgression
    {
        public const uint SaveAddress = 0x145B7B80;
        public const int ReadSize = 0x724;
        static ProgressionField Bit(string name, int offset, int mask, int allowed)
        {
            return new ProgressionField(name, 1, b => (b[offset] & ~allowed) != 0 ? (int?)null : (b[offset] & mask) == mask ? 1 : 0);
        }
        static ProgressionField Event(string name, int index, int mask)
        { return Bit(name, 0x624 + index, mask, 255); }
        static bool Boss(byte[] b, int stage) { return (b[0x380 + stage * 0x24 + 0x21] & 8) != 0; }
        static bool ValidBoss(byte[] b, int stage) { return (b[0x380 + stage * 0x24 + 0x21] & 0xC0) == 0; }
        static bool Chest(byte[] b, int stage, int bit)
        { return (b[0x380 + stage * 0x24 + 3 - bit / 8] & (1 << (bit % 8))) != 0; }
        static bool WordBit(byte[] b, int offset, int bit)
        { return (b[offset + bit / 32 * 4 + 3 - bit % 32 / 8] & (1 << (bit % 8))) != 0; }
        static bool EventBit(byte[] b, int flag) { return (b[0x624 + (flag >> 8)] & (flag & 255)) == (flag & 255); }
        static bool MaggieReward(byte[] b)
        { return (b[0x92] & 0x80) != 0 && !b.Skip(0x86).Take(8).Any(v => v == 0x9B) && b[0x624 + 0xCC] == 0; }
        // Individual HD sources, not max life minus a guessed number of bosses.
        // The GC floor-50 heart is absent; HD adds the sea-room-46 salvage.
        static readonly int[,] heartChests = { {0,10}, {0,6}, {2,3}, {0,3}, {0,0}, {0,20},
            {12,26}, {12,5}, {12,6}, {10,1}, {10,0} };
        static readonly int[] heartCharts = { 9, 11, 17, 18, 13, 12, 14, 30, 15, 8, 10, 16 };
        static readonly int[] heartEvents = { 0x0F10, 0x1340, 0x1B20, 0x1020, 0x1B10, 0x2280,
            0xAE03, 0xAC03, 0xB503, 0x3E04, 0x2E20, 0x2010, 0x2B40 };
        static int? HeartPieces(byte[] b)
        {
            int squid = b[0x624 + 0xFE] & 7;
            if (squid > 3) return null;
            int count = heartEvents.Count(flag => EventBit(b, flag));
            if (squid > 0) count++;
            if ((b[0x624 + 0xB7] & 3) > 0) count++;
            if (MaggieReward(b)) count++;
            for (int i = 0; i < heartChests.GetLength(0); i++)
                if (Chest(b, heartChests[i, 0], heartChests[i, 1])) count++;
            if (WordBit(b, 0x394, 2)) count++;
            if (WordBit(b, 0x394, 8)) count++;
            foreach (int room in new[] { 6, 16, 17 }) if ((b[0x5C0 + room * 2 + 1] & 1) != 0) count++;
            foreach (int chart in heartCharts)
                if (WordBit(b, 0xF4, chart - 1) && WordBit(b, 0xD4, chart - 1)) count++;
            int maxLife = (b[0] << 8) | b[1];
            // Reward flags may precede the item demo. Never announce pieces
            // before even their cumulative health gain could have been applied.
            return count > 44 || count > maxLife - 12 ? (int?)null : count;
        }
        static ProgressionField Dungeon(string name, int stage, int pearlMask, int swordMask)
        {
            return new ProgressionField(name, 1, b => !ValidBoss(b, stage) || (b[0xBF] & ~7) != 0 || (b[0xB4] & ~15) != 0 ? (int?)null :
                Boss(b, stage) && (b[0xBF] & pearlMask) == pearlMask && (b[0xB4] & swordMask) == swordMask ? 1 : 0);
        }
        public static int PopCount(int bits) { int count = 0; for (; bits != 0; bits &= bits - 1) count++; return count; }
        // HD chart packs, relative to the established save header. Special charts
        // and Triforce charts are deliberately excluded (46 normal charts).
        static readonly int[] treasureMasks = { 255, 255, 255, 0xEA, 0, 0x07, 255, 0xF3 };
        static int ChartCount(byte[] b, bool salvaged)
        {
            int count = 0;
            for (int i = 0; i < treasureMasks.Length; i++)
                count += PopCount(b[0xD4 + i] & treasureMasks[i] & (salvaged ? b[0xF4 + i] : 255));
            return count;
        }
        static readonly ReadOnlyCollection<ProgressionField> fields = Array.AsReadOnly(new[] {
            new ProgressionField("treasure_charts", 46, b => ChartCount(b, false)),
            new ProgressionField("chart_salvages", 46, b => ChartCount(b, true)),
            // HD initializes the other five deciphering bits to EA. Only
            // 1/3/5 (bits 0/2/4) correspond to the three obtainable charts.
            new ProgressionField("deciphered_triforce_charts", 3, b => PopCount(b[0x145] & b[0xD7] & 0x15)),
            Event("tower_pearls_placed", 0x14, 0xD0),
            Event("medli_temple", 0x29, 0x20), Event("makar_temple", 0x29, 0x10),
            Event("gallery_first", 0x3A, 0x01),
            Event("gallery_complete", 0x3D, 0x08),
            Event("puppet_ganon_defeated", 0x3F, 0x10),
            Event("hurricane_spin", 0x0B, 0x20), Event("orca_heart_reward", 0x0F, 0x10),
            Event("cabana_deed", 0x1C, 0x08), Event("mila_reward", 0x0E, 0x08),
            Event("killer_bees_reward", 0x13, 0x40), Event("town_decoration_reward", 0x1B, 0x10),
            new ProgressionField("lighthouse_reward", 1, b => (b[0x624 + 0x1C] & 2) != 0 &&
                (b[0x624 + 0x1B] & 0x20) != 0 ? 1 : 0),
            new ProgressionField("maggie_reward", 1, b => MaggieReward(b) ? 1 : 0),
            Event("trading_reward", 0x3E, 0x04), Event("hoskit_letter_read", 0xAE, 0x03),
            new ProgressionField("ghost_ship_reward", 1, b => (b[0x624 + 0x88] & 3) == 3 && Chest(b, 10, 23) ? 1 : 0),
            new ProgressionField("savage_30", 1, b => Chest(b, 13, 11) ? 1 : 0),
            new ProgressionField("savage_50", 1, b => Chest(b, 13, 12) && (b[0xB8] & 1) != 0 ? 1 : 0),
            new ProgressionField("tingle_statues", 5, b => Enumerable.Range(3, 5).Count(stage => Chest(b, stage, 15))),
            // Six different sea switches, read from the local EUR actor parameters.
            // The fairy's later switch 0x34 and ocean salvage bits do not count kills.
            new ProgressionField("big_octos", 6, b => PopCount(b[0x386] & 0xE0) + PopCount(b[0x385] & 7)),
            new ProgressionField("heart_pieces", 44, HeartPieces),
            // Context-derived below: no save bit is invented for the final duel.
            new ProgressionField("ganondorf_defeated", 1, b => null),
            Event("flight_reward", 0x2B, 0x40),
            new ProgressionField("mail_25", 1, b => (b[0x624 + 0xC2] & 3) == 3 ? 1 : 0),
            new ProgressionField("squid_win", 1, b => (b[0x624 + 0xFE] & 7) >= 1 &&
                (b[0x624 + 0xFE] & 7) <= 3 ? 1 : 0),
            new ProgressionField("squid_20_reward", 1, b => (b[0x624 + 0x0E] & 4) != 0 &&
                b[0x624 + 0xBE] >= 9 && b[0x624 + 0xBE] <= 20 && (b[0xD6] & 0x20) != 0 ? 1 : 0),
            new ProgressionField("spectacle_win", 1, b => (b[0x624 + 0xB7] & 3) >= 1 ? 1 : 0),
            new ProgressionField("forest_water_reward", 1, b => (b[0x624 + 1] & 2) != 0 &&
                (b[0x624 + 0x2E] & 0x20) != 0 ? 1 : 0),
            // In the unmodified HD game chart 45 is the teacher's final reward.
            Bit("teacher_chart_45", 0xD7, 0x40, 255),
            Bit("telescope", 0x51, 1, 5), Bit("hero_sword", 0xB4, 1, 15), Bit("hero_shield", 0xB5, 1, 3),
            Bit("spoils_bag", 0x55, 1, 1), Event("met_korl", 0x0F, 0x80), Bit("sail", 0x52, 1, 3),
            Bit("wind_waker", 0x53, 1, 1), Bit("delivery_bag", 0x63, 1, 1), Bit("bait_bag", 0x5C, 1, 1),
            Bit("grappling_hook", 0x54, 1, 1), Bit("deku_leaf", 0x57, 1, 1), Bit("boomerang", 0x56, 1, 1),
            Bit("bombs", 0x5E, 1, 1), Bit("bow", 0x5D, 1, 7), Bit("skull_hammer", 0x65, 1, 1),
            Bit("elemental_arrows", 0x5D, 2, 7), Bit("light_arrows", 0x5D, 4, 7),
            Bit("power_bracelets", 0xB6, 1, 1), Bit("iron_boots", 0x5A, 1, 1),
            Bit("mirror_shield", 0xB5, 2, 3), Bit("hookshot", 0x64, 1, 1),
            Bit("swift_sail", 0x52, 2, 3), Bit("picto_box", 0x59, 1, 3), Bit("deluxe_picto", 0x59, 2, 3),
            Bit("magic_armor", 0x5B, 1, 1), Bit("master_sword", 0xB4, 2, 15),
            new ProgressionField("magic_capacity", 32, b => b[0x13] == 0 || b[0x13] == 16 || b[0x13] == 32 ? b[0x13] : (int?)null),
            new ProgressionField("bomb_capacity", 99, b => b[0x70] == 0 || b[0x70] == 30 || b[0x70] == 60 || b[0x70] == 99 ? b[0x70] : (int?)null),
            new ProgressionField("arrow_capacity", 99, b => b[0x6F] == 0 || b[0x6F] == 30 || b[0x6F] == 60 || b[0x6F] == 99 ? b[0x6F] : (int?)null),
            new ProgressionField("wallet_capacity", 5000, b => b[0x12] <= 2 ? new[] { 500, 1000, 5000 }[b[0x12]] : (int?)null),
            Bit("winds_requiem", 0xBD, 1, 63), Bit("ballad_gales", 0xBD, 2, 63),
            Bit("command_melody", 0xBD, 4, 63), Bit("earth_lyric", 0xBD, 8, 63),
            Bit("wind_aria", 0xBD, 16, 63), Bit("song_passing", 0xBD, 32, 63),
            new ProgressionField("song_count", 6, b => b[0xBD] <= 63 ? PopCount(b[0xBD]) : (int?)null),
            new ProgressionField("triforce_count", 8, b => PopCount(b[0xBE])),
            new ProgressionField("bottle_count", 4, b => {
                int count = 0; for (int i = 0x4A; i <= 0x4D; i++) { if (b[i] == 255) continue; if (b[i] < 0x50 || b[i] > 0x59) return null; count++; } return count;
            }),
            new ProgressionField("grandma_and_soup", 1, b => (b[0x64E] & 0x20) != 0 && b.Skip(0x4A).Take(4).Any(v => v == 0x54 || v == 0x55) ? 1 : 0),
            Bit("nayru_pearl", 0xBF, 1, 7),
            Dungeon("gohma_and_din", 3, 2, 0), Dungeon("kalle_and_farore", 4, 4, 0),
            Dungeon("gohdan", 5, 0, 0), Dungeon("helmaroc", 2, 0, 0),
            Dungeon("jalhalla_and_sword", 6, 0, 4), Dungeon("molgera_and_sword", 7, 0, 8),
            Event("zelda_awakened", 0x2D, 2), Event("barrier_broken", 0x2C, 2),
            new ProgressionField("sea_sectors", 49, b => {
                int count = 0; for (int i = 0; i < 49; i++) { if (b[0x104 + i] > 3) return null; if ((b[0x104 + i] & 1) != 0) count++; } return count;
            })
        });
        public static ReadOnlyCollection<ProgressionField> Fields { get { return fields; } }
        public static ProgressionField Find(string name) { return fields.FirstOrDefault(f => f.Name == name); }
        public static bool ValidThreshold(string name, int threshold)
        {
            var f = Find(name);
            if (f == null || threshold <= 0 || threshold > f.Maximum) return false;
            if (name == "magic_capacity") return threshold == 16 || threshold == 32;
            if (name == "bomb_capacity" || name == "arrow_capacity") return threshold == 30 || threshold == 60 || threshold == 99;
            if (name == "wallet_capacity") return threshold == 500 || threshold == 1000 || threshold == 5000;
            return true;
        }
        public static GameSnapshot Parse(byte[] bytes) { return Parse(bytes, null); }
        public static GameSnapshot Parse(byte[] bytes, GameplayContext context)
        {
            if (bytes == null || bytes.Length < ReadSize) throw new System.IO.InvalidDataException("Blocco progressione incompleto.");
            var snapshot = GameSnapshot.Parse(bytes);
            snapshot.HeroClothes = (bytes[0x64E] & 0x80) != 0 ? 1 : 0;
            foreach (var field in fields) { var value = field.Decode(bytes); if (value.HasValue) snapshot.SetProgression(field.Name, value.Value); }
            snapshot.Context = context;
            if (context != null && context.State == GameplayState.Ready && context.Layer >= 0)
                snapshot.SetProgression("ganondorf_defeated", context.Stage == "GTower" && context.StartPoint == 2 &&
                    context.Room == 0 && context.Layer == 9 && EventBit(bytes, 0x3F10) && bytes[0xBE] == 255 &&
                    (bytes[0xB4] & 15) == 15 ? 1 : 0);
            return snapshot;
        }
    }
}
