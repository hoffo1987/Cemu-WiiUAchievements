using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;

namespace WiiUAchievements
{
    public enum ArtKind { Gem, Heart }
    public enum Tier { Bronze, Silver, Gold, Platinum }

    // Medal rank is editorial metadata, independent of points and verification status.
    // The picture comes from the game's style: the owner's icon when one exists, otherwise
    // an original glyph on the game's sky inside the frame of the same rarity.
    public sealed class Art
    {
        public string Id;
        public ArtKind Kind;
        public int Level;
        public Tier Tier;
        public string Badge;
        public GameStyle Style = GameStyle.Neutral;
        public Motif Motif;
        public Color Tint;
        public string Number;

        public static Art For(Achievement a, IList<Achievement> all) { return For(a, all, null); }
        public static Art For(Achievement a, IList<Achievement> all, string gameId)
        {
            var art = new Art { Id = a.Id, Kind = a.Field == "max_health" ? ArtKind.Heart : ArtKind.Gem, Style = GameStyle.For(gameId) };
            art.Level = all.Count(x => !x.IsPlanned && x.Field == a.Field && x.Threshold < a.Threshold);
            art.Tier = (Tier)a.Rarity;
            art.Badge = art.Kind == ArtKind.Heart ? (a.Threshold / 4).ToString(CultureInfo.InvariantCulture) : null;
            if (a.Field == "rupees") art.Motif = Motif.Gem;
            else if (a.Field == "max_health") { art.Motif = Motif.Heart; art.Number = art.Badge; }
            else Glyphs.Resolve(a, gameId, out art.Motif, out art.Tint, out art.Number);
            return art;
        }
        public bool HasIcon { get { return Style.Icon(Id) != null; } }
        public static string TierName(Tier tier)
        {
            switch (tier) { case Tier.Platinum: return "Platino"; case Tier.Gold: return "Oro"; case Tier.Silver: return "Argento"; default: return "Bronzo"; }
        }
    }

    // Rasterised medals at a few device sizes, shared by cards, detail and popup. Drawing a
    // cached bitmap keeps hover/celebration frames cheap; the cache is bounded.
    public static class Medals
    {
        sealed class Entry { public Bitmap Bitmap; public long Used; }
        const int Limit = 160;
        static readonly int[] sizes = { 32, 48, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>(StringComparer.Ordinal);
        static long use;
        public static int Builds { get; private set; }
        public static int Cached { get { return cache.Count; } }

        // Locked medals keep a trace of colour, slightly lifted, like greyed system icons.
        static ColorMatrix LockedMatrix()
        {
            const float t = 0.86f, k = 0.8f, lift = 0.2f;
            float[] w = { 0.3f, 0.59f, 0.11f };
            var m = new float[5][];
            for (int i = 0; i < 5; i++) m[i] = new float[5];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++) m[i][j] = t * k * w[i] + (i == j ? 1 - t : 0);
            m[3][3] = 1;
            for (int j = 0; j < 3; j++) m[4][j] = t * lift;
            m[4][4] = 1;
            return new ColorMatrix(m);
        }

        public static RectangleF GlyphBox(RectangleF medal)
        {
            return Theme.Square(new PointF(medal.X + medal.Width * 0.535f, medal.Y + medal.Height * 0.47f), medal.Width * 0.56f);
        }
        static float DeviceScale(Graphics g)
        {
            using (var m = g.Transform) { var e = m.Elements; return (float)Math.Sqrt(e[0] * e[0] + e[1] * e[1]); }
        }
        static int Bucket(float pixels)
        {
            foreach (int s in sizes) if (s >= pixels - 0.5f) return s;
            return sizes[sizes.Length - 1];
        }

        public static void Draw(Graphics g, RectangleF r, Art art, float mono)
        {
            if (r.Width < 1) return;
            if (!art.Style.HasFrames) { Theme.VectorMedal(g, r, art, mono); return; }
            var bitmap = Get(art, Bucket(r.Width * DeviceScale(g)), mono > 0.5f);
            var interpolation = g.InterpolationMode; var offset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(bitmap, r);
            g.InterpolationMode = interpolation; g.PixelOffsetMode = offset;
        }
        public static Bitmap Get(Art art, int pixels, bool locked)
        {
            string key = art.Style.GameId + "|" + art.Id + "|" + art.Tier + "|" + pixels + (locked ? "|L" : "|U");
            Entry entry;
            if (!cache.TryGetValue(key, out entry))
            {
                if (cache.Count >= Limit)
                {
                    var oldest = cache.OrderBy(pair => pair.Value.Used).First();
                    oldest.Value.Bitmap.Dispose(); cache.Remove(oldest.Key);
                }
                entry = new Entry { Bitmap = Render(art, pixels, locked) };
                cache.Add(key, entry); Builds++;
            }
            entry.Used = ++use;
            return entry.Bitmap;
        }
        public static void Clear()
        {
            foreach (var entry in cache.Values) entry.Bitmap.Dispose();
            cache.Clear();
        }

        public static Bitmap Render(Art art, int pixels, bool locked)
        {
            var color = new Bitmap(pixels, pixels, PixelFormat.Format32bppPArgb);
            try
            {
                using (var g = Graphics.FromImage(color))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    var full = new RectangleF(0, 0, pixels, pixels);
                    var icon = art.Style.Icon(art.Id);
                    if (icon != null) g.DrawImage(icon, full);
                    else
                    {
                        g.DrawImage(art.Style.Sky, full);
                        var box = GlyphBox(full);
                        Glyphs.Draw(g, box, art.Motif, art.Tint, art.Level);
                        Glyphs.Plate(g, box, art.Number);
                        g.DrawImage(art.Style.Frame(art.Tier) ?? art.Style.Frame(Tier.Bronze), full);
                    }
                }
                if (!locked) return color;
                var grey = new Bitmap(pixels, pixels, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(grey))
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(LockedMatrix());
                    g.DrawImage(color, new Rectangle(0, 0, pixels, pixels), 0, 0, pixels, pixels, GraphicsUnit.Pixel, attributes);
                }
                color.Dispose();
                return grey;
            }
            catch { color.Dispose(); throw; }
        }
    }
}
