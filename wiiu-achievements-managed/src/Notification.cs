using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WiiUAchievements
{
    // Per-pixel alpha windows (soft shadow, rounded corners, fades) through UpdateLayeredWindow.
    static class Layered
    {
        [StructLayout(LayoutKind.Sequential)] struct Point32 { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct Size32 { public int Cx, Cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct Blend { public byte Op, Flags, Alpha, Format; }
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref Point32 position, ref Size32 size, IntPtr src, ref Point32 source, int key, ref Blend blend, int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

        public static bool Disabled;
        public static bool Available { get { return !Disabled && Environment.OSVersion.Platform == PlatformID.Win32NT; } }

        public static bool Update(IntPtr hwnd, Bitmap bitmap, Point location, byte alpha)
        {
            IntPtr screen = GetDC(IntPtr.Zero), memory = CreateCompatibleDC(screen), hbitmap = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                hbitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memory, hbitmap);
                var size = new Size32 { Cx = bitmap.Width, Cy = bitmap.Height };
                var source = new Point32();
                var position = new Point32 { X = location.X, Y = location.Y };
                var blend = new Blend { Op = 0, Flags = 0, Alpha = alpha, Format = 1 }; // AC_SRC_OVER, AC_SRC_ALPHA
                return UpdateLayeredWindow(hwnd, screen, ref position, ref size, memory, ref source, 0, ref blend, 2); // ULW_ALPHA
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
                if (hbitmap != IntPtr.Zero) { SelectObject(memory, previous); DeleteObject(hbitmap); }
                DeleteDC(memory);
            }
        }
    }

    // Unlock popup: slides in at the top of the screen that shows Cemu, never takes focus and
    // lets clicks pass through to the game.
    public sealed class ToastForm : Form
    {
        public const float Duration = 5.6f;
        const float Edge = 34, CardW = 480, CardH = 124, Panel = 132, InTime = 0.5f, OutTime = 0.42f;
        readonly Achievement achievement;
        readonly Art art;
        readonly string header;
        readonly Rectangle area;
        readonly System.Windows.Forms.Timer frame = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = new Stopwatch();
        readonly bool layered;
        Bitmap fallbackFrame;
        float scale = 1;
        public bool Failed { get; private set; }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; // NOACTIVATE | TOOLWINDOW | TOPMOST
                if (layered) cp.ExStyle |= 0x00080000 | 0x00000020; // LAYERED | TRANSPARENT (click-through)
                return cp;
            }
        }

        public ToastForm(Achievement achievement, Art art, string header, Screen screen)
        {
            this.achievement = achievement; this.art = art; this.header = header;
            layered = Layered.Available;
            area = (screen ?? Screen.PrimaryScreen).WorkingArea;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(246, 249, 251);
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = Math.Max(1f, g.DpiX / 96f); } catch (Exception) { scale = 1; }
            if (layered) { Size = new Size(1, 1); Location = new Point(area.Left, area.Top - 400); }
            else
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                Size = new Size((int)(CardW * scale), (int)(CardH * scale));
                using (var path = Theme.Round(new RectangleF(0, 0, Width, Height), 20 * scale)) Region = new System.Drawing.Region(path);
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top - Height);
            }
            frame.Interval = 15;
            frame.Tick += delegate { Step(); };
            Shown += delegate { clock.Start(); Step(); frame.Start(); };
            FormClosed += delegate { frame.Stop(); frame.Dispose(); if (fallbackFrame != null) fallbackFrame.Dispose(); };
        }

        void Step()
        {
            float t = (float)clock.Elapsed.TotalSeconds;
            if (t >= Duration) { Close(); return; }
            float slide, alpha;
            Motion(t, out slide, out alpha);
            int w = (int)Math.Ceiling((CardW + Edge * 2) * scale);
            int x = area.Left + (area.Width - w) / 2, top = area.Top + (int)((22 - Edge) * scale);
            int y = top - (int)((1 - slide) * (CardH + Edge + 30) * scale);
            if (layered)
            {
                using (var bitmap = Render(achievement, art, header, t, scale))
                {
                    bool ok = false;
                    try { ok = Layered.Update(Handle, bitmap, new Point(x, y), (byte)(255 * alpha)); }
                    catch (Exception) { ok = false; }
                    if (!ok) { Failed = true; Layered.Disabled = true; frame.Stop(); Close(); }
                }
                return;
            }
            if (fallbackFrame != null) fallbackFrame.Dispose();
            fallbackFrame = Render(achievement, art, header, t, scale);
            Location = new Point(x + (int)(Edge * scale), y + (int)(Edge * scale));
            try { Opacity = Math.Max(0.05, alpha); } catch (Exception) { }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (fallbackFrame == null) return;
            e.Graphics.DrawImage(fallbackFrame, -(int)(Edge * scale), -(int)(Edge * scale));
        }

        static void Motion(float t, out float slide, out float alpha)
        {
            // Reduced motion: the card stays in place and only fades in and out.
            if (WiiUAchievements.Motion.Reduced) { slide = 1; alpha = t < InTime ? Theme.EaseOut(t / InTime) : t > Duration - OutTime ? 1 - Theme.EaseIn((t - (Duration - OutTime)) / OutTime) : 1; return; }
            if (t < InTime) { slide = Theme.EaseBack(t / InTime); alpha = Theme.EaseOut(t / (InTime * 0.6f)); }
            else if (t > Duration - OutTime) { float u = (t - (Duration - OutTime)) / OutTime; slide = 1 - Theme.EaseIn(u); alpha = 1 - Theme.EaseIn(u); }
            else { slide = 1; alpha = 1; }
        }

        // Timeline: the card slides in with the game's panel; the medal pops in grey, turns
        // like a coin to reveal its colours, then the rarity glow follows. Celebration grows
        // with rarity: bronze a ring and a few sparkles, silver adds rays and the shine, gold
        // more rays and sparkles, platinum a second ring.
        public const float TurnStart = 0.62f, TurnTime = 0.36f;

        // Renders one frame of the card (with room for its shadow) at time t. Also used by --preview-toast.
        public static Bitmap Render(Achievement a, Art art, string header, float t, float scale)
        {
            var style = art.Style;
            Color tier = Theme.TierColor(art.Tier), glow = Theme.TierGlow(art.Tier);
            var bmp = new Bitmap((int)Math.Ceiling((CardW + Edge * 2) * scale), (int)Math.Ceiling((CardH + Edge * 2) * scale), PixelFormat.Format32bppArgb);
            bool still = WiiUAchievements.Motion.Reduced;
            int level = (int)art.Tier;
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(scale, scale);
                var card = new RectangleF(Edge, Edge, CardW, CardH);
                float turn = Theme.Clamp01((t - TurnStart) / TurnTime), revealed = Theme.EaseOut((t - TurnStart - TurnTime * 0.5f) / 0.5f);
                Theme.Shadow(g, card, 22, 16, 120, 6);
                // Rarity halo: brighter for gold and platinum, and it blooms when the medal turns.
                int halo = (art.Tier >= Tier.Gold ? 34 : 20) + (int)(18 * revealed * (1 - Theme.Clamp01((t - 1.6f) / 1.2f)));
                for (int i = 4; i >= 1; i--)
                    Theme.Stroke(g, Theme.Inflate(card, i * 2.6f, i * 2.6f), 22 + i * 2.6f, Color.FromArgb(halo / i + 6, glow), 3);
                Theme.Fill(g, card, 22, style.CardTop, style.Themed ? style.CardBottom : Color.FromArgb(238, 243, 247));
                style.Parchment(g, card, 22, 0.5f);
                var medalCenter = new PointF(card.X + Panel / 2, card.Y + card.Height / 2);
                using (var path = Theme.Round(card, 22))
                {
                    var state = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    // The game panel: open sea with drifting foam and rising bubbles for Wind Waker.
                    var panel = new RectangleF(card.X, card.Y, Panel, card.Height);
                    using (var b = new LinearGradientBrush(Theme.Inflate(panel, 0, 1), style.Accent, style.Deep, 90f)) g.FillRectangle(b, panel);
                    using (var light = new GraphicsPath())
                    {
                        light.AddEllipse(Theme.Square(medalCenter, 120));
                        using (var b = new PathGradientBrush(light) { CenterColor = Color.FromArgb(90 + (int)(60 * revealed), Theme.Light(glow, 0.5f)), SurroundColors = new[] { Color.FromArgb(0, glow) } }) g.FillPath(b, light);
                    }
                    if (style.Themed) style.WaveBand(g, new RectangleF(panel.X, panel.Bottom - 15, panel.Width, 16), t * 0.12f, 1);
                    else style.Pattern(g, new RectangleF(panel.X, panel.Bottom - 30, panel.Width, 30), t * 1.4f, Color.FromArgb(80, 255, 255, 255));
                    if (!still)
                    using (var pen = new Pen(Color.FromArgb(110, 255, 255, 255), 1.2f))
                        for (int i = 0; i < 6; i++)
                        {
                            float rise = (t * 22 + i * 41) % (card.Height + 16), bubble = 3 + i % 3 * 1.6f;
                            float bx = panel.X + 12 + i * 21 + (float)Math.Sin(t * 2 + i) * 3, by = panel.Bottom + 8 - rise;
                            g.DrawEllipse(pen, bx, by, bubble, bubble);
                        }
                    if (style.Themed)
                    {
                        // Gold rule between the sea panel and the text, as on the frames' ring.
                        using (var pen = new Pen(Color.FromArgb(46, 32, 26), 4.2f)) g.DrawLine(pen, panel.Right, panel.Y, panel.Right, panel.Bottom);
                        using (var b = new LinearGradientBrush(new RectangleF(panel.Right - 2, panel.Y - 1, 4, panel.Height + 2), Theme.Light(style.Trim, 0.35f), style.TrimDark, 90f))
                        using (var pen = new Pen(b, 2.2f)) g.DrawLine(pen, panel.Right, panel.Y, panel.Right, panel.Bottom);
                    }
                    else using (var b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(b, panel.Right - 1, panel.Y, 1.2f, panel.Height);
                    using (var b = new LinearGradientBrush(new RectangleF(panel.Right, card.Y, 90, card.Height), Color.FromArgb(60, style.Soft), Color.FromArgb(0, style.Soft), 0f))
                        g.FillRectangle(b, panel.Right, card.Y, 89, card.Height);
                    float shine = (t - 0.95f) / 0.8f;
                    if (shine > 0 && shine < 1 && !still && art.Tier >= Tier.Silver)
                    {
                        float sx = card.X - 120 + (card.Width + 240) * Theme.EaseInOut(shine);
                        using (var band = new GraphicsPath())
                        {
                            band.AddPolygon(new[] { new PointF(sx, card.Y), new PointF(sx + 70, card.Y), new PointF(sx + 30, card.Bottom), new PointF(sx - 40, card.Bottom) });
                            using (var b = new LinearGradientBrush(new RectangleF(sx - 40, card.Y, 111, card.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                            {
                                var blend = new ColorBlend { Positions = new[] { 0f, 0.5f, 1f }, Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(140, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) } };
                                b.InterpolationColors = blend;
                                g.FillPath(b, band);
                            }
                        }
                    }
                    float remaining = 1 - Theme.Clamp01((t - InTime) / (Duration - InTime - OutTime));
                    using (var b = new LinearGradientBrush(new RectangleF(panel.Right, card.Bottom - 4, card.Width - Panel, 4), style.Glow, style.Accent, 0f))
                        g.FillRectangle(b, panel.Right, card.Bottom - 4, (card.Width - Panel) * remaining, 4);
                    g.Restore(state);
                }
                Theme.Stroke(g, Theme.Inflate(card, -0.5f, -0.5f), 22, Theme.Mix(tier, Color.White, 0.2f), 2f);
                style.Corners(g, card, 34, 5);

                if (turn >= 0.5f)
                {
                    if (art.Tier >= Tier.Silver) Theme.Rays(g, medalCenter, 62 + 6 * level, still ? 0 : t * 26, Color.FromArgb((int)((art.Tier >= Tier.Gold ? 175 : 120) * revealed), glow));
                    for (int k = 0; k < (art.Tier == Tier.Platinum ? 2 : 1); k++)
                    {
                        float ring = Theme.Clamp01((t - TurnStart - TurnTime * 0.5f - k * 0.22f) / 0.6f);
                        if (ring > 0 && ring < 1)
                            using (var pen = new Pen(Color.FromArgb((int)(220 * (1 - ring)), glow), 0.6f + 3.4f * (1 - ring)))
                                g.DrawEllipse(pen, Theme.Square(medalCenter, 96 * (0.95f + (still ? 0.25f : 0.7f) * Theme.EaseOut(ring))));
                    }
                    int sparkles = still ? 0 : 6 + 2 * level + (art.Tier >= Tier.Gold ? 2 : 0);
                    for (int i = 0; i < sparkles; i++)
                    {
                        float u = (t - TurnStart - TurnTime * 0.5f - i * 0.025f) / 1.1f;
                        if (u <= 0 || u >= 1) continue;
                        double ang = i * Math.PI * 2 / sparkles + 0.3;
                        float d = 34 + 46 * Theme.EaseOut(u);
                        var p = new PointF(medalCenter.X + (float)Math.Cos(ang) * d, medalCenter.Y + (float)Math.Sin(ang) * d * 0.8f);
                        Theme.Sparkle(g, p, 6 * (1 - u) + 1.5f, Color.FromArgb((int)(255 * (1 - u)), i % 2 == 0 ? glow : Color.White));
                    }
                }
                float pop = (t - 0.12f) / 0.45f;
                float size = 96 * (pop <= 0 ? 0 : still ? 1 : 0.3f + 0.7f * Theme.EaseBack(pop));
                if (size > 1)
                {
                    float flip = still || turn <= 0 || turn >= 1 ? 1 : Math.Abs((float)Math.Cos(Math.PI * turn));
                    float bump = turn >= 1 && !still ? 1 + (0.06f + 0.02f * level) * (float)Math.Sin(Math.PI * Theme.Clamp01((t - TurnStart - TurnTime) / 0.3f)) : 1;
                    var medalState = g.Save();
                    g.TranslateTransform(medalCenter.X, medalCenter.Y); g.ScaleTransform(Math.Max(0.03f, flip) * bump, bump); g.TranslateTransform(-medalCenter.X, -medalCenter.Y);
                    Theme.Medal(g, Theme.Square(medalCenter, size), art, turn < 0.5f ? 0.88f : 0);
                    g.Restore(medalState);
                }

                float reveal = Theme.EaseOut((t - 0.26f) / 0.45f);
                float tx = card.X + Panel + 18 + (still ? 0 : 14 * (1 - reveal)), tw = card.Right - 18 - (card.X + Panel + 18);
                Theme.Text(g, header, Theme.Font(12, Weight.Heavy), Theme.Alpha(style.AccentDark, reveal), new RectangleF(tx, card.Y + 18, tw, 18));
                Theme.Text(g, a.Title, Theme.Font(22, Weight.Heavy), Theme.Alpha(Theme.Ink, reveal), new RectangleF(tx, card.Y + 38, tw, 32));
                // Rarity appears with the turn of the medal, then the points.
                string rank = Art.TierName(art.Tier).ToUpperInvariant();
                float rw = Theme.Measure(g, rank, Theme.Font(12.5f, Weight.Heavy)).Width + 24, ry = card.Y + 78;
                float rankIn = still ? Theme.Clamp01((t - TurnStart) / 0.3f) : Theme.EaseBack(Theme.Clamp01((t - TurnStart - TurnTime * 0.4f) / 0.4f));
                if (rankIn > 0)
                {
                    var ribbon = new RectangleF(tx, ry, rw, 20);
                    var rs = g.Save();
                    var rc = Theme.Center(ribbon);
                    g.TranslateTransform(rc.X, rc.Y); g.ScaleTransform(0.6f + 0.4f * rankIn, 0.6f + 0.4f * rankIn); g.TranslateTransform(-rc.X, -rc.Y);
                    Theme.TierRibbon(g, ribbon, art.Tier, rank, Theme.Clamp01(rankIn));
                    g.Restore(rs);
                }
                string points = "+" + a.Points + (a.Points == 1 ? " punto" : " punti");
                var star = new RectangleF(tx + rw + 12, ry + 2, 16, 16);
                Theme.Star(g, star, Theme.Alpha(Color.FromArgb(255, 226, 110), reveal), Theme.Alpha(Color.FromArgb(222, 150, 20), reveal));
                Theme.Text(g, points, Theme.Font(14, Weight.Bold), Theme.Alpha(Theme.Muted, reveal), new RectangleF(tx + rw + 34, ry, tw - rw - 34, 20), StringAlignment.Near, StringAlignment.Center, false);
            }
            return bmp;
        }
    }

    // Original bell arpeggios synthesised once in memory (no external sound file). Rarer
    // medals climb further: bronze three notes, silver four, gold and platinum a full cadence.
    public static class Chime
    {
        static readonly SoundPlayer[] players = new SoundPlayer[4];
        public static void Play() { Play(Tier.Bronze); }
        public static void Play(Tier tier)
        {
            int index = Math.Max(0, Math.Min(players.Length - 1, (int)tier));
            try
            {
                if (players[index] == null) { var player = new SoundPlayer(new MemoryStream(Build(tier))); player.Load(); players[index] = player; }
                players[index].Play();
            }
            catch (Exception) { try { SystemSounds.Asterisk.Play(); } catch (Exception) { } }
        }
        public static byte[] Build() { return Build(Tier.Bronze); }
        public static byte[] Build(Tier tier)
        {
            const int rate = 44100;
            const double C6 = 1046.50, E6 = 1318.51, G6 = 1567.98, C7 = 2093.00, E7 = 2637.02, G7 = 3135.96, C8 = 4186.01;
            var samples = new double[(int)(rate * (1.3 + 0.15 * (int)tier))];
            switch (tier)
            {
                case Tier.Bronze:
                    Note(samples, rate, 0.00, C6, 0.50); Note(samples, rate, 0.085, G6, 0.42); Note(samples, rate, 0.17, C7, 0.38);
                    Note(samples, rate, 0.17, E7, 0.12); break;
                case Tier.Silver:
                    Note(samples, rate, 0.00, C6, 0.46); Note(samples, rate, 0.075, E6, 0.4); Note(samples, rate, 0.15, G6, 0.4);
                    Note(samples, rate, 0.225, C7, 0.4); Note(samples, rate, 0.225, E7, 0.14); break;
                default:
                    Note(samples, rate, 0.00, C6, 0.44); Note(samples, rate, 0.07, E6, 0.38); Note(samples, rate, 0.14, G6, 0.38);
                    Note(samples, rate, 0.21, C7, 0.4); Note(samples, rate, 0.30, E7, 0.32); Note(samples, rate, 0.30, G7, 0.12);
                    // A warm low bell under the cadence, then a light twinkle on top.
                    Note(samples, rate, 0.30, C6 / 2, 0.2); Note(samples, rate, 0.44, G7, 0.07); Note(samples, rate, 0.5, E7, 0.06);
                    if (tier == Tier.Platinum) { Note(samples, rate, 0.39, G7, 0.26); Note(samples, rate, 0.39, C8, 0.1); Note(samples, rate, 0.58, C8, 0.05); }
                    break;
            }
            double peak = 0;
            foreach (var s in samples) peak = Math.Max(peak, Math.Abs(s));
            double gain = peak > 0 ? 0.42 / peak : 0;
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                int data = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + data); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(data);
                foreach (var s in samples) w.Write((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, s * gain * short.MaxValue)));
                w.Flush();
                return stream.ToArray();
            }
        }
        static void Note(double[] samples, int rate, double start, double frequency, double amplitude)
        {
            int first = (int)(start * rate);
            for (int i = first; i < samples.Length; i++)
            {
                double t = (i - first) / (double)rate;
                double envelope = (1 - Math.Exp(-t / 0.003)) * Math.Exp(-t / 0.32);
                double tone = Math.Sin(2 * Math.PI * frequency * t)
                    + 0.32 * Math.Sin(2 * Math.PI * frequency * 2 * t) * Math.Exp(-t / 0.12)
                    + 0.12 * Math.Sin(2 * Math.PI * frequency * 3.01 * t) * Math.Exp(-t / 0.06);
                samples[i] += amplitude * envelope * tone;
            }
        }
    }
}
