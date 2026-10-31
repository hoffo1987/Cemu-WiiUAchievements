using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WiiUAchievements
{
    public enum Weight { Regular, Bold, Heavy }

    // Original palette/vector artwork for the shared chrome; owner-supplied branding and
    // game art are loaded separately by BrandArt/GameStyle. Text uses Nunito (SIL OFL 1.1).
    public static partial class Theme
    {
        public static readonly Color Ink = Color.FromArgb(52, 58, 64), Muted = Color.FromArgb(124, 135, 144),
            Faint = Color.FromArgb(184, 193, 200), Line = Color.FromArgb(212, 219, 225),
            Accent = Color.FromArgb(0, 168, 226), AccentDark = Color.FromArgb(0, 126, 192), AccentSoft = Color.FromArgb(214, 241, 252),
            Glow = Color.FromArgb(64, 206, 255), Success = Color.FromArgb(82, 180, 46), Warning = Color.FromArgb(236, 146, 20),
            Danger = Color.FromArgb(222, 70, 82), BackTop = Color.FromArgb(250, 251, 252), BackBottom = Color.FromArgb(224, 230, 235);

        static PrivateFontCollection collection;
        static FontFamily regularFamily, heavyFamily;
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        // Fonts by tenth of a pixel and weight: an integer key, so asking for a font every frame
        // allocates nothing.
        static readonly Dictionary<int, Font> sized = new Dictionary<int, Font>();
        public static bool EmbeddedFonts { get { LoadFonts(); return regularFamily != null; } }

        static void LoadFonts()
        {
            if (collection != null) return;
            collection = new PrivateFontCollection();
            if (Type.GetType("Mono.Runtime") != null)
            {
                // libgdiplus renders memory fonts as empty boxes: use an installed Nunito if present.
                try { regularFamily = new FontFamily("Nunito"); } catch (ArgumentException) { }
                return;
            }
            foreach (var name in new[] { "Nunito-Regular.ttf", "Nunito-Bold.ttf", "Nunito-ExtraBold.ttf" })
            {
                try
                {
                    byte[] data = null;
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WiiUAchievements.Fonts." + name))
                        if (stream != null) { data = new byte[stream.Length]; int read = 0; while (read < data.Length) { int n = stream.Read(data, read, data.Length - read); if (n <= 0) break; read += n; } }
                    if (data == null)
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "fonts", name);
                        if (File.Exists(path)) data = File.ReadAllBytes(path);
                    }
                    if (data == null) continue;
                    // GDI+ reads memory fonts lazily, so this block intentionally lives as long as the process.
                    IntPtr memory = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, memory, data.Length);
                    collection.AddMemoryFont(memory, data.Length);
                }
                catch (Exception) { }
            }
            foreach (var family in collection.Families)
            {
                if (family.Name == "Nunito") regularFamily = family;
                else if (family.Name == "Nunito ExtraBold") heavyFamily = family;
            }
        }

        // Sizes are in pixels of the 96-DPI logical canvas; the window applies the DPI scale.
        public static Font Font(float size, Weight weight)
        {
            int fast = (int)Math.Round(size * 10) * 4 + (int)weight;
            Font cached;
            if (sized.TryGetValue(fast, out cached)) return cached;
            cached = MakeFont(size, weight);
            sized[fast] = cached;
            return cached;
        }
        static Font MakeFont(float size, Weight weight)
        {
            string key = size.ToString("0.0", CultureInfo.InvariantCulture) + weight;
            Font font;
            if (fonts.TryGetValue(key, out font)) return font;
            LoadFonts();
            try
            {
                if (weight == Weight.Heavy && heavyFamily != null) font = new Font(heavyFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
                else if (regularFamily != null)
                {
                    var style = weight == Weight.Regular ? FontStyle.Regular : FontStyle.Bold;
                    if (regularFamily.IsStyleAvailable(style)) font = new Font(regularFamily, size, style, GraphicsUnit.Pixel);
                }
            }
            catch (Exception) { font = null; }
            if (font == null) font = new Font("Segoe UI", size, weight == Weight.Regular ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Pixel);
            fonts[key] = font;
            return font;
        }
        public static Font Mono(float size)
        {
            string key = "mono" + size.ToString("0.0", CultureInfo.InvariantCulture);
            Font font;
            if (!fonts.TryGetValue(key, out font)) fonts[key] = font = new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Pixel);
            return font;
        }

        // ---- Colour and timing helpers ----
        public static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        public static float EaseOut(float t) { t = Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        public static float EaseIn(float t) { t = Clamp01(t); return t * t * t; }
        public static float EaseInOut(float t) { t = Clamp01(t); return t < 0.5f ? 4 * t * t * t : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2; }
        public static float EaseBack(float t) { t = Clamp01(t); const float c = 1.70158f; float u = t - 1; return 1 + (c + 1) * u * u * u + c * u * u; }
        public static float Approach(float value, float target, float dt, float rate)
        {
            float next = value + (target - value) * (1 - (float)Math.Exp(-dt * rate));
            return Math.Abs(next - target) < 0.002f ? target : next;
        }
        public static Color Mix(Color a, Color b, float t)
        {
            t = Clamp01(t);
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        public static Color Alpha(Color c, float alpha) { return Color.FromArgb((int)(c.A * Clamp01(alpha)), c.R, c.G, c.B); }
        public static Color Light(Color c, float t) { return Mix(c, Color.FromArgb(c.A, 255, 255, 255), t); }
        public static Color Dark(Color c, float t) { return Mix(c, Color.FromArgb(c.A, 0, 0, 0), t); }
        public static Color Desaturate(Color c, float t)
        {
            if (t <= 0) return c;
            int l = (int)(c.R * 0.3f + c.G * 0.59f + c.B * 0.11f);
            l = Math.Min(255, (int)(l * 0.82f + 52));
            return Mix(c, Color.FromArgb(c.A, l, l, l + 4 > 255 ? 255 : l + 4), t);
        }

        // ---- Text ----
        // Text formats are immutable once built; one per alignment/wrap combination and thread.
        [ThreadStatic] static Dictionary<int, StringFormat> formats;
        [ThreadStatic] static SolidBrush textBrush;
        static StringFormat CachedFormat(StringAlignment h, StringAlignment v, bool wrap)
        {
            if (formats == null) formats = new Dictionary<int, StringFormat>();
            int key = (int)h * 8 + (int)v * 2 + (wrap ? 1 : 0);
            StringFormat f;
            if (!formats.TryGetValue(key, out f)) formats[key] = f = Format(h, v, wrap);
            return f;
        }
        // Measurements repeat every frame with the same strings: remember them per font.
        [ThreadStatic] static Dictionary<Font, Dictionary<string, SizeF>> measured;
        [ThreadStatic] static int measuredCount;
        static Dictionary<string, SizeF> MeasureCache(Font font)
        {
            if (measured == null || measuredCount > 6000) { measured = new Dictionary<Font, Dictionary<string, SizeF>>(); measuredCount = 0; }
            Dictionary<string, SizeF> cache;
            if (!measured.TryGetValue(font, out cache)) measured[font] = cache = new Dictionary<string, SizeF>(StringComparer.Ordinal);
            return cache;
        }
        static StringFormat Format(StringAlignment h, StringAlignment v, bool wrap)
        {
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.Alignment = h; f.LineAlignment = v;
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            if (wrap) { f.FormatFlags |= StringFormatFlags.LineLimit; f.Trimming = StringTrimming.EllipsisWord; }
            else { f.FormatFlags |= StringFormatFlags.NoWrap; f.Trimming = StringTrimming.EllipsisCharacter; }
            return f;
        }
        public static SizeF Measure(Graphics g, string text, Font font)
        {
            // Results depend on the font and the text rendering mode; all callers measure with
            // the same grid-fitted settings as they draw.
            var cache = MeasureCache(font);
            string key = text ?? "";
            SizeF size;
            if (cache.TryGetValue(key, out size)) return size;
            size = g.MeasureString(key, font, new PointF(0, 0), CachedFormat(StringAlignment.Near, StringAlignment.Near, false));
            cache[key] = size; measuredCount++;
            return size;
        }
        public static SizeF Measure(Graphics g, string text, Font font, float width)
        {
            var cache = MeasureCache(font);
            string key = width.ToString("0.0", CultureInfo.InvariantCulture) + "\u0001" + (text ?? "");
            SizeF size;
            if (cache.TryGetValue(key, out size)) return size;
            size = g.MeasureString(text ?? "", font, new SizeF(width, 10000), CachedFormat(StringAlignment.Near, StringAlignment.Near, true));
            cache[key] = size; measuredCount++;
            return size;
        }
        public static void Text(Graphics g, string text, Font font, Color color, RectangleF r)
        {
            Text(g, text, font, color, r, StringAlignment.Near, StringAlignment.Near, false);
        }
        public static void Text(Graphics g, string text, Font font, Color color, RectangleF r, StringAlignment h, StringAlignment v, bool wrap)
        {
            if (String.IsNullOrEmpty(text) || color.A == 0 || r.Width <= 1) return;
            float need = font.Size * 1.6f;
            if (!wrap && r.Height < need)
            {
                // Some GDI+ implementations drop a line that does not fit its box entirely.
                float grow = need - r.Height;
                r = new RectangleF(r.X, v == StringAlignment.Center ? r.Y - grow / 2 : v == StringAlignment.Far ? r.Y - grow : r.Y, r.Width, need);
            }
            if (textBrush == null) textBrush = new SolidBrush(color); else textBrush.Color = color;
            g.DrawString(text, font, textBrush, r, CachedFormat(h, v, wrap));
        }
        public static void ShadowText(Graphics g, string text, Font font, Color color, RectangleF r, StringAlignment h, StringAlignment v, bool wrap, int shadow)
        {
            r.Offset(0, 1.5f); Text(g, text, font, Color.FromArgb(shadow, 0, 30, 60), r, h, v, wrap);
            r.Offset(0, -1.5f); Text(g, text, font, color, r, h, v, wrap);
        }

        // ---- Shapes ----
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        public static GraphicsPath TopRound(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }
        public static RectangleF Inflate(RectangleF r, float x, float y) { r.Inflate(x, y); return r; }
        public static RectangleF Square(PointF center, float size) { return new RectangleF(center.X - size / 2, center.Y - size / 2, size, size); }
        public static PointF Center(RectangleF r) { return new PointF(r.X + r.Width / 2, r.Y + r.Height / 2); }

        public static void Fill(Graphics g, GraphicsPath path, Color color) { using (var b = new SolidBrush(color)) g.FillPath(b, path); }
        public static void Fill(Graphics g, RectangleF r, float radius, Color top, Color bottom)
        {
            if (r.Width < 1 || r.Height < 1) return;
            using (var path = Round(r, radius))
            using (var b = new LinearGradientBrush(Inflate(r, 0, 1), top, bottom, LinearGradientMode.Vertical)) g.FillPath(b, path);
        }
        public static void Stroke(Graphics g, RectangleF r, float radius, Color color, float width)
        {
            using (var path = Round(r, radius)) using (var p = new Pen(color, width)) g.DrawPath(p, path);
        }
        // Soft drop shadow built from translucent layers.
        public static void Shadow(Graphics g, RectangleF r, float radius, float spread, int alpha, float dy)
        {
            const int steps = 6;
            // The element is drawn on top: skipping its interior keeps big shadows cheap.
            var state = g.Save();
            if (r.Width > 6 && r.Height > 6)
                using (var hole = Round(Inflate(r, -1.5f, -1.5f), Math.Max(0, radius - 1.5f))) g.SetClip(hole, CombineMode.Exclude);
            for (int i = steps; i >= 1; i--)
            {
                float grow = spread * i / steps;
                var rr = new RectangleF(r.X - grow, r.Y - grow + dy, r.Width + grow * 2, r.Height + grow * 2);
                using (var path = Round(rr, radius + grow)) Fill(g, path, Color.FromArgb(Math.Max(1, alpha / steps), 20, 44, 64));
            }
            g.Restore(state);
        }
        // The cyan outline used as a selection cursor in the Wii U system menus.
        public static void Cursor(Graphics g, RectangleF r, float radius, float strength)
        {
            if (strength <= 0.01f) return;
            for (int i = 4; i >= 1; i--)
                Stroke(g, Inflate(r, i * 2.2f, i * 2.2f), radius + i * 2.2f, Color.FromArgb((int)(strength * 30 / i), Glow), 3);
            Stroke(g, Inflate(r, 1, 1), radius + 1, Color.FromArgb((int)(255 * strength), Accent), 3);
        }

        // ---- Icons (unit-box vector drawings) ----
        static PointF U(RectangleF r, float x, float y) { return new PointF(r.X + r.Width * x, r.Y + r.Height * y); }

        public static GraphicsPath HeartPath(RectangleF r)
        {
            Func<float, float, PointF> P = (x, y) => new PointF(r.X + r.Width * (0.5f + x * 0.5f), r.Y + r.Height * (0.5f + y * 0.5f));
            var p = new GraphicsPath();
            p.AddBezier(P(0, 0.92f), P(-0.36f, 0.62f), P(-1f, 0.22f), P(-1f, -0.3f));
            p.AddBezier(P(-1f, -0.3f), P(-1f, -0.74f), P(-0.7f, -0.96f), P(-0.47f, -0.96f));
            p.AddBezier(P(-0.47f, -0.96f), P(-0.24f, -0.96f), P(-0.05f, -0.8f), P(0, -0.56f));
            p.AddBezier(P(0, -0.56f), P(0.05f, -0.8f), P(0.24f, -0.96f), P(0.47f, -0.96f));
            p.AddBezier(P(0.47f, -0.96f), P(0.7f, -0.96f), P(1f, -0.74f), P(1f, -0.3f));
            p.AddBezier(P(1f, -0.3f), P(1f, 0.22f), P(0.36f, 0.62f), P(0, 0.92f));
            p.CloseFigure();
            return p;
        }
        public static void Heart(Graphics g, RectangleF r, float mono)
        {
            using (var path = HeartPath(r))
            {
                using (var b = new LinearGradientBrush(Inflate(r, 0, 1), Desaturate(Color.FromArgb(255, 104, 120), mono), Desaturate(Color.FromArgb(206, 22, 52), mono), 70f))
                    g.FillPath(b, path);
                var state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    g.FillEllipse(b, r.X + r.Width * 0.14f, r.Y + r.Height * 0.12f, r.Width * 0.28f, r.Height * 0.2f);
                g.Restore(state);
                using (var pen = new Pen(Desaturate(Color.FromArgb(240, 186, 60), mono), Math.Max(1.2f, r.Width * 0.075f)) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
            }
        }
        // Quarter-heart gauge, filled clockwise from the lower-left quarter.
        public static void HeartQuarters(Graphics g, RectangleF r, int quarters)
        {
            using (var path = HeartPath(r))
            {
                Fill(g, path, Color.FromArgb(120, 255, 255, 255));
                if (quarters > 0)
                {
                    var state = g.Save();
                    using (var pie = new GraphicsPath())
                    {
                        if (quarters >= 4) pie.AddRectangle(Inflate(r, 2, 2));
                        else { var big = Inflate(r, r.Width, r.Height); pie.AddPie(big.X, big.Y, big.Width, big.Height, 90, quarters * 90); }
                        g.SetClip(pie, CombineMode.Intersect);
                    }
                    using (var b = new LinearGradientBrush(Inflate(r, 0, 1), Color.FromArgb(255, 96, 112), Color.FromArgb(212, 28, 56), 90f)) g.FillPath(b, path);
                    g.Restore(state);
                }
                using (var pen = new Pen(Color.FromArgb(170, 40, 56), Math.Max(1f, r.Width * 0.09f)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
            }
        }

        static readonly Color[] gemColors = {
            Color.FromArgb(58, 190, 74), Color.FromArgb(46, 126, 226), Color.FromArgb(226, 52, 64),
            Color.FromArgb(146, 72, 214), Color.FromArgb(240, 178, 30), Color.FromArgb(170, 182, 194) };
        public static Color GemColor(int level) { return gemColors[Math.Max(0, Math.Min(gemColors.Length - 1, level))]; }
        public static void Gem(Graphics g, RectangleF r, Color color, float mono)
        {
            color = Desaturate(color, mono);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, hh = r.Height * 0.47f, hw = hh * 0.58f, sh = hh * 0.44f;
            Func<float, float, PointF> P = (x, y) => new PointF(cx + x, cy + y);
            PointF[] o = { P(0, -hh), P(hw, -sh), P(hw, sh), P(0, hh), P(-hw, sh), P(-hw, -sh) };
            float ih = hh * 0.6f, iw = hw * 0.5f, ish = sh * 0.62f;
            PointF[] i = { P(0, -ih), P(iw, -ish), P(iw, ish), P(0, ih), P(-iw, ish), P(-iw, -ish) };
            Color[] facets = { Light(color, 0.42f), Light(color, 0.62f), Dark(color, 0.18f), Dark(color, 0.36f), Dark(color, 0.24f), Light(color, 0.18f) };
            for (int k = 0; k < 6; k++)
            {
                int n = (k + 1) % 6;
                using (var b = new SolidBrush(facets[k])) g.FillPolygon(b, new[] { o[k], o[n], i[n], i[k] });
            }
            using (var b = new LinearGradientBrush(new RectangleF(cx - iw, cy - ih - 1, iw * 2, ih * 2 + 2), Light(color, 0.3f), Dark(color, 0.05f), 90f)) g.FillPolygon(b, i);
            using (var pen = new Pen(Alpha(Light(color, 0.75f), 0.55f), Math.Max(0.8f, r.Width * 0.018f))) g.DrawPolygon(pen, i);
            using (var pen = new Pen(Dark(color, 0.5f), Math.Max(1f, r.Width * 0.035f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, o);
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                g.FillPolygon(b, new[] { P(-iw * 0.7f, -ish * 0.5f), P(-iw * 0.15f, -ih * 0.75f), P(-iw * 0.05f, -ih * 0.45f), P(-iw * 0.55f, -ish * 0.05f) });
        }
        public static void Star(Graphics g, RectangleF r, Color top, Color bottom)
        {
            var pts = new PointF[10];
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height * 0.53f, R = r.Width * 0.5f;
            for (int k = 0; k < 10; k++)
            {
                double a = -Math.PI / 2 + k * Math.PI / 5;
                float rad = k % 2 == 0 ? R : R * 0.46f;
                pts[k] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
            }
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(pts);
                using (var b = new LinearGradientBrush(Inflate(r, 0, 1), top, bottom, 90f)) g.FillPath(b, path);
                using (var pen = new Pen(Dark(bottom, 0.25f), Math.Max(1f, r.Width * 0.06f)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
            }
        }
        public static void Sparkle(Graphics g, PointF c, float size, Color color)
        {
            if (color.A == 0 || size <= 0.2f) return;
            float s = size, w = size * 0.22f;
            var pts = new[] { new PointF(c.X, c.Y - s), new PointF(c.X + w, c.Y - w), new PointF(c.X + s, c.Y), new PointF(c.X + w, c.Y + w),
                new PointF(c.X, c.Y + s), new PointF(c.X - w, c.Y + w), new PointF(c.X - s, c.Y), new PointF(c.X - w, c.Y - w) };
            using (var b = new SolidBrush(color)) g.FillPolygon(b, pts);
        }
        public static void Trophy(Graphics g, RectangleF r)
        {
            Color top = Color.FromArgb(255, 222, 110), bottom = Color.FromArgb(214, 150, 20), edge = Color.FromArgb(170, 112, 10);
            using (var pen = new Pen(edge, r.Width * 0.08f))
            {
                g.DrawArc(pen, U(r, 0.06f, 0.16f).X, U(r, 0.06f, 0.16f).Y, r.Width * 0.3f, r.Height * 0.3f, 90, 180);
                g.DrawArc(pen, U(r, 0.64f, 0.16f).X, U(r, 0.64f, 0.16f).Y, r.Width * 0.3f, r.Height * 0.3f, 270, 180);
            }
            using (var cup = new GraphicsPath())
            {
                cup.AddLine(U(r, 0.2f, 0.1f), U(r, 0.8f, 0.1f));
                cup.AddBezier(U(r, 0.8f, 0.1f), U(r, 0.8f, 0.42f), U(r, 0.66f, 0.58f), U(r, 0.55f, 0.6f));
                cup.AddLine(U(r, 0.55f, 0.6f), U(r, 0.55f, 0.72f));
                cup.AddLine(U(r, 0.7f, 0.76f), U(r, 0.7f, 0.9f));
                cup.AddLine(U(r, 0.3f, 0.9f), U(r, 0.3f, 0.76f));
                cup.AddLine(U(r, 0.45f, 0.72f), U(r, 0.45f, 0.6f));
                cup.AddBezier(U(r, 0.45f, 0.6f), U(r, 0.34f, 0.58f), U(r, 0.2f, 0.42f), U(r, 0.2f, 0.1f));
                cup.CloseFigure();
                using (var b = new LinearGradientBrush(Inflate(r, 0, 1), top, bottom, 90f)) g.FillPath(b, cup);
                using (var pen = new Pen(edge, Math.Max(1f, r.Width * 0.05f)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, cup);
            }
            Sparkle(g, U(r, 0.38f, 0.3f), r.Width * 0.1f, Color.FromArgb(230, 255, 255, 255));
        }
        public static void Lock(Graphics g, RectangleF r, Color color)
        {
            using (var pen = new Pen(color, r.Width * 0.11f)) g.DrawArc(pen, U(r, 0.3f, 0.12f).X, U(r, 0.3f, 0.12f).Y, r.Width * 0.4f, r.Height * 0.52f, 180, 180);
            using (var body = Round(new RectangleF(U(r, 0.18f, 0.42f), new SizeF(r.Width * 0.64f, r.Height * 0.48f)), r.Width * 0.1f)) Fill(g, body, color);
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, Square(U(r, 0.5f, 0.62f), r.Width * 0.14f));
        }
        public static void Check(Graphics g, RectangleF r, Color color, float width)
        {
            using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { U(r, 0.26f, 0.52f), U(r, 0.43f, 0.68f), U(r, 0.75f, 0.34f) });
        }
        public static void Gear(Graphics g, RectangleF r, Color color)
        {
            var pts = new List<PointF>();
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, outer = r.Width * 0.48f, inner = r.Width * 0.34f;
            for (int k = 0; k < 8; k++)
            {
                double a = k * Math.PI / 4;
                foreach (var pair in new[] { new[] { -0.36, inner }, new[] { -0.2, outer }, new[] { 0.2, outer }, new[] { 0.36, inner } })
                    pts.Add(new PointF(cx + (float)(Math.Cos(a + pair[0]) * pair[1]), cy + (float)(Math.Sin(a + pair[0]) * pair[1])));
            }
            using (var path = new GraphicsPath(FillMode.Alternate))
            {
                path.AddPolygon(pts.ToArray());
                path.AddEllipse(Square(new PointF(cx, cy), r.Width * 0.3f));
                Fill(g, path, color);
            }
        }
        public static void ListIcon(Graphics g, RectangleF r, Color color)
        {
            for (int k = 0; k < 3; k++)
            {
                float y = r.Y + r.Height * (0.24f + k * 0.26f);
                using (var b = new SolidBrush(color)) g.FillEllipse(b, r.X + r.Width * 0.1f, y - r.Height * 0.07f, r.Height * 0.14f, r.Height * 0.14f);
                using (var bar = Round(new RectangleF(r.X + r.Width * 0.33f, y - r.Height * 0.06f, r.Width * 0.58f, r.Height * 0.12f), r.Height * 0.06f)) Fill(g, bar, color);
            }
        }
        public static void Avatar(Graphics g, RectangleF r)
        {
            using (var circle = new GraphicsPath())
            {
                circle.AddEllipse(r);
                using (var b = new LinearGradientBrush(Inflate(r, 0, 1), Color.FromArgb(120, 222, 255), Color.FromArgb(0, 150, 214), 90f)) g.FillPath(b, circle);
                var state = g.Save();
                g.SetClip(circle, CombineMode.Intersect);
                using (var b = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                {
                    g.FillEllipse(b, Square(U(r, 0.5f, 0.4f), r.Width * 0.36f));
                    g.FillEllipse(b, r.X + r.Width * 0.18f, r.Y + r.Height * 0.64f, r.Width * 0.64f, r.Height * 0.6f);
                }
                g.Restore(state);
            }
        }
        // Wii U GamePad style face-button glyph.
        public static void Glyph(Graphics g, RectangleF r, string letter, Color fill, Color ink)
        {
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, r);
            Text(g, letter, Font(r.Height * 0.62f, Weight.Heavy), ink, new RectangleF(r.X - 2, r.Y + r.Height * 0.02f, r.Width + 4, r.Height), StringAlignment.Center, StringAlignment.Center, false);
        }
        public static void Rays(Graphics g, PointF c, float radius, float angle, Color color)
        {
            if (color.A == 0) return;
            using (var path = new GraphicsPath())
            {
                for (int k = 0; k < 12; k++)
                {
                    double a = (angle + k * 30) * Math.PI / 180, w = 0.13;
                    path.AddPolygon(new[] { c, new PointF(c.X + (float)Math.Cos(a - w) * radius, c.Y + (float)Math.Sin(a - w) * radius),
                        new PointF(c.X + (float)Math.Cos(a + w) * radius, c.Y + (float)Math.Sin(a + w) * radius) });
                }
                using (var b = new PathGradientBrush(new[] { new PointF(c.X - radius, c.Y - radius), new PointF(c.X + radius, c.Y - radius), new PointF(c.X + radius, c.Y + radius), new PointF(c.X - radius, c.Y + radius) }))
                {
                    b.CenterPoint = c; b.CenterColor = color; b.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillPath(b, path);
                }
            }
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(Square(c, radius * 1.1f));
                using (var b = new PathGradientBrush(glow) { CenterColor = Alpha(color, 0.55f), SurroundColors = new[] { Color.FromArgb(0, color) } }) g.FillPath(b, glow);
            }
        }

        // Rarity metals sampled from the game frames, so pills, ribbons and glows match the medals.
        public static void TierColors(Tier tier, out Color light, out Color dark)
        {
            switch (tier)
            {
                case Tier.Platinum: light = Color.FromArgb(222, 246, 255); dark = Color.FromArgb(70, 150, 196); break;
                case Tier.Gold: light = Color.FromArgb(255, 226, 112); dark = Color.FromArgb(196, 132, 14); break;
                case Tier.Silver: light = Color.FromArgb(236, 242, 248); dark = Color.FromArgb(112, 128, 148); break;
                default: light = Color.FromArgb(240, 178, 116); dark = Color.FromArgb(150, 78, 32); break;
            }
        }
        public static Color TierColor(Tier tier) { Color light, dark; TierColors(tier, out light, out dark); return Mix(light, dark, 0.55f); }
        // Light used for rays, sparkles and the popup glow of each rarity.
        public static Color TierGlow(Tier tier)
        {
            switch (tier)
            {
                case Tier.Platinum: return Color.FromArgb(170, 236, 255);
                case Tier.Gold: return Color.FromArgb(255, 212, 84);
                case Tier.Silver: return Color.FromArgb(206, 226, 255);
                default: return Color.FromArgb(255, 176, 112);
            }
        }
        // Dark lettering on a light metal face stays legible for locked medals too.
        public static void TierLabelColors(Tier tier, out Color face, out Color ink)
        {
            switch (tier)
            {
                case Tier.Platinum: face = Color.FromArgb(222, 242, 255); ink = Color.FromArgb(28, 62, 88); break;
                case Tier.Gold: face = Color.FromArgb(250, 221, 128); ink = Color.FromArgb(79, 48, 5); break;
                case Tier.Silver: face = Color.FromArgb(230, 237, 244); ink = Color.FromArgb(37, 55, 72); break;
                default: face = Color.FromArgb(246, 214, 180); ink = Color.FromArgb(84, 43, 17); break;
            }
        }
        // A quiet metallic edge leaves a uniform, high-contrast text surface.
        public static void TierRibbon(Graphics g, RectangleF r, Tier tier, string text, float alpha)
        {
            if (alpha <= 0.01f) return;
            Color light, dark;
            TierLabelColors(tier, out light, out dark);
            float radius = r.Height / 2;
            using (var path = Round(r, radius))
            {
                Fill(g, path, Alpha(light, alpha));
                using (var pen = new Pen(Alpha(Mix(light, dark, 0.5f), alpha), 1f)) g.DrawPath(pen, path);
            }
            var font = Font(r.Height * 0.62f, Weight.Heavy);
            Text(g, text, font, Alpha(dark, alpha), r, StringAlignment.Center, StringAlignment.Center, false);
        }

        // Game medal (owner icon or glyph in the rarity frame); falls back to the vector medallion.
        public static void Medal(Graphics g, RectangleF r, Art art, float mono) { Medals.Draw(g, r, art, mono); }
        // Vector medallion for games without frame artwork: metallic tier ring, inner disc, gem/heart.
        public static void VectorMedal(Graphics g, RectangleF r, Art art, float mono)
        {
            Color light, dark;
            TierColors(art.Tier, out light, out dark);
            light = Desaturate(light, mono); dark = Desaturate(dark, mono);
            using (var b = new SolidBrush(Color.FromArgb(40, 20, 44, 64))) g.FillEllipse(b, r.X + r.Width * 0.06f, r.Y + r.Height * 0.1f, r.Width * 0.88f, r.Height * 0.94f);
            using (var b = new LinearGradientBrush(Inflate(r, 1, 1), light, dark, 55f)) g.FillEllipse(b, r);
            using (var pen = new Pen(Alpha(Dark(dark, 0.25f), 0.8f), Math.Max(1f, r.Width * 0.02f))) g.DrawEllipse(pen, r);
            var inner = Inflate(r, -r.Width * 0.11f, -r.Height * 0.11f);
            Color a = art.Kind == ArtKind.Heart ? Color.FromArgb(255, 246, 248) : Color.FromArgb(238, 251, 255);
            Color z = art.Kind == ArtKind.Heart ? Color.FromArgb(255, 200, 212) : Color.FromArgb(160, 222, 246);
            using (var b = new LinearGradientBrush(Inflate(inner, 0, 1), Desaturate(a, mono), Desaturate(z, mono), 90f)) g.FillEllipse(b, inner);
            using (var pen = new Pen(Alpha(dark, 0.5f), Math.Max(1f, r.Width * 0.025f))) g.DrawEllipse(pen, inner);
            var icon = Inflate(inner, -inner.Width * 0.17f, -inner.Height * 0.17f);
            if (art.Kind == ArtKind.Heart) { icon.Offset(0, inner.Height * 0.03f); Heart(g, icon, mono); }
            else Gem(g, icon, GemColor(art.Level), mono);
            using (var gloss = new GraphicsPath())
            {
                gloss.AddEllipse(inner);
                var state = g.Save();
                g.SetClip(gloss, CombineMode.Intersect);
                using (var b = new SolidBrush(Color.FromArgb(46, 255, 255, 255))) g.FillEllipse(b, inner.X - inner.Width * 0.2f, inner.Y - inner.Height * 0.55f, inner.Width * 1.4f, inner.Height);
                g.Restore(state);
            }
            if (art.Badge != null)
            {
                var badge = new RectangleF(r.Right - r.Width * 0.36f, r.Bottom - r.Height * 0.36f, r.Width * 0.38f, r.Height * 0.38f);
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, badge);
                using (var pen = new Pen(Desaturate(Color.FromArgb(206, 22, 52), mono), Math.Max(1f, r.Width * 0.025f))) g.DrawEllipse(pen, badge);
                Text(g, art.Badge, Font(badge.Height * 0.56f, Weight.Heavy), Desaturate(Color.FromArgb(200, 30, 56), mono), Inflate(badge, 3, 0), StringAlignment.Center, StringAlignment.Center, false);
            }
        }
        public static void StatusBadge(Graphics g, RectangleF medal, bool unlocked)
        {
            var r = new RectangleF(medal.Right - medal.Width * 0.34f, medal.Y - medal.Height * 0.02f, medal.Width * 0.36f, medal.Height * 0.36f);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, Inflate(r, 1.5f, 1.5f));
            using (var b = new SolidBrush(unlocked ? Success : Color.FromArgb(150, 160, 168))) g.FillEllipse(b, r);
            if (unlocked) Check(g, Inflate(r, -r.Width * 0.08f, -r.Height * 0.08f), Color.White, Math.Max(1.5f, r.Width * 0.14f));
            else Lock(g, Inflate(r, -r.Width * 0.22f, -r.Height * 0.2f), Color.White);
        }
    }
}
