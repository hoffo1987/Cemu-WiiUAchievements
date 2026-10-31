using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace WiiUAchievements
{
    // Each game section is dressed in its own style inside the Wii U layout: backdrop, bars,
    // buttons, tabs, cards, panels, banner, medal frames and popup take the game's palette and
    // ornaments. The Games catalog and games without a style use the neutral Wii U look.
    public sealed partial class GameStyle
    {
        public string GameId { get; private set; }
        public bool Themed { get; private set; }
        public Color Accent { get; private set; }      // progress, headers, countdown
        public Color AccentDark { get; private set; }
        public Color Glow { get; private set; }
        public Color Soft { get; private set; }        // pale wash behind content
        public Color Warm { get; private set; }        // secondary highlight (sail cream)
        public Color Deep { get; private set; }        // popup panel depth
        // Chrome palette. Values are only assigned in the two definitions below.
        public Color BackTop, BackBottom, Muted, Trim, TrimDark;
        public Color BarTop, BarBottom, BarLine, FootTop, FootBottom, FootLine;
        public Color ButtonTop, ButtonBottom, ButtonEdge, ButtonInk, GlyphFill, GlyphInk;
        public Color PrimaryTop, PrimaryBottom, PrimaryEdge, PrimaryInk, PrimaryGlyph, PrimaryGlyphInk;
        public Color TrackTop, TrackBottom, TrackEdge, BubbleTop, BubbleBottom, BubbleOffTop, BubbleOffBottom;
        public Color CardTop, CardBottom, CardLockedTop, CardLockedBottom, CardEdge, SheetTop, SheetBottom, Drawer;
        readonly Dictionary<string, Bitmap> art = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        readonly HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        public static readonly GameStyle Neutral = new GameStyle
        {
            GameId = "", Accent = Theme.Accent, AccentDark = Theme.AccentDark, Glow = Theme.Glow,
            Soft = Theme.AccentSoft, Warm = C(255, 236, 170), Deep = C(0, 104, 168),
            BackTop = Theme.BackTop, BackBottom = Theme.BackBottom, Muted = Theme.Muted, Trim = Theme.Accent, TrimDark = Theme.AccentDark,
            BarTop = Color.White, BarBottom = C(236, 241, 244), BarLine = C(204, 212, 218),
            FootTop = C(242, 245, 248), FootBottom = C(220, 227, 233), FootLine = C(198, 207, 214),
            ButtonTop = Color.White, ButtonBottom = C(233, 238, 242), ButtonEdge = C(190, 199, 207), ButtonInk = Theme.Ink, GlyphFill = C(104, 114, 122), GlyphInk = Color.White,
            PrimaryTop = C(64, 202, 248), PrimaryBottom = C(0, 148, 214), PrimaryEdge = C(0, 120, 184), PrimaryInk = Color.White,
            PrimaryGlyph = Color.FromArgb(245, 255, 255, 255), PrimaryGlyphInk = Theme.AccentDark,
            TrackTop = C(220, 227, 232), TrackBottom = C(231, 236, 240), TrackEdge = C(204, 212, 218),
            BubbleTop = Theme.Glow, BubbleBottom = Theme.AccentDark, BubbleOffTop = C(198, 206, 212), BubbleOffBottom = C(178, 188, 196),
            CardTop = Color.White, CardBottom = C(240, 245, 248), CardLockedTop = C(251, 252, 253), CardLockedBottom = C(235, 239, 242), CardEdge = C(212, 220, 226),
            SheetTop = C(248, 250, 251), SheetBottom = C(232, 238, 242), Drawer = Color.FromArgb(248, 33, 40, 48)
        };
        // The Wind Waker HD, after the medal frames: open sea and foam-crested waves, sail
        // cream and parchment, gold scrolls with a dark warm outline.
        public static readonly GameStyle WindWaker = new GameStyle
        {
            GameId = GameCatalog.WindWakerId, Themed = true, Accent = C(0, 150, 206), AccentDark = C(0, 92, 158),
            Glow = C(86, 214, 242), Soft = C(218, 243, 251), Warm = C(250, 236, 196), Deep = C(4, 70, 136),
            BackTop = C(239, 248, 251), BackBottom = C(221, 238, 244), Muted = C(116, 112, 103), Trim = C(240, 194, 80), TrimDark = C(140, 90, 28),
            BarTop = C(255, 254, 250), BarBottom = C(249, 245, 233), BarLine = C(205, 184, 139),
            FootTop = C(254, 251, 243), FootBottom = C(246, 239, 221), FootLine = C(205, 184, 139),
            ButtonTop = C(255, 254, 249), ButtonBottom = C(248, 242, 226), ButtonEdge = C(198, 171, 114), ButtonInk = C(76, 52, 34), GlyphFill = C(122, 84, 50), GlyphInk = C(255, 244, 214),
            PrimaryTop = C(38, 178, 222), PrimaryBottom = C(0, 100, 168), PrimaryEdge = C(170, 120, 36), PrimaryInk = Color.White,
            PrimaryGlyph = C(255, 230, 150), PrimaryGlyphInk = C(0, 84, 140),
            TrackTop = C(236, 223, 194), TrackBottom = C(246, 238, 218), TrackEdge = C(204, 176, 124),
            BubbleTop = C(38, 178, 222), BubbleBottom = C(0, 92, 158), BubbleOffTop = C(208, 188, 150), BubbleOffBottom = C(184, 160, 118),
            CardTop = C(255, 254, 250), CardBottom = C(250, 245, 231), CardLockedTop = C(255, 254, 250), CardLockedBottom = C(247, 244, 235), CardEdge = C(224, 211, 183),
            SheetTop = C(255, 253, 246), SheetBottom = C(247, 241, 225), Drawer = Color.FromArgb(252, 8, 46, 82)
        };

        public static GameStyle For(string gameId)
        {
            return gameId == GameCatalog.WindWakerId ? WindWaker : Neutral;
        }

        // ---- Artwork: embedded by build.ps1 from assets/games/<game>/, or read beside the EXE.
        Bitmap Load(string relative)
        {
            Bitmap bitmap;
            if (art.TryGetValue(relative, out bitmap)) return bitmap;
            if (missing.Contains(relative) || GameId.Length == 0) return null;
            try
            {
                string resource = "WiiUAchievements.Games." + GameId + "." + relative.Replace('/', '.');
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream != null) bitmap = Copy(stream);
                    else
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "games", GameId, relative.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(path)) using (var file = File.OpenRead(path)) bitmap = Copy(file);
                    }
                }
            }
            catch (Exception) { bitmap = null; }
            if (bitmap == null) missing.Add(relative); else art[relative] = bitmap;
            return bitmap;
        }
        static Bitmap Copy(Stream stream)
        {
            // GDI+ keeps reading the source stream lazily: copy into a private bitmap.
            using (var image = Image.FromStream(stream))
            {
                var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bitmap)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(image, 0, 0, image.Width, image.Height); }
                return bitmap;
            }
        }
        public Bitmap Icon(string achievementId) { return Load("icons/" + achievementId + ".png"); }
        public Bitmap Frame(Tier tier) { return Load("frame-" + tier.ToString().ToLowerInvariant() + ".png"); }
        public Bitmap Sky { get { return Load("sky.png"); } }
        public bool HasFrames { get { return Frame(Tier.Bronze) != null && Sky != null; } }

        // A light repeating motif for headers and the popup panel: foam curls for the sea.
        public void Pattern(Graphics g, RectangleF r, float phase, Color color)
        {
            if (color.A == 0 || r.Width < 4) return;
            if (this != WindWaker)
            {
                using (var pen = new Pen(color, 1.2f))
                    for (float x = r.X - r.Height + (phase * 12) % 12; x < r.Right; x += 12) g.DrawLine(pen, x, r.Bottom, x + r.Height, r.Y);
                return;
            }
            float step = 46, size = Math.Min(16, r.Height * 0.6f);
            float offset = (phase * 14) % step;
            using (var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int row = 0; row * size * 1.25f < r.Height + size; row++)
                {
                    float y = r.Y + size * 0.6f + row * size * 1.25f, shift = row % 2 == 0 ? 0 : step / 2;
                    for (float x = r.X - step + offset + shift; x < r.Right + step; x += step)
                        Curl(g, pen, new PointF(x, y), size);
                }
            }
        }
        static void Curl(Graphics g, Pen pen, PointF c, float s)
        {
            // A breaking-wave curl: a long crest that rolls into a small spiral.
            using (var path = new GraphicsPath())
            {
                path.AddBezier(c.X - s * 1.3f, c.Y + s * 0.35f, c.X - s * 0.6f, c.Y + s * 0.3f, c.X - s * 0.3f, c.Y - s * 0.55f, c.X + s * 0.25f, c.Y - s * 0.5f);
                path.AddBezier(c.X + s * 0.25f, c.Y - s * 0.5f, c.X + s * 0.75f, c.Y - s * 0.45f, c.X + s * 0.75f, c.Y + s * 0.15f, c.X + s * 0.3f, c.Y + s * 0.15f);
                path.AddBezier(c.X + s * 0.3f, c.Y + s * 0.15f, c.X + s * 0.05f, c.Y + s * 0.15f, c.X + s * 0.02f, c.Y - s * 0.15f, c.X + s * 0.22f, c.Y - s * 0.18f);
                g.DrawPath(pen, path);
            }
        }

        // ---- Owner UI art (assets/games/<game>/ui): gold wave corners, Triforce crest, parchment.
        static readonly string[] cornerNames = { "ui/corner-top-left.png", "ui/corner-top-right.png", "ui/corner-bottom-left.png", "ui/corner-bottom-right.png" };
        public bool HasUiArt { get { return Themed && Load(cornerNames[0]) != null; } }
        // Art is resampled once per device size; frames then copy pixels 1:1, which keeps
        // repaints of bars, banner and dialogs cheap even on ultrawide screens.
        readonly Dictionary<string, Bitmap> scaled = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        static float DeviceScale(Graphics g) { using (var m = g.Transform) return Math.Max(0.25f, (float)Math.Sqrt(m.Elements[0] * m.Elements[0] + m.Elements[1] * m.Elements[1])); }
        Bitmap Scaled(string relative, int width, int height, float alpha)
        {
            string key = relative + "|" + width + "x" + height + "|" + (int)Math.Round(alpha * 20);
            Bitmap bitmap;
            if (scaled.TryGetValue(key, out bitmap)) return bitmap;
            var source = Load(relative);
            if (source == null || width < 1 || height < 1) return null;
            if (scaled.Count >= 160) { foreach (var old in scaled.Values) old.Dispose(); scaled.Clear(); }
            bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            using (var a = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                a.SetWrapMode(WrapMode.TileFlipXY);
                if (alpha < 1) a.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
                g.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, a);
            }
            scaled[key] = bitmap;
            return bitmap;
        }
        void Blit(Graphics g, Bitmap bitmap, float x, float y, float scale)
        {
            var interpolation = g.InterpolationMode; var offset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(bitmap, x, y, bitmap.Width / scale, bitmap.Height / scale);
            g.InterpolationMode = interpolation; g.PixelOffsetMode = offset;
        }
        // Corner pieces whose outer edges sit `outset` outside r; mask bits: 1 TL, 2 TR, 4 BL, 8 BR.
        public void Corners(Graphics g, RectangleF r, float size, float outset) { Corners(g, r, size, outset, 15); }
        public void Corners(Graphics g, RectangleF r, float size, float outset, int mask)
        {
            if (!Themed) return;
            float scale = DeviceScale(g);
            for (int i = 0; i < 4; i++)
            {
                var source = (mask & (1 << i)) != 0 ? Load(cornerNames[i]) : null;
                if (source == null) continue;
                float w = size, h = size * source.Height / source.Width;
                var image = Scaled(cornerNames[i], (int)Math.Round(w * scale), (int)Math.Round(h * scale), 1);
                float x = i % 2 == 0 ? r.X - outset : r.Right + outset - w, y = i < 2 ? r.Y - outset : r.Bottom + outset - h;
                Blit(g, image, x, y, scale);
            }
        }
        // The crest, with its gold rule centred on `rule`. Returns false without the art.
        public bool Crest(Graphics g, PointF rule, float width, float alpha)
        {
            var source = Themed ? Load("ui/crest.png") : null;
            if (source == null || alpha <= 0) return false;
            float scale = DeviceScale(g), h = width * source.Height / source.Width;
            var image = Scaled("ui/crest.png", (int)Math.Round(width * scale), (int)Math.Round(h * scale), alpha);
            Blit(g, image, rule.X - width / 2, rule.Y - h * 0.767f, scale);
            return true;
        }
        // Sail-cloth parchment laid over a surface at the given strength. The tile is pre-scaled
        // to the device so the brush samples it 1:1.
        const float ParchmentTile = 0.6f;
        public void Parchment(Graphics g, GraphicsPath area, float alpha)
        {
            ParchmentTexture(g, area, alpha, "ui/parchment.png");
        }
        void ParchmentTexture(Graphics g, GraphicsPath area, float alpha, string relative)
        {
            var source = Themed ? Load(relative) : null;
            if (source == null || alpha <= 0) return;
            float scale = DeviceScale(g);
            int size = Math.Max(8, (int)Math.Round(source.Width * ParchmentTile * scale));
            var texture = Scaled(relative, size, size, alpha);
            var interpolation = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            using (var brush = new TextureBrush(texture, WrapMode.Tile))
            {
                brush.ScaleTransform(1 / scale, 1 / scale);
                g.FillPath(brush, area);
            }
            g.InterpolationMode = interpolation;
        }
        // Texture brushes are slow (a full settings panel took ~20 ms per frame): each surface
        // size keeps its parchment as a ready bitmap, copied 1:1 afterwards. The device scale is
        // rounded to quarter steps so a panel zooming in briefly reuses the same picture.
        public void Parchment(Graphics g, RectangleF r, float radius, float alpha)
        {
            if (!Themed || alpha <= 0 || r.Width < 1 || r.Height < 1 || Load("ui/parchment.png") == null) return;
            float scale = (float)Math.Max(0.25, Math.Round(DeviceScale(g) * 4) / 4);
            int width = (int)Math.Ceiling(r.Width * scale), height = (int)Math.Ceiling(r.Height * scale);
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "parchment|{0}x{1}|{2:0.0}|{3}", width, height, radius, (int)Math.Round(alpha * 40));
            Bitmap surface;
            if (!scaled.TryGetValue(key, out surface))
            {
                if (scaled.Count >= 160) { foreach (var old in scaled.Values) old.Dispose(); scaled.Clear(); }
                surface = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (var b = Graphics.FromImage(surface))
                {
                    b.SmoothingMode = SmoothingMode.AntiAlias; b.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    b.ScaleTransform(scale, scale);
                    var area = new RectangleF(0, 0, r.Width, r.Height);
                    using (var path = radius > 0 ? Theme.Round(area, radius) : Rect(area)) Parchment(b, path, alpha);
                }
                scaled[key] = surface;
            }
            Blit(g, surface, r.X, r.Y, scale);
        }
        static GraphicsPath Rect(RectangleF r) { var p = new GraphicsPath(); p.AddRectangle(r); return p; }

        // ---- Banner video: the owner's Outset Island wallpaper (dawn, day, sunset, night).
        public VideoBanner OpenBannerVideo()
        {
            if (this != WindWaker) return null;
            var video = VideoBanner.Open(GameId, "banner.mp4", new double[] { 0, 80, 161, 245 });
            if (video == null) return null;
            // Keep the island from palm tops to the jetty in view, right of the title.
            video.FocusTop = 116; video.FocusBottom = 300; video.FocusX = 300; video.Anchor = 0.6f;
            return video;
        }

        // ---------------------------------------------------------------- ornaments
        // Drawn in the manner of the medal frames: dark warm outline, teal waves with cream
        // foam, gold scrolls. Neutral style draws none of them.
        static readonly Color Outline = C(46, 32, 26), Foam = C(252, 242, 214), WaveTop = C(46, 170, 176), WaveBottom = C(14, 92, 150);

        // One breaking wave whose crest curls to the right (or left when mirrored).
        static GraphicsPath WaveShape(float x0, float w, float top, float bottom, bool mirror)
        {
            float h = bottom - top;
            Func<float, float> X = f => mirror ? x0 + w - w * f : x0 + w * f;
            var p = new GraphicsPath();
            p.AddBezier(X(0), bottom, X(0.18f), bottom - h * 0.3f, X(0.3f), top + h * 0.02f, X(0.58f), top);
            p.AddBezier(X(0.58f), top, X(0.8f), top - h * 0.02f, X(0.92f), top + h * 0.3f, X(0.8f), top + h * 0.46f);
            p.AddBezier(X(0.8f), top + h * 0.46f, X(0.72f), top + h * 0.56f, X(0.86f), bottom - h * 0.2f, X(1.08f), bottom);
            p.CloseFigure();
            return p;
        }
        public static void Wave(Graphics g, float x0, float w, float top, float bottom, bool mirror, float alpha)
        {
            float h = bottom - top, line = Math.Max(1f, h * 0.06f);
            using (var shape = WaveShape(x0, w, top, bottom, mirror))
            {
                using (var b = new LinearGradientBrush(new RectangleF(x0, top - 1, w, h + 2), Theme.Alpha(WaveTop, alpha), Theme.Alpha(WaveBottom, alpha), 90f)) g.FillPath(b, shape);
                using (var pen = new Pen(Theme.Alpha(Outline, alpha * 0.85f), line) { LineJoin = LineJoin.Round }) g.DrawPath(pen, shape);
            }
            Func<float, float> X = f => mirror ? x0 + w - w * f : x0 + w * f;
            using (var crest = new GraphicsPath())
            {
                crest.AddBezier(X(0.22f), top + h * 0.42f, X(0.32f), top + h * 0.1f, X(0.46f), top + h * 0.04f, X(0.6f), top + h * 0.06f);
                crest.AddBezier(X(0.6f), top + h * 0.06f, X(0.78f), top + h * 0.08f, X(0.82f), top + h * 0.34f, X(0.7f), top + h * 0.36f);
                crest.AddBezier(X(0.7f), top + h * 0.36f, X(0.62f), top + h * 0.37f, X(0.62f), top + h * 0.26f, X(0.68f), top + h * 0.24f);
                using (var pen = new Pen(Theme.Alpha(Outline, alpha * 0.85f), h * 0.2f + line * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawPath(pen, crest);
                using (var pen = new Pen(Theme.Alpha(Foam, alpha), h * 0.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawPath(pen, crest);
            }
        }
        // A row of overlapping waves along the top (crests up) or bottom of r.
        public void WaveBand(Graphics g, RectangleF r, float phase, float alpha)
        {
            if (!Themed || r.Width < 4) return;
            if (Load("ui/wave-0.png") != null)
            {
                var clip = g.Save();
                g.SetClip(r, CombineMode.Intersect);
                float x = r.X - r.Height * 2;
                for (int i = 0; x < r.Right; i++)
                {
                    string name = "ui/wave-" + (i % 6) + ".png";
                    var source = Load(name);
                    float waveWidth = r.Height * source.Width / source.Height;
                    DrawArt(g, name, new RectangleF(x, r.Y, waveWidth, r.Height), alpha);
                    x += waveWidth - r.Height * 0.08f;
                }
                g.Restore(clip);
                return;
            }
            float w = r.Height * 2.6f, start = r.X - w + (phase % 1) * w;
            var state = g.Save();
            g.SetClip(new RectangleF(r.X, r.Y - r.Height, r.Width, r.Height * 2), CombineMode.Intersect);
            for (float x = start; x < r.Right + w; x += w * 0.78f) Wave(g, x, w, r.Y, r.Bottom + r.Height * 0.1f, false, alpha);
            g.Restore(state);
        }
        // Static bands on bars and panels are repainted often (scrolling touches the bottom bar):
        // keep a few rasterised at device resolution.
        readonly Dictionary<string, Bitmap> bands = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        public void WaveBandCached(Graphics g, RectangleF r, float phase, float alpha)
        {
            if (!Themed || r.Width < 4 || r.Height < 1) return;
            float scale;
            using (var m = g.Transform) scale = Math.Max(0.5f, m.Elements[0]);
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}x{1:0.0}@{2:0.00}/{3:0.00}/{4:0.00}", r.Width, r.Height, scale, phase, alpha);
            Bitmap band;
            if (!bands.TryGetValue(key, out band))
            {
                if (bands.Count >= 8) { foreach (var old in bands.Values) old.Dispose(); bands.Clear(); }
                band = new Bitmap((int)Math.Ceiling(r.Width * scale) + 2, (int)Math.Ceiling(r.Height * 2.2f * scale) + 2, PixelFormat.Format32bppPArgb);
                using (var b = Graphics.FromImage(band))
                {
                    b.SmoothingMode = SmoothingMode.AntiAlias; b.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    b.ScaleTransform(scale, scale);
                    WaveBand(b, new RectangleF(0, r.Height, r.Width, r.Height), phase, alpha);
                }
                bands[key] = band;
            }
            var interpolation = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(band, r.X, r.Y - r.Height, band.Width / scale, band.Height / scale);
            g.InterpolationMode = interpolation;
        }
        // Gold scroll: a thick inked spiral like the frames' cloud ornaments.
        public static void Swirl(Graphics g, PointF c, float radius, bool mirror, Color light, Color dark)
        {
            var points = new PointF[28];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1), a = (float)(Math.PI * (0.15 + 1.75 * t)), rr = radius * (1 - 0.68f * t);
                points[i] = new PointF(c.X + (mirror ? -1 : 1) * (float)Math.Cos(a) * rr, c.Y - (float)Math.Sin(a) * rr);
            }
            using (var pen = new Pen(Outline, radius * 0.46f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawCurve(pen, points);
            using (var b = new LinearGradientBrush(Theme.Inflate(new RectangleF(c.X - radius, c.Y - radius, radius * 2, radius * 2), 2, 2), light, dark, 90f))
            using (var pen = new Pen(b, radius * 0.28f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawCurve(pen, points);
            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(0.8f, radius * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawCurve(pen, points, 0, 10, 0.5f);
        }
        // A gold rule with a dark outline, as on the frames' metal ring.
        public void TrimLine(Graphics g, float x0, float x1, float y, float thickness)
        {
            if (!Themed) return;
            using (var pen = new Pen(Outline, thickness + 2)) g.DrawLine(pen, x0, y, x1, y);
            using (var b = new LinearGradientBrush(new RectangleF(x0, y - thickness, Math.Max(1, x1 - x0), thickness * 2), Theme.Light(Trim, 0.35f), TrimDark, 90f))
            using (var pen = new Pen(b, thickness)) g.DrawLine(pen, x0, y, x1, y);
        }
        // Crest: two gold scrolls around a small gem, centred on c.
        public void Crest(Graphics g, PointF c, float size)
        {
            if (!Themed) return;
            Swirl(g, new PointF(c.X - size * 0.62f, c.Y), size * 0.42f, true, Theme.Light(Trim, 0.4f), TrimDark);
            Swirl(g, new PointF(c.X + size * 0.62f, c.Y), size * 0.42f, false, Theme.Light(Trim, 0.4f), TrimDark);
            using (var gem = new GraphicsPath())
            {
                gem.AddPolygon(new[] { new PointF(c.X, c.Y - size * 0.42f), new PointF(c.X + size * 0.3f, c.Y), new PointF(c.X, c.Y + size * 0.42f), new PointF(c.X - size * 0.3f, c.Y) });
                using (var b = new LinearGradientBrush(Theme.Inflate(gem.GetBounds(), 1, 1), Theme.Light(Trim, 0.5f), TrimDark, 90f)) g.FillPath(b, gem);
                using (var pen = new Pen(Outline, Math.Max(1, size * 0.08f)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, gem);
            }
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, c.X - size * 0.09f, c.Y - size * 0.22f, size * 0.12f, size * 0.12f);
        }

        // ---------------------------------------------------------------- dressing of shared surfaces
        public void DecorateBackdrop(Graphics g, Size size, float scale)
        {
            if (!Themed) return;
            // Open turquoise sea rising behind the lower half of the page, with faint swell lines.
            float horizon = size.Height * 0.5f;
            using (var b = new LinearGradientBrush(new RectangleF(0, horizon - 1, size.Width, size.Height - horizon + 2), Color.FromArgb(0, 40, 186, 200), Color.FromArgb(58, 20, 160, 190), 90f))
                g.FillRectangle(b, 0, horizon, size.Width, size.Height - horizon);
            using (var pen = new Pen(Color.FromArgb(26, 255, 255, 255), 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 9; i++)
                {
                    float y = horizon + (size.Height - horizon) * (0.18f + i * 0.09f), x = (i * 0.37f % 1) * size.Width * 0.8f;
                    g.DrawLine(pen, x, y, x + size.Width * (0.12f + i % 3 * 0.05f), y);
                }
            // Wind sits in open sky/gaps, at its natural aspect ratio; the logo zones stay clear.
            DrawArt(g, "ui/wind-0.png", new RectangleF(size.Width * 0.08f, size.Height * 0.4f, 180 * scale, 80 * scale), 0.16f);
            DrawArt(g, "ui/wind-2.png", new RectangleF(size.Width * 0.35f, size.Height * 0.65f, 230 * scale, 90 * scale), 0.14f);
            DrawArt(g, "ui/wind-ribbon.png", new RectangleF(size.Width * 0.59f, 332 * scale, 245 * scale, 76 * scale), 0.12f);
            DrawArt(g, "ui/wind-scroll.png", new RectangleF(size.Width - 300 * scale, size.Height - 180 * scale, 250 * scale, 80 * scale), 0.1f);
        }
        public void DecorateTopBar(Graphics g, float width, float height)
        {
            if (!Themed) return;
            Parchment(g, new RectangleF(0, 0, width, height), 0, 0.4f);
        }
        public void DecorateFooter(Graphics g, RectangleF r, float phase)
        {
            if (!Themed) return;
            Parchment(g, r, 0, 0.4f);
            WaveBandCached(g, new RectangleF(r.X, r.Y - 9, r.Width, 11), phase, 0.9f);
            using (var pen = new Pen(BarLine, 1)) g.DrawLine(pen, r.X, r.Y + 0.5f, r.Right, r.Y + 0.5f);
        }
        // Parchment card with a faint wave in the corner; earned gold/platinum cards get the gold corners.
        public void DecorateCard(Graphics g, RectangleF r, float radius, bool ornate)
        {
            if (!Themed) return;
            Parchment(g, r, radius, 0.45f);
            var state = g.Save();
            using (var clip = Theme.Round(r, radius)) g.SetClip(clip, CombineMode.Intersect);
            DrawArt(g, "ui/ocean-wave.png", new RectangleF(r.Right - 102, r.Bottom - 22, 102, 23), 0.14f);
            g.Restore(state);
            if (ornate && HasUiArt) SimpleCorners(g, r, 19);
        }
        // Ornate border for dialogs and the settings sheet: gold rule, owner corners and crest
        // (or drawn corner waves and scroll crest when the art is missing).
        public void DecoratePanel(Graphics g, RectangleF r, float radius)
        {
            if (!Themed) return;
            if (!SimpleFrame(g, r, false, 12))
                Theme.Stroke(g, Theme.Inflate(r, -1, -1), radius - 1, ButtonEdge, 1.2f);
        }
        // Compass rose ring around the completion dial.
        public void CompassRing(Graphics g, PointF c, float radius)
        {
            if (!Themed) return;
            using (var pen = new Pen(Outline, 5)) g.DrawEllipse(pen, Theme.Square(c, radius * 2));
            using (var b = new LinearGradientBrush(Theme.Inflate(Theme.Square(c, radius * 2), 2, 2), Theme.Light(Trim, 0.4f), TrimDark, 60f))
            using (var pen = new Pen(b, 3)) g.DrawEllipse(pen, Theme.Square(c, radius * 2));
            using (var pen = new Pen(Color.FromArgb(150, TrimDark), 1.3f))
                for (int i = 0; i < 32; i++)
                {
                    double a = i * Math.PI / 16;
                    float inner = radius - (i % 4 == 0 ? 9 : 5), outer = radius - 2.5f;
                    g.DrawLine(pen, c.X + (float)Math.Cos(a) * inner, c.Y + (float)Math.Sin(a) * inner, c.X + (float)Math.Cos(a) * outer, c.Y + (float)Math.Sin(a) * outer);
                }
            // North point, as on the sea chart.
            using (var north = new GraphicsPath())
            {
                north.AddPolygon(new[] { new PointF(c.X, c.Y - radius - 9), new PointF(c.X + 7, c.Y - radius + 3), new PointF(c.X - 7, c.Y - radius + 3) });
                using (var b = new LinearGradientBrush(Theme.Inflate(north.GetBounds(), 1, 1), C(240, 90, 70), C(170, 40, 34), 90f)) g.FillPath(b, north);
                using (var pen = new Pen(Outline, 1.4f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, north);
            }
        }
    }
}
