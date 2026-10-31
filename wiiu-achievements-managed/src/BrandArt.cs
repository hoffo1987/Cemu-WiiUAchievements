using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace WiiUAchievements
{
    // Owner-supplied project branding, independent of the selected game's style.
    public static class BrandArt
    {
        static readonly Bitmap logo = Load("logo.png"), icon = Load("app-icon.png");
        static Bitmap Load(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WiiUAchievements.Brand." + name))
            {
                if (stream != null) using (var image = Image.FromStream(stream)) return new Bitmap(image);
            }
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "brand", name);
            if (File.Exists(path)) using (var image = Image.FromFile(path)) return new Bitmap(image);
            return null;
        }
        public static bool Available { get { return logo != null && icon != null; } }
        public static bool DrawLogo(Graphics g, RectangleF area) { return Draw(g, logo, area); }
        public static bool DrawIcon(Graphics g, RectangleF area) { return Draw(g, icon, area); }
        static bool Draw(Graphics g, Bitmap source, RectangleF area)
        {
            if (source == null) return false;
            float ratio = Math.Min(area.Width / source.Width, area.Height / source.Height);
            float w = source.Width * ratio, h = source.Height * ratio;
            var mode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, new RectangleF(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
            g.InterpolationMode = mode;
            return true;
        }
    }
}
