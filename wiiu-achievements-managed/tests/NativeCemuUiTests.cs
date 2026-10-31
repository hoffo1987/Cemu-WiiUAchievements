using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WiiUAchievements;

class NativeCemuUiTests
{
    static int checks;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static object Get(MainForm form, string field) { return typeof(MainForm).GetField(field, Private).GetValue(form); }
    static void Call(MainForm form, string method, params object[] args)
    { typeof(MainForm).GetMethod(method, Private).Invoke(form, args); }
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static void LE(byte[] bytes, int at, ulong value, int length)
    { for (int i = 0; i < length; i++) bytes[at + i] = (byte)(value >> (i * 8)); }
    static byte[] Packet(ulong time, ulong generation, NativeCemuState state, int rupees)
    {
        var bytes = new byte[NativeCemuFeed.PacketSize];
        LE(bytes, 0, 1, 4); LE(bytes, 4, (uint)state, 4); LE(bytes, 8, generation, 8); LE(bytes, 16, time, 8);
        LE(bytes, 24, NativeCemuFeed.SupportedTitleId, 8); LE(bytes, 36, NativeCemuFeed.SupportedRpxHash, 4);
        Encoding.ASCII.GetBytes("sea").CopyTo(bytes, 40); Encoding.ASCII.GetBytes("sea").CopyTo(bytes, 52);
        // Both big-endian player references: 0x20001000.
        bytes[66] = bytes[74] = 0x20; bytes[68] = bytes[76] = 0x10;
        bytes[83] = bytes[85] = 12; bytes[86] = (byte)(rupees >> 8); bytes[87] = (byte)rupees;
        for (int i = 0x4A; i <= 0x4D; i++) bytes[82 + i] = 255;
        Buffer.BlockCopy(bytes, 40, bytes, 1910, 42); return bytes;
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            int feedChecks = NativeCemuTests.Run(Path.Combine(root, "feed"));
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var feed = new NativeCemuFeed();
            using (var form = new MainForm(false, false, Path.Combine(root, "ui-" + Guid.NewGuid().ToString("N")), feed))
            {
                ((System.Windows.Forms.Timer)Get(form, "timer")).Stop();
                Check((bool)Get(form, "nativeListening") && (bool)Get(form, "recording"), "native session automatically listens without process access");
                Check(Get(form, "memory") == null && Get(form, "gameReader") == null, "native adapter never opens a process reader");
                Check(((List<Achievement>)Get(form, "rules")).Count == 110, "native window loads the unchanged 110-objective catalog");
                Check(((List<Achievement>)Get(form, "rules")).Count(a => !a.IsPlanned) == 70, "only the existing 70 rules can execute");
                var store = (ProgressStore)Get(form, "progress");
                for (ulong i = 0; i < 13; i++)
                {
                    feed.Submit(Packet(100 + i * 100, 1, NativeCemuState.Running, 10));
                    Call(form, "Tick", null, EventArgs.Empty); Thread.Sleep(105);
                }
                Check(store.Contains("wallet-10"), "native frames reach the real gameplay gate, engine and isolated progress store");
                var saved = store.Get("wallet-10").Utc;
                Check(((GameSnapshot)Get(form, "current")).Rupees == 10, "native gameplay refreshes the existing presentation");
                feed.Submit(Packet(1500, 2, NativeCemuState.Paused, 100)); Call(form, "Tick", null, EventArgs.Empty);
                Check(Get(form, "current") == null && !store.Contains("wallet-100"), "emulator pause clears live presentation without evaluating frozen memory");
                feed.Submit(Packet(1600, 3, NativeCemuState.Running, 100)); Call(form, "Tick", null, EventArgs.Empty);
                Check(!store.Contains("wallet-100"), "resume starts a fresh gameplay gate");
                Call(form, "Disconnect", "test");
                Check(!(bool)Get(form, "nativeListening") && !(bool)Get(form, "recording"), "disable stops native recording intent");
                Call(form, "StartDemo"); Check(!(bool)Get(form, "demo"), "native session cannot switch into demo progress");
                Call(form, "Connect"); ((System.Windows.Forms.Timer)Get(form, "timer")).Stop();
                Check(((ProgressStore)Get(form, "progress")).Get("wallet-10").Utc == saved, "reactivation preserves identity and award timestamp");
                Call(form, "TogglePause");
                Check((bool)Get(form, "paused") && !(bool)Get(form, "recording"), "native recording has an independent manual pause");
                Call(form, "TogglePause"); ((System.Windows.Forms.Timer)Get(form, "timer")).Stop();
                Check(!(bool)Get(form, "paused") && (bool)Get(form, "recording"), "manual resume restores native recording intent");
                form.Show(); Application.DoEvents(); form.Close();
                Check(!form.Visible && !form.IsDisposed && (bool)Get(form, "recording"), "closing native achievements hides the panel and keeps recording");
                Call(form, "ShutdownNative");
                Check(form.IsDisposed, "emulator shutdown disposes the native panel");
            }
            Console.WriteLine("Native Cemu: " + feedChecks + " feed + " + checks + " UI checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
