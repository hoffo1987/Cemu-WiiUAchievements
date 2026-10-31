using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace WiiUAchievements
{
    // Uses the existing theme, sea artwork, rounded cards and input/accessibility
    // system. This is a catalog, not a promise of support for unvalidated games.
    public sealed partial class MainForm
    {
        bool libraryOpen;
        float libraryScroll;
        RectangleF libraryGridRect, libraryArtRect;

        void ToggleLibrary()
        {
            if (libraryOpen) { CloseLibrary(); return; }
            libraryOpen = true; libraryScroll = 0;
            menu = null; menuOpen = menuTarget = 0; searchEditing = false; PlaceSearch();
            introStart = Motion.Reduced ? -10 : (float)clock.Elapsed.TotalSeconds;
            drawer = drawerTarget = 0; dialog = dialogTarget = 0; detail = null;
            focused = hovered = pressed = null;
            Invalidate(); Kick();
        }
        void CloseLibrary()
        {
            libraryOpen = false; focused = hovered = pressed = null;
            introStart = Motion.Reduced ? -10 : (float)clock.Elapsed.TotalSeconds;
            Invalidate(); Kick();
        }
        void ArrangeLibrary(float width, float height)
        {
            libraryGridRect = new RectangleF(Pad - 8, TopH + 104, width - Pad * 2 + 16, Math.Max(1, height - BottomH - TopH - 116));
            float inner = libraryGridRect.Width - 16;
            int count = GameCatalog.Games.Count;
            int cols = Math.Max(1, Math.Min(count, (int)((inner + Gap) / (400 + Gap))));
            float cardWidth = Math.Min(560, (inner - Gap * (cols - 1)) / cols);
            int rows = (count + cols - 1) / cols;
            // One row grows to use the height; several rows keep a compact card.
            float cardHeight = rows == 1 ? Math.Max(300, Math.Min(430, libraryGridRect.Height - 16)) : 320;
            float left = libraryGridRect.X + 8 + (inner - (cols * cardWidth + (cols - 1) * Gap)) / 2;
            // A single row sits a little lower in tall windows instead of leaving a void below it.
            float lift = rows == 1 ? Math.Max(0, (libraryGridRect.Height - 16 - cardHeight) * 0.3f) : 0;
            libraryScroll = Math.Max(0, Math.Min(libraryScroll, Math.Max(0, rows * (cardHeight + Gap) + 16 - libraryGridRect.Height)));
            for (int i = 0; i < count; i++)
            {
                var game = GameCatalog.Games[i];
                var bounds = new RectangleF(left + i % cols * (cardWidth + Gap),
                    libraryGridRect.Y + 8 + lift + i / cols * (cardHeight + Gap) - libraryScroll, cardWidth, cardHeight);
                var tile = Put("game:" + game.Id, Kind.GameTile, bounds, game.Title,
                    () => { try { SelectGame(game); } catch (Exception ex) { Log("Gioco non aperto: " + ex.Message); } },
                    game.Available && (game == selectedGame || CanConnect), 0);
                tile.Checked = game == selectedGame;
                if (game == selectedGame) libraryArtRect = new RectangleF(bounds.X, bounds.Y - 4, bounds.Width, Math.Max(140, cardHeight * 0.48f) + 6);
                tile.Help = game.Description + (game.Available ? " " + String.Join("; ", game.Profiles.Select(p => p.Label + ", Cemu " + p.CemuVersion)) : " Non disponibile.");
                if (game.Available && game != selectedGame && !CanConnect) tile.Help += " Scollega la sessione prima di cambiare gioco.";
            }
            AddButton("library-back", Kind.Button, "Obiettivi", "B", Pad, height - BottomH + 16, CloseLibrary, true, 0);
        }
        void DrawLibrary(Graphics g, RectangleF clip, float time)
        {
            var style = Style;
            Theme.GridIcon(g, new RectangleF(Pad + 4, TopH + 27, 24, 24), style.AccentDark);
            Theme.Text(g, "Giochi", Theme.Font(30, Weight.Heavy), Theme.Ink, new RectangleF(Pad + 38, TopH + 16, 300, 44));
            Theme.Text(g, "Scegli un gioco per aprire i suoi obiettivi. Ogni gioco ha il proprio stile e i propri progressi.", Theme.Font(15, Weight.Bold), style.Muted,
                new RectangleF(Pad + 4, TopH + 62, W - Pad * 2 - 230, 24));
            int available = GameCatalog.Games.Count(x => x.Available);
            string summary = available == 1 ? "1 gioco disponibile" : available + " giochi disponibili";
            var sf = Theme.Font(13, Weight.Heavy);
            float sw = Measure(summary, sf).Width + 26;
            var chip = new RectangleF(W - Pad - sw, TopH + 26, sw, 28);
            Theme.Fill(g, chip, 14, Color.White, style.Soft);
            Theme.Stroke(g, chip, 14, Theme.Mix(style.Accent, Color.White, 0.5f), 1);
            Theme.Text(g, summary, sf, style.AccentDark, chip, StringAlignment.Center, StringAlignment.Center, false);

            var state = g.Save();
            g.SetClip(Theme.Inflate(libraryGridRect, 0, 6), CombineMode.Intersect);
            int index = 0;
            foreach (var game in GameCatalog.Games)
            {
                var widget = widgets["game:" + game.Id];
                if (!libraryGridRect.IntersectsWith(widget.Bounds) || !clip.IntersectsWith(Theme.Inflate(widget.Bounds, 24, 24))) { index++; continue; }
                float appear = Motion.Reduced ? 1 : Theme.EaseOut((time - introStart - index++ * 0.06f) / 0.32f);
                if (appear <= 0) continue;
                var cardState = g.Save();
                if (appear < 1) g.TranslateTransform(0, 16 * (1 - appear));
                DrawGameCard(g, game, widget, time);
                g.Restore(cardState);
            }
            g.Restore(state);
        }
        void DrawGameCard(Graphics g, GameDefinition game, Widget widget, float time)
        {
            var style = Style;
            var r = widget.Bounds;
            float hv = widget.Enabled ? widget.Hover : 0;
            if (!Motion.Reduced) r.Offset(0, -3 * hv + 1.5f * widget.Press);
            bool open = game.Available;
            Theme.Shadow(g, r, 22, 8 + 4 * hv, open ? 44 + (int)(24 * hv) : 26, 3 + hv);
            if (open) Theme.Fill(g, r, 22, Color.White, Color.FromArgb(239, 244, 247));
            else Theme.Fill(g, r, 22, Color.FromArgb(200, 248, 250, 252), Color.FromArgb(180, 232, 238, 243));
            float artH = Math.Max(140, r.Height * 0.48f);
            var art = new RectangleF(r.X, r.Y, r.Width, artH);
            var inner = g.Save();
            using (var clip = Theme.Round(r, 22)) g.SetClip(clip, CombineMode.Intersect);
            if (game.Id == GameCatalog.WindWakerId)
            {
                bool drawn = false;
                var playing = game == selectedGame ? BannerVideo : null;
                try { drawn = playing != null && playing.Draw(g, art, 0); } catch (Exception) { drawn = false; }
                if (!drawn) banner.Draw(g, art, 0, ambientTime, scale);
                using (var b = new LinearGradientBrush(new RectangleF(art.X, art.Bottom - 50, art.Width, 51), Color.FromArgb(0, 3, 38, 65), Color.FromArgb(120, 3, 38, 65), 90f))
                    g.FillRectangle(b, art.X, art.Bottom - 50, art.Width, 50);
                var gameStyle = GameStyle.For(game.Id);
                float badge = Math.Min(artH - 24, 150);
                if (!gameStyle.DrawArt(g, "badge.png", new RectangleF(r.X + (r.Width - badge) / 2, r.Y + (artH - badge) / 2, badge, badge), 1))
                    Theme.ShadowText(g, game.Title, Theme.Font(24, Weight.Heavy), Color.White,
                        new RectangleF(r.X + 22, r.Y + artH / 2 - 18, r.Width - 44, 36), StringAlignment.Center, StringAlignment.Center, false, 100);
            }
            else
            {
                Theme.Fill(g, art, 0, Color.FromArgb(228, 239, 246), Color.FromArgb(209, 224, 234));
                using (var pen = new Pen(Color.FromArgb(40, 0, 50, 80), 1.3f))
                    for (float d = -artH; d < r.Width; d += 10) g.DrawLine(pen, r.X + d, art.Bottom, r.X + d + artH, art.Y);
                var c = Theme.Center(art);
                for (int i = 0; i < 4; i++)
                {
                    var box = new RectangleF(c.X - 46 + i % 2 * 52, c.Y - 46 + i / 2 * 52, 40, 40);
                    Theme.Fill(g, box, 10, Color.FromArgb(220, 255, 255, 255), Color.FromArgb(200, 236, 243, 248));
                    Theme.StrokeDashed(g, box, 10, Color.FromArgb(150, 120, 150, 172), 1.4f);
                }
            }
            g.Restore(inner);
            if (open) Theme.Stroke(g, r, 22, widget.Checked ? style.Accent : Theme.Line, widget.Checked ? 2 : 1);
            else Theme.StrokeDashed(g, Theme.Inflate(r, -0.5f, -0.5f), 21.5f, Color.FromArgb(160, 130, 146, 158), 1.5f);

            string badgeText = open ? (widget.Checked ? "Selezionato" : "Disponibile") : "In arrivo";
            var badgeFont = Theme.Font(12, Weight.Heavy);
            float badgeWidth = Measure(badgeText, badgeFont).Width + (widget.Checked ? 40 : 26);
            var badgeRect = new RectangleF(r.X + 16, r.Y + 14, badgeWidth, 26);
            Theme.Shadow(g, badgeRect, 13, 3, 30, 1);
            Theme.Fill(g, badgeRect, 13, widget.Checked ? style.PrimaryTop : Color.White, widget.Checked ? style.PrimaryBottom : Color.FromArgb(238, 246, 250));
            float bx = badgeRect.X + 13;
            if (widget.Checked) { Theme.Check(g, new RectangleF(bx - 3, badgeRect.Y + 4, 18, 18), Color.White, 2.4f); bx += 15; }
            Theme.Text(g, badgeText, badgeFont, widget.Checked ? Color.White : open ? style.AccentDark : Theme.LockedInk,
                new RectangleF(bx, badgeRect.Y, badgeRect.Right - bx - 8, badgeRect.Height), StringAlignment.Near, StringAlignment.Center, false);

            float x = r.X + 24, w = r.Width - 48, y = art.Bottom + 16;
            Theme.Text(g, open ? game.Title : "Altri giochi", Theme.Font(22, Weight.Heavy), open ? Theme.Ink : Theme.LockedInk, new RectangleF(x, y, w, 30));
            y += 32;
            string configuration = open ? String.Join("  ·  ", game.Profiles.Select(p => p.Label + " · Cemu " + p.CemuVersion)) : "Nuovi titoli in futuro";
            Theme.Text(g, configuration, Theme.Font(13, Weight.Bold), open ? style.AccentDark : Theme.LockedInk, new RectangleF(x, y, w, 20));
            y += 26;
            float bottom = r.Bottom - 20;
            if (open && game == selectedGame)
            {
                // Progress of the selected game, as on the banner ring.
                float barY = bottom - 44;
                Theme.Text(g, game.Description, Theme.Font(13, Weight.Regular), style.Muted, new RectangleF(x, y, w, Math.Max(18, barY - y - 6)), StringAlignment.Near, StringAlignment.Near, true);
                var bar = new RectangleF(x, barY + 4, w * 0.55f, 9);
                Theme.Fill(g, bar, 4.5f, Color.FromArgb(206, 216, 224), Color.FromArgb(226, 233, 238));
                float ratio = ActiveCount == 0 ? 0 : UnlockedCount / (float)ActiveCount;
                if (ratio > 0) Theme.Fill(g, new RectangleF(bar.X, bar.Y, Math.Max(9, bar.Width * ratio), bar.Height), 4.5f, style.Glow, style.AccentDark);
                int total = rules.Where(a => !a.IsPlanned).Sum(a => a.Points);
                Theme.Text(g, UnlockedCount + " di " + ActiveCount + " attivi · " + EarnedPoints + "/" + total + " punti", Theme.Font(12.5f, Weight.Bold), Theme.Ink,
                    new RectangleF(bar.Right + 12, barY - 2, r.Right - 24 - bar.Right - 12, 22), StringAlignment.Near, StringAlignment.Center, false);
                string cta = widget.Enabled ? "Apri obiettivi" : "Scollega per cambiare";
                var cf = Theme.Font(14, Weight.Heavy);
                float cw = Measure(cta, cf).Width + 50;
                var pill = new RectangleF(r.Right - 24 - cw, bottom - 16, cw, 32);
                Theme.Fill(g, pill, 16, Theme.Mix(style.PrimaryTop, Theme.Light(style.PrimaryTop, 0.3f), hv), style.PrimaryBottom);
                Theme.Text(g, cta, cf, Color.White, new RectangleF(pill.X + 16, pill.Y, cw - 34, pill.Height), StringAlignment.Near, StringAlignment.Center, false);
                Theme.Chevron(g, new RectangleF(pill.Right - 26, pill.Y + 9, 14, 14), Color.White, 3, 2.4f);
                Theme.Text(g, "Progressi locali del profilo «" + (String.IsNullOrWhiteSpace(player.Text) ? "senza nome" : player.Text.Trim()) + "»", Theme.Font(12, Weight.Regular), style.Muted,
                    new RectangleF(x, bottom - 14, pill.X - x - 12, 26), StringAlignment.Near, StringAlignment.Center, false);
            }
            else
            {
                Theme.Text(g, open ? game.Description : "Non ancora disponibile. Il catalogo si amplierà con i prossimi giochi supportati, ognuno con il proprio stile e i propri obiettivi.",
                    Theme.Font(13, Weight.Regular), style.Muted, new RectangleF(x, y, w, bottom - y - 30), StringAlignment.Near, StringAlignment.Near, true);
                if (open)
                    Theme.Text(g, widget.Enabled ? "Seleziona gioco  ›" : "Scollega la sessione per cambiare gioco", Theme.Font(13, Weight.Bold),
                        widget.Enabled ? style.AccentDark : Theme.LockedInk, new RectangleF(x, bottom - 22, w, 22));
            }
            if (widget.Enabled) Theme.Cursor(g, r, 22, Math.Max(FocusStrength(widget, time), hv * 0.6f));
        }
    }
}
