using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WiiUAchievements
{
    // Original cel-shaded sea artwork for the game banner. Static layers are cached at device
    // resolution; clouds, waves and sparkles are animated from a single time value.
    public sealed class OceanBanner : IDisposable
    {
        Bitmap sky;
        Size skySize;
        float skyScale;
        Bitmap[] clouds;
        float cloudScale;

        public void Dispose()
        {
            if (sky != null) sky.Dispose();
            if (clouds != null) foreach (var c in clouds) c.Dispose();
            sky = null; clouds = null;
        }

        static float Horizon(float h) { return h * 0.6f; }

        public void Draw(Graphics g, RectangleF r, float radius, float time, float scale)
        {
            var size = new Size(Math.Max(1, (int)Math.Ceiling(r.Width * scale)), Math.Max(1, (int)Math.Ceiling(r.Height * scale)));
            if (sky == null || size != skySize || skyScale != scale) BuildSky(size, scale, r.Width, r.Height, radius);
            if (clouds == null || cloudScale != scale) BuildClouds(scale);
            var state = g.Save();
            using (var clip = Theme.Round(r, radius))
            {
                g.SetClip(clip, CombineMode.Intersect);
                var mode = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(sky, r.X, r.Y, r.Width, r.Height);
                float[] speeds = { 7f, 4.5f, 10f };
                float[] heights = { 0.06f, 0.2f, 0.02f };
                float[] seeds = { 0.18f, 0.55f, 0.86f };
                for (int k = 0; k < clouds.Length; k++)
                {
                    float w = clouds[k].Width / scale, h = clouds[k].Height / scale;
                    float span = r.Width + w;
                    float x = (seeds[k] * span + time * speeds[k]) % span - w;
                    g.DrawImage(clouds[k], r.X + x, r.Y + r.Height * heights[k], w, h);
                }
                g.InterpolationMode = mode;
                DrawSea(g, r, time);
                // Legibility veil behind the title text on the left.
                using (var b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width * 0.62f + 2, r.Height), Color.FromArgb(165, 6, 50, 96), Color.FromArgb(0, 6, 50, 96), 0f))
                    g.FillRectangle(b, r.X, r.Y, r.Width * 0.62f, r.Height);
            }
            g.Restore(state);
        }

        void DrawSea(Graphics g, RectangleF r, float time)
        {
            float horizon = r.Y + Horizon(r.Height), depth = r.Bottom - horizon;
            float[] rows = { 0.1f, 0.3f, 0.54f, 0.8f }, amp = { 1.2f, 2.2f, 3.4f, 5f }, wave = { 46, 70, 104, 150 }, speed = { 9, 15, 23, 34 };
            Color far = Color.FromArgb(70, 178, 232), near = Color.FromArgb(10, 90, 168);
            for (int k = 0; k < rows.Length; k++)
            {
                float y0 = horizon + depth * rows[k], phase = time * speed[k] * (k % 2 == 0 ? 1 : -1);
                int n = (int)(r.Width / 6) + 3;
                var pts = new PointF[n + 2];
                for (int i = 0; i < n; i++)
                {
                    float x = r.X + i * 6 - 6;
                    double a = 2 * Math.PI * (x + phase) / wave[k];
                    pts[i] = new PointF(x, y0 + (float)(amp[k] * Math.Sin(a) + amp[k] * 0.35 * Math.Sin(a * 1.9 + k)));
                }
                pts[n] = new PointF(r.Right + 6, r.Bottom + 2);
                pts[n + 1] = new PointF(r.X - 6, r.Bottom + 2);
                using (var b = new SolidBrush(Theme.Mix(far, near, (k + 1) / (float)rows.Length))) g.FillPolygon(b, pts);
                var crest = new PointF[n];
                Array.Copy(pts, crest, n);
                using (var pen = new Pen(Color.FromArgb(70 + k * 34, 255, 255, 255), 1 + k * 0.55f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, crest);
                if (k == rows.Length - 1)
                {
                    // Curling foam swirls on the nearest swell.
                    using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        for (float x = r.X - wave[k] + ((phase % wave[k]) + wave[k]) % wave[k]; x < r.Right + wave[k]; x += wave[k])
                        {
                            float cx = x - wave[k] * 0.25f;
                            g.DrawArc(pen, cx - 7, y0 - amp[k] - 2, 14, 12, 200, 250);
                        }
                }
            }
            for (int i = 0; i < 7; i++)
            {
                float a = (float)Math.Pow(Math.Max(0, Math.Sin(time * 1.7 + i * 2.1)), 4);
                var p = new PointF(r.X + r.Width * (0.08f + 0.13f * i + 0.03f * (i % 2)), horizon + depth * (0.2f + 0.11f * (i % 4)));
                Theme.Sparkle(g, p, 3 + 3 * a, Color.FromArgb((int)(220 * a), 255, 255, 255));
            }
        }

        void BuildSky(Size size, float scale, float w, float h, float radius)
        {
            if (sky != null) sky.Dispose();
            sky = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            skySize = size; skyScale = scale;
            using (var g = Graphics.FromImage(sky))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(scale, scale);
                float horizon = Horizon(h);
                using (var b = new LinearGradientBrush(new RectangleF(0, -1, w, horizon + 2), Color.FromArgb(52, 168, 236), Color.FromArgb(200, 242, 255), 90f))
                    g.FillRectangle(b, 0, 0, w, horizon + 1);
                var sun = new PointF(w * 0.6f, h * 0.24f);
                using (var glow = new GraphicsPath())
                {
                    glow.AddEllipse(Theme.Square(sun, 210));
                    using (var b = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(170, 255, 251, 224), SurroundColors = new[] { Color.FromArgb(0, 255, 251, 224) } }) g.FillPath(b, glow);
                }
                using (var b = new SolidBrush(Color.FromArgb(255, 238, 160))) g.FillEllipse(b, Theme.Square(sun, 50));
                using (var b = new SolidBrush(Color.FromArgb(255, 252, 228))) g.FillEllipse(b, Theme.Square(sun, 40));
                using (var b = new SolidBrush(Color.FromArgb(120, 116, 178, 214)))
                {
                    g.FillEllipse(b, w * 0.3f, horizon - 9, 120, 22);
                    g.FillEllipse(b, w * 0.36f, horizon - 6, 90, 16);
                    g.FillEllipse(b, w * 0.82f, horizon - 7, 140, 18);
                }
                DrawIsland(g, new PointF(w * 0.5f, horizon));
                using (var b = new LinearGradientBrush(new RectangleF(0, horizon - 1, w, h - horizon + 2), Color.FromArgb(76, 184, 236), Color.FromArgb(12, 94, 170), 90f))
                    g.FillRectangle(b, 0, horizon, w, h - horizon);
                using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.5f)) g.DrawLine(pen, 0, horizon, w, horizon);
            }
        }

        static void DrawIsland(Graphics g, PointF p)
        {
            using (var path = new GraphicsPath())
            {
                path.AddBezier(p.X - 70, p.Y + 2, p.X - 46, p.Y - 16, p.X - 20, p.Y - 26, p.X + 4, p.Y - 25);
                path.AddBezier(p.X + 4, p.Y - 25, p.X + 30, p.Y - 24, p.X + 52, p.Y - 12, p.X + 74, p.Y + 2);
                path.CloseFigure();
                using (var b = new LinearGradientBrush(new RectangleF(p.X - 70, p.Y - 27, 144, 30), Color.FromArgb(118, 206, 92), Color.FromArgb(46, 140, 70), 90f)) g.FillPath(b, path);
            }
            using (var b = new SolidBrush(Color.FromArgb(246, 228, 170))) g.FillEllipse(b, p.X - 80, p.Y - 3, 164, 7);
            using (var pen = new Pen(Color.FromArgb(120, 82, 50), 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawBezier(pen, p.X + 18, p.Y - 22, p.X + 20, p.Y - 34, p.X + 24, p.Y - 44, p.X + 30, p.Y - 50);
            using (var b = new SolidBrush(Color.FromArgb(56, 160, 74)))
                for (int k = 0; k < 5; k++)
                {
                    var state = g.Save();
                    g.TranslateTransform(p.X + 30, p.Y - 50);
                    g.RotateTransform(-160 + k * 50);
                    g.FillEllipse(b, 0, -3.5f, 20, 7);
                    g.Restore(state);
                }
        }

        void BuildClouds(float scale)
        {
            if (clouds != null) foreach (var c in clouds) c.Dispose();
            cloudScale = scale;
            clouds = new[] { Cloud(170, 56, scale, 0), Cloud(120, 42, scale, 1), Cloud(210, 64, scale, 2) };
        }

        static Bitmap Cloud(float w, float h, float scale, int seed)
        {
            var bmp = new Bitmap((int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var shape = new GraphicsPath())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(scale, scale);
                float baseY = h - 6;
                float[][] puffs = {
                    new[] { 0.06f, 0.52f, 0.3f }, new[] { 0.24f, 0.3f, 0.42f }, new[] { 0.48f, 0.14f, 0.5f },
                    new[] { 0.7f, 0.32f, 0.38f }, new[] { 0.84f, 0.5f, 0.26f } };
                foreach (var p in puffs)
                {
                    float d = h * p[2] * 2.1f;
                    shape.AddEllipse(w * p[0], h * p[1], d, d);
                }
                var state = g.Save();
                g.SetClip(new RectangleF(0, 0, w, baseY));
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 255))) g.FillPath(b, shape);
                g.SetClip(new RectangleF(0, baseY - h * 0.32f, w, h * 0.32f), CombineMode.Intersect);
                using (var b = new SolidBrush(Color.FromArgb(214, 238, 252))) g.FillPath(b, shape);
                g.Restore(state);
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 255))) g.FillRectangle(b, w * 0.12f, baseY - h * 0.33f, w * 0.76f, 2);
                // Signature spiral curl.
                var curl = new PointF[40];
                float cx = w * (0.4f + 0.12f * seed), cy = h * 0.42f;
                for (int i = 0; i < curl.Length; i++)
                {
                    double t = i / (double)(curl.Length - 1), a = t * Math.PI * 3.2;
                    float rad = (float)(2 + t * h * 0.24);
                    curl[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad * 0.8f);
                }
                using (var pen = new Pen(Color.FromArgb(196, 228, 246), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(pen, curl);
            }
            return bmp;
        }
    }
}
