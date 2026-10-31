using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WiiUAchievements
{
    // Original line icons for status, search, menus and navigation, drawn in a unit box like
    // the other vector icons. Every state icon pairs with text: colour is never the only cue.
    public static partial class Theme
    {
        // Text colours for the objective states: dark enough for small text on cream or white.
        public static readonly Color UnlockedInk = Color.FromArgb(40, 124, 30), LockedInk = Color.FromArgb(84, 96, 106),
            ExperimentalInk = Color.FromArgb(166, 94, 0), PlannedInk = Color.FromArgb(104, 82, 160);

        static Pen RoundPen(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
        // Laboratory flask: the detector is implemented, the mapping is still being playtested.
        public static void Flask(Graphics g, RectangleF r, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddLine(U(r, 0.38f, 0.1f), U(r, 0.38f, 0.42f));
                path.AddLine(U(r, 0.38f, 0.42f), U(r, 0.12f, 0.84f));
                path.AddBezier(U(r, 0.12f, 0.84f), U(r, 0.08f, 0.92f), U(r, 0.14f, 0.96f), U(r, 0.22f, 0.96f));
                path.AddLine(U(r, 0.22f, 0.96f), U(r, 0.78f, 0.96f));
                path.AddBezier(U(r, 0.78f, 0.96f), U(r, 0.86f, 0.96f), U(r, 0.92f, 0.92f), U(r, 0.88f, 0.84f));
                path.AddLine(U(r, 0.88f, 0.84f), U(r, 0.62f, 0.42f));
                path.AddLine(U(r, 0.62f, 0.42f), U(r, 0.62f, 0.1f));
                using (var pen = RoundPen(color, Math.Max(1.2f, r.Width * 0.1f))) g.DrawPath(pen, path);
            }
            using (var liquid = new GraphicsPath())
            {
                liquid.AddPolygon(new[] { U(r, 0.27f, 0.62f), U(r, 0.73f, 0.62f), U(r, 0.86f, 0.88f), U(r, 0.14f, 0.88f) });
                Fill(g, liquid, color);
            }
            using (var pen = RoundPen(color, Math.Max(1.2f, r.Width * 0.1f))) g.DrawLine(pen, U(r, 0.28f, 0.08f), U(r, 0.72f, 0.08f));
        }
        // Hourglass: the target is listed but its detector is not implemented yet.
        public static void Pending(Graphics g, RectangleF r, Color color)
        {
            float w = Math.Max(1.2f, r.Width * 0.1f);
            using (var pen = RoundPen(color, w))
            {
                g.DrawLine(pen, U(r, 0.2f, 0.08f), U(r, 0.8f, 0.08f));
                g.DrawLine(pen, U(r, 0.2f, 0.92f), U(r, 0.8f, 0.92f));
                using (var glass = new GraphicsPath())
                {
                    glass.AddBezier(U(r, 0.28f, 0.1f), U(r, 0.28f, 0.36f), U(r, 0.46f, 0.42f), U(r, 0.46f, 0.5f));
                    glass.AddBezier(U(r, 0.46f, 0.5f), U(r, 0.46f, 0.58f), U(r, 0.28f, 0.64f), U(r, 0.28f, 0.9f));
                    glass.AddLine(U(r, 0.28f, 0.9f), U(r, 0.72f, 0.9f));
                    glass.AddBezier(U(r, 0.72f, 0.9f), U(r, 0.72f, 0.64f), U(r, 0.54f, 0.58f), U(r, 0.54f, 0.5f));
                    glass.AddBezier(U(r, 0.54f, 0.5f), U(r, 0.54f, 0.42f), U(r, 0.72f, 0.36f), U(r, 0.72f, 0.1f));
                    glass.CloseFigure();
                    g.DrawPath(pen, glass);
                }
            }
            using (var sand = new GraphicsPath())
            {
                sand.AddPolygon(new[] { U(r, 0.5f, 0.66f), U(r, 0.66f, 0.86f), U(r, 0.34f, 0.86f) });
                Fill(g, sand, color);
            }
        }
        public static void Search(Graphics g, RectangleF r, Color color)
        {
            float w = Math.Max(1.4f, r.Width * 0.12f);
            using (var pen = RoundPen(color, w))
            {
                g.DrawEllipse(pen, r.X + r.Width * 0.1f, r.Y + r.Height * 0.1f, r.Width * 0.56f, r.Height * 0.56f);
                g.DrawLine(pen, U(r, 0.6f, 0.6f), U(r, 0.9f, 0.9f));
            }
        }
        // direction: 0 down, 1 up, 2 left, 3 right.
        public static void Chevron(Graphics g, RectangleF r, Color color, int direction, float width)
        {
            PointF[] down = { U(r, 0.2f, 0.36f), U(r, 0.5f, 0.66f), U(r, 0.8f, 0.36f) };
            PointF[] up = { U(r, 0.2f, 0.64f), U(r, 0.5f, 0.34f), U(r, 0.8f, 0.64f) };
            PointF[] left = { U(r, 0.64f, 0.2f), U(r, 0.34f, 0.5f), U(r, 0.64f, 0.8f) };
            PointF[] right = { U(r, 0.36f, 0.2f), U(r, 0.66f, 0.5f), U(r, 0.36f, 0.8f) };
            using (var pen = RoundPen(color, width)) g.DrawLines(pen, direction == 0 ? down : direction == 1 ? up : direction == 2 ? left : right);
        }
        public static void Close(Graphics g, RectangleF r, Color color, float width)
        {
            using (var pen = RoundPen(color, width))
            {
                g.DrawLine(pen, U(r, 0.2f, 0.2f), U(r, 0.8f, 0.8f));
                g.DrawLine(pen, U(r, 0.8f, 0.2f), U(r, 0.2f, 0.8f));
            }
        }
        // Two staggered bars with an arrow: "order by".
        public static void SortIcon(Graphics g, RectangleF r, Color color)
        {
            float w = Math.Max(1.3f, r.Width * 0.11f);
            using (var pen = RoundPen(color, w))
            {
                g.DrawLine(pen, U(r, 0.3f, 0.12f), U(r, 0.3f, 0.86f));
                g.DrawLines(pen, new[] { U(r, 0.1f, 0.66f), U(r, 0.3f, 0.88f), U(r, 0.5f, 0.66f) });
                g.DrawLine(pen, U(r, 0.62f, 0.22f), U(r, 0.92f, 0.22f));
                g.DrawLine(pen, U(r, 0.62f, 0.5f), U(r, 0.84f, 0.5f));
                g.DrawLine(pen, U(r, 0.62f, 0.78f), U(r, 0.76f, 0.78f));
            }
        }
        public static void GridIcon(Graphics g, RectangleF r, Color color)
        {
            float cell = r.Width * 0.38f, step = r.Width - cell;
            for (int i = 0; i < 4; i++)
                using (var box = Round(new RectangleF(r.X + i % 2 * step, r.Y + i / 2 * step, cell, cell), cell * 0.3f)) Fill(g, box, color);
        }
        public static void Info(Graphics g, RectangleF r, Color color)
        {
            using (var pen = new Pen(color, Math.Max(1.2f, r.Width * 0.1f))) g.DrawEllipse(pen, Inflate(r, -r.Width * 0.05f, -r.Height * 0.05f));
            using (var b = new SolidBrush(color)) g.FillEllipse(b, Square(U(r, 0.5f, 0.3f), r.Width * 0.14f));
            using (var pen = RoundPen(color, Math.Max(1.2f, r.Width * 0.12f))) g.DrawLine(pen, U(r, 0.5f, 0.46f), U(r, 0.5f, 0.74f));
        }
        // Small compass rose: the "Percorso" (playthrough order) sorting and chapter filter.
        public static void Compass(Graphics g, RectangleF r, Color color)
        {
            using (var pen = new Pen(color, Math.Max(1.1f, r.Width * 0.08f))) g.DrawEllipse(pen, Inflate(r, -r.Width * 0.06f, -r.Height * 0.06f));
            var c = Center(r);
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[] { new PointF(c.X, r.Y + r.Height * 0.16f), new PointF(c.X + r.Width * 0.13f, c.Y), new PointF(c.X, r.Bottom - r.Height * 0.16f), new PointF(c.X - r.Width * 0.13f, c.Y) });
                Fill(g, path, color);
            }
        }
        // Folder tabs: categories.
        public static void Tag(Graphics g, RectangleF r, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[] { U(r, 0.1f, 0.18f), U(r, 0.56f, 0.18f), U(r, 0.92f, 0.52f), U(r, 0.56f, 0.88f), U(r, 0.1f, 0.88f) });
                using (var pen = RoundPen(color, Math.Max(1.2f, r.Width * 0.1f))) g.DrawPath(pen, path);
            }
            using (var b = new SolidBrush(color)) g.FillEllipse(b, Square(U(r, 0.32f, 0.53f), r.Width * 0.16f));
        }
        public static void StrokeDashed(Graphics g, RectangleF r, float radius, Color color, float width)
        {
            using (var path = Round(r, radius))
            using (var pen = new Pen(color, width) { DashStyle = DashStyle.Custom, DashPattern = new[] { 3.2f, 2.4f }, DashCap = DashCap.Round })
                g.DrawPath(pen, path);
        }
        // Pill with an icon and a label in the state's ink, on a pale tint of the same colour.
        // Returns the width used.
        public static float StateChip(Graphics g, float x, float y, float height, Color ink, Action<Graphics, RectangleF, Color> icon, string text, Font font, float maxWidth)
        {
            float tw = Measure(g, text, font).Width + 2, w = Math.Min(maxWidth, tw + (icon != null ? height : 0) + 16);
            var r = new RectangleF(x, y, w, height);
            Fill(g, r, height / 2, Color.FromArgb(30, ink), Color.FromArgb(38, ink));
            Stroke(g, r, height / 2, Color.FromArgb(70, ink), 1);
            float tx = x + 8;
            if (icon != null)
            {
                icon(g, Square(new PointF(x + 8 + height * 0.36f, y + height / 2), height * 0.64f), ink);
                tx += height * 0.76f + 2;
            }
            Text(g, text, font, ink, new RectangleF(tx, y, r.Right - tx - 6, height), StringAlignment.Near, StringAlignment.Center, false);
            return w;
        }
        // Applies a uniform opacity to an offscreen frame while compositing it.
        public static void DrawFaded(Graphics g, Bitmap bitmap, RectangleF destination, float alpha)
        {
            if (alpha >= 0.995f) { g.DrawImage(bitmap, destination); return; }
            if (alpha <= 0.005f) return;
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha });
                var points = new[] { destination.Location, new PointF(destination.Right, destination.Y), new PointF(destination.X, destination.Bottom) };
                g.DrawImage(bitmap, points, new RectangleF(0, 0, bitmap.Width, bitmap.Height), GraphicsUnit.Pixel, attributes);
            }
        }
    }
}
