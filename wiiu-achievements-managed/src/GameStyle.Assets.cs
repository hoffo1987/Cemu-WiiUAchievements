using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WiiUAchievements
{
    public sealed partial class GameStyle
    {
        // Fit transparent ornaments without changing their proportions. Device-size copies
        // share the bounded resampling cache used by the existing corner art.
        public bool DrawArt(Graphics g, string relative, RectangleF area, float alpha)
        {
            var source = Themed ? Load(relative) : null;
            if (source == null || area.Width <= 0 || area.Height <= 0 || alpha <= 0) return false;
            float fit = Math.Min(area.Width / source.Width, area.Height / source.Height), scale = DeviceScale(g);
            float w = source.Width * fit, h = source.Height * fit;
            var bitmap = Scaled(relative, Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale)), alpha);
            Blit(g, bitmap, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, scale);
            return true;
        }
        public void BannerCorners(Graphics g, RectangleF r)
        {
            // A matched pair of small corners frames the picture without crowding its logo.
            Corners(g, r, 32, 1, 3);
        }
        public void SimpleCorners(Graphics g, RectangleF r, float size)
        {
            string[] names = { "simple-top-left", "simple-top-right", "simple-bottom-left", "simple-bottom-right" };
            for (int i = 0; i < 4; i++)
                DrawArt(g, "ui/" + names[i] + ".png", new RectangleF(i % 2 == 0 ? r.X - 1 : r.Right - size + 1, i < 2 ? r.Y - 1 : r.Bottom - size + 1, size, size), 0.9f);
        }
        // Only the atlas border is used: corners are scaled uniformly and straight
        // edges repeat at the same scale. No illustrated centre is stretched.
        public bool SimpleFrame(Graphics g, RectangleF r, bool sea, float corner)
        {
            var source = Themed ? Load(sea ? "ui/panel-sea.png" : "ui/panel-gold.png") : null;
            if (source == null || r.Width < 4 || r.Height < 4) return false;
            float scale = DeviceScale(g);
            int width = Math.Max(1, (int)Math.Round(r.Width * scale)), height = Math.Max(1, (int)Math.Round(r.Height * scale));
            int cap = Math.Max(1, Math.Min((int)Math.Round(corner * scale), Math.Min(width, height) / 3));
            string key = "simple-border:" + sea + "|" + width + "x" + height + "|" + cap;
            Bitmap frame;
            if (!scaled.TryGetValue(key, out frame))
            {
                if (scaled.Count >= 160) { foreach (var old in scaled.Values) old.Dispose(); scaled.Clear(); }
                frame = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                int edge = Math.Min(32, Math.Min(source.Width, source.Height) / 3);
                float ratio = cap / (float)edge;
                int band = Math.Min(14, edge);
                float thickness = band * ratio;
                using (var b = Graphics.FromImage(frame))
                {
                    b.CompositingMode = CompositingMode.SourceCopy;
                    b.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    b.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    RepeatEdge(b, source, new RectangleF(edge, 0, source.Width - edge * 2, band), new RectangleF(cap, 0, width - cap * 2, thickness), ratio, true);
                    RepeatEdge(b, source, new RectangleF(edge, source.Height - band, source.Width - edge * 2, band), new RectangleF(cap, height - thickness, width - cap * 2, thickness), ratio, true);
                    RepeatEdge(b, source, new RectangleF(0, edge, band, source.Height - edge * 2), new RectangleF(0, cap, thickness, height - cap * 2), ratio, false);
                    RepeatEdge(b, source, new RectangleF(source.Width - band, edge, band, source.Height - edge * 2), new RectangleF(width - thickness, cap, thickness, height - cap * 2), ratio, false);
                    for (int i = 0; i < 4; i++)
                    {
                        var from = new RectangleF(i % 2 == 0 ? 0 : source.Width - edge, i < 2 ? 0 : source.Height - edge, edge, edge);
                        var to = new RectangleF(i % 2 == 0 ? 0 : width - cap, i < 2 ? 0 : height - cap, cap, cap);
                        b.DrawImage(source, to, from, GraphicsUnit.Pixel);
                    }
                }
                scaled[key] = frame;
            }
            Blit(g, frame, r.X, r.Y, scale);
            return true;
        }
        static void RepeatEdge(Graphics g, Bitmap source, RectangleF from, RectangleF to, float ratio, bool horizontal)
        {
            var state = g.Save(); g.SetClip(to, CombineMode.Intersect);
            float w = from.Width * ratio, h = from.Height * ratio;
            float length = horizontal ? to.Width : to.Height, step = horizontal ? w : h;
            for (float offset = 0; offset < length; offset += step)
                g.DrawImage(source, new RectangleF(to.X + (horizontal ? offset : 0), to.Y + (horizontal ? 0 : offset), w, h), from, GraphicsUnit.Pixel);
            g.Restore(state);
        }
    }
}
