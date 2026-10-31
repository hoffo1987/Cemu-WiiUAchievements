using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WiiUAchievements
{
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
