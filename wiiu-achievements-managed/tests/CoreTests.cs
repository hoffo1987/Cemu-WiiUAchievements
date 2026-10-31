using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WiiUAchievements;

class CoreTests
{
    static int tests;
    static string temporary;
    static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        tests++; Console.WriteLine("PASS " + name);
    }
    static GameSnapshot S(int rupees, int frame)
    {
        return new GameSnapshot { Rupees = rupees, Health = 12, MaxHealth = 12, Utc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(frame * 100) };
    }
    static Achievement Rule() { return new Achievement { Id = "ten", Title = "Ten", Description = "Ten", Field = "rupees", Threshold = 10, Points = 1 }; }
    static ProgressStore Store(string name) { return new ProgressStore(Path.Combine(temporary, name), "player", "EUR-v0"); }
    static AchievementEngine Engine(ProgressStore store) { return new AchievementEngine(new System.Collections.Generic.List<Achievement> { Rule() }, store); }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")]
    static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);

    static int Fixture(string file)
    {
        IntPtr block = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x100000000UL, 0x2000, 4);
        if (block == IntPtr.Zero) return 2;
        try
        {
            IntPtr code = new IntPtr(block.ToInt64() + 0x02000000);
            IntPtr data = new IntPtr(block.ToInt64() + 0x10000000);
            if (VirtualAlloc(code, (UIntPtr)0x1000U, 0x1000, 4) == IntPtr.Zero || VirtualAlloc(data, (UIntPtr)0x102000U, 0x1000, 4) == IntPtr.Zero) return 3;
            // One real candidate deliberately straddles a scanner chunk boundary.
            byte[] header = { 0, 12, 0, 12, 0, 5 };
            Marshal.Copy(header, 0, new IntPtr(data.ToInt64() + 0xFFFFE), header.Length);
            Marshal.Copy(header, 0, new IntPtr(data.ToInt64() + 0x100), header.Length);
            IntPtr savePage = new IntPtr(block.ToInt64() + 0x145B7000);
            if (VirtualAlloc(savePage, (UIntPtr)0x2000U, 0x1000, 4) == IntPtr.Zero) return 4;
            var save = new byte[WindWakerProgression.ReadSize]; save[1] = 12; save[3] = 12; save[4] = 1; save[5] = 65;
            save[0x51] = 5; save[0xB4] = 1; save[0x64E] = 0x80;
            for (int i = 0x4A; i <= 0x4D; i++) save[i] = 255;
            Marshal.Copy(save, 0, new IntPtr(block.ToInt64() + WindWakerProgression.SaveAddress), save.Length);
            IntPtr contextPage = new IntPtr(block.ToInt64() + 0x10474000);
            if (VirtualAlloc(contextPage, (UIntPtr)0x1000U, 0x1000, 4) == IntPtr.Zero) return 5;
            byte[] scene = new byte[26]; scene[0] = scene[12] = (byte)'s'; scene[1] = scene[13] = (byte)'e'; scene[2] = scene[14] = (byte)'a';
            Marshal.Copy(scene, 0, new IntPtr(block.ToInt64() + GameplayContext.StageAddress), scene.Length);
            byte[] players = new byte[16]; players[0] = players[8] = 0x20; players[2] = players[10] = 0x10;
            Marshal.Copy(players, 0, new IntPtr(block.ToInt64() + GameplayContext.PlayerAddress), players.Length);
            File.WriteAllText(file, block.ToInt64().ToString());
            Console.ReadLine();
            // Only the boundary candidate changes. This emulates a live value and its stale copy.
            Marshal.WriteByte(new IntPtr(data.ToInt64() + 0xFFFFE + 5), 10);
            Console.WriteLine("changed"); Console.Out.Flush();
            Console.ReadLine();
            return 0;
        }
        finally { VirtualFree(block, UIntPtr.Zero, 0x8000); }
    }

    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--fixture") return Fixture(args[1]);
        try
        {
            bool portable = args.Contains("--portable");
            string resultDirectory = args.FirstOrDefault(a => !a.StartsWith("--"));
            temporary = resultDirectory != null ? Path.GetFullPath(resultDirectory) : Path.Combine(Path.GetTempPath(), "wiiua-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            var sample = GameSnapshot.Parse(new byte[] { 0, 12, 0, 10, 1, 44 });
            Check(sample.Rupees == 300 && sample.Health == 10 && sample.MaxHealth == 12, "Big-endian snapshot, quarter hearts");
            bool rejected = false;
            try { GameSnapshot.Parse(new byte[] { 0, 12, 0, 13, 0, 0 }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Reject impossible health");
            var store = Store("baseline"); var engine = Engine(store);
            for (int i = 0; i < 5; i++) engine.Update(S(10, i));
            Check(!store.Contains("ten"), "Existing save does not award on connect");
            engine.Update(S(5, 5)); engine.Update(S(5, 6)); engine.Update(S(5, 7)); engine.Update(S(10, 8)); engine.Update(S(10, 9));
            Check(!store.Contains("ten"), "Require three consecutive matching samples");
            Check(engine.Update(S(10, 10)).Count == 1 && store.Contains("ten"), "Live crossing awards and persists");
            Check(engine.Update(S(10, 11)).Count == 0, "No duplicate award");
            Check(Store("baseline").Contains("ten"), "Progress survives process restart");
            Check(!new ProgressStore(Path.Combine(temporary, "baseline"), "other-player", "EUR-v0").Contains("ten"), "Separate local profiles");
            store = Store("glitch"); engine = Engine(store);
            engine.Update(S(0, 0)); engine.Update(S(0, 1)); engine.Update(S(0, 2));
            engine.Update(S(10, 3)); engine.Update(S(0, 4)); engine.Update(S(10, 5)); engine.Update(S(10, 6));
            Check(!store.Contains("ten"), "Transient spike is rejected and streak resets");
            engine.Update(S(10, 30)); engine.Update(S(10, 31)); engine.Update(S(10, 32));
            Check(!store.Contains("ten"), "Sampling gap discards pending crossing");
            store = Store("reset"); engine = Engine(store);
            engine.Update(S(0, 0)); engine.Update(S(10, 1)); engine.Reset();
            for (int i = 2; i < 7; i++) engine.Update(S(10, i));
            Check(!store.Contains("ten"), "Disconnect resets baseline and pending state");
            store = Store("corrupt"); store.Award("one", DateTime.UtcNow); store.Award("two", DateTime.UtcNow);
            string progressFile = Directory.GetFiles(Path.Combine(temporary, "corrupt"), "*.xml").Single();
            File.WriteAllText(progressFile, "<broken");
            var recovered = Store("corrupt");
            Check(recovered.Contains("one") && recovered.Warning != null && Directory.GetFiles(Path.Combine(temporary, "corrupt"), "*.damaged-*").Length == 1, "Corruption preserves original and recovers last backup");

            SystemTests.Run(Check, temporary, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "achievements.xml"));
            ProgressionTests.Run(Check, temporary);
            GameplayTests.Run(Check, temporary);
            RecoveryTests.Run(Check, temporary);
            CloudTests.Run(Check, temporary, portable);
            if (!portable)
            {
            // Real Win32 integration: a separate process with a synthetic Cemu-style mapping.
            string ready = Path.Combine(temporary, "fixture.txt");
            var child = Process.Start(new ProcessStartInfo {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "--fixture \"" + ready + "\"", UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true });
            try
            {
                for (int i = 0; i < 100 && !File.Exists(ready) && !child.HasExited; i++) Thread.Sleep(50);
                if (!File.Exists(ready)) throw new Exception("Fixture failed to start");
                ulong address = (ulong)Int64.Parse(File.ReadAllText(ready));
                using (var memory = new ProcessMemory(child.Id))
                {
                    Check(memory.FindGuestBases().Contains(address), "Detect separate process 4 GiB guest reservation");
                    var decoded = new WindWakerMemoryReader().Read(memory, address);
                    Check(decoded.Rupees == 321 && decoded.HeroClothes == 1 && decoded.Value("telescope") == 1 && decoded.Value("hero_sword") == 1,
                        "Production progression reader applies guest-base translation to a synthetic two-page save block");
                    var hits = Calibration.Scan(memory, new Observation { MaxHealth = 12, Health = 12, Rupees = 5 }, CancellationToken.None, p => { });
                    Check(hits.Count == 2 && hits.Any(c => c.SaveAddress == 0x100FFFFE), "Scanner finds candidates across chunk boundary");
                    child.StandardInput.WriteLine("change"); child.StandardInput.Flush();
                    if (child.StandardOutput.ReadLine() != "changed") throw new Exception("Fixture did not update");
                    hits = Calibration.Filter(memory, hits, new Observation { MaxHealth = 12, Health = 12, Rupees = 10 });
                    Check(hits.Count == 1 && hits[0].SaveAddress == 0x100FFFFE, "Live filtering removes stale memory copy");
                    Check(GameSnapshot.Parse(memory.Read(address + hits[0].SaveAddress, 6)).Rupees == 10, "ReadProcessMemory reads live value from child");
                }
                child.StandardInput.WriteLine("exit"); child.StandardInput.Flush(); child.WaitForExit(5000);
            }
            finally { if (!child.HasExited) child.Kill(); child.Dispose(); }
            }
            else Console.WriteLine("SKIP Win32 integration (--portable); no Cemu or game used.");
            Console.WriteLine("SUCCESS " + tests + " checks; artifacts: " + temporary);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
