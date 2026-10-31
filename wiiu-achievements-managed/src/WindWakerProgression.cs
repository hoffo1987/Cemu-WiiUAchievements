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
        static ProgressionField Dungeon(string name, int stage, int pearlMask, int swordMask)
        {
            return new ProgressionField(name, 1, b => !ValidBoss(b, stage) || (b[0xBF] & ~7) != 0 || (b[0xB4] & ~15) != 0 ? (int?)null :
                Boss(b, stage) && (b[0xBF] & pearlMask) == pearlMask && (b[0xB4] & swordMask) == swordMask ? 1 : 0);
        }
        public static int PopCount(int bits) { int count = 0; for (; bits != 0; bits &= bits - 1) count++; return count; }
        static readonly ReadOnlyCollection<ProgressionField> fields = Array.AsReadOnly(new[] {
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
        public static GameSnapshot Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < ReadSize) throw new System.IO.InvalidDataException("Blocco progressione incompleto.");
            var snapshot = GameSnapshot.Parse(bytes);
            snapshot.HeroClothes = (bytes[0x64E] & 0x80) != 0 ? 1 : 0;
            foreach (var field in fields) { var value = field.Decode(bytes); if (value.HasValue) snapshot.SetProgression(field.Name, value.Value); }
            return snapshot;
        }
    }
}
