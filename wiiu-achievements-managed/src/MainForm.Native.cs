using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WiiUAchievements
{
    // In-process presentation/session adapter. The engine, decoders, progress store and
    // cloud client are the same ones used by the independent application.
    public sealed partial class MainForm
    {
        readonly NativeCemuFeed nativeFeed;
        bool nativeListening, nativeShutdown;
        bool NativeMode { get { return nativeFeed != null; } }

        void ConnectNative()
        {
            if (nativeListening) return;
            try
            {
                if (String.IsNullOrWhiteSpace(player.Text)) throw new InvalidOperationException("Inserisci un nome per il profilo locale.");
                activeProfile = selectedGame.Profiles[0];
                session = new GameSession { TitleId = activeProfile.TitleId, Version = activeProfile.TitleVersion,
                    RpxHash = activeProfile.RpxHash, CemuVersion = activeProfile.CemuVersion, Pid = Process.GetCurrentProcess().Id };
                progress = OpenProgress(session.Fingerprint);
                engine = new AchievementEngine(rules, progress, AchievementRecognition.IncludeExistingSave);
                nativeListening = true; recording = true; paused = false; current = null;
                SaveSettings(); SetConnected(true); RefreshList(); timer.Start();
                hint = "Gli obiettivi seguono la partita in Cemu. Chiudere questa finestra lascia attiva la registrazione.";
                SetAutomaticWaiting("Avvia The Wind Waker HD EUR v0 in Cemu; menu, caricamenti e pause sospendono il rilevamento.");
            }
            catch (Exception ex)
            {
                nativeListening = false; recording = false; engine = null;
                SetConnected(false); SetStatus("Obiettivi non attivi", ex.Message, StatusKind.Error); Log(ex.Message);
            }
        }

        void TickNative()
        {
            if (!nativeListening || !recording || engine == null) return;
            try
            {
                NativeCemuSample sample;
                if (!nativeFeed.TryRead(out sample))
                {
                    if (!nativeFeed.IsFresh)
                    {
                        engine.Reset(); SetAutomaticWaiting("In attesa di nuovi fotogrammi della partita in Cemu.");
                    }
                    return; // Never count the same frozen frame twice.
                }
                if (sample.Reset) engine.Reset();
                if (sample.State != NativeCemuState.Running || sample.Snapshot == null)
                {
                    engine.Reset();
                    SetAutomaticWaiting(sample.State == NativeCemuState.Paused ? "Emulazione in pausa." : "Nessuna partita supportata attiva in Cemu.");
                    return;
                }
                ProcessSample(sample.Snapshot);
            }
            catch (Exception ex)
            {
                Disconnect("Registrazione sospesa: " + ex.Message);
                SetStatus("Obiettivi non attivi", ex.Message, StatusKind.Error);
            }
        }

        internal void ShutdownNative()
        {
            nativeShutdown = true; Close(); Dispose();
        }
    }

    // The CLR is hosted by Cemu; WinForms owns its own background STA/message loop.
    // Native frame callbacks only publish bounded packets. No window, disk or cloud
    // work runs on a PPC, renderer or debugger thread.
    public static class NativeCemuUi
    {
        static readonly object sync = new object();
        static MainForm form;
        static Thread thread;
        static bool requestedShow, stopping;
        static string startupError;

        public static void Start()
        {
            lock (sync)
            {
                if (thread != null || stopping) return;
                thread = new Thread(Run) { IsBackground = true, Name = "Wii U Achievements UI" };
                thread.SetApartmentState(ApartmentState.STA); thread.Start();
            }
        }

        static void Run()
        {
            try
            {
                // Match Program.Main exactly: two writers must never share an archive.
                string name;
                using (var sha = SHA256.Create()) name = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    Path.GetFullPath(UserData.DefaultDirectory).TrimEnd('\\').ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
                bool first;
                using (var mutex = new Mutex(true, "Local\\WiiUAchievements-" + name, out first))
                {
                    if (!first) throw new InvalidOperationException("Chiudi l'app Wii U Achievements separata e riavvia Cemu per attivare gli obiettivi integrati.");
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (var window = new MainForm(false, false, null, NativeCemuBridge.Feed))
                    using (var context = new ApplicationContext())
                    {
                        IntPtr handle = window.Handle;
                        bool show;
                        lock (sync) { if (stopping) return; form = window; show = requestedShow; }
                        if (show) window.Show();
                        Application.Run(context);
                        lock (sync) form = null;
                    }
                    mutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                bool show;
                lock (sync) { startupError = ex.Message; show = requestedShow && !stopping; form = null; }
                if (show) ShowError(ex.Message);
            }
        }

        public static void Show()
        {
            MainForm target; string error;
            lock (sync) { if (stopping) return; requestedShow = true; target = form; error = startupError; }
            if (error != null) { ShowError(error); return; }
            Post(target, delegate { target.Show(); if (target.WindowState == FormWindowState.Minimized) target.WindowState = FormWindowState.Normal; target.Activate(); });
        }

        public static void Stop()
        {
            MainForm target;
            lock (sync) { stopping = true; target = form; }
            Post(target, delegate { target.ShutdownNative(); Application.ExitThread(); });
        }

        static void Post(MainForm target, Action action)
        {
            if (target == null) return;
            try { if (!target.IsDisposed && target.IsHandleCreated) target.BeginInvoke(action); }
            catch (InvalidOperationException) { } // Concurrent process/window teardown.
        }

        static void ShowError(string message)
        {
            var notice = new Thread(delegate() { MessageBox.Show(message, "Wii U Achievements", MessageBoxButtons.OK, MessageBoxIcon.Information); });
            notice.IsBackground = true; notice.SetApartmentState(ApartmentState.STA); notice.Start();
        }
    }
}
