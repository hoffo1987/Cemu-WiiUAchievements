using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WiiUAchievements
{
    public enum Motif
    {
        Gem, Heart, Sail, SwiftSail, Pearl, StoneMask, MasterSword, Cage, Crown, Lantern, Whirlwind, Shard, Triangle,
        Castle, LightArrow, Tower, Harp, Fiddle, Puppet, Sunrise, GrapplingHook, Leaf, Boomerang, Bomb, Bow, Hammer,
        ElementArrows, Bracelet, Boots, MirrorShield, Hookshot, Armor, MagicJar, Quiver, Purse, Note, Cyclone, SunMoon,
        Notes, Bottle, HeartPiece, Scroll, GoldScroll, Statue, Figurine, SeaChart, Chest, GhostShip, OctoEye, CalmSea,
        Stairs, Soup, Magnifier, Target, Letter, LoveLetter, Sword, Spin, House, Pendant, Drop, Key, Lighthouse, Wings,
        Cannon, Trade, Bunting, Feather, Compass, Star
    }

    // Original cel-shaded item drawings (dark warm outline, two tones, a gloss) that sit on
    // the game's sky disc inside the rarity frame, matching the owner's supplied icons.
    // Coordinates are fractions of the glyph box.
    public static class Glyphs
    {
        sealed class Entry { public Motif Motif; public Color Tint; public string Number; }
        static readonly Dictionary<string, Entry> windWaker = new Dictionary<string, Entry>(StringComparer.Ordinal);
        static void M(string id, Motif motif, Color tint, string number = null) { windWaker[id] = new Entry { Motif = motif, Tint = tint, Number = number }; }
        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        static Glyphs()
        {
            Color stone = C(156, 150, 138), wood = C(176, 112, 58), gold = C(250, 198, 58), iron = C(132, 140, 150);
            M("story-sail", Motif.Sail, C(250, 236, 196));
            M("story-din-pearl", Motif.Pearl, C(236, 64, 60)); M("story-farore-pearl", Motif.Pearl, C(70, 196, 92)); M("story-nayru-pearl", Motif.Pearl, C(64, 132, 236));
            M("story-gohdan", Motif.StoneMask, stone); M("story-master-sword", Motif.MasterSword, C(110, 96, 210));
            M("story-helmaroc", Motif.Cage, iron); M("story-zelda", Motif.Crown, gold); M("story-jalhalla", Motif.Lantern, C(255, 214, 96));
            M("story-molgera", Motif.Whirlwind, C(112, 216, 196)); M("story-triforce-first", Motif.Shard, gold); M("story-triforce-complete", Motif.Triangle, gold);
            M("story-hyrule-barrier", Motif.Castle, stone); M("story-light-arrows", Motif.LightArrow, C(255, 236, 140));
            M("story-tower-rises", Motif.Tower, stone); M("story-medli-sage", Motif.Harp, gold); M("story-makar-sage", Motif.Fiddle, wood);
            M("story-puppet-ganon", Motif.Puppet, wood); M("story-ganondorf", Motif.Sunrise, C(255, 186, 72));
            M("gear-grappling-hook", Motif.GrapplingHook, iron); M("gear-deku-leaf", Motif.Leaf, C(92, 186, 72)); M("gear-boomerang", Motif.Boomerang, C(214, 160, 92));
            M("gear-bombs", Motif.Bomb, C(48, 64, 118)); M("gear-bow", Motif.Bow, wood); M("gear-skull-hammer", Motif.Hammer, iron);
            M("gear-elemental-arrows", Motif.ElementArrows, C(255, 120, 60)); M("gear-power-bracelets", Motif.Bracelet, gold); M("gear-iron-boots", Motif.Boots, iron);
            M("gear-mirror-shield", Motif.MirrorShield, C(210, 228, 240)); M("gear-hookshot", Motif.Hookshot, iron); M("gear-swift-sail", Motif.SwiftSail, C(250, 236, 196));
            M("gear-magic-armor", Motif.Armor, C(64, 196, 168)); M("gear-magic-double", Motif.MagicJar, C(88, 206, 96), "x2");
            M("gear-bombs-99", Motif.Bomb, C(48, 64, 118), "99"); M("gear-arrows-99", Motif.Quiver, wood, "99"); M("gear-wallet-max", Motif.Purse, C(196, 120, 220));
            M("collect-command-melody", Motif.Note, C(255, 170, 60)); M("collect-ballad-gales", Motif.Cyclone, C(80, 200, 236));
            M("collect-earth-lyric", Motif.Note, C(214, 128, 64)); M("collect-wind-aria", Motif.Note, C(96, 206, 120)); M("collect-song-passing", Motif.SunMoon, gold);
            M("collect-songs-6", Motif.Notes, C(64, 168, 236), "6"); M("collect-bottles-4", Motif.Bottle, C(170, 226, 244), "4");
            M("explore-chart-10", Motif.SeaChart, C(240, 222, 170), "10"); M("explore-chart-25", Motif.SeaChart, C(240, 222, 170), "25"); M("explore-chart-49", Motif.SeaChart, C(240, 222, 170), "49");
            M("explore-salvage-first", Motif.Chest, wood); M("explore-salvage-10", Motif.Chest, wood, "10"); M("explore-salvage-46", Motif.Chest, wood, "46");
            M("explore-ghost-ship", Motif.GhostShip, C(120, 96, 168)); M("explore-big-octo", Motif.OctoEye, C(206, 104, 160)); M("explore-all-octos", Motif.CalmSea, C(64, 168, 220));
            M("explore-savage-30", Motif.Stairs, stone, "30"); M("explore-savage-50", Motif.Stairs, stone, "50");
            M("collect-heart-piece-first", Motif.HeartPiece, C(236, 56, 72)); M("collect-heart-pieces-12", Motif.HeartPiece, C(236, 56, 72), "12"); M("collect-heart-pieces-44", Motif.HeartPiece, C(236, 56, 72), "44");
            M("collect-treasure-charts-10", Motif.Scroll, C(240, 222, 170), "10"); M("collect-treasure-charts-25", Motif.Scroll, C(240, 222, 170), "25"); M("collect-treasure-charts-46", Motif.Scroll, C(240, 222, 170), "46");
            M("collect-triforce-charts", Motif.GoldScroll, C(240, 222, 170), "8"); M("collect-tingle-statues-5", Motif.Statue, gold, "5");
            M("collect-gallery-first", Motif.Figurine, C(84, 176, 92)); M("collect-gallery-complete", Motif.Figurine, gold, "134");
            M("side-grandma", Motif.Soup, C(236, 150, 60)); M("side-killer-bees", Motif.Magnifier, C(196, 226, 246)); M("side-sinking-ships", Motif.Target, C(226, 62, 56));
            M("side-sinking-ships-20", Motif.Target, C(226, 62, 56), "20"); M("side-mail-25", Motif.Letter, C(250, 240, 214), "25"); M("side-orca-500", Motif.Sword, C(220, 232, 242), "500");
            M("side-hurricane-spin", Motif.Spin, C(120, 214, 246)); M("side-cabana", Motif.House, wood); M("side-joy-pendants-40", Motif.Pendant, C(250, 140, 190), "40");
            M("side-forest-water", Motif.Drop, C(80, 180, 236)); M("side-mila", Motif.Key, gold); M("side-maggie", Motif.LoveLetter, C(250, 240, 214));
            M("side-lighthouse", Motif.Lighthouse, C(236, 70, 60)); M("side-flight-control", Motif.Wings, C(232, 242, 252)); M("side-spectacle", Motif.Cannon, C(70, 76, 96));
            M("side-trading", Motif.Trade, gold); M("side-town-decoration", Motif.Bunting, C(236, 84, 84)); M("side-golden-feathers", Motif.Feather, gold);
        }

        // Explicit per-game choice first, then a neutral emblem for the category.
        public static void Resolve(Achievement a, string gameId, out Motif motif, out Color tint, out string number)
        {
            Entry e;
            number = null;
            if (gameId == GameCatalog.WindWakerId && windWaker.TryGetValue(a.Id, out e)) { motif = e.Motif; tint = e.Tint; number = e.Number; return; }
            switch (a.Category)
            {
                case "Storia": motif = Motif.Star; tint = C(255, 206, 72); break;
                case "Equipaggiamento": motif = Motif.Sword; tint = C(220, 232, 242); break;
                case "Esplorazione": motif = Motif.Compass; tint = C(240, 222, 170); break;
                case "Collezioni": motif = Motif.Chest; tint = C(176, 112, 58); break;
                case "Secondarie": motif = Motif.Letter; tint = C(250, 240, 214); break;
                default: motif = Motif.Star; tint = C(255, 206, 72); break;
            }
        }
        public static bool HasExplicit(string gameId, string achievementId)
        {
            return gameId == GameCatalog.WindWakerId && windWaker.ContainsKey(achievementId);
        }

        // ---------------------------------------------------------------- drawing kit
        static readonly Color InkLine = Color.FromArgb(52, 36, 30);
        static float Lw(RectangleF r) { return Math.Max(1f, r.Width * 0.042f); }
        static PointF P(RectangleF r, float x, float y) { return new PointF(r.X + r.Width * x, r.Y + r.Height * y); }
        static RectangleF R(RectangleF r, float x, float y, float w, float h) { return new RectangleF(r.X + r.Width * x, r.Y + r.Height * y, r.Width * w, r.Height * h); }
        static GraphicsPath Poly(RectangleF r, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(r, xy[i * 2], xy[i * 2 + 1]);
            var p = new GraphicsPath(); p.AddPolygon(pts); return p;
        }
        static GraphicsPath Oval(RectangleF r, float x, float y, float w, float h) { var p = new GraphicsPath(); p.AddEllipse(R(r, x, y, w, h)); return p; }
        static GraphicsPath Box(RectangleF r, float x, float y, float w, float h, float radius) { return Theme.Round(R(r, x, y, w, h), r.Width * radius); }
        static GraphicsPath Curve(RectangleF r, bool closed, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(r, xy[i * 2], xy[i * 2 + 1]);
            var p = new GraphicsPath();
            if (closed) p.AddClosedCurve(pts, 0.5f); else p.AddCurve(pts, 0.5f);
            return p;
        }
        static Color Ink(Color c) { return Theme.Mix(Theme.Dark(c, 0.7f), InkLine, 0.55f); }

        // Two-tone fill (lit upper-left, shaded lower-right), outline and optional gloss.
        static void Cel(Graphics g, RectangleF r, GraphicsPath p, Color c, bool gloss = true)
        {
            var b = p.GetBounds();
            if (b.Width < 0.5f || b.Height < 0.5f) return;
            using (var brush = new LinearGradientBrush(Theme.Inflate(b, 1, 1), Theme.Light(c, 0.26f), Theme.Dark(c, 0.08f), 70f)) g.FillPath(brush, p);
            var state = g.Save();
            g.SetClip(p, CombineMode.Intersect);
            using (var shade = new GraphicsPath())
            {
                shade.AddPolygon(new[] { new PointF(b.Right + 2, b.Y + b.Height * 0.35f), new PointF(b.Right + 2, b.Bottom + 2), new PointF(b.X + b.Width * 0.3f, b.Bottom + 2) });
                Theme.Fill(g, shade, Color.FromArgb(70, Theme.Dark(c, 0.55f)));
            }
            if (gloss) using (var hb = new SolidBrush(Color.FromArgb(110, 255, 255, 255)))
                g.FillEllipse(hb, b.X + b.Width * 0.12f, b.Y + b.Height * 0.08f, b.Width * 0.34f, b.Height * 0.2f);
            g.Restore(state);
            using (var pen = new Pen(Ink(c), Lw(r)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, p);
        }
        static void Flat(Graphics g, GraphicsPath p, Color c) { Theme.Fill(g, p, c); }
        // Outlined stroke: a dark wide line under a coloured one, like an inked rope or shaft.
        static void Line(Graphics g, RectangleF r, Color c, float width, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(r, xy[i * 2], xy[i * 2 + 1]);
            float w = r.Width * width;
            using (var pen = new Pen(Ink(c), w + Lw(r) * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
            using (var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
        }
        static void CurveLine(Graphics g, RectangleF r, Color c, float width, params float[] xy)
        {
            using (var p = Curve(r, false, xy))
            {
                float w = r.Width * width;
                using (var pen = new Pen(Ink(c), w + Lw(r) * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, p);
                using (var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, p);
            }
        }
        static void Glow(Graphics g, PointF c, float radius, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(Theme.Square(c, radius * 2));
                using (var b = new PathGradientBrush(path) { CenterColor = color, SurroundColors = new[] { Color.FromArgb(0, color) } }) g.FillPath(b, path);
            }
        }
        static GraphicsState Turn(Graphics g, RectangleF r, float degrees)
        {
            var state = g.Save();
            var c = Theme.Center(r);
            g.TranslateTransform(c.X, c.Y); g.RotateTransform(degrees); g.TranslateTransform(-c.X, -c.Y);
            return state;
        }
        static void Spark(Graphics g, RectangleF r, float x, float y, float size)
        {
            Theme.Sparkle(g, P(r, x, y), r.Width * size, Color.FromArgb(235, 255, 255, 255));
        }

        // Number plate shared by series objectives: a cream tag in the lower right.
        public static void Plate(Graphics g, RectangleF r, string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            var font = Theme.Font(r.Height * (text.Length > 2 ? 0.2f : 0.24f), Weight.Heavy);
            float w = Math.Max(r.Width * 0.34f, Theme.Measure(g, text, font).Width + r.Width * 0.12f), h = r.Height * 0.3f;
            var plate = new RectangleF(r.Right - w + r.Width * 0.08f, r.Bottom - h + r.Height * 0.04f, w, h);
            using (var path = Theme.Round(plate, h / 2))
            {
                using (var b = new LinearGradientBrush(Theme.Inflate(plate, 0, 1), Color.FromArgb(255, 250, 230), Color.FromArgb(244, 224, 176), 90f)) g.FillPath(b, path);
                using (var pen = new Pen(InkLine, Lw(r))) g.DrawPath(pen, path);
            }
            Theme.Text(g, text, font, Color.FromArgb(120, 64, 28), Theme.Inflate(plate, 4, 0), StringAlignment.Center, StringAlignment.Center, false);
        }

        // ---------------------------------------------------------------- motifs
        public static void Draw(Graphics g, RectangleF r, Motif motif, Color tint, int level)
        {
            switch (motif)
            {
                case Motif.Gem: Theme.Gem(g, r, Theme.GemColor(level), 0); break;
                case Motif.Heart: Theme.Heart(g, Theme.Inflate(r, -r.Width * 0.06f, -r.Height * 0.06f), 0); break;
                case Motif.Sail: Sail(g, r, tint, false); break;
                case Motif.SwiftSail: Sail(g, r, tint, true); break;
                case Motif.Pearl: Pearl(g, r, tint); break;
                case Motif.StoneMask: StoneMask(g, r, tint); break;
                case Motif.MasterSword: { var s = Turn(g, r, 40); Sword(g, r, C(226, 236, 246), tint, C(52, 66, 138), true); g.Restore(s); break; }
                case Motif.Sword: { var s = Turn(g, r, 40); Sword(g, r, tint, C(120, 120, 140), C(132, 84, 46), false); g.Restore(s); Spark(g, r, 0.82f, 0.2f, 0.12f); break; }
                case Motif.Cage: Cage(g, r, tint); break;
                case Motif.Crown: Crown(g, r, tint); break;
                case Motif.Lantern: Lantern(g, r, tint); break;
                case Motif.Whirlwind: Whirlwind(g, r, tint); break;
                case Motif.Shard: Shard(g, r, tint); break;
                case Motif.Triangle: TriangleFull(g, r, tint); break;
                case Motif.Castle: Castle(g, r, tint); break;
                case Motif.LightArrow: { Glow(g, Theme.Center(r), r.Width * 0.45f, Color.FromArgb(150, 255, 246, 190)); var s = Turn(g, r, 45); Arrow(g, r, C(255, 250, 220), tint, tint); g.Restore(s); Spark(g, r, 0.78f, 0.22f, 0.13f); Spark(g, r, 0.2f, 0.7f, 0.08f); break; }
                case Motif.Tower: Tower(g, r, tint); break;
                case Motif.Harp: Harp(g, r, tint); break;
                case Motif.Fiddle: Fiddle(g, r, tint); break;
                case Motif.Puppet: Puppet(g, r, tint); break;
                case Motif.Sunrise: Sunrise(g, r, tint); break;
                case Motif.GrapplingHook: GrapplingHook(g, r, tint); break;
                case Motif.Leaf: Leaf(g, r, tint); break;
                case Motif.Boomerang: Boomerang(g, r, tint); break;
                case Motif.Bomb: Bomb(g, r, tint); break;
                case Motif.Bow: Bow(g, r, tint); break;
                case Motif.Hammer: Hammer(g, r, tint); break;
                case Motif.ElementArrows:
                    {
                        var s = Turn(g, r, 30); Arrow(g, Shift(r, -0.1f, 0), C(214, 190, 150), C(255, 110, 50), C(255, 200, 70)); g.Restore(s);
                        s = Turn(g, r, 60); Arrow(g, Shift(r, 0.1f, 0), C(214, 190, 150), C(110, 210, 250), C(220, 246, 255)); g.Restore(s);
                        break;
                    }
                case Motif.Bracelet: Bracelet(g, r, tint); break;
                case Motif.Boots: Boots(g, r, tint); break;
                case Motif.MirrorShield: MirrorShield(g, r, tint); break;
                case Motif.Hookshot: Hookshot(g, r, tint); break;
                case Motif.Armor: Armor(g, r, tint); break;
                case Motif.MagicJar: MagicJar(g, r, tint); break;
                case Motif.Quiver: Quiver(g, r, tint); break;
                case Motif.Purse: Purse(g, r, tint); break;
                case Motif.Note: Note(g, R(r, 0.08f, 0.04f, 0.84f, 0.92f), tint); Spark(g, r, 0.8f, 0.24f, 0.1f); break;
                case Motif.Cyclone: Cyclone(g, r, tint); break;
                case Motif.SunMoon: SunMoon(g, r, tint); break;
                case Motif.Notes:
                    Note(g, R(r, -0.02f, 0.2f, 0.56f, 0.62f), C(255, 170, 60)); Note(g, R(r, 0.46f, 0.16f, 0.56f, 0.62f), C(96, 206, 120));
                    Note(g, R(r, 0.2f, -0.02f, 0.56f, 0.62f), tint); break;
                case Motif.Bottle: Bottle(g, r, tint, false); break;
                case Motif.HeartPiece: HeartPiece(g, r, tint); break;
                case Motif.Scroll: Scroll(g, r, tint, false); break;
                case Motif.GoldScroll: Scroll(g, r, tint, true); break;
                case Motif.Statue: Statue(g, r, tint); break;
                case Motif.Figurine: Figurine(g, r, tint); break;
                case Motif.SeaChart: SeaChart(g, r, tint); break;
                case Motif.Chest: Chest(g, r, tint); break;
                case Motif.GhostShip: GhostShip(g, r, tint); break;
                case Motif.OctoEye: OctoEye(g, r, tint); break;
                case Motif.CalmSea: CalmSea(g, r, tint); break;
                case Motif.Stairs: Stairs(g, r, tint); break;
                case Motif.Soup: Soup(g, r, tint); break;
                case Motif.Magnifier: Magnifier(g, r, tint); break;
                case Motif.Target: Target(g, r, tint); break;
                case Motif.Letter: Letter(g, r, tint, false); break;
                case Motif.LoveLetter: Letter(g, r, tint, true); break;
                case Motif.Spin: Spin(g, r, tint); break;
                case Motif.House: House(g, r, tint); break;
                case Motif.Pendant: Pendant(g, r, tint); break;
                case Motif.Drop: Drop(g, r, tint); break;
                case Motif.Key: Key(g, r, tint); break;
                case Motif.Lighthouse: Lighthouse(g, r, tint); break;
                case Motif.Wings: Wings(g, r, tint); break;
                case Motif.Cannon: Cannon(g, r, tint); break;
                case Motif.Trade: Trade(g, r, tint); break;
                case Motif.Bunting: Bunting(g, r, tint); break;
                case Motif.Feather: Feather(g, r, tint); break;
                case Motif.Compass: Compass(g, r, tint); break;
                default: Theme.Star(g, Theme.Inflate(r, -r.Width * 0.08f, -r.Height * 0.08f), Theme.Light(tint, 0.3f), Theme.Dark(tint, 0.2f)); break;
            }
        }
        static RectangleF Shift(RectangleF r, float x, float y) { r.Offset(r.Width * x, r.Height * y); return r; }
        static void Pie(GraphicsPath p, RectangleF b, float start, float sweep) { p.AddPie(b.X, b.Y, b.Width, b.Height, start, sweep); }

        static void Sail(Graphics g, RectangleF r, Color cloth, bool swift)
        {
            if (swift)
                for (int i = 0; i < 3; i++) Line(g, r, Color.White, 0.035f, 0.02f, 0.3f + i * 0.17f, 0.2f, 0.3f + i * 0.17f);
            using (var hull = Poly(r, 0.14f, 0.8f, 0.9f, 0.8f, 0.76f, 0.95f, 0.26f, 0.95f)) Cel(g, r, hull, C(206, 52, 44), false);
            Line(g, r, C(126, 84, 50), 0.05f, 0.34f, 0.06f, 0.34f, 0.84f);
            using (var sail = new GraphicsPath())
            {
                sail.AddBezier(P(r, 0.36f, 0.1f), P(r, 0.7f, 0.12f), P(r, 0.96f, 0.36f), P(r, 0.86f, 0.62f));
                sail.AddBezier(P(r, 0.86f, 0.62f), P(r, 0.78f, 0.72f), P(r, 0.56f, 0.76f), P(r, 0.36f, 0.76f));
                sail.CloseFigure();
                Cel(g, r, sail, cloth, false);
                var state = g.Save(); g.SetClip(sail, CombineMode.Intersect);
                using (var stripe = new SolidBrush(swift ? C(214, 60, 52) : C(150, 120, 90))) g.FillRectangle(stripe, R(r, 0.3f, 0.66f, 0.7f, 0.05f));
                g.Restore(state);
            }
            // The wind emblem: a rolling spiral.
            CurveLine(g, r, swift ? C(214, 60, 52) : C(36, 140, 128), 0.045f, 0.5f, 0.52f, 0.56f, 0.34f, 0.7f, 0.3f, 0.74f, 0.44f, 0.64f, 0.48f, 0.62f, 0.42f);
        }
        static void Pearl(Graphics g, RectangleF r, Color c)
        {
            var center = P(r, 0.5f, 0.52f);
            Glow(g, center, r.Width * 0.48f, Color.FromArgb(150, Theme.Light(c, 0.4f)));
            using (var orb = Oval(r, 0.2f, 0.22f, 0.6f, 0.6f))
            {
                using (var b = new PathGradientBrush(orb) { CenterPoint = P(r, 0.42f, 0.4f), CenterColor = Theme.Light(c, 0.75f), SurroundColors = new[] { Theme.Dark(c, 0.25f) } }) g.FillPath(b, orb);
                using (var pen = new Pen(Ink(c), Lw(r))) g.DrawPath(pen, orb);
            }
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, R(r, 0.31f, 0.3f, 0.16f, 0.1f));
            Spark(g, r, 0.78f, 0.22f, 0.12f);
        }
        static void StoneMask(Graphics g, RectangleF r, Color c)
        {
            using (var head = Box(r, 0.16f, 0.12f, 0.68f, 0.76f, 0.16f)) Cel(g, r, head, c, false);
            using (var brow = Poly(r, 0.22f, 0.32f, 0.46f, 0.38f, 0.46f, 0.44f, 0.22f, 0.4f)) Cel(g, r, brow, Theme.Dark(c, 0.25f), false);
            using (var brow = Poly(r, 0.78f, 0.32f, 0.54f, 0.38f, 0.54f, 0.44f, 0.78f, 0.4f)) Cel(g, r, brow, Theme.Dark(c, 0.25f), false);
            foreach (float x in new[] { 0.34f, 0.66f })
            {
                Glow(g, P(r, x, 0.52f), r.Width * 0.12f, Color.FromArgb(200, 255, 90, 60));
                using (var eye = Oval(r, x - 0.06f, 0.47f, 0.12f, 0.1f)) Cel(g, r, eye, C(255, 214, 90), false);
            }
            using (var mouth = Box(r, 0.34f, 0.68f, 0.32f, 0.08f, 0.03f)) Cel(g, r, mouth, Theme.Dark(c, 0.4f), false);
        }
        static void Sword(Graphics g, RectangleF r, Color blade, Color guard, Color grip, bool master)
        {
            if (master) Glow(g, P(r, 0.5f, 0.36f), r.Width * 0.42f, Color.FromArgb(120, 190, 220, 255));
            using (var b = Poly(r, 0.5f, 0.0f, 0.585f, 0.1f, 0.585f, 0.64f, 0.415f, 0.64f, 0.415f, 0.1f)) Cel(g, r, b, blade, false);
            using (var edge = Poly(r, 0.5f, 0.04f, 0.56f, 0.12f, 0.56f, 0.6f, 0.5f, 0.6f)) Flat(g, edge, Color.FromArgb(120, Theme.Dark(blade, 0.35f)));
            using (var gd = new GraphicsPath())
            {
                gd.AddBezier(P(r, 0.24f, 0.6f), P(r, 0.36f, 0.66f), P(r, 0.64f, 0.66f), P(r, 0.76f, 0.6f));
                gd.AddLine(P(r, 0.76f, 0.6f), P(r, 0.7f, 0.72f));
                gd.AddBezier(P(r, 0.7f, 0.72f), P(r, 0.6f, 0.75f), P(r, 0.4f, 0.75f), P(r, 0.3f, 0.72f));
                gd.CloseFigure();
                Cel(g, r, gd, guard, false);
            }
            using (var h = Box(r, 0.44f, 0.73f, 0.12f, 0.17f, 0.03f)) Cel(g, r, h, grip, false);
            using (var p = Oval(r, 0.42f, 0.87f, 0.16f, 0.12f)) Cel(g, r, p, master ? C(255, 210, 70) : guard, false);
            if (master) Spark(g, r, 0.5f, 0.06f, 0.12f);
        }
        static void Cage(Graphics g, RectangleF r, Color c)
        {
            using (var b = Oval(r, 0.18f, 0.78f, 0.64f, 0.14f)) Cel(g, r, b, Theme.Dark(c, 0.15f), false);
            using (var dome = new GraphicsPath())
            {
                dome.AddArc(R(r, 0.22f, 0.12f, 0.56f, 0.6f), 180, 180);
                using (var pen = new Pen(Ink(c), r.Width * 0.07f)) g.DrawPath(pen, dome);
                using (var pen = new Pen(c, r.Width * 0.035f)) g.DrawPath(pen, dome);
            }
            foreach (float x in new[] { 0.22f, 0.36f, 0.64f, 0.78f }) Line(g, r, c, 0.035f, x, 0.42f, x, 0.82f);
            // The open door swings out to the right.
            Line(g, r, c, 0.035f, 0.5f, 0.42f, 0.9f, 0.5f, 0.9f, 0.86f);
            using (var f = Curve(r, true, 0.32f, 0.62f, 0.42f, 0.2f, 0.5f, 0.06f, 0.52f, 0.24f, 0.42f, 0.6f)) Cel(g, r, f, C(240, 236, 220), false);
            Line(g, r, C(196, 150, 110), 0.02f, 0.36f, 0.66f, 0.48f, 0.2f);
        }
        static void Crown(Graphics g, RectangleF r, Color c)
        {
            using (var p = Poly(r, 0.14f, 0.36f, 0.32f, 0.54f, 0.5f, 0.22f, 0.68f, 0.54f, 0.86f, 0.36f, 0.8f, 0.76f, 0.2f, 0.76f)) Cel(g, r, p, c);
            using (var band = Box(r, 0.18f, 0.7f, 0.64f, 0.12f, 0.03f)) Cel(g, r, band, Theme.Dark(c, 0.12f), false);
            foreach (var x in new[] { 0.14f, 0.5f, 0.86f }) using (var o = Oval(r, x - 0.05f, x == 0.5f ? 0.15f : 0.29f, 0.1f, 0.1f)) Cel(g, r, o, Theme.Light(c, 0.2f), false);
            using (var gem = Oval(r, 0.42f, 0.71f, 0.16f, 0.1f)) Cel(g, r, gem, C(220, 50, 70), false);
            using (var gem = Oval(r, 0.24f, 0.72f, 0.1f, 0.08f)) Cel(g, r, gem, C(60, 130, 230), false);
            using (var gem = Oval(r, 0.66f, 0.72f, 0.1f, 0.08f)) Cel(g, r, gem, C(60, 130, 230), false);
        }
        static void Lantern(Graphics g, RectangleF r, Color c)
        {
            Glow(g, P(r, 0.5f, 0.56f), r.Width * 0.5f, Color.FromArgb(170, c));
            Line(g, r, C(80, 70, 90), 0.035f, 0.38f, 0.2f, 0.5f, 0.06f, 0.62f, 0.2f);
            using (var cap = Poly(r, 0.3f, 0.26f, 0.7f, 0.26f, 0.62f, 0.18f, 0.38f, 0.18f)) Cel(g, r, cap, C(96, 84, 110), false);
            using (var glass = Box(r, 0.3f, 0.26f, 0.4f, 0.48f, 0.08f)) Cel(g, r, glass, Theme.Light(c, 0.35f));
            using (var flame = Curve(r, true, 0.5f, 0.36f, 0.58f, 0.52f, 0.5f, 0.64f, 0.42f, 0.52f)) Cel(g, r, flame, C(255, 150, 50), false);
            using (var bottom = Poly(r, 0.28f, 0.74f, 0.72f, 0.74f, 0.64f, 0.84f, 0.36f, 0.84f)) Cel(g, r, bottom, C(96, 84, 110), false);
            Spark(g, r, 0.2f, 0.4f, 0.09f); Spark(g, r, 0.82f, 0.62f, 0.08f);
        }
        static void Whirlwind(Graphics g, RectangleF r, Color c)
        {
            CurveLine(g, r, c, 0.06f, 0.14f, 0.3f, 0.5f, 0.2f, 0.86f, 0.3f);
            CurveLine(g, r, Theme.Light(c, 0.3f), 0.055f, 0.22f, 0.5f, 0.52f, 0.42f, 0.8f, 0.5f);
            CurveLine(g, r, c, 0.05f, 0.32f, 0.7f, 0.54f, 0.64f, 0.7f, 0.7f);
            CurveLine(g, r, Theme.Light(c, 0.3f), 0.045f, 0.42f, 0.86f, 0.52f, 0.84f, 0.6f, 0.86f);
            using (var leaf = Curve(r, true, 0.78f, 0.12f, 0.9f, 0.18f, 0.84f, 0.28f, 0.74f, 0.2f)) Cel(g, r, leaf, C(110, 196, 84), false);
            Spark(g, r, 0.2f, 0.14f, 0.09f);
        }
        static void Shard(Graphics g, RectangleF r, Color c)
        {
            Glow(g, P(r, 0.5f, 0.55f), r.Width * 0.48f, Color.FromArgb(150, 255, 236, 150));
            using (var p = Poly(r, 0.5f, 0.12f, 0.88f, 0.82f, 0.12f, 0.82f)) Cel(g, r, p, c);
            Line(g, r, Theme.Dark(c, 0.3f), 0.02f, 0.62f, 0.34f, 0.54f, 0.5f, 0.66f, 0.62f);
            Spark(g, r, 0.84f, 0.24f, 0.13f);
        }
        static void TriangleFull(Graphics g, RectangleF r, Color c)
        {
            Glow(g, P(r, 0.5f, 0.55f), r.Width * 0.52f, Color.FromArgb(170, 255, 236, 150));
            using (var p = Poly(r, 0.5f, 0.08f, 0.71f, 0.47f, 0.29f, 0.47f)) Cel(g, r, p, c);
            using (var p = Poly(r, 0.29f, 0.5f, 0.5f, 0.89f, 0.08f, 0.89f)) Cel(g, r, p, c);
            using (var p = Poly(r, 0.71f, 0.5f, 0.92f, 0.89f, 0.5f, 0.89f)) Cel(g, r, p, c);
            Spark(g, r, 0.18f, 0.22f, 0.12f); Spark(g, r, 0.86f, 0.3f, 0.09f);
        }
        static void Castle(Graphics g, RectangleF r, Color c)
        {
            Glow(g, P(r, 0.5f, 0.5f), r.Width * 0.5f, Color.FromArgb(110, 255, 220, 120));
            using (var wall = Poly(r, 0.24f, 0.44f, 0.76f, 0.44f, 0.76f, 0.88f, 0.24f, 0.88f)) Cel(g, r, wall, c, false);
            foreach (var x in new[] { 0.1f, 0.7f })
            {
                using (var t = Poly(r, x, 0.3f, x + 0.2f, 0.3f, x + 0.2f, 0.88f, x, 0.88f)) Cel(g, r, t, Theme.Light(c, 0.08f), false);
                using (var roof = Poly(r, x - 0.03f, 0.31f, x + 0.1f, 0.08f, x + 0.23f, 0.31f)) Cel(g, r, roof, C(70, 110, 190), false);
            }
            using (var gate = new GraphicsPath())
            {
                gate.AddArc(R(r, 0.38f, 0.56f, 0.24f, 0.2f), 180, 180);
                gate.AddLine(P(r, 0.62f, 0.66f), P(r, 0.62f, 0.88f)); gate.AddLine(P(r, 0.38f, 0.88f), P(r, 0.38f, 0.66f));
                gate.CloseFigure();
                Cel(g, r, gate, C(92, 64, 44), false);
            }
            for (int i = 0; i < 3; i++) using (var m = Box(r, 0.28f + i * 0.17f, 0.38f, 0.1f, 0.08f, 0.01f)) Cel(g, r, m, c, false);
        }
        static void Arrow(Graphics g, RectangleF r, Color shaft, Color head, Color glow)
        {
            Line(g, r, shaft, 0.065f, 0.5f, 0.2f, 0.5f, 0.86f);
            using (var f = Poly(r, 0.5f, 0.7f, 0.36f, 0.8f, 0.36f, 0.98f, 0.5f, 0.88f)) Cel(g, r, f, C(240, 240, 240), false);
            using (var f = Poly(r, 0.5f, 0.7f, 0.64f, 0.8f, 0.64f, 0.98f, 0.5f, 0.88f)) Cel(g, r, f, C(220, 220, 225), false);
            Glow(g, P(r, 0.5f, 0.14f), r.Width * 0.22f, Color.FromArgb(200, glow));
            using (var h = Poly(r, 0.5f, 0.0f, 0.67f, 0.28f, 0.5f, 0.22f, 0.33f, 0.28f)) Cel(g, r, h, head, false);
        }
        static void Tower(Graphics g, RectangleF r, Color c)
        {
            using (var t = Poly(r, 0.36f, 0.22f, 0.64f, 0.22f, 0.7f, 0.8f, 0.3f, 0.8f)) Cel(g, r, t, c, false);
            using (var top = Poly(r, 0.32f, 0.24f, 0.68f, 0.24f, 0.62f, 0.14f, 0.38f, 0.14f)) Cel(g, r, top, Theme.Light(c, 0.15f), false);
            using (var door = Box(r, 0.45f, 0.6f, 0.1f, 0.2f, 0.04f)) Cel(g, r, door, C(70, 60, 54), false);
            for (int i = 0; i < 3; i++) Line(g, r, Theme.Dark(c, 0.25f), 0.012f, 0.33f + i * 0.01f, 0.38f + i * 0.13f, 0.67f - i * 0.01f, 0.38f + i * 0.13f);
            using (var sea = Curve(r, false, 0.06f, 0.82f, 0.22f, 0.76f, 0.38f, 0.82f, 0.5f, 0.77f, 0.62f, 0.82f, 0.78f, 0.76f, 0.94f, 0.82f))
            using (var pen = new Pen(C(255, 255, 255), r.Width * 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { using (var ink = new Pen(C(40, 110, 170), r.Width * 0.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(ink, sea); g.DrawPath(pen, sea); }
            Color[] pearls = { C(236, 64, 60), C(70, 196, 92), C(64, 132, 236) };
            for (int i = 0; i < 3; i++) using (var o = Oval(r, 0.18f + i * 0.27f, 0.0f + (i == 1 ? 0 : 0.06f), 0.12f, 0.12f)) Cel(g, r, o, pearls[i], false);
        }
        static void Harp(Graphics g, RectangleF r, Color c)
        {
            using (var frame = new GraphicsPath())
            {
                frame.AddBezier(P(r, 0.26f, 0.9f), P(r, 0.14f, 0.5f), P(r, 0.3f, 0.12f), P(r, 0.62f, 0.12f));
                frame.AddBezier(P(r, 0.62f, 0.12f), P(r, 0.86f, 0.12f), P(r, 0.86f, 0.36f), P(r, 0.7f, 0.42f));
                using (var ink = new Pen(Ink(c), r.Width * 0.12f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(ink, frame);
                using (var pen = new Pen(c, r.Width * 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, frame);
            }
            Line(g, r, c, 0.05f, 0.7f, 0.42f, 0.36f, 0.9f, 0.26f, 0.9f);
            for (int i = 0; i < 5; i++)
            {
                float x = 0.3f + i * 0.08f;
                using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1, r.Width * 0.015f))) g.DrawLine(pen, P(r, x, 0.2f + i * 0.02f), P(r, x, 0.78f - i * 0.07f));
            }
            using (var gem = Oval(r, 0.56f, 0.08f, 0.1f, 0.1f)) Cel(g, r, gem, C(230, 60, 70), false);
        }
        static void Fiddle(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, -28);
            Line(g, r, C(70, 46, 30), 0.06f, 0.5f, 0.06f, 0.5f, 0.42f);
            using (var head = Oval(r, 0.45f, 0.02f, 0.1f, 0.1f)) Cel(g, r, head, C(70, 46, 30), false);
            using (var body = new GraphicsPath())
            {
                body.AddClosedCurve(new[] { P(r, 0.5f, 0.36f), P(r, 0.66f, 0.42f), P(r, 0.62f, 0.58f), P(r, 0.7f, 0.76f), P(r, 0.5f, 0.94f), P(r, 0.3f, 0.76f), P(r, 0.38f, 0.58f), P(r, 0.34f, 0.42f) }, 0.6f);
                Cel(g, r, body, c);
            }
            Line(g, r, C(60, 40, 28), 0.03f, 0.5f, 0.44f, 0.5f, 0.86f);
            g.Restore(s);
            Line(g, r, C(214, 196, 160), 0.022f, 0.14f, 0.28f, 0.9f, 0.76f);
            Spark(g, r, 0.84f, 0.2f, 0.1f);
        }
        static void Puppet(Graphics g, RectangleF r, Color c)
        {
            Line(g, r, C(244, 240, 228), 0.025f, 0.16f, 0.2f, 0.18f, 0.46f); Line(g, r, C(244, 240, 228), 0.025f, 0.84f, 0.2f, 0.84f, 0.48f);
            Line(g, r, C(244, 240, 228), 0.025f, 0.5f, 0.3f, 0.5f, 0.44f);
            Line(g, r, C(244, 240, 228), 0.025f, 0.2f, 0.64f, 0.26f, 0.94f); Line(g, r, C(244, 240, 228), 0.025f, 0.82f, 0.66f, 0.76f, 0.94f);
            using (var bar = Box(r, 0.08f, 0.1f, 0.84f, 0.13f, 0.04f)) Cel(g, r, bar, c, false);
            using (var bar = Box(r, 0.43f, 0.0f, 0.14f, 0.34f, 0.04f)) Cel(g, r, bar, c, false);
            // The cut: a bright snap between the frayed string ends.
            Theme.Sparkle(g, P(r, 0.19f, 0.55f), r.Width * 0.12f, C(255, 236, 140));
            Theme.Sparkle(g, P(r, 0.83f, 0.57f), r.Width * 0.12f, C(255, 236, 140));
            using (var head = Oval(r, 0.32f, 0.44f, 0.36f, 0.34f)) Cel(g, r, head, C(150, 110, 170));
            using (var b = new SolidBrush(C(255, 214, 90))) { g.FillEllipse(b, R(r, 0.4f, 0.56f, 0.07f, 0.07f)); g.FillEllipse(b, R(r, 0.53f, 0.56f, 0.07f, 0.07f)); }
        }
        static void Sunrise(Graphics g, RectangleF r, Color c)
        {
            var center = P(r, 0.5f, 0.66f);
            Theme.Rays(g, center, r.Width * 0.6f, -15, Color.FromArgb(170, 255, 236, 160));
            using (var sun = new GraphicsPath())
            {
                Pie(sun, R(r, 0.2f, 0.36f, 0.6f, 0.6f), 180, 180);
                Cel(g, r, sun, c);
            }
            using (var sea = Box(r, 0.04f, 0.66f, 0.92f, 0.26f, 0.08f)) Cel(g, r, sea, C(40, 140, 210), false);
            Line(g, r, Color.White, 0.03f, 0.16f, 0.76f, 0.4f, 0.76f); Line(g, r, Color.White, 0.03f, 0.54f, 0.84f, 0.84f, 0.84f);
        }
        static void GrapplingHook(Graphics g, RectangleF r, Color c)
        {
            using (var coil = new GraphicsPath())
            {
                for (int i = 0; i < 3; i++) coil.AddEllipse(R(r, 0.06f + i * 0.03f, 0.08f + i * 0.03f, 0.42f - i * 0.06f, 0.34f - i * 0.06f));
                using (var ink = new Pen(Ink(C(196, 160, 110)), r.Width * 0.07f)) g.DrawPath(ink, coil);
                using (var pen = new Pen(C(214, 180, 126), r.Width * 0.035f)) g.DrawPath(pen, coil);
            }
            CurveLine(g, r, C(214, 180, 126), 0.035f, 0.4f, 0.32f, 0.54f, 0.42f, 0.62f, 0.5f);
            Line(g, r, c, 0.06f, 0.62f, 0.48f, 0.62f, 0.8f);
            CurveLine(g, r, c, 0.05f, 0.62f, 0.8f, 0.46f, 0.82f, 0.4f, 0.68f);
            CurveLine(g, r, c, 0.05f, 0.62f, 0.8f, 0.78f, 0.82f, 0.84f, 0.68f);
            Line(g, r, c, 0.05f, 0.62f, 0.8f, 0.62f, 0.92f);
        }
        static void Leaf(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, 20);
            using (var leaf = new GraphicsPath())
            {
                leaf.AddBezier(P(r, 0.5f, 0.06f), P(r, 0.92f, 0.22f), P(r, 0.88f, 0.66f), P(r, 0.5f, 0.8f));
                leaf.AddBezier(P(r, 0.5f, 0.8f), P(r, 0.12f, 0.66f), P(r, 0.08f, 0.22f), P(r, 0.5f, 0.06f));
                Cel(g, r, leaf, c);
            }
            Line(g, r, Theme.Dark(c, 0.3f), 0.025f, 0.5f, 0.14f, 0.5f, 0.96f);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.28f + i * 0.14f;
                using (var pen = new Pen(Theme.Dark(c, 0.3f), Math.Max(1, r.Width * 0.018f))) { g.DrawLine(pen, P(r, 0.5f, y + 0.06f), P(r, 0.3f, y)); g.DrawLine(pen, P(r, 0.5f, y + 0.06f), P(r, 0.7f, y)); }
            }
            g.Restore(s);
        }
        static void Boomerang(Graphics g, RectangleF r, Color c)
        {
            using (var p = new GraphicsPath())
            {
                p.AddBezier(P(r, 0.1f, 0.3f), P(r, 0.3f, 0.2f), P(r, 0.5f, 0.3f), P(r, 0.56f, 0.52f));
                p.AddBezier(P(r, 0.56f, 0.52f), P(r, 0.66f, 0.42f), P(r, 0.8f, 0.4f), P(r, 0.92f, 0.5f));
                p.AddBezier(P(r, 0.92f, 0.5f), P(r, 0.84f, 0.66f), P(r, 0.62f, 0.7f), P(r, 0.5f, 0.82f));
                p.AddBezier(P(r, 0.5f, 0.82f), P(r, 0.4f, 0.56f), P(r, 0.26f, 0.42f), P(r, 0.1f, 0.3f));
                Cel(g, r, p, c);
            }
            using (var tip = Oval(r, 0.08f, 0.24f, 0.12f, 0.1f)) Cel(g, r, tip, C(220, 60, 52), false);
            using (var tip = Oval(r, 0.82f, 0.45f, 0.12f, 0.1f)) Cel(g, r, tip, C(220, 60, 52), false);
            Spark(g, r, 0.3f, 0.72f, 0.09f);
        }
        static void Bomb(Graphics g, RectangleF r, Color c)
        {
            CurveLine(g, r, C(214, 190, 140), 0.035f, 0.58f, 0.24f, 0.66f, 0.12f, 0.78f, 0.12f);
            Glow(g, P(r, 0.8f, 0.1f), r.Width * 0.14f, Color.FromArgb(220, 255, 200, 80));
            Theme.Sparkle(g, P(r, 0.8f, 0.1f), r.Width * 0.09f, C(255, 240, 160));
            using (var cap = Box(r, 0.48f, 0.2f, 0.16f, 0.1f, 0.02f)) Cel(g, r, cap, C(150, 150, 160), false);
            using (var ball = Oval(r, 0.14f, 0.26f, 0.64f, 0.64f)) Cel(g, r, ball, c);
        }
        static void Bow(Graphics g, RectangleF r, Color c)
        {
            using (var bow = new GraphicsPath())
            {
                bow.AddBezier(P(r, 0.3f, 0.06f), P(r, 0.86f, 0.18f), P(r, 0.86f, 0.82f), P(r, 0.3f, 0.94f));
                using (var ink = new Pen(Ink(c), r.Width * 0.12f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(ink, bow);
                using (var pen = new Pen(c, r.Width * 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, bow);
            }
            using (var pen = new Pen(Color.FromArgb(240, 250, 250, 240), Math.Max(1, r.Width * 0.02f))) g.DrawLine(pen, P(r, 0.3f, 0.06f), P(r, 0.3f, 0.94f));
            using (var grip = Box(r, 0.66f, 0.42f, 0.1f, 0.16f, 0.03f)) Cel(g, r, grip, C(90, 140, 70), false);
            var s = Turn(g, r, 90); Arrow(g, r, C(214, 190, 150), C(170, 180, 190), Color.FromArgb(0, 0, 0, 0)); g.Restore(s);
        }
        static void Hammer(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, -30);
            Line(g, r, C(150, 100, 56), 0.08f, 0.5f, 0.4f, 0.5f, 0.94f);
            using (var head = Box(r, 0.16f, 0.14f, 0.68f, 0.3f, 0.08f)) Cel(g, r, head, c);
            using (var face = Box(r, 0.1f, 0.18f, 0.1f, 0.22f, 0.03f)) Cel(g, r, face, Theme.Dark(c, 0.2f), false);
            using (var face = Box(r, 0.8f, 0.18f, 0.1f, 0.22f, 0.03f)) Cel(g, r, face, Theme.Dark(c, 0.2f), false);
            // A small skull mark on the head.
            using (var skull = Oval(r, 0.42f, 0.2f, 0.16f, 0.14f)) Cel(g, r, skull, C(240, 236, 220), false);
            using (var b = new SolidBrush(InkLine)) { g.FillEllipse(b, R(r, 0.45f, 0.25f, 0.04f, 0.04f)); g.FillEllipse(b, R(r, 0.51f, 0.25f, 0.04f, 0.04f)); }
            g.Restore(s);
        }
        static void Bracelet(Graphics g, RectangleF r, Color c)
        {
            using (var ring = new GraphicsPath(FillMode.Alternate))
            {
                ring.AddEllipse(R(r, 0.1f, 0.2f, 0.8f, 0.62f));
                ring.AddEllipse(R(r, 0.24f, 0.32f, 0.52f, 0.36f));
                Cel(g, r, ring, c);
            }
            using (var band = new GraphicsPath(FillMode.Alternate))
            {
                band.AddEllipse(R(r, 0.16f, 0.25f, 0.68f, 0.52f)); band.AddEllipse(R(r, 0.19f, 0.28f, 0.62f, 0.46f));
                Flat(g, band, Color.FromArgb(90, Theme.Dark(c, 0.4f)));
            }
            using (var gem = Oval(r, 0.42f, 0.66f, 0.16f, 0.14f)) Cel(g, r, gem, C(226, 50, 66), false);
            using (var gem = Oval(r, 0.42f, 0.14f, 0.16f, 0.12f)) Cel(g, r, gem, C(60, 150, 236), false);
        }
        static void Boots(Graphics g, RectangleF r, Color c)
        {
            using (var p = Poly(r, 0.3f, 0.1f, 0.62f, 0.1f, 0.62f, 0.56f, 0.9f, 0.68f, 0.9f, 0.86f, 0.24f, 0.86f, 0.24f, 0.6f)) Cel(g, r, p, c);
            using (var sole = Box(r, 0.2f, 0.82f, 0.74f, 0.1f, 0.03f)) Cel(g, r, sole, Theme.Dark(c, 0.35f), false);
            using (var cuff = Box(r, 0.26f, 0.08f, 0.4f, 0.1f, 0.03f)) Cel(g, r, cuff, Theme.Light(c, 0.15f), false);
            foreach (var y in new[] { 0.32f, 0.5f }) using (var rivet = Oval(r, 0.4f, y, 0.07f, 0.07f)) Cel(g, r, rivet, Theme.Light(c, 0.3f), false);
        }
        static void MirrorShield(Graphics g, RectangleF r, Color c)
        {
            using (var rim = Oval(r, 0.1f, 0.08f, 0.8f, 0.84f)) Cel(g, r, rim, C(232, 186, 64));
            using (var face = Oval(r, 0.2f, 0.18f, 0.6f, 0.64f))
            {
                using (var b = new LinearGradientBrush(R(r, 0.2f, 0.18f, 0.6f, 0.64f), Color.White, c, 50f)) g.FillPath(b, face);
                using (var pen = new Pen(Ink(c), Lw(r))) g.DrawPath(pen, face);
            }
            using (var p = Poly(r, 0.5f, 0.26f, 0.62f, 0.52f, 0.5f, 0.74f, 0.38f, 0.52f)) Cel(g, r, p, C(70, 130, 220), false);
            using (var shine = Poly(r, 0.26f, 0.34f, 0.36f, 0.24f, 0.46f, 0.3f, 0.3f, 0.48f)) Flat(g, shine, Color.FromArgb(200, 255, 255, 255));
            Spark(g, r, 0.78f, 0.2f, 0.12f);
        }
        static void Hookshot(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, -35);
            for (int i = 0; i < 3; i++) using (var link = Oval(r, 0.42f, 0.24f + i * 0.09f, 0.16f, 0.12f)) { using (var pen = new Pen(Ink(c), r.Width * 0.07f)) g.DrawPath(pen, link); using (var pen = new Pen(c, r.Width * 0.035f)) g.DrawPath(pen, link); }
            using (var tip = Poly(r, 0.5f, 0.0f, 0.7f, 0.2f, 0.58f, 0.2f, 0.58f, 0.26f, 0.42f, 0.26f, 0.42f, 0.2f, 0.3f, 0.2f)) Cel(g, r, tip, c, false);
            using (var barrel = Box(r, 0.34f, 0.5f, 0.32f, 0.3f, 0.08f)) Cel(g, r, barrel, C(206, 160, 70));
            using (var ring = Box(r, 0.32f, 0.54f, 0.36f, 0.08f, 0.02f)) Cel(g, r, ring, C(150, 110, 60), false);
            using (var grip = Box(r, 0.42f, 0.76f, 0.16f, 0.22f, 0.04f)) Cel(g, r, grip, C(120, 80, 50), false);
            g.Restore(s);
        }
        static void Armor(Graphics g, RectangleF r, Color c)
        {
            Glow(g, P(r, 0.5f, 0.5f), r.Width * 0.5f, Color.FromArgb(130, Theme.Light(c, 0.4f)));
            using (var p = new GraphicsPath())
            {
                p.AddBezier(P(r, 0.5f, 0.1f), P(r, 0.7f, 0.18f), P(r, 0.84f, 0.16f), P(r, 0.86f, 0.18f));
                p.AddBezier(P(r, 0.86f, 0.18f), P(r, 0.88f, 0.56f), P(r, 0.74f, 0.8f), P(r, 0.5f, 0.92f));
                p.AddBezier(P(r, 0.5f, 0.92f), P(r, 0.26f, 0.8f), P(r, 0.12f, 0.56f), P(r, 0.14f, 0.18f));
                p.AddBezier(P(r, 0.14f, 0.18f), P(r, 0.16f, 0.16f), P(r, 0.3f, 0.18f), P(r, 0.5f, 0.1f));
                Cel(g, r, p, c);
            }
            Theme.Gem(g, R(r, 0.36f, 0.3f, 0.28f, 0.42f), C(58, 190, 74), 0);
        }
        static void MagicJar(Graphics g, RectangleF r, Color c)
        {
            using (var cork = Box(r, 0.38f, 0.06f, 0.24f, 0.14f, 0.03f)) Cel(g, r, cork, C(196, 140, 84), false);
            using (var neck = Box(r, 0.4f, 0.18f, 0.2f, 0.12f, 0.02f)) Cel(g, r, neck, C(210, 236, 246), false);
            using (var jar = Box(r, 0.2f, 0.28f, 0.6f, 0.64f, 0.2f))
            {
                Cel(g, r, jar, C(214, 238, 248));
                var state = g.Save(); g.SetClip(jar, CombineMode.Intersect);
                using (var liquid = Box(r, 0.2f, 0.48f, 0.6f, 0.5f, 0.02f)) Cel(g, r, liquid, c, false);
                g.Restore(state);
                using (var pen = new Pen(Ink(c), Lw(r))) g.DrawPath(pen, jar);
            }
            using (var b = new SolidBrush(Color.FromArgb(190, 255, 255, 255))) g.FillEllipse(b, R(r, 0.28f, 0.34f, 0.1f, 0.24f));
        }
        static void Quiver(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, 18);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.4f + i * 0.1f;
                Line(g, r, C(214, 190, 150), 0.03f, x, 0.1f, x, 0.5f);
                using (var f = Poly(r, x, 0.04f, x + 0.05f, 0.12f, x, 0.18f, x - 0.05f, 0.12f)) Cel(g, r, f, C(240, 90, 80), false);
            }
            using (var body = Box(r, 0.32f, 0.3f, 0.36f, 0.62f, 0.1f)) Cel(g, r, body, c);
            using (var band = Box(r, 0.3f, 0.36f, 0.4f, 0.08f, 0.02f)) Cel(g, r, band, C(206, 160, 70), false);
            using (var band = Box(r, 0.3f, 0.76f, 0.4f, 0.08f, 0.02f)) Cel(g, r, band, C(206, 160, 70), false);
            g.Restore(s);
        }
        static void Purse(Graphics g, RectangleF r, Color c)
        {
            using (var bag = new GraphicsPath())
            {
                bag.AddBezier(P(r, 0.36f, 0.3f), P(r, 0.06f, 0.42f), P(r, 0.06f, 0.92f), P(r, 0.5f, 0.92f));
                bag.AddBezier(P(r, 0.5f, 0.92f), P(r, 0.94f, 0.92f), P(r, 0.94f, 0.42f), P(r, 0.64f, 0.3f));
                bag.CloseFigure();
                Cel(g, r, bag, c);
            }
            using (var neck = Poly(r, 0.34f, 0.14f, 0.66f, 0.14f, 0.62f, 0.32f, 0.38f, 0.32f)) Cel(g, r, neck, Theme.Light(c, 0.1f), false);
            Line(g, r, C(250, 210, 80), 0.035f, 0.34f, 0.3f, 0.66f, 0.3f);
            Theme.Gem(g, R(r, 0.36f, 0.42f, 0.28f, 0.42f), C(240, 178, 30), 0);
        }
        static void Note(Graphics g, RectangleF r, Color c)
        {
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(R(r, 0.16f, 0.62f, 0.36f, 0.28f));
                Cel(g, r, p, c);
            }
            Line(g, r, c, 0.07f, 0.48f, 0.74f, 0.48f, 0.12f);
            using (var flag = new GraphicsPath())
            {
                flag.AddBezier(P(r, 0.48f, 0.1f), P(r, 0.62f, 0.2f), P(r, 0.86f, 0.24f), P(r, 0.78f, 0.52f));
                flag.AddBezier(P(r, 0.78f, 0.52f), P(r, 0.74f, 0.36f), P(r, 0.62f, 0.32f), P(r, 0.5f, 0.3f));
                flag.CloseFigure();
                Cel(g, r, flag, c, false);
            }
        }
        static void Cyclone(Graphics g, RectangleF r, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                float y = 0.16f + i * 0.18f, w = 0.7f - i * 0.12f;
                using (var e = new GraphicsPath())
                {
                    e.AddArc(R(r, 0.5f - w / 2 + (i % 2 == 0 ? 0.04f : -0.04f), y, w, 0.16f), 160, 300);
                    using (var ink = new Pen(Ink(c), r.Width * 0.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(ink, e);
                    using (var pen = new Pen(i % 2 == 0 ? c : Theme.Light(c, 0.4f), r.Width * 0.055f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, e);
                }
            }
            Note(g, R(r, 0.56f, 0.0f, 0.42f, 0.42f), C(255, 214, 80));
        }
        static void SunMoon(Graphics g, RectangleF r, Color c)
        {
            Theme.Rays(g, P(r, 0.5f, 0.5f), r.Width * 0.55f, 10, Color.FromArgb(140, 255, 236, 160));
            using (var sun = new GraphicsPath()) { Pie(sun, R(r, 0.16f, 0.16f, 0.68f, 0.68f), 90, 180); Cel(g, r, sun, c, false); }
            using (var moon = new GraphicsPath()) { Pie(moon, R(r, 0.16f, 0.16f, 0.68f, 0.68f), 270, 180); Cel(g, r, moon, C(90, 100, 180), false); }
            using (var b = new SolidBrush(C(250, 246, 210))) g.FillEllipse(b, R(r, 0.58f, 0.32f, 0.12f, 0.12f));
            Theme.Sparkle(g, P(r, 0.7f, 0.66f), r.Width * 0.05f, Color.White);
            using (var pen = new Pen(InkLine, Lw(r))) g.DrawEllipse(pen, R(r, 0.16f, 0.16f, 0.68f, 0.68f));
        }
        static void Bottle(Graphics g, RectangleF r, Color c, bool soup)
        {
            using (var cork = Box(r, 0.4f, 0.06f, 0.2f, 0.12f, 0.03f)) Cel(g, r, cork, C(196, 140, 84), false);
            using (var body = new GraphicsPath())
            {
                body.AddLine(P(r, 0.42f, 0.16f), P(r, 0.58f, 0.16f));
                body.AddBezier(P(r, 0.58f, 0.16f), P(r, 0.58f, 0.3f), P(r, 0.82f, 0.34f), P(r, 0.82f, 0.62f));
                body.AddBezier(P(r, 0.82f, 0.62f), P(r, 0.82f, 0.94f), P(r, 0.18f, 0.94f), P(r, 0.18f, 0.62f));
                body.AddBezier(P(r, 0.18f, 0.62f), P(r, 0.18f, 0.34f), P(r, 0.42f, 0.3f), P(r, 0.42f, 0.16f));
                body.CloseFigure();
                using (var b = new SolidBrush(Color.FromArgb(200, c))) g.FillPath(b, body);
                var state = g.Save(); g.SetClip(body, CombineMode.Intersect);
                using (var liquid = Oval(r, 0.0f, 0.56f, 1f, 0.6f)) Cel(g, r, liquid, soup ? C(236, 150, 60) : Theme.Light(c, 0.2f), false);
                g.Restore(state);
                using (var pen = new Pen(Ink(c), Lw(r))) g.DrawPath(pen, body);
            }
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, R(r, 0.27f, 0.42f, 0.09f, 0.22f));
        }
        static void HeartPiece(Graphics g, RectangleF r, Color c)
        {
            var heart = R(r, 0.08f, 0.1f, 0.84f, 0.8f);
            using (var whole = Theme.HeartPath(heart))
            {
                using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillPath(b, whole);
                using (var pen = new Pen(Color.FromArgb(150, Ink(c)), Lw(r) * 0.7f) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, whole);
                var state = g.Save();
                var big = Theme.Inflate(heart, heart.Width, heart.Height);
                using (var pie = new GraphicsPath()) { pie.AddPie(big.X, big.Y, big.Width, big.Height, 180, 90); g.SetClip(pie, CombineMode.Intersect); }
                Cel(g, r, whole, c);
                g.Restore(state);
            }
            Spark(g, r, 0.26f, 0.2f, 0.1f);
        }
        static void Scroll(Graphics g, RectangleF r, Color paper, bool golden)
        {
            using (var sheet = Box(r, 0.16f, 0.18f, 0.68f, 0.64f, 0.04f)) Cel(g, r, sheet, paper, false);
            using (var top = Box(r, 0.1f, 0.1f, 0.8f, 0.14f, 0.07f)) Cel(g, r, top, Theme.Dark(paper, 0.12f), false);
            using (var bottom = Box(r, 0.1f, 0.76f, 0.8f, 0.14f, 0.07f)) Cel(g, r, bottom, Theme.Dark(paper, 0.12f), false);
            if (golden)
            {
                Glow(g, P(r, 0.5f, 0.5f), r.Width * 0.24f, Color.FromArgb(160, 255, 236, 150));
                using (var t = Poly(r, 0.5f, 0.32f, 0.64f, 0.62f, 0.36f, 0.62f)) Cel(g, r, t, C(250, 198, 58), false);
            }
            else
            {
                using (var pen = new Pen(C(150, 120, 90), Math.Max(1, r.Width * 0.02f)) { DashStyle = DashStyle.Dash }) g.DrawBezier(pen, P(r, 0.24f, 0.66f), P(r, 0.34f, 0.4f), P(r, 0.52f, 0.62f), P(r, 0.6f, 0.4f));
                Line(g, r, C(220, 50, 50), 0.035f, 0.58f, 0.32f, 0.72f, 0.46f); Line(g, r, C(220, 50, 50), 0.035f, 0.72f, 0.32f, 0.58f, 0.46f);
            }
        }
        static void Statue(Graphics g, RectangleF r, Color c)
        {
            using (var b = Box(r, 0.26f, 0.78f, 0.48f, 0.12f, 0.03f)) Cel(g, r, b, C(150, 140, 130), false);
            using (var body = Poly(r, 0.34f, 0.46f, 0.66f, 0.46f, 0.72f, 0.8f, 0.28f, 0.8f)) Cel(g, r, body, c);
            using (var head = Oval(r, 0.32f, 0.16f, 0.36f, 0.34f)) Cel(g, r, head, c);
            using (var hood = Poly(r, 0.5f, 0.02f, 0.62f, 0.2f, 0.38f, 0.2f)) Cel(g, r, hood, Theme.Dark(c, 0.1f), false);
            using (var b = new SolidBrush(InkLine)) { g.FillEllipse(b, R(r, 0.41f, 0.3f, 0.05f, 0.05f)); g.FillEllipse(b, R(r, 0.54f, 0.3f, 0.05f, 0.05f)); }
            Spark(g, r, 0.8f, 0.24f, 0.1f);
        }
        static void Figurine(Graphics g, RectangleF r, Color c)
        {
            using (var b = Oval(r, 0.2f, 0.76f, 0.6f, 0.16f)) Cel(g, r, b, C(200, 170, 120), false);
            using (var body = Poly(r, 0.38f, 0.48f, 0.62f, 0.48f, 0.68f, 0.82f, 0.32f, 0.82f)) Cel(g, r, body, c);
            using (var head = Oval(r, 0.34f, 0.2f, 0.32f, 0.3f)) Cel(g, r, head, C(250, 214, 170));
            using (var cap = Poly(r, 0.32f, 0.3f, 0.5f, 0.12f, 0.8f, 0.36f, 0.66f, 0.3f)) Cel(g, r, cap, c, false);
            using (var b = new SolidBrush(InkLine)) { g.FillEllipse(b, R(r, 0.42f, 0.32f, 0.04f, 0.05f)); g.FillEllipse(b, R(r, 0.54f, 0.32f, 0.04f, 0.05f)); }
        }
        static void SeaChart(Graphics g, RectangleF r, Color paper)
        {
            var area = R(r, 0.12f, 0.12f, 0.76f, 0.76f);
            using (var sheet = Box(r, 0.12f, 0.12f, 0.76f, 0.76f, 0.05f)) Cel(g, r, sheet, paper, false);
            using (var pen = new Pen(Color.FromArgb(150, 120, 90, 60), Math.Max(1, r.Width * 0.012f)))
                for (int i = 1; i < 4; i++)
                {
                    g.DrawLine(pen, area.X + area.Width * i / 4, area.Y + 2, area.X + area.Width * i / 4, area.Bottom - 2);
                    g.DrawLine(pen, area.X + 2, area.Y + area.Height * i / 4, area.Right - 2, area.Y + area.Height * i / 4);
                }
            using (var b = new SolidBrush(Color.FromArgb(150, 80, 170, 220)))
                foreach (var cell in new[] { 0, 1, 4, 5, 6, 9, 10, 15 }) g.FillRectangle(b, area.X + area.Width * (cell % 4) / 4 + 1, area.Y + area.Height * (cell / 4) / 4 + 1, area.Width / 4 - 2, area.Height / 4 - 2);
            using (var island = Oval(r, 0.6f, 0.4f, 0.14f, 0.1f)) Cel(g, r, island, C(110, 190, 90), false);
            Compass(g, R(r, 0.08f, 0.56f, 0.36f, 0.36f), C(250, 198, 58));
        }
        static void Compass(Graphics g, RectangleF r, Color c)
        {
            using (var disc = Oval(r, 0.1f, 0.1f, 0.8f, 0.8f)) Cel(g, r, disc, C(250, 244, 226), false);
            using (var n = Poly(r, 0.5f, 0.14f, 0.6f, 0.5f, 0.4f, 0.5f)) Cel(g, r, n, C(220, 56, 52), false);
            using (var s = Poly(r, 0.5f, 0.86f, 0.6f, 0.5f, 0.4f, 0.5f)) Cel(g, r, s, C(90, 110, 140), false);
            using (var hub = Oval(r, 0.44f, 0.44f, 0.12f, 0.12f)) Cel(g, r, hub, c, false);
        }
        static void Chest(Graphics g, RectangleF r, Color c)
        {
            using (var lid = new GraphicsPath())
            {
                lid.AddArc(R(r, 0.12f, 0.2f, 0.76f, 0.36f), 180, 180);
                lid.AddLine(P(r, 0.88f, 0.38f), P(r, 0.88f, 0.46f)); lid.AddLine(P(r, 0.12f, 0.46f), P(r, 0.12f, 0.38f));
                lid.CloseFigure();
                Cel(g, r, lid, c);
            }
            using (var body = Box(r, 0.12f, 0.46f, 0.76f, 0.38f, 0.03f)) Cel(g, r, body, Theme.Dark(c, 0.08f), false);
            foreach (var x in new[] { 0.22f, 0.7f }) using (var band = Box(r, x, 0.22f, 0.08f, 0.62f, 0.02f)) Cel(g, r, band, C(240, 196, 70), false);
            using (var lockp = Box(r, 0.43f, 0.42f, 0.14f, 0.16f, 0.03f)) Cel(g, r, lockp, C(250, 214, 90), false);
            foreach (var b in new[] { new[] { 0.84f, 0.16f, 0.08f }, new[] { 0.74f, 0.06f, 0.05f }, new[] { 0.92f, 0.04f, 0.04f } })
                using (var bubble = Oval(r, b[0], b[1], b[2], b[2])) { using (var br = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillPath(br, bubble); using (var pen = new Pen(C(40, 130, 190), Math.Max(1, r.Width * 0.018f))) g.DrawPath(pen, bubble); }
        }
        static void GhostShip(Graphics g, RectangleF r, Color c)
        {
            Glow(g, P(r, 0.5f, 0.5f), r.Width * 0.5f, Color.FromArgb(140, 190, 140, 255));
            using (var moon = Oval(r, 0.62f, 0.04f, 0.24f, 0.24f)) Cel(g, r, moon, C(250, 246, 210), false);
            using (var hull = Poly(r, 0.08f, 0.62f, 0.92f, 0.62f, 0.78f, 0.84f, 0.2f, 0.84f)) Cel(g, r, hull, Theme.Dark(c, 0.35f), false);
            Line(g, r, Theme.Dark(c, 0.4f), 0.04f, 0.46f, 0.12f, 0.46f, 0.64f);
            using (var sail = Poly(r, 0.24f, 0.2f, 0.44f, 0.18f, 0.44f, 0.56f, 0.3f, 0.52f, 0.26f, 0.56f, 0.2f, 0.46f)) Cel(g, r, sail, Theme.Light(c, 0.35f), false);
            using (var sail = Poly(r, 0.5f, 0.22f, 0.72f, 0.26f, 0.76f, 0.54f, 0.66f, 0.5f, 0.6f, 0.56f, 0.5f, 0.54f)) Cel(g, r, sail, Theme.Light(c, 0.35f), false);
            for (int i = 0; i < 3; i++) using (var b = new SolidBrush(C(255, 236, 140))) g.FillEllipse(b, R(r, 0.3f + i * 0.14f, 0.68f, 0.06f, 0.06f));
        }
        static void OctoEye(Graphics g, RectangleF r, Color c)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = 0.18f + i * 0.16f;
                CurveLine(g, r, Theme.Dark(c, 0.1f), 0.07f, x, 0.66f, x + (i % 2 == 0 ? -0.06f : 0.06f), 0.8f, x, 0.94f);
            }
            using (var body = Oval(r, 0.1f, 0.08f, 0.8f, 0.68f)) Cel(g, r, body, c);
            using (var eye = Oval(r, 0.26f, 0.22f, 0.48f, 0.4f)) Cel(g, r, eye, C(255, 250, 230), false);
            using (var iris = Oval(r, 0.4f, 0.28f, 0.22f, 0.28f)) Cel(g, r, iris, C(240, 170, 40), false);
            using (var b = new SolidBrush(InkLine)) g.FillEllipse(b, R(r, 0.46f, 0.34f, 0.1f, 0.16f));
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, R(r, 0.45f, 0.33f, 0.05f, 0.05f));
        }
        static void CalmSea(Graphics g, RectangleF r, Color c)
        {
            using (var sun = Oval(r, 0.56f, 0.08f, 0.32f, 0.32f)) Cel(g, r, sun, C(255, 210, 80));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.48f + i * 0.16f;
                CurveLine(g, r, i == 1 ? Theme.Light(c, 0.3f) : c, 0.06f, 0.08f, y, 0.3f, y - 0.05f, 0.5f, y, 0.7f, y - 0.05f, 0.92f, y);
            }
            Spark(g, r, 0.22f, 0.24f, 0.1f);
        }
        static void Stairs(Graphics g, RectangleF r, Color c)
        {
            using (var hole = Oval(r, 0.06f, 0.1f, 0.88f, 0.82f)) Cel(g, r, hole, C(40, 34, 52), false);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.62f - i * 0.12f, x = 0.18f + i * 0.08f;
                using (var step = Box(r, x, y, 0.64f - i * 0.16f, 0.14f, 0.02f)) Cel(g, r, step, Theme.Mix(c, C(40, 34, 52), i * 0.18f), false);
            }
            Glow(g, P(r, 0.5f, 0.26f), r.Width * 0.14f, Color.FromArgb(150, 255, 120, 90));
        }
        static void Soup(Graphics g, RectangleF r, Color c)
        {
            for (int i = 0; i < 3; i++) CurveLine(g, r, Color.White, 0.03f, 0.34f + i * 0.16f, 0.36f, 0.3f + i * 0.16f, 0.24f, 0.36f + i * 0.16f, 0.12f);
            using (var bowl = new GraphicsPath())
            {
                bowl.AddArc(R(r, 0.08f, 0.2f, 0.84f, 0.66f), 0, 180);
                bowl.CloseFigure();
                Cel(g, r, bowl, C(80, 140, 200));
            }
            using (var soup = Oval(r, 0.1f, 0.46f, 0.8f, 0.16f)) Cel(g, r, soup, c, false);
            using (var b = Box(r, 0.36f, 0.84f, 0.28f, 0.08f, 0.03f)) Cel(g, r, b, C(60, 110, 170), false);
        }
        static void Magnifier(Graphics g, RectangleF r, Color c)
        {
            Line(g, r, C(150, 100, 56), 0.1f, 0.62f, 0.62f, 0.88f, 0.88f);
            using (var rim = new GraphicsPath(FillMode.Alternate))
            {
                rim.AddEllipse(R(r, 0.1f, 0.1f, 0.6f, 0.6f)); rim.AddEllipse(R(r, 0.18f, 0.18f, 0.44f, 0.44f));
                Cel(g, r, rim, C(232, 186, 64), false);
            }
            using (var lens = Oval(r, 0.18f, 0.18f, 0.44f, 0.44f)) using (var b = new SolidBrush(Color.FromArgb(170, c))) g.FillPath(b, lens);
            using (var b = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) g.FillEllipse(b, R(r, 0.26f, 0.24f, 0.12f, 0.08f));
            Theme.Sparkle(g, P(r, 0.4f, 0.42f), r.Width * 0.08f, C(255, 214, 80));
        }
        static void Target(Graphics g, RectangleF r, Color c)
        {
            for (int i = 0; i < 3; i++) using (var ring = Oval(r, 0.1f + i * 0.13f, 0.1f + i * 0.13f, 0.8f - i * 0.26f, 0.8f - i * 0.26f)) Cel(g, r, ring, i % 2 == 0 ? c : Color.White, false);
            using (var pen = new Pen(Ink(c), Lw(r)))
            {
                g.DrawLine(pen, P(r, 0.5f, 0.02f), P(r, 0.5f, 0.24f)); g.DrawLine(pen, P(r, 0.5f, 0.76f), P(r, 0.5f, 0.98f));
                g.DrawLine(pen, P(r, 0.02f, 0.5f), P(r, 0.24f, 0.5f)); g.DrawLine(pen, P(r, 0.76f, 0.5f), P(r, 0.98f, 0.5f));
            }
            using (var ball = Oval(r, 0.62f, 0.12f, 0.22f, 0.22f)) Cel(g, r, ball, C(60, 66, 80));
        }
        static void Letter(Graphics g, RectangleF r, Color c, bool heart)
        {
            var s = Turn(g, r, -8);
            using (var env = Box(r, 0.1f, 0.24f, 0.8f, 0.54f, 0.05f)) Cel(g, r, env, c);
            using (var pen = new Pen(Ink(c), Lw(r)) { LineJoin = LineJoin.Round }) g.DrawLines(pen, new[] { P(r, 0.12f, 0.27f), P(r, 0.5f, 0.56f), P(r, 0.88f, 0.27f) });
            if (heart) using (var h = Theme.HeartPath(R(r, 0.39f, 0.46f, 0.22f, 0.2f))) Cel(g, r, h, C(232, 64, 96), false);
            else using (var seal = Oval(r, 0.42f, 0.48f, 0.16f, 0.16f)) Cel(g, r, seal, C(206, 52, 44), false);
            g.Restore(s);
        }
        static void Spin(Graphics g, RectangleF r, Color c)
        {
            for (int i = 0; i < 2; i++)
            {
                using (var arc = new GraphicsPath())
                {
                    arc.AddArc(R(r, 0.04f + i * 0.12f, 0.04f + i * 0.12f, 0.92f - i * 0.24f, 0.92f - i * 0.24f), 200 + i * 150, 250);
                    using (var ink = new Pen(Ink(c), r.Width * (0.11f - i * 0.02f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(ink, arc);
                    using (var pen = new Pen(Theme.Light(c, 0.25f * i), r.Width * (0.065f - i * 0.015f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pen, arc);
                }
            }
            var box = R(r, 0.08f, 0.08f, 0.84f, 0.84f);
            var s = Turn(g, box, 135); Sword(g, box, C(226, 236, 246), C(120, 120, 140), C(132, 84, 46), false); g.Restore(s);
        }
        static void House(Graphics g, RectangleF r, Color c)
        {
            using (var walls = Box(r, 0.2f, 0.44f, 0.6f, 0.44f, 0.02f)) Cel(g, r, walls, C(240, 222, 180), false);
            using (var roof = Poly(r, 0.08f, 0.48f, 0.5f, 0.1f, 0.92f, 0.48f)) Cel(g, r, roof, c);
            using (var door = Box(r, 0.42f, 0.62f, 0.16f, 0.26f, 0.03f)) Cel(g, r, door, C(140, 90, 56), false);
            using (var win = Box(r, 0.26f, 0.54f, 0.12f, 0.12f, 0.02f)) Cel(g, r, win, C(130, 200, 240), false);
            using (var win = Box(r, 0.62f, 0.54f, 0.12f, 0.12f, 0.02f)) Cel(g, r, win, C(130, 200, 240), false);
        }
        static void Pendant(Graphics g, RectangleF r, Color c)
        {
            using (var chain = new GraphicsPath())
            {
                chain.AddBezier(P(r, 0.16f, 0.1f), P(r, 0.2f, 0.42f), P(r, 0.8f, 0.42f), P(r, 0.84f, 0.1f));
                using (var pen = new Pen(C(232, 186, 64), Math.Max(1, r.Width * 0.03f)) { DashStyle = DashStyle.Dot }) g.DrawPath(pen, chain);
            }
            using (var setting = Oval(r, 0.3f, 0.36f, 0.4f, 0.48f)) Cel(g, r, setting, C(232, 186, 64), false);
            using (var gem = new GraphicsPath())
            {
                gem.AddPolygon(new[] { P(r, 0.5f, 0.42f), P(r, 0.62f, 0.54f), P(r, 0.5f, 0.78f), P(r, 0.38f, 0.54f) });
                Cel(g, r, gem, c);
            }
            Spark(g, r, 0.74f, 0.4f, 0.1f);
        }
        static void Drop(Graphics g, RectangleF r, Color c)
        {
            using (var drop = new GraphicsPath())
            {
                drop.AddBezier(P(r, 0.5f, 0.06f), P(r, 0.66f, 0.3f), P(r, 0.84f, 0.48f), P(r, 0.84f, 0.64f));
                drop.AddBezier(P(r, 0.84f, 0.64f), P(r, 0.84f, 0.84f), P(r, 0.68f, 0.94f), P(r, 0.5f, 0.94f));
                drop.AddBezier(P(r, 0.5f, 0.94f), P(r, 0.32f, 0.94f), P(r, 0.16f, 0.84f), P(r, 0.16f, 0.64f));
                drop.AddBezier(P(r, 0.16f, 0.64f), P(r, 0.16f, 0.48f), P(r, 0.34f, 0.3f), P(r, 0.5f, 0.06f));
                Cel(g, r, drop, c);
            }
            using (var leaf = Curve(r, true, 0.56f, 0.7f, 0.72f, 0.56f, 0.8f, 0.7f, 0.66f, 0.8f)) Cel(g, r, leaf, C(92, 186, 72), false);
        }
        static void Key(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, -40);
            using (var bow = new GraphicsPath(FillMode.Alternate))
            {
                bow.AddEllipse(R(r, 0.32f, 0.04f, 0.36f, 0.36f)); bow.AddEllipse(R(r, 0.42f, 0.14f, 0.16f, 0.16f));
                Cel(g, r, bow, c);
            }
            using (var shaft = Box(r, 0.45f, 0.38f, 0.1f, 0.54f, 0.03f)) Cel(g, r, shaft, c, false);
            using (var bit = Box(r, 0.55f, 0.72f, 0.14f, 0.07f, 0.02f)) Cel(g, r, bit, c, false);
            using (var bit = Box(r, 0.55f, 0.84f, 0.1f, 0.07f, 0.02f)) Cel(g, r, bit, c, false);
            g.Restore(s);
            Spark(g, r, 0.8f, 0.2f, 0.12f);
        }
        static void Lighthouse(Graphics g, RectangleF r, Color c)
        {
            Theme.Rays(g, P(r, 0.5f, 0.24f), r.Width * 0.5f, 165, Color.FromArgb(150, 255, 240, 170));
            using (var tower = Poly(r, 0.4f, 0.32f, 0.6f, 0.32f, 0.68f, 0.86f, 0.32f, 0.86f))
            {
                Cel(g, r, tower, Color.White, false);
                var state = g.Save(); g.SetClip(tower, CombineMode.Intersect);
                using (var b = new SolidBrush(c)) { g.FillRectangle(b, R(r, 0.2f, 0.42f, 0.6f, 0.12f)); g.FillRectangle(b, R(r, 0.2f, 0.66f, 0.6f, 0.12f)); }
                g.Restore(state);
                using (var pen = new Pen(Ink(c), Lw(r))) g.DrawPath(pen, tower);
            }
            using (var lamp = Box(r, 0.4f, 0.18f, 0.2f, 0.14f, 0.03f)) Cel(g, r, lamp, C(255, 226, 120), false);
            using (var roof = Poly(r, 0.36f, 0.19f, 0.5f, 0.06f, 0.64f, 0.19f)) Cel(g, r, roof, c, false);
            using (var rock = Oval(r, 0.18f, 0.82f, 0.64f, 0.14f)) Cel(g, r, rock, C(120, 110, 100), false);
        }
        static void Wings(Graphics g, RectangleF r, Color c)
        {
            foreach (int side in new[] { -1, 1 })
            {
                using (var wing = new GraphicsPath())
                {
                    float x0 = 0.5f + side * 0.04f;
                    wing.AddBezier(P(r, x0, 0.4f), P(r, 0.5f + side * 0.2f, 0.1f), P(r, 0.5f + side * 0.46f, 0.12f), P(r, 0.5f + side * 0.46f, 0.3f));
                    wing.AddBezier(P(r, 0.5f + side * 0.46f, 0.3f), P(r, 0.5f + side * 0.4f, 0.4f), P(r, 0.5f + side * 0.42f, 0.5f), P(r, 0.5f + side * 0.34f, 0.56f));
                    wing.AddBezier(P(r, 0.5f + side * 0.34f, 0.56f), P(r, 0.5f + side * 0.3f, 0.66f), P(r, 0.5f + side * 0.2f, 0.7f), P(r, x0, 0.62f));
                    wing.CloseFigure();
                    Cel(g, r, wing, c);
                }
                using (var pen = new Pen(Color.FromArgb(150, 120, 150, 180), Math.Max(1, r.Width * 0.015f)))
                    for (int i = 0; i < 3; i++) g.DrawLine(pen, P(r, 0.5f + side * (0.1f + i * 0.1f), 0.52f - i * 0.04f), P(r, 0.5f + side * (0.18f + i * 0.1f), 0.3f - i * 0.03f));
            }
            using (var body = Oval(r, 0.44f, 0.36f, 0.12f, 0.3f)) Cel(g, r, body, C(240, 200, 120), false);
        }
        static void Cannon(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, -24);
            using (var barrel = Box(r, 0.16f, 0.32f, 0.64f, 0.26f, 0.12f)) Cel(g, r, barrel, c);
            using (var mouth = Oval(r, 0.74f, 0.3f, 0.12f, 0.3f)) Cel(g, r, mouth, Theme.Dark(c, 0.4f), false);
            g.Restore(s);
            using (var wheel = Oval(r, 0.22f, 0.54f, 0.32f, 0.32f)) Cel(g, r, wheel, C(150, 100, 56));
            using (var hub = Oval(r, 0.33f, 0.65f, 0.1f, 0.1f)) Cel(g, r, hub, C(232, 186, 64), false);
            Glow(g, P(r, 0.86f, 0.12f), r.Width * 0.14f, Color.FromArgb(200, 255, 220, 120));
            using (var ball = Oval(r, 0.78f, 0.04f, 0.14f, 0.14f)) Cel(g, r, ball, C(60, 66, 80), false);
        }
        static void Trade(Graphics g, RectangleF r, Color c)
        {
            using (var arc = new GraphicsPath())
            {
                arc.AddArc(R(r, 0.14f, 0.14f, 0.72f, 0.72f), 200, 140);
                using (var ink = new Pen(Ink(C(80, 170, 230)), r.Width * 0.11f)) g.DrawPath(ink, arc);
                using (var pen = new Pen(C(80, 170, 230), r.Width * 0.065f)) g.DrawPath(pen, arc);
            }
            using (var head = Poly(r, 0.76f, 0.12f, 0.92f, 0.36f, 0.68f, 0.34f)) Cel(g, r, head, C(80, 170, 230), false);
            using (var arc = new GraphicsPath())
            {
                arc.AddArc(R(r, 0.14f, 0.14f, 0.72f, 0.72f), 20, 140);
                using (var ink = new Pen(Ink(C(240, 120, 70)), r.Width * 0.11f)) g.DrawPath(ink, arc);
                using (var pen = new Pen(C(240, 120, 70), r.Width * 0.065f)) g.DrawPath(pen, arc);
            }
            using (var head = Poly(r, 0.24f, 0.88f, 0.08f, 0.64f, 0.32f, 0.66f)) Cel(g, r, head, C(240, 120, 70), false);
            using (var coin = Oval(r, 0.34f, 0.34f, 0.32f, 0.32f)) Cel(g, r, coin, c);
            Theme.Sparkle(g, P(r, 0.5f, 0.5f), r.Width * 0.07f, Color.White);
        }
        static void Bunting(Graphics g, RectangleF r, Color c)
        {
            using (var rope = new GraphicsPath())
            {
                rope.AddBezier(P(r, 0.04f, 0.2f), P(r, 0.3f, 0.42f), P(r, 0.7f, 0.42f), P(r, 0.96f, 0.2f));
                using (var pen = new Pen(Ink(c), Math.Max(1, r.Width * 0.03f))) g.DrawPath(pen, rope);
            }
            Color[] colors = { c, C(250, 198, 58), C(64, 168, 236), C(92, 186, 72), C(196, 120, 220) };
            for (int i = 0; i < 5; i++)
            {
                float x = 0.12f + i * 0.18f, t = (x - 0.04f) / 0.92f, y = 0.2f + 0.66f * t * (1 - t) * 0.92f + 0.02f;
                using (var flag = Poly(r, x - 0.08f, y, x + 0.08f, y, x, y + 0.32f)) Cel(g, r, flag, colors[i], false);
            }
            Theme.Sparkle(g, P(r, 0.24f, 0.8f), r.Width * 0.06f, Color.White); Theme.Sparkle(g, P(r, 0.76f, 0.84f), r.Width * 0.05f, Color.White);
        }
        static void Feather(Graphics g, RectangleF r, Color c)
        {
            var s = Turn(g, r, 35);
            using (var vane = new GraphicsPath())
            {
                vane.AddBezier(P(r, 0.5f, 0.04f), P(r, 0.8f, 0.2f), P(r, 0.76f, 0.6f), P(r, 0.52f, 0.8f));
                vane.AddBezier(P(r, 0.52f, 0.8f), P(r, 0.3f, 0.62f), P(r, 0.22f, 0.24f), P(r, 0.5f, 0.04f));
                Cel(g, r, vane, c);
            }
            Line(g, r, Theme.Dark(c, 0.35f), 0.025f, 0.5f, 0.12f, 0.52f, 0.96f);
            using (var pen = new Pen(Theme.Dark(c, 0.3f), Math.Max(1, r.Width * 0.014f)))
                for (int i = 0; i < 4; i++) { float y = 0.24f + i * 0.12f; g.DrawLine(pen, P(r, 0.51f, y + 0.05f), P(r, 0.34f, y)); g.DrawLine(pen, P(r, 0.51f, y + 0.05f), P(r, 0.68f, y)); }
            g.Restore(s);
            Spark(g, r, 0.8f, 0.2f, 0.12f); Spark(g, r, 0.2f, 0.74f, 0.08f);
        }
    }
}
