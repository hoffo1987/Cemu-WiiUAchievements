using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace WiiUAchievements
{
    // Detail card of one objective. Left/right (or the side arrows) browse the objectives shown
    // by the current filters; the content slides in the direction of travel.
    public sealed partial class MainForm
    {
        const float DialogW = 560, DetailSlide = 0.24f;
        float detailSwap = -10;
        int detailDirection;

        string DetailStatus(Achievement a)
        {
            if (a.IsPlanned) return "Rilevamento non ancora implementato: questo obiettivo non si sblocca automaticamente.";
            if (a.Experimental) return "Rilevamento implementato e sperimentale: da verificare durante la partita.";
            return "Rilevamento attivo: la condizione è letta in sola lettura dalla memoria del gioco.";
        }
        const float DetailTitleW = DialogW - 120, DetailTextW = DialogW - 96, DetailDescriptionMax = 70, DetailNoteMax = 78;
        // Long titles step down a size instead of being cut.
        Font DetailTitleFont(Achievement a) { var font = Theme.Font(26, Weight.Heavy); return Measure(a.Title, font).Width <= DetailTitleW ? font : Theme.Font(22, Weight.Heavy); }
        float DetailDescriptionHeight(Achievement a) { return Math.Min(DetailDescriptionMax, Theme.Measure(measure, a.Description, Theme.Font(15.5f, Weight.Regular), DetailTextW).Height + 2); }
        float DetailNoteHeight(Achievement a)
        {
            string note = a.IsPlanned ? a.Requirement : DetailStatus(a);
            return Math.Min(DetailNoteMax, Theme.Measure(measure, note, Theme.Font(13.5f, Weight.Regular), DetailTextW).Height + 2);
        }
        void ArrangeDialog(float w, float h)
        {
            var a = detail;
            float height = 276 + DetailDescriptionHeight(a) + 12 + 28 + 16 + (16 + 38 + 10 + 18 + DetailNoteHeight(a) + 14) + 18 + 44 + 22;
            height = Math.Min(h - 24, height);
            dialogRect = new RectangleF((w - DialogW) / 2, Math.Max(12, (h - height) / 2), DialogW, height);
            AddButton("close", Kind.Primary, "Chiudi", "B", dialogRect.X + (DialogW - ButtonWidth("Chiudi", "B")) / 2, dialogRect.Bottom - 64, CloseDialog, true, 2);
            Put("detail-x", Kind.Icon, new RectangleF(dialogRect.Right - 50, dialogRect.Y + 14, 34, 34), "Chiudi dettagli", CloseDialog, true, 2).Help = "Torna agli obiettivi";
            var list = Filtered();
            int index = list.FindIndex(x => x.Id == a.Id);
            float cy = dialogRect.Y + dialogRect.Height / 2 - 26, side = Math.Min(64, (w - DialogW) / 2 - 4);
            if (side >= 44)
            {
                Put("detail-prev", Kind.Icon, new RectangleF(dialogRect.X - side + 2, cy, 52, 52), "Obiettivo precedente", () => StepDetail(-1), index > 0, 2).Help = index > 0 ? list[index - 1].Title : "Primo obiettivo dell'elenco";
                Put("detail-next", Kind.Icon, new RectangleF(dialogRect.Right + side - 54, cy, 52, 52), "Obiettivo successivo", () => StepDetail(1), index >= 0 && index < list.Count - 1, 2).Help = index >= 0 && index < list.Count - 1 ? list[index + 1].Title : "Ultimo obiettivo dell'elenco";
            }
        }
        void StepDetail(int direction)
        {
            if (detail == null || dialogTarget <= 0) return;
            var list = Filtered();
            int index = list.FindIndex(x => x.Id == detail.Id) + direction;
            if (index < 0 || index >= list.Count) return;
            detail = list[index]; detailDirection = direction;
            detailSwap = Motion.Reduced ? -10 : (float)clock.Elapsed.TotalSeconds;
            Invalidate(); Kick();
        }
        // After browsing in the dialog, bring the last shown card into view in the grid.
        void RevealDetailTile()
        {
            if (detail == null) return;
            Arrange();
            Widget tile;
            if (!widgets.TryGetValue("tile:" + detail.Id, out tile) || !tile.Visible) return;
            var b = tile.Bounds;
            if (b.Y < gridRect.Y + 8) scrollTarget -= gridRect.Y + 12 - b.Y;
            else if (b.Bottom > gridRect.Bottom - 8) scrollTarget += b.Bottom - gridRect.Bottom + 14;
            if (focusVisible) focused = tile;
            InvalidateScene();
        }

        void DrawDialog(Graphics g, float t)
        {
            float e = Theme.EaseOut(dialog);
            using (var b = new SolidBrush(Color.FromArgb((int)(128 * e), 10, 26, 42))) g.FillRectangle(b, 0, 0, W, H);
            var r = dialogRect;
            var c = Theme.Center(r);
            var a = detail;
            var art = arts[a.Id];
            var unlock = progress == null || a.IsPlanned ? null : progress.Get(a.Id);
            bool done = unlock != null;
            var cardState = StateOf(a, unlock);
            var style = Style;
            var state = g.Save();
            float s = Motion.Reduced ? 1 : 0.92f + 0.08f * Theme.EaseBack(dialog);
            g.TranslateTransform(c.X, c.Y + (Motion.Reduced ? 0 : 14 * (1 - e))); g.ScaleTransform(s, s); g.TranslateTransform(-c.X, -c.Y);
            Theme.Shadow(g, r, 26, 18, 100, 7);
            Theme.Fill(g, r, 26, style.CardTop, style.Themed ? style.CardBottom : Color.FromArgb(236, 242, 246));
            Color tier = Theme.TierColor(art.Tier), glow = Theme.TierGlow(art.Tier);
            using (var path = Theme.Round(r, 26))
            {
                var inner = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, 192), Color.FromArgb(done ? 120 : 46, done ? tier : Theme.LockedInk), Color.FromArgb(0, tier), 90f))
                    g.FillRectangle(b, r.X, r.Y, r.Width, 190);
                if (done) style.Pattern(g, new RectangleF(r.X, r.Y + 4, r.Width, 60), Motion.Reduced ? 0 : t * 0.6f, Color.FromArgb(46, 255, 255, 255));
                g.Restore(inner);
            }
            Theme.Stroke(g, r, 26, done ? Theme.Mix(tier, Color.White, 0.35f) : Color.FromArgb(214, 222, 228), done ? 1.5f : 1);
            style.Parchment(g, r, 26, 0.45f);
            style.DecoratePanel(g, r, 26);
            if (cardState == CardState.Planned) Theme.StrokeDashed(g, Theme.Inflate(r, -7, -7), 20, Color.FromArgb(130, Theme.PlannedInk), 1.4f);

            // Browsed content slides in from the side it came from, inside the card.
            float swap = Theme.Clamp01((t - detailSwap) / DetailSlide), slide = swap < 1 ? detailDirection * 44 * (1 - Theme.EaseOut(swap)) : 0;
            var content = g.Save();
            using (var clip = Theme.Round(Theme.Inflate(r, -2, -2), 24)) g.SetClip(clip, CombineMode.Intersect);
            g.TranslateTransform(slide, 0);
            float enter = Theme.EaseBack(Theme.Clamp01((dialog - 0.2f) / 0.8f)) * (swap < 1 ? 0.85f + 0.15f * Theme.EaseBack(swap) : 1);
            if (Motion.Reduced) enter = 1;
            var mc = new PointF(c.X, r.Y + 98 + (done && !Motion.Reduced ? (float)Math.Sin(t * 1.7f) * 2.5f : 0));
            float size = 136 * (0.55f + 0.45f * enter);
            var well = Theme.Square(mc, 168);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(well);
                Color center = done ? Color.FromArgb(150, glow) : Color.FromArgb(80, style.Themed ? style.Soft : Color.FromArgb(214, 226, 234));
                using (var b = new PathGradientBrush(path) { CenterColor = center, SurroundColors = new[] { Color.FromArgb(0, center) } }) g.FillPath(b, path);
            }
            if (done) Theme.Rays(g, mc, 124, Motion.Reduced ? 0 : t * 10, Color.FromArgb((int)(140 * enter), glow));
            Theme.Medal(g, Theme.Square(mc, size), art, done ? 0 : 0.88f);
            if (cardState == CardState.Planned) using (var b = new SolidBrush(Color.FromArgb(110, style.CardTop))) g.FillEllipse(b, Theme.Square(mc, size - 4));
            DrawStateBadge(g, Theme.Square(mc, size), cardState);

            Theme.Text(g, a.Title, DetailTitleFont(a), Theme.Ink, new RectangleF(r.X + 60, r.Y + 182, DetailTitleW, 38), StringAlignment.Center, StringAlignment.Center, false);
            string rank = Art.TierName(art.Tier).ToUpperInvariant();
            string points = a.IsPlanned ? "Punti proposti: " + a.Points : "+" + a.Points + (a.Points == 1 ? " punto" : " punti");
            var pf = Theme.Font(13.5f, Weight.Heavy);
            float rw = Measure(rank, Theme.Font(15, Weight.Heavy)).Width + 30, pw = Measure(points, pf).Width + 40, row = c.X - (rw + 8 + pw) / 2;
            Theme.TierRibbon(g, new RectangleF(row, r.Y + 229, rw, 26), art.Tier, rank, 1);
            var pr = new RectangleF(row + rw + 8, r.Y + 228, pw, 28);
            if (done) Theme.Fill(g, pr, 14, Theme.Dark(tier, 0.02f), Theme.Dark(tier, 0.3f));
            else if (a.IsPlanned) Theme.StrokeDashed(g, pr, 14, Theme.PlannedInk, 1.3f);
            else Theme.Fill(g, pr, 14, Color.FromArgb(24, Theme.LockedInk), Color.FromArgb(40, Theme.LockedInk));
            Color pink = done ? Color.White : a.IsPlanned ? Theme.PlannedInk : Theme.LockedInk;
            Theme.Star(g, new RectangleF(pr.X + 11, pr.Y + 6, 16, 16), Color.FromArgb(255, 228, 118), Color.FromArgb(226, 152, 20));
            Theme.Text(g, points, pf, pink, new RectangleF(pr.X + 31, pr.Y, pr.Width - 35, pr.Height), StringAlignment.Near, StringAlignment.Center, false);

            float y = r.Y + 270, dh = DetailDescriptionHeight(a);
            Theme.Text(g, a.Description, Theme.Font(15.5f, Weight.Regular), Theme.Mix(style.Muted, Theme.Ink, 0.35f), new RectangleF(r.X + 48, y, r.Width - 96, dh), StringAlignment.Center, StringAlignment.Near, true);
            y += dh + 12;
            DrawProgress(g, a, unlock, new RectangleF(r.X + 40, y, r.Width - 80, 28), true);
            y += 28 + 16;

            // Facts table: where the objective sits in the journey and how it is detected.
            float nh = DetailNoteHeight(a);
            var box = new RectangleF(r.X + 32, y, r.Width - 64, 16 + 38 + 10 + 18 + nh + 14);
            Theme.Fill(g, box, 14, Color.FromArgb(150, Color.White), Color.FromArgb(110, Color.White));
            Theme.Stroke(g, box, 14, Color.FromArgb(150, style.Themed ? style.ButtonEdge : Theme.Line), 1);
            var label = Theme.Font(11.5f, Weight.Heavy);
            var value = Theme.Font(14, Weight.Bold);
            float half = (box.Width - 32) / 2;
            Theme.Tag(g, new RectangleF(box.X + 16, box.Y + 14, 13, 13), style.AccentDark);
            Theme.Text(g, "CATEGORIA", label, style.AccentDark, new RectangleF(box.X + 34, box.Y + 12, half - 20, 18));
            Theme.Text(g, a.Category, value, Theme.Ink, new RectangleF(box.X + 16, box.Y + 31, half - 8, 22));
            Theme.Compass(g, new RectangleF(box.X + 16 + half + 16, box.Y + 14, 13, 13), style.AccentDark);
            Theme.Text(g, "TAPPA DEL VIAGGIO", label, style.AccentDark, new RectangleF(box.X + 34 + half + 16, box.Y + 12, half - 20, 18));
            Theme.Text(g, a.Chapter, value, Theme.Ink, new RectangleF(box.X + 16 + half + 16, box.Y + 31, half - 8, 22));
            using (var pen = new Pen(Color.FromArgb(90, style.Themed ? style.ButtonEdge : Theme.Line), 1)) g.DrawLine(pen, box.X + 16, box.Y + 62, box.Right - 16, box.Y + 62);
            StateIcon(cardState)(g, new RectangleF(box.X + 16, box.Y + 72, 13, 13), StateInk(cardState));
            Theme.Text(g, a.IsPlanned ? "REQUISITO DA VERIFICARE" : "RILEVAMENTO", label, StateInk(cardState), new RectangleF(box.X + 34, box.Y + 70, box.Width - 50, 18));
            Theme.Text(g, a.IsPlanned ? a.Requirement : DetailStatus(a), Theme.Font(13.5f, Weight.Regular), Theme.Ink,
                new RectangleF(box.X + 16, box.Y + 90, box.Width - 32, nh), StringAlignment.Near, StringAlignment.Near, true);
            g.Restore(content);

            DrawButton(g, widgets["close"], t);
            DrawIconButton(g, widgets["detail-x"], t, (gg, b, ink) => Theme.Close(gg, b, ink, 2.4f));
            g.Restore(state);
            // The browse arrows stay outside the card and do not scale with it.
            Widget prev, next;
            if (widgets.TryGetValue("detail-prev", out prev) && prev.Visible && prev.Layer == 2)
            {
                var ps = g.Save();
                if (e < 1) { var pc = Theme.Center(prev.Bounds); g.TranslateTransform(pc.X, pc.Y); g.ScaleTransform(e, e); g.TranslateTransform(-pc.X, -pc.Y); }
                DrawIconButton(g, prev, t, (gg, b, ink) => Theme.Chevron(gg, b, ink, 2, 2.8f));
                g.Restore(ps);
            }
            if (widgets.TryGetValue("detail-next", out next) && next.Visible && next.Layer == 2)
            {
                var ns = g.Save();
                if (e < 1) { var nc = Theme.Center(next.Bounds); g.TranslateTransform(nc.X, nc.Y); g.ScaleTransform(e, e); g.TranslateTransform(-nc.X, -nc.Y); }
                DrawIconButton(g, next, t, (gg, b, ink) => Theme.Chevron(gg, b, ink, 3, 2.8f));
                g.Restore(ns);
            }
        }
    }
}
