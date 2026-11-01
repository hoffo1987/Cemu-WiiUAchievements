using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace WiiUAchievements
{
    // Objective cards. Each state has its own icon, label and outline so it reads without colour:
    // unlocked (check, rarity border), locked (lock), experimental (flask) and not implemented
    // (hourglass, dashed outline). Presentation only: states come from progress and the rules.
    public sealed partial class MainForm
    {
        enum CardState { Unlocked, Locked, Experimental, Planned }
        const float MinCardW = 340, MedalSize = 80, TextLeft = 112, FooterTop = TileH - 32, IntroStep = 0.03f, IntroTime = 0.3f, IntroSpan = 0.75f;

        CardState StateOf(Achievement a, Unlock unlock)
        {
            if (a.IsPlanned) return CardState.Planned;
            if (unlock != null) return CardState.Unlocked;
            return a.Experimental ? CardState.Experimental : CardState.Locked;
        }
        static Color StateInk(CardState s)
        {
            switch (s)
            {
                case CardState.Unlocked: return Theme.UnlockedInk;
                case CardState.Experimental: return Theme.ExperimentalInk;
                case CardState.Planned: return Theme.PlannedInk;
                default: return Theme.LockedInk;
            }
        }
        static string StateLabel(CardState s)
        {
            switch (s)
            {
                case CardState.Unlocked: return "Sbloccato";
                case CardState.Experimental: return "Sperimentale";
                case CardState.Planned: return "Non attivo";
                default: return "Da sbloccare";
            }
        }
        static Action<Graphics, RectangleF, Color> StateIcon(CardState s)
        {
            switch (s)
            {
                case CardState.Unlocked: return (g, r, c) => Theme.Check(g, Theme.Inflate(r, r.Width * 0.08f, r.Height * 0.08f), c, Math.Max(1.6f, r.Width * 0.2f));
                case CardState.Experimental: return Theme.Flask;
                case CardState.Planned: return Theme.Pending;
                default: return (g, r, c) => Theme.Lock(g, r, c);
            }
        }
        // The footer row holds the state on the left and the points on the right.
        static string PointsText(Achievement a) { return a.IsPlanned ? "?" : "+" + a.Points; }
        float PointsWidth(Achievement a) { return Measure(PointsText(a), Theme.Font(13, Weight.Heavy)).Width + 20; }
        RectangleF FooterRect(RectangleF card, Achievement a) { return new RectangleF(card.X + TextLeft, card.Y + FooterTop, card.Width - TextLeft - 22 - PointsWidth(a), 22); }
        // Card text: the title tightens, then takes two lines; the description fills the rest
        // above the footer (up to three lines) and tightens before it would be cut.
        void CardText(Achievement a, RectangleF r, out Font titleFont, out RectangleF title, out Font descriptionFont, out RectangleF description)
        {
            float tx = r.X + TextLeft, tw = r.Right - 14 - tx;
            titleFont = Theme.Font(16.5f, Weight.Heavy);
            if (Measure(a.Title, titleFont).Width > tw) titleFont = Theme.Font(15, Weight.Heavy);
            float th = Measure(a.Title, titleFont).Width <= tw ? 24 : Math.Min(44, Theme.Measure(measure, a.Title, titleFont, tw).Height + 2);
            title = new RectangleF(tx, r.Y + 10, tw, th);
            float dy = title.Bottom + 3;
            description = new RectangleF(tx, dy, tw, r.Y + FooterTop - 2 - dy);
            descriptionFont = Theme.Font(13, Weight.Regular);
            foreach (float size in new[] { 12.5f, 12 })
                if (Theme.Measure(measure, a.Description, descriptionFont, tw).Height > description.Height) descriptionFont = Theme.Font(size, Weight.Regular);
        }
        // True when the whole title (two lines at most) and description fit a card of this width.
        bool CardTextFits(Achievement a, float width)
        {
            Font titleFont, descriptionFont; RectangleF title, description;
            CardText(a, new RectangleF(0, 0, width, TileH), out titleFont, out title, out descriptionFont, out description);
            return Theme.Measure(measure, a.Title, titleFont, title.Width).Height <= 46 &&
                Theme.Measure(measure, a.Description, descriptionFont, description.Width).Height <= description.Height + 0.5f;
        }

        // Per-objective widget id, accessible description and click action, built once.
        readonly Dictionary<Achievement, string[]> tileText = new Dictionary<Achievement, string[]>();
        readonly Dictionary<Achievement, Action> tileOpen = new Dictionary<Achievement, Action>();
        void ArrangeGrid(float w, float h, float top)
        {
            if (tileText.Count > rules.Count * 3) { tileText.Clear(); tileOpen.Clear(); }
            gridRect = new RectangleF(Pad - 8, top, w - Pad * 2 + 16, Math.Max(1, h - BottomH - top));
            var items = Filtered();
            float inner = gridRect.Width - 16;
            int cols = Math.Max(1, (int)((inner + Gap) / (MinCardW + Gap)));
            float tileW = (inner - Gap * (cols - 1)) / cols;
            int rows = (items.Count + cols - 1) / cols;
            contentHeight = rows * (TileH + Gap) - Gap + 24;
            float maxScroll = Math.Max(0, contentHeight - gridRect.Height);
            scrollTarget = Math.Max(0, Math.Min(maxScroll, scrollTarget));
            scroll = Math.Max(0, Math.Min(maxScroll, scroll));
            for (int i = 0; i < items.Count; i++)
            {
                var a = items[i];
                var r = new RectangleF(gridRect.X + 8 + (i % cols) * (tileW + Gap), gridRect.Y + 12 + (i / cols) * (TileH + Gap) - scroll, tileW, TileH);
                string[] text;
                if (!tileText.TryGetValue(a, out text))
                    tileText[a] = text = new[] { "tile:" + a.Id, a.Chapter + ". " + a.Category + ". " + Art.TierName(arts[a.Id].Tier) + ". " + a.Description +
                        (a.IsPlanned ? " Da verificare. " + a.Requirement : a.Experimental ? " Sperimentale: rilevamento implementato, da verificare giocando." : "") };
                Achievement target = a;
                Action open;
                if (!tileOpen.TryGetValue(a, out open)) tileOpen[a] = open = () => OpenDetail(target);
                var tile = Put(text[0], Kind.Tile, r, a.Title, open, true, 0);
                tile.Achievement = a;
                tile.Help = text[1];
            }
            if (items.Count == 0 && (filter != 0 || FiltersActive))
            {
                float bw = ButtonWidth("Mostra tutti gli obiettivi", null);
                AddButton("empty-reset", Kind.Button, "Mostra tutti gli obiettivi", null, gridRect.X + (gridRect.Width - bw) / 2, EmptyTop() + 170, () => { filter = 0; ResetFilters(); }, true, 0, 40, bw);
            }
        }

        void DrawGrid(Graphics g, RectangleF clip, float t)
        {
            var state = g.Save();
            g.SetClip(new RectangleF(0, gridRect.Y, W, gridRect.Height), CombineMode.Intersect);
            int inView = visible.Count(v => v.Kind == Kind.Tile && v.Bounds.Bottom >= gridRect.Y - TileH && v.Bounds.Y <= gridRect.Bottom + TileH);
            var first = visible.FirstOrDefault(v => v.Kind == Kind.Tile);
            if (first != null) UpdateTileBudget(first.Bounds.Size, inView);
            int count = 0, drawn = 0;
            foreach (var w in visible)
            {
                if (w.Kind != Kind.Tile) continue;
                count++;
                if (w.Bounds.Bottom < gridRect.Y - 12 || w.Bounds.Y > gridRect.Bottom + 12 || !clip.IntersectsWith(Theme.Inflate(w.Bounds, 18, 18))) continue;
                // A short staggered rise and fade, ordered by position in the viewport (never by
                // catalog index, so cards reached by scrolling do not wait).
                float appear = Motion.Reduced ? 1 : (t - introStart - Math.Min(drawn++ * IntroStep, IntroSpan - IntroTime)) / IntroTime;
                if (appear <= 0) continue;
                if (appear >= 1) { DrawTile(g, w, t, 1); continue; }
                float e = Theme.EaseOut(appear);
                var tileState = g.Save();
                var c = Theme.Center(w.Bounds);
                float k = 0.94f + 0.06f * e;
                g.TranslateTransform(c.X, c.Y + 12 * (1 - e)); g.ScaleTransform(k, k); g.TranslateTransform(-c.X, -c.Y);
                DrawTile(g, w, t, e);
                g.Restore(tileState);
            }
            if (count == 0) DrawEmpty(g, t);
            float top = gridRect.Y / H, bottom = gridRect.Bottom / H;
            Color fadeTop = Theme.Mix(Style.BackTop, Style.BackBottom, top), fadeBottom = Theme.Mix(Style.BackTop, Style.BackBottom, bottom);
            if (scroll > 1)
                using (var b = new LinearGradientBrush(new RectangleF(0, gridRect.Y - 1, W, 16), fadeTop, Color.FromArgb(0, fadeTop), 90f)) g.FillRectangle(b, 0, gridRect.Y, W, 14);
            if (contentHeight - scroll > gridRect.Height + 1)
                using (var b = new LinearGradientBrush(new RectangleF(0, gridRect.Bottom - 15, W, 16), Color.FromArgb(0, fadeBottom), fadeBottom, 90f)) g.FillRectangle(b, 0, gridRect.Bottom - 14, W, 14);
            g.Restore(state);
            if (contentHeight > gridRect.Height + 1)
            {
                var track = new RectangleF(W - 10, gridRect.Y + 10, 6, gridRect.Height - 20);
                float thumbH = Math.Max(40, track.Height * gridRect.Height / contentHeight);
                float thumbY = track.Y + (track.Height - thumbH) * (scroll / Math.Max(1, contentHeight - gridRect.Height));
                Theme.Fill(g, track, 3, Color.FromArgb(26, 20, 44, 64), Color.FromArgb(26, 20, 44, 64));
                Theme.Fill(g, new RectangleF(track.X, thumbY, track.Width, thumbH), 3, Color.FromArgb(190, Style.Accent), Color.FromArgb(210, Style.AccentDark));
            }
        }
        // The empty state is one centred block: illustration, message and (with filters) a reset.
        float EmptyTop() { return gridRect.Y + Math.Max(6, (gridRect.Height - 212) / 2); }
        void DrawEmpty(Graphics g, float t)
        {
            var style = Style;
            var c = Theme.Center(gridRect);
            float cy = EmptyTop() + 46;
            bool searching = Query.Length > 0, none = filter == 1 && !FiltersActive;
            var disc = Theme.Square(new PointF(c.X, cy), 92);
            Theme.Shadow(g, disc, 46, 8, 36, 3);
            using (var b = new LinearGradientBrush(Theme.Inflate(disc, 0, 1), style.CardTop, style.Themed ? style.CardBottom : style.Soft, 90f)) g.FillEllipse(b, disc);
            using (var pen = new Pen(style.Themed ? style.ButtonEdge : Theme.Line, 1.4f)) g.DrawEllipse(pen, disc);
            if (searching) Theme.Search(g, Theme.Square(new PointF(c.X, cy), 44), style.AccentDark);
            else Theme.Trophy(g, Theme.Square(new PointF(c.X, cy - 1), 50));
            string title = searching ? "Nessun risultato per «" + Query + "»" : none ? "Nessun obiettivo sbloccato, per ora" : "Nessun obiettivo con questi filtri";
            string detail = searching ? "Prova con un'altra parola: nome, isola, tappa o rarità." :
                none ? "Collega Cemu e gioca: gli sblocchi compariranno qui con la loro medaglia." : "Cambia scheda, categoria o tappa per vedere gli altri obiettivi.";
            Theme.Text(g, title, Theme.Font(19, Weight.Heavy), Theme.Ink, new RectangleF(gridRect.X + 20, cy + 58, gridRect.Width - 40, 28), StringAlignment.Center, StringAlignment.Center, false);
            Theme.Text(g, detail, Theme.Font(14, Weight.Regular), style.Muted, new RectangleF(gridRect.X + 20, cy + 88, gridRect.Width - 40, 22), StringAlignment.Center, StringAlignment.Center, false);
            Widget reset;
            if (widgets.TryGetValue("empty-reset", out reset) && reset.Visible) DrawButton(g, reset, t);
        }

        void DrawTileVector(Graphics g, Widget w, float t, bool drawFooter)
        {
            var a = w.Achievement;
            var art = arts[a.Id];
            var unlock = progress == null || a.IsPlanned ? null : progress.Get(a.Id);
            bool done = unlock != null;
            var cardState = StateOf(a, unlock);
            float party = Celebration(a.Id, t);
            float cursor = Math.Max(w.Hover, FocusStrength(w, t));
            var style = Style;
            var r = w.Bounds;
            if (!Motion.Reduced) r.Offset(0, -3f * w.Hover + 1.5f * w.Press);
            Theme.Shadow(g, r, 16, 4 + 4 * cursor, 30 + (int)(28 * cursor), 2 + 1.5f * cursor);
            if (cardState == CardState.Planned)
                Theme.Fill(g, r, 16, Color.FromArgb(214, style.CardLockedTop), Color.FromArgb(196, Theme.Mix(style.CardLockedBottom, style.BackBottom, 0.4f)));
            else Theme.Fill(g, r, 16, done ? style.CardTop : style.CardLockedTop, done ? Theme.Mix(style.CardBottom, style.Soft, style.Themed ? 0.15f : 0.4f) : style.CardLockedBottom);
            style.DecorateCard(g, r, 16, false);
            Color tier = Theme.TierColor(art.Tier), glow = Theme.TierGlow(art.Tier);
            using (var path = Theme.Round(r, 16))
            {
                var clipState = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                if (done)
                {
                    // Rarity wash with the game's motif, like a themed header on the Wii U Menu.
                    using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, 60), Color.FromArgb(84, tier), Color.FromArgb(0, tier), 90f)) g.FillRectangle(b, r.X, r.Y, r.Width, 58);
                    style.Pattern(g, new RectangleF(r.X + TextLeft - 10, r.Y, r.Width - TextLeft + 10, 30), 0, Color.FromArgb(40, 255, 255, 255));
                }
                if (party >= 0 && !Motion.Reduced)
                {
                    float u = Theme.EaseInOut(Theme.Clamp01((party - 0.2f) / 0.55f)), sx = r.X - 90 + (r.Width + 180) * u;
                    using (var band = new GraphicsPath())
                    {
                        band.AddPolygon(new[] { new PointF(sx, r.Y), new PointF(sx + 60, r.Y), new PointF(sx + 20, r.Bottom), new PointF(sx - 40, r.Bottom) });
                        Theme.Fill(g, band, Color.FromArgb((int)(130 * (1 - party)), 255, 255, 255));
                    }
                }
                g.Restore(clipState);
            }
            if (cardState == CardState.Planned) Theme.StrokeDashed(g, Theme.Inflate(r, -0.5f, -0.5f), 15.5f, Color.FromArgb(150, Theme.PlannedInk), 1.5f);
            else Theme.Stroke(g, r, 16, done ? Theme.Mix(tier, Color.White, 0.25f) : style.CardEdge, done ? 1.7f : style.Themed ? 1.2f : 1);

            // The medal sits in a soft well that glows in its rarity once earned.
            var mc = new PointF(r.X + 16 + MedalSize / 2, r.Y + 56);
            var well = Theme.Square(mc, MedalSize + 14);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(well);
                Color center = done ? Color.FromArgb(130, glow) : Color.FromArgb(cardState == CardState.Planned ? 40 : 90, style.Themed ? style.Soft : Color.FromArgb(214, 226, 234));
                using (var b = new PathGradientBrush(path) { CenterColor = center, SurroundColors = new[] { Color.FromArgb(0, center) } }) g.FillPath(b, path);
            }
            float pop = 1 + 0.05f * cursor, flip = 1;
            bool grey = !done;
            if (party >= 0)
            {
                // Coin turn from the grey medal to the coloured one, then a soft pop and a burst;
                // rarer medals get rays and more sparkles. Reduced motion keeps only the glow.
                float turn = Theme.Clamp01(party / 0.3f);
                grey = turn < 0.5f;
                if (!Motion.Reduced)
                {
                    flip = Math.Abs((float)Math.Cos(Math.PI * turn));
                    pop = 1 + (0.14f + 0.03f * (int)art.Tier) * (float)Math.Sin(Math.PI * Theme.Clamp01((party - 0.3f) / 0.35f));
                }
                if (!grey)
                {
                    if (art.Tier >= Tier.Silver && !Motion.Reduced) Theme.Rays(g, mc, 60 + 6 * (int)art.Tier, party * 160, Color.FromArgb((int)((art.Tier >= Tier.Gold ? 190 : 120) * (1 - party)), glow));
                    float ring = Theme.Clamp01((party - 0.15f) / 0.5f);
                    if (ring < 1)
                        using (var pen = new Pen(Color.FromArgb((int)(210 * (1 - ring)), glow), 0.5f + 3 * (1 - ring)))
                            g.DrawEllipse(pen, Theme.Square(mc, MedalSize * (0.95f + (Motion.Reduced ? 0.2f : 0.75f) * Theme.EaseOut(ring))));
                }
            }
            var medal = Theme.Square(mc, MedalSize * pop);
            var turnState = g.Save();
            if (flip < 0.999f) { g.TranslateTransform(mc.X, mc.Y); g.ScaleTransform(Math.Max(0.03f, flip), 1); g.TranslateTransform(-mc.X, -mc.Y); }
            Theme.Medal(g, medal, art, grey ? 0.88f : 0);
            g.Restore(turnState);
            if (cardState == CardState.Planned)
                using (var b = new SolidBrush(Color.FromArgb(120, style.CardLockedTop))) g.FillEllipse(b, Theme.Inflate(medal, -2, -2));
            if (party < 0 || party > 0.3f) DrawStateBadge(g, Theme.Square(mc, MedalSize), party >= 0 ? CardState.Unlocked : cardState);
            Theme.TierRibbon(g, new RectangleF(mc.X - 40, r.Y + 104, 80, 20), art.Tier, Art.TierName(art.Tier).ToUpperInvariant(), 1);

            float tx = r.X + TextLeft, tw = r.Right - 14 - tx;
            DrawPoints(g, a, r, cardState, tier);
            Font titleFont, descriptionFont; RectangleF titleRect, descriptionRect;
            CardText(a, r, out titleFont, out titleRect, out descriptionFont, out descriptionRect);
            Theme.Text(g, a.Title, titleFont, Theme.Ink, titleRect, StringAlignment.Near, StringAlignment.Near, titleRect.Height > 30);
            Theme.Text(g, a.Description, descriptionFont, style.Muted, descriptionRect, StringAlignment.Near, StringAlignment.Near, true);
            if (drawFooter) DrawProgress(g, a, unlock, FooterRect(r, a), false);
            Theme.Cursor(g, r, 16, cursor);
            // Earned gold and platinum cards wear the gold corners.
            if (done && art.Tier >= Tier.Gold && style.HasUiArt) style.SimpleCorners(g, r, 20);
            if (party >= 0)
            {
                Theme.Stroke(g, Theme.Inflate(r, 1, 1), 17, Color.FromArgb((int)(230 * (1 - party)), glow), 2.5f);
                float burst = Theme.Clamp01((party - 0.28f) / 0.72f);
                int sparkles = Motion.Reduced ? 0 : 6 + 2 * (int)art.Tier + (art.Tier >= Tier.Gold ? 2 : 0);
                if (burst > 0)
                    for (int i = 0; i < sparkles; i++)
                    {
                        double ang = i * Math.PI * 2 / sparkles + 0.4;
                        float d = 30 + 46 * Theme.EaseOut(burst);
                        var p = new PointF(mc.X + (float)Math.Cos(ang) * d, mc.Y + (float)Math.Sin(ang) * d * 0.75f);
                        Theme.Sparkle(g, p, 5.5f * (1 - burst) + 1, Color.FromArgb((int)(255 * (1 - burst)), i % 2 == 0 ? glow : Color.White));
                    }
            }
        }
        RectangleF DrawPoints(Graphics g, Achievement a, RectangleF r, CardState s, Color tier)
        {
            string pts = PointsText(a);
            var pf = Theme.Font(13, Weight.Heavy);
            float pw = PointsWidth(a);
            var pill = new RectangleF(r.Right - 14 - pw, r.Y + FooterTop, pw, 22);
            Color ink;
            if (s == CardState.Unlocked) { Theme.Fill(g, pill, 12, Theme.Dark(tier, 0.02f), Theme.Dark(tier, 0.3f)); ink = Color.White; }
            else if (s == CardState.Planned) { Theme.StrokeDashed(g, pill, 12, Theme.PlannedInk, 1.3f); ink = Theme.PlannedInk; }
            else { Theme.Fill(g, pill, 12, Color.FromArgb(24, Theme.LockedInk), Color.FromArgb(40, Theme.LockedInk)); ink = s == CardState.Experimental ? Theme.ExperimentalInk : Theme.LockedInk; }
            if (s == CardState.Unlocked) Theme.Text(g, pts, pf, Color.FromArgb(90, 0, 0, 0), new RectangleF(pill.X, pill.Y + 0.8f, pill.Width, pill.Height), StringAlignment.Center, StringAlignment.Center, false);
            Theme.Text(g, pts, pf, ink, pill, StringAlignment.Center, StringAlignment.Center, false);
            return pill;
        }
        // Round badge on the medal's shoulder: check, lock, flask or hourglass.
        static void DrawStateBadge(Graphics g, RectangleF medal, CardState s)
        {
            var r = new RectangleF(medal.Right - medal.Width * 0.34f, medal.Y - medal.Height * 0.02f, medal.Width * 0.36f, medal.Height * 0.36f);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, Theme.Inflate(r, 1.6f, 1.6f));
            Color fill = s == CardState.Unlocked ? Theme.Success : s == CardState.Experimental ? Theme.Warning : s == CardState.Planned ? Theme.PlannedInk : Color.FromArgb(132, 143, 152);
            using (var b = new LinearGradientBrush(Theme.Inflate(r, 0, 1), Theme.Light(fill, 0.15f), Theme.Dark(fill, 0.1f), 90f)) g.FillEllipse(b, r);
            var icon = Theme.Inflate(r, -r.Width * 0.22f, -r.Height * 0.2f);
            if (s == CardState.Unlocked) Theme.Check(g, Theme.Inflate(r, -r.Width * 0.08f, -r.Height * 0.08f), Color.White, Math.Max(1.5f, r.Width * 0.14f));
            else StateIcon(s)(g, icon, Color.White);
        }

        // Card footer (left aligned) or detail status line (centred): a state chip with its icon,
        // then the date, the reason or the live progress bar.
        void DrawProgress(Graphics g, Achievement a, Unlock unlock, RectangleF r, bool centered)
        {
            var style = Style;
            var font = Theme.Font(centered ? 13.5f : 12.5f, Weight.Bold);
            // An earned timestamp is primary information. The full state chip used to
            // consume the narrow card's footer and silently suppress the whole date.
            // Keep the check and reserve the remaining row for a complete numeric date.
            if (!centered && unlock != null && !a.IsPlanned)
            {
                Theme.Check(g, new RectangleF(r.X + 1, r.Y + (r.Height - 16) / 2, 16, 16), Theme.UnlockedInk, 1.8f);
                Theme.Text(g, unlock.Utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Italian), font, style.Muted,
                    new RectangleF(r.X + 24, r.Y, r.Width - 24, r.Height), StringAlignment.Near, StringAlignment.Center, false);
                return;
            }
            var chipFont = Theme.Font(centered ? 13 : 12, Weight.Heavy);
            float chipH = centered ? 26 : 22;
            CardState chip = StateOf(a, unlock);
            string trailing = null;
            Color trailingInk = style.Muted;
            if (a.IsPlanned) trailing = centered ? "nessuno sblocco automatico" : "da verificare";
            else if (unlock != null) trailing = unlock.Utc.ToLocalTime().ToString(centered ? "d MMMM yyyy 'alle' HH:mm" : "d MMM, HH:mm", Italian);
            else if (current == null)
                trailing = paused ? (centered ? "registrazione in pausa" : "in pausa") : demo ? (centered ? "in attesa dei dati simulati" : "demo") :
                    centered ? "collega Cemu per vedere i progressi" : a.Experimental ? "da ottenere" : null;
            else if (!current.HasValue(a.Field)) { trailing = "dato non disponibile"; trailingInk = Theme.ExperimentalInk; }
            else
            {
                int value = current.Value(a.Field);
                var evaluation = engine == null ? null : engine.Status(a.Id);
                if (evaluation != null && evaluation.Phase == AchievementPhase.AlreadySatisfied)
                { trailing = a.Field == "rupees" || a.Field == "max_health" ? "soglia già raggiunta all'avvio" : "già presente all'avvio"; trailingInk = Theme.ExperimentalInk; }
                else if (evaluation != null && evaluation.Phase == AchievementPhase.Stabilizing) trailing = "sincronizzazione…";
                else if (evaluation != null && evaluation.Phase == AchievementPhase.Confirming) { trailing = "verifica " + evaluation.Confirmations + "/" + AchievementEngine.RequiredSamples; trailingInk = style.AccentDark; }
                else if (a.Threshold == 1 && a.Field != "rupees") trailing = "in attesa";
                else { DrawBar(g, a, value, r, font, centered); return; }
            }
            float chipW = Theme.Measure(g, StateLabel(chip), chipFont).Width + 2 + chipH + 16;
            float textW = trailing == null ? 0 : Measure(trailing, font).Width + 4;
            float total = chipW + (trailing != null ? 8 + textW : 0);
            float x = centered ? r.X + Math.Max(0, (r.Width - total) / 2) : r.X, y = r.Y + (r.Height - chipH) / 2;
            float used = Theme.StateChip(g, x, y, chipH, StateInk(chip), StateIcon(chip), StateLabel(chip), chipFont, r.Right - x);
            // Secondary words appear only when they fit whole; the chip alone already says the state.
            if (trailing != null && r.Right - (x + used + 8) >= textW)
                Theme.Text(g, trailing, font, trailingInk, new RectangleF(x + used + 8, r.Y, r.Right - (x + used + 8), r.Height), StringAlignment.Near, StringAlignment.Center, false);
        }
        void DrawBar(Graphics g, Achievement a, int value, RectangleF r, Font font, bool centered)
        {
            bool hearts = a.Field == "max_health";
            string label = hearts ? Hearts(value) + " / " + (a.Threshold / 4) + " cuori" : value + " / " + a.Threshold;
            float lw = Measure(label, font).Width + 6;
            float width = centered ? Math.Min(r.Width, 340) : r.Width;
            float x0 = r.X + (r.Width - width) / 2;
            var bar = new RectangleF(x0, r.Y + r.Height / 2 - 4.5f, width - lw - 10, 9);
            if (bar.Width < 8) { Theme.Text(g, label, font, Theme.Ink, r); return; }
            Theme.Fill(g, bar, 4.5f, Color.FromArgb(206, 216, 224), Color.FromArgb(226, 233, 238));
            float ratio = Theme.Clamp01(value / (float)a.Threshold);
            if (ratio > 0)
            {
                var fill = new RectangleF(bar.X, bar.Y, Math.Max(9, bar.Width * ratio), bar.Height);
                Theme.Fill(g, fill, 4.5f, Style.Glow, Style.AccentDark);
                using (var gloss = Theme.Round(new RectangleF(fill.X + 2, fill.Y + 1, fill.Width - 4, 3), 1.5f)) Theme.Fill(g, gloss, Color.FromArgb(90, 255, 255, 255));
            }
            Theme.Text(g, label, font, Theme.Ink, new RectangleF(bar.Right + 10, r.Y, lw, r.Height), StringAlignment.Near, StringAlignment.Center, false);
        }
    }
}
