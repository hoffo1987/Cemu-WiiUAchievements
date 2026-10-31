using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;

namespace WiiUAchievements
{
    public sealed partial class MainForm
    {
        sealed class TileImage : IDisposable
        {
            public Bitmap Bitmap;
            public SizeF Size;
            public float Scale;
            public bool Unlocked;
            public string Footer;
            public long Used;
            public void Dispose() { Bitmap.Dispose(); }
        }
        // The cache holds the whole catalog when memory allows (about 72 MB of card pixels), and
        // never fewer than twice the cards in view.
        const int TileCacheLimit = 48, TileCacheMaximum = 400;
        const long TileCacheBytes = 72L * 1024 * 1024;
        int tileCacheBudget = TileCacheLimit;
        const float TileBleed = 20;
        readonly Dictionary<string, TileImage> tileImages = new Dictionary<string, TileImage>();
        long tileImageUse;
        int tileImageBuilds;

        void ClearTileImages()
        {
            foreach (var entry in tileImages.Values) entry.Dispose();
            tileImages.Clear();
        }
        void UpdateTileBudget(SizeF tile, int inView)
        {
            long bytes = (long)Math.Ceiling((tile.Width + TileBleed * 2) * scale) * (long)Math.Ceiling((tile.Height + TileBleed * 2) * scale) * 4;
            int byMemory = (int)Math.Min(TileCacheMaximum, TileCacheBytes / Math.Max(1, bytes));
            tileCacheBudget = Math.Min(TileCacheMaximum, Math.Max(Math.Max(TileCacheLimit, inView * 2 + 8), Math.Min(byMemory, rules.Count + 8)));
        }

        // The cached picture of a card at rest. Hover, press and keyboard focus are drawn on top
        // of it (lift, shadow, outline), so a moving cursor never redraws the card itself.
        TileImage TileImageFor(Achievement a, SizeF size, float t)
        {
            var unlock = progress == null || a.IsPlanned ? null : progress.Get(a.Id);
            bool done = unlock != null;
            string footer = StaticFooter(a, unlock);
            TileImage entry;
            if (tileImages.TryGetValue(a.Id, out entry) &&
                (entry.Size != size || entry.Scale != scale || entry.Unlocked != done || entry.Footer != footer))
            {
                entry.Dispose(); tileImages.Remove(a.Id); entry = null;
            }
            if (entry == null)
            {
                while (tileImages.Count >= tileCacheBudget)
                {
                    var oldest = tileImages.OrderBy(pair => pair.Value.Used).First();
                    oldest.Value.Dispose(); tileImages.Remove(oldest.Key);
                }
                var bitmap = new Bitmap((int)Math.Ceiling((size.Width + TileBleed * 2) * scale),
                    (int)Math.Ceiling((size.Height + TileBleed * 2) * scale), PixelFormat.Format32bppPArgb);
                try
                {
                    using (var surface = Graphics.FromImage(bitmap))
                    {
                        surface.SmoothingMode = SmoothingMode.AntiAlias;
                        surface.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                        surface.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        surface.ScaleTransform(scale, scale);
                        DrawTileVector(surface, new Widget { Achievement = a,
                            Bounds = new RectangleF(TileBleed, TileBleed, size.Width, size.Height) }, t, footer != null);
                    }
                }
                catch { bitmap.Dispose(); throw; }
                entry = new TileImage { Bitmap = bitmap, Size = size, Scale = scale, Unlocked = done, Footer = footer };
                tileImages.Add(a.Id, entry); tileImageBuilds++;
            }
            return entry;
        }
        bool TileImageReady(Achievement a, SizeF size)
        {
            TileImage entry;
            if (!tileImages.TryGetValue(a.Id, out entry) || entry.Size != size || entry.Scale != scale) return false;
            var unlock = progress == null || a.IsPlanned ? null : progress.Get(a.Id);
            return entry.Unlocked == (unlock != null) && entry.Footer == StaticFooter(a, unlock);
        }

        // alpha < 1 only while the card fades in after a filter change.
        void DrawTile(Graphics g, Widget w, float t, float alpha)
        {
            // Only an unlock celebration redraws the card itself (the medal turns and glows).
            if (Celebration(w.Achievement.Id, t) >= 0)
            {
                DrawTileVector(g, w, t, true);
                return;
            }
            var a = w.Achievement;
            var entry = TileImageFor(a, w.Bounds.Size, t);
            entry.Used = ++tileImageUse;
            float cursor = Math.Max(w.Hover, FocusStrength(w, t));
            var r = w.Bounds;
            if (!Motion.Reduced) r.Y += -3f * w.Hover + 1.5f * w.Press;
            r.X = (float)Math.Round(r.X * scale) / scale;
            r.Y = (float)Math.Round(r.Y * scale) / scale;
            // A raised card casts a deeper shadow under its cached picture.
            if (cursor > 0.002f) Theme.Shadow(g, r, 16, 4 + 4 * cursor, (int)(28 * cursor), 2 + 1.5f * cursor);
            // Pixel-aligned 1:1 copy: no resampling needed (unless the intro is zooming the card).
            var interpolation = g.InterpolationMode; var offset = g.PixelOffsetMode; var quality = g.CompositingQuality;
            using (var m = g.Transform)
                if (Math.Abs(m.Elements[0] - scale) < 0.001f && m.Elements[1] == 0)
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingQuality = CompositingQuality.HighSpeed;
                }
            var destination = new RectangleF(r.X - TileBleed, r.Y - TileBleed, entry.Bitmap.Width / scale, entry.Bitmap.Height / scale);
            if (alpha < 1) Theme.DrawFaded(g, entry.Bitmap, destination, alpha);
            else g.DrawImage(entry.Bitmap, destination.X, destination.Y, destination.Width, destination.Height);
            g.InterpolationMode = interpolation; g.PixelOffsetMode = offset; g.CompositingQuality = quality;
            if (entry.Footer == null && alpha > 0.5f)
            {
                var unlock = progress == null || a.IsPlanned ? null : progress.Get(a.Id);
                DrawProgress(g, a, unlock, FooterRect(r, a), false);
            }
            if (cursor > 0.002f) Theme.Cursor(g, r, 16, cursor);
        }
        // Footers that do not depend on live samples (pending, unlocked, disconnected) are
        // part of the cached card; the key changes whenever their text would change.
        string StaticFooter(Achievement a, Unlock unlock)
        {
            if (a.IsPlanned) return "planned";
            if (unlock != null) return "unlocked " + unlock.Utc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (current == null) return "idle " + a.Experimental + paused + demo;
            return null;
        }

        // While nothing else moves, prepare cards before they are needed: first those just above
        // and below the viewport, then the rest of the list, then the other filters' cards.
        // One card at a time, within a few milliseconds, so input and painting stay responsive.
        readonly Stopwatch prewarmClock = new Stopwatch();
        bool PrewarmTiles(float t, double budgetMs)
        {
            if (libraryOpen) return false;
            SizeF size = SizeF.Empty;
            var tiles = new List<Widget>();
            foreach (var w in visible) if (w.Kind == Kind.Tile) { tiles.Add(w); size = w.Bounds.Size; }
            if (tiles.Count == 0) return false;
            float middle = gridRect.Y + gridRect.Height / 2;
            var listed = new HashSet<Achievement>(tiles.Select(w => w.Achievement));
            var order = tiles.OrderBy(w => Math.Abs(w.Bounds.Y + w.Bounds.Height / 2 - middle)).Select(w => w.Achievement)
                .Concat(rules.Where(a => !listed.Contains(a)));
            int room = tileCacheBudget - 4;
            prewarmClock.Restart();
            foreach (var a in order)
            {
                if (TileImageReady(a, size)) continue;
                if (tileImages.Count >= room || prewarmClock.Elapsed.TotalMilliseconds > budgetMs) return tileImages.Count < room;
                TileImageFor(a, size, t);
            }
            return false;
        }
    }
}
