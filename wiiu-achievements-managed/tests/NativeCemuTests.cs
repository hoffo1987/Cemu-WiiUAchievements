using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WiiUAchievements;

public static class NativeCemuTests
{
    static int checks;
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception("Native Cemu: " + message); checks++; }
    static void U32(byte[] b, int offset, uint value)
    { for (int i = 0; i < 4; i++) b[offset + i] = (byte)(value >> (8 * i)); }
    static void U64(byte[] b, int offset, ulong value)
    { for (int i = 0; i < 8; i++) b[offset + i] = (byte)(value >> (8 * i)); }
    static void BE32(byte[] b, int offset, uint value)
    { for (int i = 0; i < 4; i++) b[offset + i] = (byte)(value >> (24 - 8 * i)); }
    static void Scene(byte[] b, int offset, string stage)
    {
        Array.Clear(b, offset, 8); Array.Clear(b, offset + 12, 8);
        byte[] name = Encoding.ASCII.GetBytes(stage);
        Buffer.BlockCopy(name, 0, b, offset, name.Length);
        Buffer.BlockCopy(name, 0, b, offset + 12, name.Length);
    }
    static byte[] Packet(ulong milliseconds, ulong generation = 1, NativeCemuState state = NativeCemuState.Running)
    {
        var b = new byte[NativeCemuFeed.PacketSize];
        U32(b, 0, NativeCemuFeed.Protocol); U32(b, 4, (uint)state);
        U64(b, 8, generation); U64(b, 16, milliseconds);
        U64(b, 24, NativeCemuFeed.SupportedTitleId); U32(b, 36, NativeCemuFeed.SupportedRpxHash);
        Scene(b, 40, "sea"); BE32(b, 66, 0x20001000); BE32(b, 74, 0x20001000);
        b[83] = b[85] = 12;
        for (int i = 0x4A; i <= 0x4D; i++) b[82 + i] = 255;
        Buffer.BlockCopy(b, 40, b, 1910, 42);
        return b;
    }
    static NativeCemuSample Read(NativeCemuFeed feed)
    { NativeCemuSample result; Check(feed.TryRead(out result), "expected one queued observation"); return result; }
    static void Empty(NativeCemuFeed feed)
    { NativeCemuSample result; Check(!feed.TryRead(out result) && result == null, "an observation is consumed once"); }
    static NativeCemuSample Decode(byte[] packet)
    {
        var feed = new NativeCemuFeed(); Check(feed.Submit(packet), "reconstructed packet accepted"); return Read(feed);
    }

    public static int Run(string root)
    {
        checks = 0;
        var feed = new NativeCemuFeed(); Empty(feed);
        DateTime before = DateTime.UtcNow;
        var packet = Packet(100);
        Check(feed.Submit(packet) && feed.IsFresh, "exact running profile accepted and fresh");
        DateTime after = DateTime.UtcNow;
        packet[87] = 99;
        var sample = Read(feed);
        Check(sample.State == NativeCemuState.Running && sample.Reset && sample.Generation == 1 && sample.MonotonicMilliseconds == 100,
            "first observation identifies its native segment");
        Check(sample.Snapshot.Rupees == 0 && sample.Snapshot.Health == 12 && sample.Snapshot.MaxHealth == 12,
            "queue owns the bytes and parses the big-endian header");
        Check(sample.Snapshot.Utc.Kind == DateTimeKind.Utc && sample.Snapshot.Utc >= before && sample.Snapshot.Utc <= after,
            "UTC is receipt time, independent of the native monotonic epoch");
        Check(sample.Snapshot.Context != null && sample.Snapshot.Context.State == GameplayState.Ready,
            "native observations always include an actual gameplay context");
        Empty(feed);
        Check(!feed.Submit(Packet(100)), "duplicate native frame cannot be evaluated twice"); Empty(feed);
        Check(!feed.Submit(Packet(150)) && !feed.Submit(Packet(199)), "sub-100ms frames cannot accelerate confirmations"); Empty(feed);
        Check(feed.Submit(Packet(200)), "100ms cadence accepted");
        Check(!Read(feed).Reset, "ordinary coherent observations preserve continuity");

        Check(feed.Submit(Packet(300)) && feed.Submit(Packet(400)) && feed.Submit(Packet(500)), "producer can replace bounded pending slot");
        sample = Read(feed);
        Check(sample.MonotonicMilliseconds == 500 && !sample.Reset, "bounded queue emits only newest coherent observation"); Empty(feed);
        Check(feed.Submit(Packet(600, 1, NativeCemuState.Paused)) && feed.Submit(Packet(700)), "pause and resume can occur before consumption");
        Check(Read(feed).Reset, "overwritten pause remains a sticky reset boundary");
        Check(feed.Submit(Packet(800, 2)) && feed.Submit(Packet(900, 3)), "new generations supersede queued title segments");
        sample = Read(feed); Check(sample.Reset && sample.Generation == 3, "overwritten generation cannot inherit confirmations");
        Check(!feed.Submit(Packet(899, 3)), "native time reversal fails closed");
        Check(Read(feed).State == NativeCemuState.Stopped && !feed.IsFresh, "time reversal publishes reset instead of stale gameplay");
        Check(feed.Submit(Packet(1000, 3)) && Read(feed).Reset, "fresh segment can recover after reversal");

        packet = Packet(1100, 3, NativeCemuState.Paused); sample = Decode(packet);
        Check(sample.State == NativeCemuState.Paused && sample.Reset && sample.Snapshot == null, "paused frames never decode game data");
        packet = Packet(0, 4, NativeCemuState.Stopped); U64(packet, 24, 0); U32(packet, 36, 0);
        sample = Decode(packet);
        Check(sample.State == NativeCemuState.Stopped && sample.Reset && sample.Snapshot == null, "unload accepts empty title metadata as a lifecycle boundary");

        Action<byte[], string> reject = (bytes, message) => {
            var f = new NativeCemuFeed(); f.Submit(Packet(100));
            Check(!f.Submit(bytes), message);
            var boundary = Read(f); Check(boundary.Reset && boundary.State == NativeCemuState.Stopped && boundary.Snapshot == null && !f.IsFresh,
                "rejected input replaces pending game data with a reset"); Empty(f);
            Check(f.Submit(Packet(200)) && Read(f).Reset, "valid input recovers with fresh continuity");
        };
        reject(null, "null packet rejected"); reject(new byte[NativeCemuFeed.PacketSize - 1], "truncated packet rejected");
        reject(new byte[NativeCemuFeed.PacketSize + 1], "oversized packet rejected");
        packet = Packet(200); U32(packet, 0, 2); reject(packet, "unknown protocol rejected");
        packet = Packet(200); U32(packet, 4, 3); reject(packet, "unknown native state rejected");
        packet = Packet(200); U64(packet, 24, NativeCemuFeed.SupportedTitleId + 1); reject(packet, "foreign title rejected");
        packet = Packet(200); U32(packet, 32, 1); reject(packet, "unsupported game update rejected");
        packet = Packet(200); U32(packet, 36, NativeCemuFeed.SupportedRpxHash + 1); reject(packet, "different executable rejected");
        packet = Packet(200, 1, NativeCemuState.Paused); U32(packet, 36, 0); reject(packet, "paused foreign executable also rejected");

        long ticks = 0;
        feed = new NativeCemuFeed(() => ticks, 1000);
        feed.Submit(Packet(100)); Read(feed);
        ticks = 1000; Check(feed.IsFresh, "freshness includes the one-second boundary");
        ticks = 1001; Check(!feed.IsFresh, "managed monotonic time detects a dead native producer");
        sample = Read(feed); Check(sample.Reset && sample.State == NativeCemuState.Stopped, "stale feed emits one reset boundary"); Empty(feed);
        ticks = 1100; feed.Submit(Packet(200)); Check(Read(feed).Reset && feed.IsFresh, "producer recovery does not inherit old confirmations");
        ticks = 2201; Check(!feed.Submit(Packet(200)) && !feed.IsFresh, "frozen timestamps cannot renew freshness"); Read(feed); Empty(feed);
        feed = new NativeCemuFeed(() => ticks, 1000); feed.Submit(Packet(300)); ticks += 1001;
        sample = Read(feed); Check(sample.State == NativeCemuState.Stopped && sample.Snapshot == null, "unconsumed stale data is discarded");
        ticks = 4000; feed = new NativeCemuFeed(() => ticks, 1000); feed.Submit(Packet(100)); Read(feed);
        ticks = 3999; Check(!feed.IsFresh, "managed clock reversal cannot certify freshness");
        Check(Read(feed).Reset, "managed clock reversal invalidates pending continuity");

        packet = Packet(100); packet[1910] = (byte)'X'; sample = Decode(packet);
        Check(sample.Reset && sample.Snapshot.Context.State == GameplayState.Loading && !sample.Snapshot.HasValue("telescope"),
            "torn context never decodes progression bytes");
        packet = Packet(100); Scene(packet, 40, "Name"); Buffer.BlockCopy(packet, 40, packet, 1910, 42);
        packet[83] = 0; sample = Decode(packet);
        Check(sample.Snapshot.Context.State == GameplayState.Menu, "file select blocks even an invalid save header");
        packet = Packet(100); Scene(packet, 40, "sea_T"); Buffer.BlockCopy(packet, 40, packet, 1910, 42);
        Check(Decode(packet).Snapshot.Context.State == GameplayState.Menu, "title-background actor cannot become native gameplay");
        packet = Packet(100); packet[64] = packet[1934] = 2;
        Check(Decode(packet).Snapshot.Context.State == GameplayState.Unknown, "unknown transition encoding stays unavailable");
        packet = Packet(100); Array.Clear(packet, 66, 16); Array.Clear(packet, 1936, 16);
        Check(Decode(packet).Snapshot.Context.State == GameplayState.Loading, "missing player references suspend native sampling");
        packet = Packet(100); packet[83] = 1;
        sample = Decode(packet); Check(sample.Reset && sample.Snapshot.Context.State == GameplayState.Unknown,
            "impossible save headers cannot reach the engine as Ready observations");

        // The bridge uses the actual decoder instead of maintaining a native or
        // managed duplicate mapping. Exercise all registered fields for parity.
        packet = Packet(100); packet[82 + 0x51] = 5; packet[82 + 0xB4] = 15; packet[82 + 0xB5] = 3;
        packet[82 + 0xBD] = 63; packet[82 + 0xBE] = 128; packet[82 + 0xBF] = 7;
        packet[82 + 0x64E] = 0xA0; packet[82 + 0x4A] = 0x54; packet[82 + 0x5D] = 7;
        packet[82 + 0x6F] = 99; packet[82 + 0x70] = 60; packet[82 + 0x13] = 32; packet[82 + 0x12] = 2;
        var save = new byte[WindWakerProgression.ReadSize]; Buffer.BlockCopy(packet, 82, save, 0, save.Length);
        var expected = WindWakerProgression.Parse(save); sample = Decode(packet);
        Check(sample.Snapshot.HeroClothes == expected.HeroClothes && WindWakerProgression.Fields.All(f =>
            sample.Snapshot.HasValue(f.Name) == expected.HasValue(f.Name) && (!expected.HasValue(f.Name) || sample.Snapshot.Value(f.Name) == expected.Value(f.Name))),
            "all 50 progression fields retain the production decoder's exact semantics");
        packet[82 + 0x5D] = 255; sample = Decode(packet);
        Check(!sample.Snapshot.HasValue("bow") && !sample.Snapshot.HasValue("elemental_arrows") && !sample.Snapshot.HasValue("light_arrows"),
            "unavailable progression does not turn into an award or a zero baseline");

        feed = new NativeCemuFeed(); feed.Submit(Packet(100)); Read(feed);
        packet = Packet(200); Scene(packet, 40, "Name"); Buffer.BlockCopy(packet, 40, packet, 1910, 42);
        feed.Submit(packet); feed.Submit(Packet(300)); Check(Read(feed).Reset, "overwritten file-select context clears engine continuity");
        feed.Submit(Packet(1500)); Check(Read(feed).Reset, "native sampling gaps greater than one second reset continuity");
        feed.Reset(); Check(Read(feed).Reset && !feed.IsFresh, "explicit shutdown reset invalidates the feed"); Empty(feed);

        // Full feed -> production engine -> disk path with synthetic timestamps.
        var rules = new List<Achievement> {
            new Achievement { Id = "native-ten", Title = "ten", Description = "ten", Field = "rupees", Threshold = 10, Points = 1 },
            new Achievement { Id = "native-pending", Title = "pending", Description = "pending", Requirement = "Synthetic pending target", Points = 1 }
        };
        string directory = Path.Combine(root, "native-feed-" + Guid.NewGuid().ToString("N"));
        var store = new ProgressStore(directory, "native-test", "synthetic");
        var engine = new AchievementEngine(rules, store, AchievementRecognition.IncludeExistingSave);
        feed = new NativeCemuFeed();
        DateTime origin = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        int awardCount = 0;
        for (int frame = 0; frame <= 10; frame++)
        {
            packet = Packet((ulong)(frame * 100)); packet[87] = 15;
            feed.Submit(packet); sample = Read(feed); if (sample.Reset) engine.Reset();
            sample.Snapshot.Utc = origin.AddMilliseconds(frame * 100);
            awardCount += engine.Update(sample.Snapshot).Count;
            if (frame < 10) Check(!store.Contains("native-ten"), "loaded condition waits for stable context and three paced confirmations");
        }
        Check(awardCount == 1 && store.Contains("native-ten") && !store.Contains("native-pending"),
            "native feed uses existing-save recognition without enabling planned targets");
        Check(new ProgressStore(directory, "native-test", "synthetic").Get("native-ten").Utc == origin.AddSeconds(1),
            "synthetic native award survives reopening with exact observation timestamp");
        return checks;
    }
}
