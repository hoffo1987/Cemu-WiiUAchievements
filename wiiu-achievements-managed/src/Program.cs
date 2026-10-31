using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WiiUAchievements
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var toastPreview = args.FirstOrDefault(a => a.StartsWith("--preview-toast="));
                if (toastPreview != null) { SaveToastPreview(Path.GetFullPath(toastPreview.Substring(16))); return 0; }
                var medalPreview = args.FirstOrDefault(a => a.StartsWith("--preview-medals="));
                if (medalPreview != null) { SaveMedalSheet(Path.GetFullPath(medalPreview.Substring(17))); return 0; }
                var dataArgument = args.FirstOrDefault(a => a.StartsWith("--data-dir="));
                string isolatedData = dataArgument == null ? null : Path.GetFullPath(dataArgument.Substring(11));
                if (isolatedData == null && args.Any(a => a.StartsWith("--preview="))) isolatedData = Path.Combine(Path.GetTempPath(), "WiiUAchievements-preview-" + Guid.NewGuid().ToString("N"));
                string instance;
                using (var sha = SHA256.Create()) instance = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(isolatedData ?? UserData.DefaultDirectory).TrimEnd('\\').ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
                bool first;
                using (var mutex = new Mutex(true, "Local\\WiiUAchievements-" + instance, out first))
                {
                if (!first) { MessageBox.Show("Questa copia dell'app è già aperta.", "Wii U Achievements"); return 0; }
                using (var form = new MainForm(args.Contains("--demo"), args.Contains("--connect"), isolatedData))
                {
                    var preview = args.FirstOrDefault(a => a.StartsWith("--preview="));
                    if (preview != null)
                    {
                        var timer = new System.Windows.Forms.Timer { Interval = 1200 };
                        timer.Tick += delegate { timer.Stop(); form.SavePreview(Path.GetFullPath(preview.Substring(10))); form.Close(); timer.Dispose(); };
                        timer.Start();
                    }
                    Application.Run(form);
                }
                }
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Wii U Achievements", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
        // Developer aid: one frame of the unlock popup, rendered without showing a window.
        static void SaveToastPreview(string path)
        {
            var game = GameCatalog.Default;
            var rules = game.LoadRules(AppDomain.CurrentDomain.BaseDirectory);
            var sample = rules.Where(a => !a.IsPlanned).OrderByDescending(a => a.Rarity).ThenByDescending(a => a.Points).First();
            using (var frame = ToastForm.Render(sample, Art.For(sample, rules, game.Id), "OBIETTIVO SBLOCCATO", 1.45f, 1))
                frame.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        // Developer aid: every medal of the default game, unlocked and locked, in catalog order.
        static void SaveMedalSheet(string path)
        {
            var game = GameCatalog.Default;
            var rules = game.LoadRules(AppDomain.CurrentDomain.BaseDirectory);
            const int cell = 112, columns = 10, label = 30;
            int rows = (rules.Count + columns - 1) / columns;
            using (var sheet = new System.Drawing.Bitmap(columns * cell, rows * (cell * 2 + label)))
            {
                using (var g = System.Drawing.Graphics.FromImage(sheet))
                {
                    g.Clear(Theme.BackTop);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    for (int i = 0; i < rules.Count; i++)
                    {
                        var art = Art.For(rules[i], rules, game.Id);
                        float x = i % columns * cell, y = i / columns * (cell * 2 + label);
                        Theme.Medal(g, new System.Drawing.RectangleF(x + 8, y + 4, cell - 16, cell - 16), art, 0);
                        Theme.Medal(g, new System.Drawing.RectangleF(x + 8, y + cell, cell - 16, cell - 16), art, 0.88f);
                        Theme.Text(g, rules[i].Title, Theme.Font(10, Weight.Bold), Theme.Ink, new System.Drawing.RectangleF(x + 2, y + cell * 2 - 12, cell - 4, label + 8),
                            System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Near, true);
                    }
                }
                sheet.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
