using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WiiUAchievements;

class UiTests
{
    static int checks;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object target, string field) { return target.GetType().GetField(field, Private | BindingFlags.Public).GetValue(target); }
    static void Set(object target, string field, object value) { target.GetType().GetField(field, Private | BindingFlags.Public).SetValue(target, value); }
    static object Call(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Private).Invoke(target, args); }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        checks++; Console.WriteLine("PASS " + name);
    }
    static void Settle()
    {
        for (int i = 0; i < 55; i++) { Application.DoEvents(); Thread.Sleep(10); }
    }
    // Account work continues in the background (login, then an automatic sync): wait until the
    // form has been idle for several consecutive polls before asserting on its controls.
    static void WaitCloudIdle(MainForm form)
    {
        int quiet = 0;
        for (int i = 0; i < 300 && quiet < 8; i++)
        {
            Application.DoEvents(); Thread.Sleep(20);
            quiet = (bool)Get(form, "cloudBusy") ? 0 : quiet + 1;
        }
    }
    static string CloudState(MainForm form)
    {
        return " (busy=" + Get(form, "cloudBusy") + ", signed=" + ((CloudSync)Get(form, "cloud")).IsSignedIn + ", login=" + Get(Widget(form, "cloud-login"), "Enabled") +
            ", logout=" + Get(Widget(form, "cloud-logout"), "Enabled") + ", readOnly=" + ((TextBox)Get(form, "cloudEmail")).ReadOnly + ", message=" + Get(form, "cloudMessage") + ")";
    }
    static object Widget(MainForm form, string id) { Call(form, "Arrange"); return ((IDictionary)Get(form, "widgets"))[id]; }
    static void Click(MainForm form, string id)
    {
        object widget = Widget(form, id);
        var r = (RectangleF)Get(widget, "Bounds"); float scale = (float)Get(form, "scale");
        var e = new MouseEventArgs(MouseButtons.Left, 1, (int)((r.X + r.Width / 2) * scale), (int)((r.Y + r.Height / 2) * scale), 0);
        Call(form, "OnMouseDown", e); Call(form, "OnMouseUp", e); Settle();
    }
    static void Key(MainForm form, Keys key)
    {
        object[] args = { new Message(), key };
        form.GetType().GetMethod("ProcessCmdKey", Private).Invoke(form, args); Settle();
    }
    static void RenderScroll(MainForm form, Graphics graphics, float offset)
    {
        Set(form, "scroll", offset); Set(form, "scrollTarget", offset);
        graphics.ResetTransform(); graphics.ResetClip();
        Call(form, "Arrange");
        var grid = (RectangleF)Get(form, "gridRect");
        float scale = (float)Get(form, "scale");
        var clip = new RectangleF(0, grid.Y, form.ClientSize.Width / scale, grid.Height);
        graphics.SetClip(new RectangleF(clip.X * scale, clip.Y * scale, clip.Width * scale, clip.Height * scale));
        Call(form, "Render", graphics, clip);
    }
    static long FooterPixels(MainForm form, Bitmap bitmap)
    {
        var tile = (RectangleF)Get(Widget(form, "tile:wallet-10"), "Bounds");
        var wallet = ((List<Achievement>)Get(form, "rules")).Single(a => a.Id == "wallet-10");
        var footer = (RectangleF)Call(form, "FooterRect", tile, wallet);
        float scale = (float)Get(form, "scale");
        long sum = 0;
        for (int y = (int)(footer.Y * scale); y < (int)(footer.Bottom * scale); y++)
            for (int x = (int)(footer.X * scale); x < (int)(footer.Right * scale); x++)
                sum += bitmap.GetPixel(x, y).ToArgb();
        return sum;
    }
    static void ScrollChecks(MainForm form, string output)
    {
        Set(form, "categoryFilter", 6);
        Set(form, "introStart", -10f); Set(form, "hovered", null); Set(form, "focused", null);
        foreach (var item in ((IDictionary)Get(form, "widgets")).Values) { Set(item, "Hover", 0f); Set(item, "Press", 0f); }
        Call(form, "ClearTileImages");
        using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            RenderScroll(form, graphics, 0);
            int builds = (int)Get(form, "tileImageBuilds");
            Check(builds > 0, "Viewport cards populate the image cache");
            RenderScroll(form, graphics, 1);
            Check((int)Get(form, "tileImageBuilds") == builds, "Scrolling within the same rows reuses card images");
            var walletBounds = (RectangleF)Get(Widget(form, "tile:wallet-10"), "Bounds");
            var walletGrid = (RectangleF)Get(form, "gridRect");
            float walletOffset = walletBounds.Top - walletGrid.Top - 12 + (float)Get(form, "scroll");
            Set(form, "current", new GameSnapshot { Rupees = 1, Health = 12, MaxHealth = 12, Utc = DateTime.UtcNow });
            RenderScroll(form, graphics, walletOffset); long first = FooterPixels(form, bitmap);
            builds = (int)Get(form, "tileImageBuilds");
            Set(form, "current", new GameSnapshot { Rupees = 9, Health = 12, MaxHealth = 12, Utc = DateTime.UtcNow });
            RenderScroll(form, graphics, walletOffset);
            Check(first != FooterPixels(form, bitmap) && (int)Get(form, "tileImageBuilds") == builds,
                "Live progress redraws while static card images remain cached");
            var saved = new ProgressStore(Path.Combine(output, "cache-unlock"), "test", "synthetic");
            saved.Award("wallet-10", DateTime.UtcNow); Set(form, "progress", saved);
            RenderScroll(form, graphics, walletOffset);
            var entry = ((IDictionary)Get(form, "tileImages"))["wallet-10"];
            Check((bool)Get(entry, "Unlocked"), "An unlock replaces the cached locked medal immediately");
            Set(form, "progress", null); Set(form, "current", null);
            Set(form, "categoryFilter", 0); Call(form, "Arrange");
            var grid = (RectangleF)Get(form, "gridRect");
            float end = (float)Get(form, "contentHeight") - grid.Height;
            double[] times = new double[80];
            for (int i = 0; i < times.Length; i++)
            {
                var watch = Stopwatch.StartNew();
                RenderScroll(form, graphics, end * i / (times.Length - 1));
                times[i] = watch.Elapsed.TotalMilliseconds;
            }
            Check(((IDictionary)Get(form, "tileImages")).Count <= (int)Get(form, "tileCacheBudget") && (int)Get(form, "tileCacheBudget") <= 400, "Long scrolling keeps the native bitmap cache within its memory budget");
            var gridNow = (RectangleF)Get(form, "gridRect");
            int inView = ((IEnumerable)Get(form, "visible")).Cast<object>().Count(v => Get(v, "Achievement") != null && ((RectangleF)Get(v, "Bounds")).IntersectsWith(gridNow));
            Check((int)Get(form, "tileCacheBudget") >= inView * 2, "Card cache budget covers twice the cards in view (fullscreen/ultrawide)");
            RenderScroll(form, graphics, end);
            int steady = (int)Get(form, "tileImageBuilds");
            RenderScroll(form, graphics, end - 1);
            Check((int)Get(form, "tileImageBuilds") == steady, "Repainting a scrolled view rebuilds no card");
            float formScale = (float)Get(form, "scale");
            var full = new RectangleF(0, 0, form.ClientSize.Width / formScale, form.ClientSize.Height / formScale);
            graphics.ResetTransform(); graphics.ResetClip(); Call(form, "Render", graphics, full);
            int layerBuilds = (int)Get(form, "layerBuilds");
            graphics.ResetTransform(); graphics.ResetClip(); Call(form, "Render", graphics, full);
            Check((int)Get(form, "layerBuilds") == layerBuilds, "Bars, tab track and banner overlay are reused between frames");
            var last = Widget(form, "tile:" + ((List<Achievement>)Call(form, "Filtered")).Last().Id);
            Check(((RectangleF)Get(last, "Bounds")).IntersectsWith(grid), "The complete catalog remains reachable by scrolling");
            Array.Sort(times);
            Console.WriteLine("SCROLL 110 cards / 80 grid frames: median={0:F2} ms p95={1:F2} ms", times[40], times[76]);
            Call(form, "ClearTileImages");
            Check(((IDictionary)Get(form, "tileImages")).Count == 0, "Clearing the image cache releases all entries");
            Set(form, "introStart", (float)((Stopwatch)Get(form, "clock")).Elapsed.TotalSeconds - 0.6f);
            RenderScroll(form, graphics, end);
            Check(((IDictionary)Get(form, "tileImages")).Count > 0,
                "Deep scrolling during the intro does not delay cards by their catalog index");
            Set(form, "introStart", -10f);
        }
        Set(form, "scroll", 0f); Set(form, "scrollTarget", 0f);
    }
    static void ToolbarChecks(MainForm form, string output)
    {
        var rules = (List<Achievement>)Get(form, "rules");
        var search = (TextBox)Get(form, "search");
        int unlockedBefore = (int)form.GetType().GetProperty("UnlockedCount", Private).GetValue(form, null);
        // Search: case and accent insensitive, across title/description/category/chapter/rarity.
        search.Text = "FORTEZZA"; Settle();
        var found = (List<Achievement>)Call(form, "Filtered");
        Check(found.Count > 0 && found.Count < rules.Count && found.All(a => (a.Title + a.Description + a.Category + a.Chapter).ToLowerInvariant().Contains("fortezza")),
            "Search filters case-insensitively by name, description, category and chapter");
        Call(form, "Arrange");
        Check((string)Get(Widget(form, "tab0"), "Glyph") == found.Count.ToString(), "Status tab counts follow the search");
        search.Text = "eredita"; Settle();
        Check(((List<Achievement>)Call(form, "Filtered")).Any(a => a.Id == "story-zelda"), "Search ignores accents (eredita finds the Zelda objective)");
        search.Text = "sword"; Settle();
        Check(((List<Achievement>)Call(form, "Filtered")).Any(a => a.Id == "story-hero-sword"), "Search knows English item names (sword finds the Hero's Sword)");
        search.Text = "boss"; Settle();
        var bosses = ((List<Achievement>)Call(form, "Filtered")).Select(a => a.Id).ToList();
        Check(new[] { "story-din-pearl", "story-farore-pearl", "story-gohdan", "story-helmaroc", "story-jalhalla", "story-molgera", "story-puppet-ganon", "story-ganondorf" }.All(bosses.Contains),
            "Search finds every boss objective by keyword (" + bosses.Count + " results)");
        search.Text = "zattera volante"; Settle(); Call(form, "Arrange");
        Check(((List<Achievement>)Call(form, "Filtered")).Count == 0 && (bool)Get(Widget(form, "empty-reset"), "Visible"),
            "A search without results shows the empty state with a reset action");
        form.SavePreview(Path.Combine(output, "search-empty.png"));
        Click(form, "empty-reset");
        Check(search.TextLength == 0 && ((List<Achievement>)Call(form, "Filtered")).Count == rules.Count, "Empty-state reset restores the full catalog");
        // Ordering: presentation only.
        Set(form, "sortMode", 1);
        var byRarity = (List<Achievement>)Call(form, "Filtered");
        Check(byRarity.Zip(byRarity.Skip(1), (x, y) => (int)x.Rarity >= (int)y.Rarity).All(ok => ok), "Rarity order puts rarer medals first");
        Set(form, "sortMode", 3);
        var byName = (List<Achievement>)Call(form, "Filtered");
        Check(byName.Count == rules.Count && String.Compare(byName.First().Title, byName.Last().Title, StringComparison.CurrentCultureIgnoreCase) < 0, "Name order is alphabetical and keeps every objective");
        Key(form, Keys.S);
        Check((int)Get(form, "sortMode") == 4, "S cycles the sort order");
        Call(form, "Arrange");
        Check((bool)Get(Widget(form, "reset-filters"), "Visible"), "A non-default order or filter offers Azzera");
        Click(form, "reset-filters");
        Check((int)Get(form, "sortMode") == 0 && (int)Get(form, "categoryFilter") == 0 && ((List<Achievement>)Call(form, "Filtered")).First().Id == "story-heros-clothes",
            "Azzera restores playthrough order and all categories");
        Check((int)form.GetType().GetProperty("UnlockedCount", Private).GetValue(form, null) == unlockedBefore, "Search, menus and ordering never change unlock state");
        // Rarity, detection and image filters share one grouped menu and combine with the rest.
        Call(form, "OpenMenu", "more"); Settle(); Call(form, "Arrange");
        var entries = ((IEnumerable)Get(form, "visible")).Cast<object>().Where(v => ((string)Get(v, "Id")).StartsWith("menu:")).ToList();
        Check(entries.Count == 12 && entries.Any(v => (string)Get(v, "Name") == "Immagine: Senza icona"), "Altri filtri lists rarity, detection and image choices in labelled groups");
        form.SavePreview(Path.Combine(output, "menu-more.png"));
        Click(form, (string)Get(entries.Single(v => (string)Get(v, "Name") == "Rarità: Oro"), "Id")); Settle();
        var gold = (List<Achievement>)Call(form, "Filtered");
        Check(gold.Count == 10 && gold.All(a => a.Rarity == AchievementRarity.Gold), "The rarity filter keeps the ten gold objectives");
        Set(form, "detectionFilter", 2);
        Check(((List<Achievement>)Call(form, "Filtered")).All(a => a.Rarity == AchievementRarity.Gold && a.Experimental && !a.IsPlanned), "Rarity and detection filters combine");
        Key(form, Keys.R);
        Check((int)Get(form, "rarityFilter") == 4, "R cycles the rarity filter");
        Call(form, "ResetFilters"); Set(form, "iconFilter", 2);
        Check(((List<Achievement>)Call(form, "Filtered")).Count == 0, "Immagine: Senza icona is empty now that all 110 objectives have personalized medals");
        Set(form, "iconFilter", 1);
        Check(((List<Achievement>)Call(form, "Filtered")).Count == rules.Count, "Immagine: Personalizzate covers the whole catalog");
        Call(form, "ResetFilters"); Settle();
        // Menus by keyboard: focus lands on the current entry; Escape closes and returns to the button.
        Set(form, "focusVisible", true); Set(form, "focused", Widget(form, "chapter"));
        Call(form, "OpenMenu", "chapter"); Settle();
        var focusedItem = Get(form, "focused");
        Check(focusedItem != null && (string)Get(focusedItem, "Id") == "menu:0" && (bool)Get(focusedItem, "Checked"), "Keyboard-opened menu focuses its selected entry");
        Key(form, Keys.Down); Key(form, Keys.Enter); Settle();
        Check((int)Get(form, "chapterFilter") == 1 && (float)Get(form, "menuTarget") == 0, "Arrow keys and Enter choose a menu entry");
        Call(form, "OpenMenu", "category"); Settle(); Key(form, Keys.Escape); Settle();
        Check((float)Get(form, "menuTarget") == 0 && Get(form, "menu") == null && (string)Get(Get(form, "focused"), "Id") == "category", "Escape closes a menu and returns focus to its button");
        Call(form, "OpenMenu", "sort"); Settle();
        var outside = new MouseEventArgs(MouseButtons.Left, 1, 4, (int)(((RectangleF)Get(form, "gridRect")).Bottom * (float)Get(form, "scale")) - 4, 0);
        Call(form, "OnMouseDown", outside); Call(form, "OnMouseUp", outside); Settle();
        Check((float)Get(form, "menuTarget") == 0, "Clicking outside a menu closes it without acting");
        Set(form, "focusVisible", false); Set(form, "focused", null);
        Call(form, "ResetFilters"); Settle();
        int children = form.AccessibilityObject.GetChildCount(); bool dropdown = false;
        for (int i = 0; i < children; i++)
        {
            var item = form.AccessibilityObject.GetChild(i);
            if (item.Role == AccessibleRole.ButtonDropDown && item.Name.StartsWith("Categoria")) dropdown = item.Name.Contains("Tutte");
        }
        Check(dropdown, "Menus reach accessibility clients as drop-down buttons with their value");
    }
    static void DetailChecks(MainForm form, string output)
    {
        var list = (List<Achievement>)Call(form, "Filtered");
        Click(form, "tile:" + list[0].Id); Settle();
        Check(!(bool)Get(Widget(form, "detail-prev"), "Enabled") && (bool)Get(Widget(form, "detail-next"), "Enabled"), "Detail browsing is bounded at the first objective");
        Key(form, Keys.Right);
        Check(((Achievement)Get(form, "detail")).Id == list[1].Id, "Right arrow shows the next objective in the current order");
        Click(form, "detail-prev");
        Check(((Achievement)Get(form, "detail")).Id == list[0].Id, "The side arrow returns to the previous objective");
        var planned = list.First(a => a.IsPlanned);
        Set(form, "detail", planned); Settle();
        form.SavePreview(Path.Combine(output, "detail-planned.png"));
        Key(form, Keys.Escape); Settle();
        Check((float)Get(form, "dialogTarget") == 0, "Escape closes the detail card");
        var states = list.Select(a => Call(form, "StateOf", a, null).ToString()).Distinct().ToList();
        Check(states.Contains("Locked") && states.Contains("Experimental") && states.Contains("Planned"), "Locked, experimental and planned objectives map to distinct card states");
        var stateType = typeof(MainForm).GetNestedType("CardState", BindingFlags.NonPublic);
        var labels = new[] { "Unlocked", "Locked", "Experimental", "Planned" }.Select(n => (string)typeof(MainForm).GetMethod("StateLabel", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new[] { Enum.Parse(stateType, n) })).ToList();
        Check(labels.Distinct().Count() == 4 && labels.All(l => l.Length > 4), "Every state has its own words (and icon), not only a colour");
    }
    static void LayoutChecks(MainForm form, string output)
    {
        Size original = form.ClientSize;
        float scale = (float)Get(form, "scale");
        foreach (var size in new[] { new Size(900, 680), new Size(1120, 800), new Size(1920, 1080), new Size(2560, 1080) })
        {
            form.ClientSize = new Size((int)(size.Width * scale), (int)(size.Height * scale)); Settle();
            if (form.ClientSize.Width < (int)(size.Width * scale) - 2) { Console.WriteLine("INFO window cannot reach " + size + " on this desktop"); continue; }
            Call(form, "Arrange");
            var ids = new[] { "tab0", "tab1", "tab2", "tab3", "category", "chapter", "sort", "search" };
            var boxes = ids.Select(id => (RectangleF)Get(Widget(form, id), "Bounds")).ToArray();
            var toolbar = (RectangleF)Get(form, "toolbarRect");
            bool inside = boxes.All(b => toolbar.Contains(b) && b.Right <= size.Width - 20 + 0.5f);
            bool separate = !boxes.Where((b, i) => boxes.Skip(i + 1).Any(o => RectangleF.Intersect(b, o).Width > 0.5f && RectangleF.Intersect(b, o).Height > 0.5f)).Any();
            Check(inside && separate, "Toolbar controls fit without overlap at " + size.Width + "x" + size.Height);
            var grid = (RectangleF)Get(form, "gridRect");
            var banner = (RectangleF)Get(form, "bannerRect");
            Check(banner.Bottom < toolbar.Y && toolbar.Bottom < grid.Y && grid.Height >= 150, "Banner, toolbar and grid stack without overlap at " + size.Width + "x" + size.Height + " (grid " + grid.Height + ")");
            var tiles = ((IEnumerable)Get(form, "visible")).Cast<object>().Where(v => Get(v, "Achievement") != null).Select(v => (RectangleF)Get(v, "Bounds")).Take(30).ToArray();
            Check(tiles.All(t => t.Width >= 340 && t.Right <= size.Width - 8), "Cards keep a readable width at " + size.Width + "x" + size.Height);
            form.SavePreview(Path.Combine(output, "layout-" + size.Width + ".png"));
        }
        form.ClientSize = original; Settle();
    }
    static void TextFitChecks(MainForm form)
    {
        // Every revised title and description fits whole on the narrowest card and in the detail card.
        var rules = (List<Achievement>)Get(form, "rules");
        var misfits = rules.Where(a => !(bool)Call(form, "CardTextFits", a, 340f)).Select(a => a.Id).ToList();
        Check(misfits.Count == 0, "All 110 titles and descriptions fit the narrowest card (" + String.Join(", ", misfits) + ")");
        float titleW = (float)typeof(MainForm).GetField("DetailTitleW", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        float textW = (float)typeof(MainForm).GetField("DetailTextW", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        using (var bitmap = new Bitmap(1, 1))
        using (var g = Graphics.FromImage(bitmap))
        {
            var detailMisfits = rules.Where(a => Theme.Measure(g, a.Title, (Font)Call(form, "DetailTitleFont", a)).Width > titleW ||
                Theme.Measure(g, a.Description, Theme.Font(15.5f, Weight.Regular), textW).Height > 70 ||
                Theme.Measure(g, a.IsPlanned ? a.Requirement : (string)Call(form, "DetailStatus", a), Theme.Font(13.5f, Weight.Regular), textW).Height > 78).Select(a => a.Id).ToList();
            Check(detailMisfits.Count == 0, "All 110 titles, descriptions and requirements fit the detail card (" + String.Join(", ", detailMisfits) + ")");
        }
    }
    static void MotionChecks(MainForm form, string output)
    {
        bool before = Motion.Reduced;
        Motion.Reduced = true;
        Call(form, "OpenSheet"); Settle();
        Check((float)Get(form, "sheet") == 1 && ((RectangleF)Get(form, "sheetRect")).Right <= form.ClientSize.Width / (float)Get(form, "scale") + 0.5f, "Reduced motion opens settings without a slide");
        string settings = Path.Combine(output, "app-data", "settings.xml");
        Click(form, "motion");
        Check(!Motion.Reduced && File.ReadAllText(settings).Contains("<motion>full</motion>"), "The movement toggle is saved with the other UI settings");
        Click(form, "motion");
        Check(Motion.Reduced && File.ReadAllText(settings).Contains("<motion>reduced</motion>"), "Reduced motion can be chosen explicitly");
        Call(form, "CloseSheet"); Settle();
        var rules = (List<Achievement>)Get(form, "rules");
        var gold = rules.Single(a => a.Id == "hearts-20");
        var art = Art.For(gold, rules, GameCatalog.WindWakerId);
        using (var early = ToastForm.Render(gold, art, "OBIETTIVO SBLOCCATO", ToastForm.TurnStart - 0.05f, 1))
        using (var late = ToastForm.Render(gold, art, "OBIETTIVO SBLOCCATO", 1.6f, 1))
        {
            Func<Bitmap, double> medal = b => { using (var crop = b.Clone(new Rectangle(34 + 18, 34 + 14, 96, 96), b.PixelFormat)) return Saturation(crop); };
            Check(medal(early) < medal(late) * 0.5, "With reduced motion the popup medal still turns from grey to colour (without flipping)");
            late.Save(Path.Combine(output, "toast-reduced.png"));
        }
        Set(form, "introStart", (float)((Stopwatch)Get(form, "clock")).Elapsed.TotalSeconds);
        Call(form, "SetFilter", 2); Call(form, "SetFilter", 0);
        Check((float)Get(form, "introStart") < 0, "Reduced motion skips the staggered card entrance");
        Motion.Reduced = before;
        Set(form, "motionChoice", null); Call(form, "TrySaveSettings");
        Check(!File.ReadAllText(settings).Contains("<motion>"), "Without an explicit choice the movement setting follows Windows");
    }
    static void PartialPaintChecks(MainForm form, string output)
    {
        Size originalSize = form.ClientSize;
        float originalScale = (float)Get(form, "scale");
        Set(form, "introStart", -10f); Set(form, "hovered", null); Set(form, "focused", null);
        foreach (float dpi in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            Set(form, "scale", dpi);
            form.ClientSize = new Size((int)(1120 * dpi), (int)(800 * dpi));
            Call(form, "ClearLayers"); Call(form, "ClearTileImages"); Call(form, "Arrange");
            Set(form, "tabX", -1f);
            using (var expected = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                using (var g = Graphics.FromImage(expected)) Call(form, "Render", g, new RectangleF(0, 0, 1120, 800));
                using (var actual = new Bitmap(expected))
                {
                    // These strips start below the old DrawTabs guard. Repainting them used
                    // to erase a horizontal band through the buttons and the adjacent help.
                    var regions = new List<RectangleF>();
                    foreach (string id in new[] { "category", "chapter", "sort", "search", "tab2" })
                    {
                        var r = (RectangleF)Get(Widget(form, id), "Bounds");
                        regions.Add(new RectangleF(r.X + 18, r.Y + 10, r.Width - 24, 14));
                    }
                    var chapter = (RectangleF)Get(Widget(form, "chapter"), "Bounds");
                    regions.Add(new RectangleF(chapter.Right + 10, chapter.Y + 9, 210, 14));
                    foreach (var region in regions)
                        using (var g = Graphics.FromImage(actual))
                        {
                            g.SetClip(new RectangleF(region.X * dpi, region.Y * dpi, region.Width * dpi, region.Height * dpi));
                            Call(form, "Render", g, region);
                        }
                    int changed = 0;
                    foreach (var region in regions)
                        for (int y = (int)(region.Top * dpi) + 1; y < (int)(region.Bottom * dpi) - 1; y++)
                            for (int x = (int)(region.Left * dpi) + 1; x < (int)(region.Right * dpi) - 1; x++)
                            {
                                var a = actual.GetPixel(x, y); var b = expected.GetPixel(x, y);
                                if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 6) changed++;
                            }
                    Check(changed == 0, "Partial toolbar repaints (tabs, menus, search, legend) match a complete frame at " + dpi + " scale (different pixels: " + changed + ")");
                    if (dpi == 1) actual.Save(Path.Combine(output, "partial-filters.png"));
                    if (dpi == 2) actual.Save(Path.Combine(output, "main-200.png"));
                }
            }
        }
        Set(form, "scale", originalScale); form.ClientSize = originalSize;
        Call(form, "ClearLayers"); Call(form, "ClearTileImages"); Call(form, "Arrange");
    }
    // Average saturation (max-min channel spread) of opaque pixels in the central square.
    static double Saturation(Bitmap bitmap)
    {
        long sum = 0, count = 0;
        int lo = bitmap.Width / 3, hi = bitmap.Width * 2 / 3;
        for (int y = lo; y < hi; y++)
            for (int x = lo; x < hi; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.A < 200) continue;
                sum += Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)); count++;
            }
        return count == 0 ? 0 : sum / (double)count;
    }
    static void ArtChecks(MainForm form, string output)
    {
        var rules = (List<Achievement>)Get(form, "rules");
        string prefix = "WiiUAchievements.Games." + GameCatalog.WindWakerId + ".";
        var resources = typeof(MainForm).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix)).ToList();
        Check(BrandArt.Available && typeof(MainForm).Assembly.GetManifestResourceNames().Contains("WiiUAchievements.Brand.logo.png"),
            "Owner project logo and trophy icon are embedded independently of game art");
        Check(resources.Contains(prefix + "logo.png") && resources.Contains(prefix + "badge.png") && resources.Count(n => n.StartsWith(prefix + "ui.")) == 40,
            "Both Wind Waker logos and all 40 prepared UI ornaments/sprites are embedded");
        using (var neutral = new Bitmap(160, 80))
        using (var g = Graphics.FromImage(neutral))
            Check(!GameStyle.Neutral.DrawArt(g, "ui/wind-ribbon.png", new RectangleF(0, 0, 160, 80), 1) &&
                !GameStyle.Neutral.DrawArt(g, "logo.png", new RectangleF(0, 0, 160, 80), 1),
                "Wind Waker ornaments and logos cannot draw on neutral/future-game chrome");
        var icons = resources.Where(n => n.StartsWith(prefix + "icons.")).Select(n => n.Substring(prefix.Length + 6, n.Length - prefix.Length - 10)).ToList();
        Check(icons.Count == 110 && icons.All(id => rules.Any(a => a.Id == id)), "All 110 personalized icons are embedded under existing achievement IDs");
        Check(new[] { "frame-bronze.png", "frame-silver.png", "frame-gold.png", "frame-platinum.png", "sky.png" }.All(n => resources.Contains(prefix + n)),
            "Every rarity frame and the shared sky are embedded");
        var arts = rules.ToDictionary(a => a.Id, a => Art.For(a, rules, GameCatalog.WindWakerId));
        Check(rules.All(a => arts[a.Id].HasIcon || a.Field == "rupees" || a.Field == "max_health" || Glyphs.HasExplicit(GameCatalog.WindWakerId, a.Id)),
            "Every Wind Waker objective has its own icon or an explicit glyph");
        Check(icons.All(id => arts[id].HasIcon) && arts.Values.Count(a => a.HasIcon) == 110, "Personalized icons cover every matching objective");
        Check(rules.All(a => arts[a.Id].Tier == (Tier)a.Rarity), "Medal frame rarity follows the curated rarity");
        foreach (var id in new[] { "wallet-10", "story-zelda", "story-molgera" })
            using (var color = Medals.Render(arts[id], 96, false))
            using (var grey = Medals.Render(arts[id], 96, true))
                Check(Saturation(color) > 40 && Saturation(grey) < Saturation(color) * 0.4, "Locked medal is a muted version of the coloured one: " + id);
        using (var crown = Medals.Render(arts["story-zelda"], 96, false))
        using (var bomb = Medals.Render(arts["gear-bombs"], 96, false))
        {
            long diff = 0;
            for (int y = 32; y < 64; y++)
                for (int x = 32; x < 64; x++)
                {
                    Color p = crown.GetPixel(x, y), q = bomb.GetPixel(x, y);
                    diff += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
                }
            Check(diff / 1024 > 30, "Different objectives draw different items inside the frame");
        }
        int before = Medals.Builds;
        Medals.Render(arts["story-zelda"], 96, false).Dispose();
        var g1 = Medals.Get(arts["story-zelda"], 112, false); var g2 = Medals.Get(arts["story-zelda"], 112, false);
        Check(ReferenceEquals(g1, g2) && Medals.Builds == before + 1, "Medal bitmaps are cached per size and state");
        foreach (var a in rules) foreach (int size in new[] { 32, 48 }) Medals.Get(arts[a.Id], size, true);
        Check(Medals.Cached <= 160, "Medal cache stays bounded");
        var gold = rules.Single(a => a.Id == "hearts-20");
        using (var early = ToastForm.Render(gold, arts[gold.Id], "OBIETTIVO SBLOCCATO", ToastForm.TurnStart - 0.05f, 1))
        using (var late = ToastForm.Render(gold, arts[gold.Id], "OBIETTIVO SBLOCCATO", 1.6f, 1))
        {
            Func<Bitmap, double> medal = b => { using (var crop = b.Clone(new Rectangle(34 + 18, 34 + 14, 96, 96), b.PixelFormat)) return Saturation(crop); };
            Check(medal(early) < medal(late) * 0.5, "Popup medal arrives grey and turns to colour");
            late.Save(Path.Combine(output, "toast-gold.png"));
        }
        Check(resources.Contains(prefix + "banner.mp4") && GameStyle.WindWaker.Themed && !GameStyle.Neutral.Themed,
            "Wind Waker has an embedded banner video and its own chrome; the system style stays neutral");
        var styleProperty = typeof(MainForm).GetProperty("Style", Private);
        Check(styleProperty.GetValue(form, null) == GameStyle.WindWaker, "The objectives section is dressed in the game style");
        Set(form, "libraryOpen", true);
        Check(styleProperty.GetValue(form, null) == GameStyle.Neutral, "The Games catalog keeps the neutral system style");
        Set(form, "libraryOpen", false);
        var video = typeof(MainForm).GetProperty("BannerVideo", Private).GetValue(form, null) as VideoBanner;
        var raw = (VideoBanner)Get(form, "video");
        for (int i = 0; i < 300 && raw != null && !raw.HasFrame && !raw.Failed; i++) Thread.Sleep(10);
        // Media Foundation may be absent (Mono, Windows N/Server): then the drawn sea is used.
        Check(raw == null || raw.Failed ? video == null : raw.HasFrame, "Banner video decodes a frame, or falls back to the drawn sea");
        Console.WriteLine("INFO banner video: " + (raw == null ? "not available" : raw.Failed ? "fallback (" + raw.Error + ")" : "decoded " + raw.Frames + " frame(s)"));
        form.SavePreview(Path.Combine(output, "themed.png"));
        var lengths = new[] { Tier.Bronze, Tier.Silver, Tier.Gold, Tier.Platinum }.Select(t => Chime.Build(t)).ToList();
        Check(lengths.All(b => b.Length > 44 && b[0] == 'R' && b[8] == 'W') && lengths.Zip(lengths.Skip(1), (x, y) => y.Length > x.Length).All(x => x),
            "Each rarity has its own synthesised chime, longer for rarer medals");
    }
    static void AutomaticWaitingChecks(MainForm form, string output)
    {
        var progress = new ProgressStore(Path.Combine(output, "automatic-wait"), "isolated", "synthetic");
        var engine = new AchievementEngine((List<Achievement>)Get(form, "rules"), progress);
        engine.Update(new GameSnapshot { Utc = DateTime.UtcNow, Context = new GameplayContext(GameplayState.Menu, "Name", 0) });
        Set(form, "engine", engine); Set(form, "progress", progress); Set(form, "recording", true);
        Call(form, "SetRecordingStatus");
        Check((string)Get(form, "statusTitle") == "Attesa automatica" && (bool)Get(form, "recording") && Get(form, "current") == null,
            "Automatic load wait preserves recording intent and clears stale displayed values");
        Set(form, "autoReconnect", true); Call(form, "Arrange");
        Check(!(bool)Get(Widget(form, "pause"), "Enabled") && (bool)Get(Widget(form, "disconnect"), "Enabled"),
            "During reconnect the unavailable pause is disabled and Scollega can cancel the watcher");
        Set(form, "connecting", true);
        Call(form, "Disconnect", "Synthetic automatic reconnect cancellation");
        Check(!(bool)Get(form, "autoReconnect") && !(bool)Get(form, "recording") && !(bool)Get(form, "connecting"),
            "Explicit disconnect cancels automatic reconnect intent");
        Set(form, "engine", null); Set(form, "progress", null);
    }
    static void CloudChecks(MainForm form, string output)
    {
        Call(form, "OpenSheet"); Settle();
        string settingsPreview = Path.Combine(output, "settings-cloud-entry.png");
        form.SavePreview(settingsPreview);
        var accountBounds = (RectangleF)Get(Widget(form, "cloud-page"), "Bounds");
        float previewScale = (float)Get(form, "scale");
        using (var preview = new Bitmap(settingsPreview))
        {
            int ink = 0;
            for (int y = (int)((accountBounds.Top + 10) * previewScale); y < (int)((accountBounds.Bottom - 10) * previewScale); y++)
                for (int x = (int)((accountBounds.Left + 12) * previewScale); x < (int)((accountBounds.Right - 12) * previewScale); x++)
                {
                    Color pixel = preview.GetPixel(x, y);
                    if (pixel.R < 160 && pixel.G < 160 && pixel.B < 160) ink++;
                }
            Check(ink > 30, "Account cloud entry is visibly painted in settings before it can be clicked");
        }
        Click(form, "cloud-page");
        Check(((TextBox)Get(form, "cloudEmail")).Visible && !((TextBox)Get(form, "player")).Visible, "Cloud account has a separate settings page");
        Check((bool)Get(Widget(form, "cloud-login"), "Enabled") && (bool)Get(Widget(form, "cloud-signup"), "Enabled") && !(bool)Get(Widget(form, "cloud-logout"), "Enabled"), "Signed-out account offers login/signup and disables logout");
        float scale = (float)Get(form, "scale"); form.ClientSize = new Size((int)(900 * scale), (int)(680 * scale)); Settle();
        var ids = new[] { "field-project", "field-apikey", "field-email", "field-pass", "cloud-signup", "cloud-login", "cloud-logout" };
        var boxes = ids.Select(id => (RectangleF)Get(Widget(form, id), "Bounds")).ToArray();
        Check(boxes.All(b => b.Bottom < 680) && !boxes.Where((b, i) => boxes.Skip(i + 1).Any(b.IntersectsWith)).Any(), "All cloud controls fit without overlap at minimum size");
        Set(form, "demo", true); Call(form, "PlaceFields");
        Check(!((TextBox)Get(form, "cloudEmail")).ReadOnly && !((TextBox)Get(form, "cloudPassword")).ReadOnly, "Account fields remain editable during a game/demo connection");
        Set(form, "demo", false);
        var password = (TextBox)Get(form, "cloudPassword"); password.Text = "SYNTHETIC_SECRET";
        var display = typeof(MainForm).GetMethod("FieldDisplayText", BindingFlags.Static | BindingFlags.NonPublic);
        Check(!((string)display.Invoke(null, new object[] { password })).Contains("SECRET"), "Animation fallback masks the password without the native textbox");
        form.SavePreview(Path.Combine(output, "cloud-small.png"));
        var gate = new ManualResetEvent(false); var entered = new ManualResetEvent(false);
        Func<string, string, string, string, string> transport = (method, url, body, token) =>
        {
            entered.Set(); gate.WaitOne(5000);
            return "{ \"idToken\": \"SYNTHETIC_ID\", \"refreshToken\": \"SYNTHETIC_REFRESH\", \"localId\": \"synthetic_user\", \"expiresIn\": \"3600\" }";
        };
        var constructor = typeof(CloudSync).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        Set(form, "cloud", constructor.Invoke(new object[] { Path.Combine(output, "cloud-ui-fixture"), transport }));
        ((TextBox)Get(form, "cloudProject")).Text = "synthetic-project"; ((TextBox)Get(form, "cloudApiKey")).Text = "SYNTHETIC_KEY";
        ((TextBox)Get(form, "cloudEmail")).Text = "synthetic@example.invalid";
        Call(form, "CloudLogin");
        try
        {
            bool reached = entered.WaitOne(2000);
            Check(reached && (bool)Get(form, "cloudBusy") && password.TextLength == 0, "Account request runs in background and clears the password immediately (request=" + reached +
                ", busy=" + Get(form, "cloudBusy") + ", password=" + password.TextLength + ", message=" + Get(form, "cloudMessage") + ")");
            int ticks = 0;
            using (var heartbeat = new System.Windows.Forms.Timer { Interval = 25 })
            {
                heartbeat.Tick += delegate { ticks++; }; heartbeat.Start(); Settle(); heartbeat.Stop();
                Check(ticks > 3 && (bool)Get(form, "cloudBusy"), "WinForms timers keep delivering while the real account request is pending");
            }
        }
        finally { gate.Set(); }
        WaitCloudIdle(form);
        Check(!(bool)Get(form, "cloudBusy") && ((CloudSync)Get(form, "cloud")).IsSignedIn, "Completed account request resumes UI with authenticated status" + CloudState(form));
        Check(!(bool)Get(Widget(form, "cloud-login"), "Enabled") && !(bool)Get(Widget(form, "cloud-signup"), "Enabled") && (bool)Get(Widget(form, "cloud-logout"), "Enabled") && ((TextBox)Get(form, "cloudEmail")).ReadOnly,
            "Connected account disables login/signup and edits while retaining logout" + CloudState(form));
        string connectedMessage = (string)Get(form, "cloudMessage");
        Call(form, "CloudLogin");
        Check((string)Get(form, "cloudMessage") == connectedMessage && ((CloudSync)Get(form, "cloud")).IsSignedIn, "Repeated login cannot replace an already connected session");
        gate.Dispose(); entered.Dispose();
        WaitCloudIdle(form);
        Call(form, "CloudLogout");
        WaitCloudIdle(form);
        Check((bool)Get(Widget(form, "cloud-login"), "Enabled") && !(bool)Get(Widget(form, "cloud-logout"), "Enabled") && !((TextBox)Get(form, "cloudEmail")).ReadOnly, "Logout restores editable login controls" + CloudState(form));
        Call(form, "CloseSheet"); Settle(); Call(form, "CloseSheet"); Settle();
        Check(password.TextLength == 0 && !password.Visible, "Leaving account settings clears and hides password");
        form.ClientSize = new Size((int)(1120 * scale), (int)(800 * scale));
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string output = args.Length == 0 ? Path.Combine(Path.GetTempPath(), "wiiua-ui-" + Guid.NewGuid().ToString("N")) : Path.GetFullPath(args[0]);
            Directory.CreateDirectory(output);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new MainForm(false, false, Path.Combine(output, "app-data")))
            {
                // This executable is built in an isolated test directory. It never attaches to Cemu.
                Set(form, "soundOn", false); Set(form, "ambientOn", false);
                form.Show(); form.Location = new Point(-20000, -20000); Settle();
                PartialPaintChecks(form, output);
                CloudChecks(form, output);
                AutomaticWaitingChecks(form, output);
                var curated = (List<Achievement>)Get(form, "rules");
                Check(Art.For(curated.Single(a => a.Id == "wallet-5000"), curated).Tier == Tier.Silver &&
                    Art.For(curated.Single(a => a.Id == "hearts-20"), curated).Tier == Tier.Gold,
                    "Medals use curated rarity independently of equal point values");
                form.SavePreview(Path.Combine(output, "main.png"));
                Check((int)form.GetType().GetProperty("ActiveCount", Private).GetValue(form, null) == 70,
                    "Completion denominator contains only executable objectives");
                Click(form, "tab3");
                var pending = (List<Achievement>)Call(form, "Filtered");
                Check(pending.Count == 40 && pending.All(a => a.IsPlanned),
                    "Implemented series targets no longer appear in the unimplemented catalog");
                string pendingId = pending.First().Id;
                Check(((string)Get(Widget(form, "tile:" + pendingId), "Help")).Contains("Da verificare"),
                    "Pending state and validation requirement reach accessibility clients");
                Check(((string)Call(form, "Describe", Widget(form, "tile:" + pendingId))).Contains("nessuno sblocco automatico"),
                    "Unimplemented story targets explicitly announce that automatic unlocks are unavailable");
                form.SavePreview(Path.Combine(output, "pending.png"));
                Click(form, "tile:" + pendingId);
                Check(((Achievement)Get(form, "detail")).Id == pendingId, "Pending objectives have an inspectable detail card");
                form.SavePreview(Path.Combine(output, "pending-detail.png"));
                Key(form, Keys.Escape); Click(form, "category");
                Check((string)Get(form, "menu") == "category" && (int)form.GetType().GetProperty("ActiveLayer", Private).GetValue(form, null) == 3,
                    "The category button opens its menu as a separate input layer");
                Call(form, "Arrange");
                Check(((IEnumerable)Get(form, "visible")).Cast<object>().Count(v => ((string)Get(v, "Id")).StartsWith("menu:")) == AchievementSet.Categories.Count + 1,
                    "The category menu lists every category plus Tutte");
                form.SavePreview(Path.Combine(output, "menu.png"));
                Click(form, "menu:1"); Settle();
                Check(((List<Achievement>)Call(form, "Filtered")).All(a => a.Category == "Storia") && (float)Get(form, "menuTarget") == 0,
                    "Choosing a menu entry filters the pending catalog and closes the menu");
                for (int i = 0; i < AchievementSet.Categories.Count; i++) Key(form, Keys.C);
                Check((int)Get(form, "categoryFilter") == 0, "C cycles all categories and returns to the full catalog");
                Click(form, "tab2");
                Check(((List<Achievement>)Call(form, "Filtered")).All(a => !a.IsPlanned), "Unlockable filter excludes unvalidated objectives");
                Click(form, "category"); Click(form, "menu:1");
                Check(((List<Achievement>)Call(form, "Filtered")).Any(a => a.Id == "story-heros-clothes") &&
                    ((List<Achievement>)Call(form, "Filtered")).Any(a => a.Id == "story-telescope" && a.Experimental),
                    "Story category contains both the confirmed tunic and executable experimental telescope");
                form.SavePreview(Path.Combine(output, "tunic-active.png"));
                Set(form, "categoryFilter", 0);
                Click(form, "tab0");
                Check(((List<Achievement>)Call(form, "Filtered")).First().Id == "story-heros-clothes", "Default ordering begins with the start of the playthrough");
                Click(form, "chapter"); Click(form, "menu:1");
                Check(((List<Achievement>)Call(form, "Filtered")).All(a => a.Chapter == "01 · Isola Primula"), "Chapter filter selects the initial playthrough group");
                Key(form, Keys.F);
                Check(((List<Achievement>)Call(form, "Filtered")).All(a => a.Chapter == "02 · Pirati"), "F advances to the next chronological group");
                Set(form, "chapterFilter", 0);
                ToolbarChecks(form, output);
                DetailChecks(form, output);
                LayoutChecks(form, output);
                TextFitChecks(form);
                MotionChecks(form, output);
                ScrollChecks(form, output);
                ArtChecks(form, output);
                Click(form, "games");
                Check((bool)Get(form, "libraryOpen"), "Mouse opens the Games section");
                Check(!(bool)Get(Widget(form, "game:future-games"), "Enabled"), "Future games show as unavailable and cannot be activated");
                Check(!(bool)Get(Widget(form, "connect"), "Visible"), "Catalog does not expose hidden achievement actions");
                form.SavePreview(Path.Combine(output, "games.png"));
                int children = form.AccessibilityObject.GetChildCount(); bool unavailable = false;
                for (int i = 0; i < children; i++)
                {
                    var item = form.AccessibilityObject.GetChild(i);
                    if (item.Name == "Altri giochi") unavailable = (item.State & AccessibleStates.Unavailable) != 0 && item.Description.Contains("Non disponibile");
                }
                Check(unavailable, "Unavailable state and explanation reach accessibility clients");
                Click(form, "game:future-games");
                Check(((GameDefinition)Get(form, "selectedGame")).Id == GameCatalog.WindWakerId && (bool)Get(form, "libraryOpen"), "Clicking an unavailable card does not change the selected game");
                Key(form, Keys.Tab); Key(form, Keys.Right); Key(form, Keys.Escape);
                Check(!(bool)Get(form, "libraryOpen"), "Escape returns from the Games section");
                Key(form, Keys.G); Click(form, "game:wind-waker-hd");
                Check(!(bool)Get(form, "libraryOpen"), "Selecting the current game opens its objectives");
                Call(form, "StartDemo"); ((System.Windows.Forms.Timer)Get(form, "timer")).Stop();
                for (int i = 0; i < 45; i++) { Call(form, "Tick", null, EventArgs.Empty); Thread.Sleep(2); }
                Check((int)form.GetType().GetProperty("UnlockedCount", Private).GetValue(form, null) == 1, "Demo unlocks the existing objective through the engine");
                Settle(); form.SavePreview(Path.Combine(output, "demo.png"));
                Click(form, "tab3"); form.SavePreview(Path.Combine(output, "pending-live.png"));
                Check((int)form.GetType().GetProperty("UnlockedCount", Private).GetValue(form, null) == 1,
                    "Displaying pending objectives while sampling does not alter unlocks");
                Click(form, "tab0");
                Click(form, "pause");
                Check((bool)Get(form, "paused") && !(bool)Get(form, "recording") && Get(form, "current") == null, "Pause stops recording and clears transient state (paused=" + Get(form, "paused") + ", recording=" + Get(form, "recording") + ", sample=" + (Get(form, "current") != null) + ", sheet=" + Get(form, "sheetTarget") + ", dialog=" + Get(form, "dialogTarget") + ")");
                Check((int)form.GetType().GetProperty("UnlockedCount", Private).GetValue(form, null) == 1, "Pause preserves earned objectives");
                form.SavePreview(Path.Combine(output, "paused.png"));
                Click(form, "games"); Key(form, Keys.B);
                Check((bool)Get(form, "paused") && !(bool)Get(form, "libraryOpen"), "Browsing games does not resume a paused session");
                Key(form, Keys.P); ((System.Windows.Forms.Timer)Get(form, "timer")).Stop();
                Check(!(bool)Get(form, "paused") && (bool)Get(form, "recording"), "P resumes recording with a fresh baseline");
                Call(form, "Tick", null, EventArgs.Empty);
                var engine = (AchievementEngine)Get(form, "engine");
                Check(engine.Status("wallet-10").Phase == AchievementPhase.Unlocked, "Resume retains the already saved unlock");
                Click(form, "games");
                Check((bool)Get(form, "recording"), "Opening the catalog does not stop an active session");
                var syntheticGame = new GameDefinition("test-game", "Synthetic test game", "Test only", "achievements.xml", AchievementSet.Revision, GameCatalog.Default.Profiles[0]);
                Call(form, "SelectGame", syntheticGame);
                Check(((GameDefinition)Get(form, "selectedGame")).Id == GameCatalog.WindWakerId, "Switching to another game is blocked during a session");
                float scale = (float)Get(form, "scale");
                form.ClientSize = new Size((int)(900 * scale), (int)(680 * scale)); Settle();
                form.SavePreview(Path.Combine(output, "games-small.png"));
                var viewport = (RectangleF)Get(form, "libraryGridRect");
                var tile = (RectangleF)Get(Widget(form, "game:wind-waker-hd"), "Bounds");
                Check(viewport.Contains(tile), "Game card fits at the supported minimum window size");
                Key(form, Keys.Escape); form.SavePreview(Path.Combine(output, "main-small.png"));
                Check(((RectangleF)Get(Widget(form, "tab3"), "Bounds")).Right < 900,
                    "All four status tabs fit at the minimum window width");
                Click(form, "tab3"); form.SavePreview(Path.Combine(output, "pending-small.png"));
                Click(form, "tile:" + pendingId); form.SavePreview(Path.Combine(output, "pending-detail-small.png")); Key(form, Keys.Escape);
                Click(form, "tab0");
                Click(form, "settings"); Settle();
                Check(((TextBox)Get(form, "player")).Visible, "Existing settings fields still open");
                form.SavePreview(Path.Combine(output, "settings-small.png"));
                Key(form, Keys.Escape); Key(form, Keys.Y);
                Check(!(bool)Get(form, "recording") && !(bool)Get(form, "demo") && Get(form, "progress") == null, "Disconnect discards demo state without affecting real progress");
                Call(form, "SelectGame", syntheticGame);
                Check(((GameDefinition)Get(form, "selectedGame")).Id == "test-game" && form.Text.Contains("Synthetic test game"), "A future registered game's rules can be selected after disconnect");
                var badGame = new GameDefinition("bad-game", "Missing set", "Test only", "missing.xml", AchievementSet.Revision, GameCatalog.Default.Profiles[0]);
                bool rejected = false;
                try { Call(form, "SelectGame", badGame); } catch (TargetInvocationException ex) { rejected = ex.InnerException is IOException; }
                Check(rejected && ((GameDefinition)Get(form, "selectedGame")).Id == "test-game", "A broken future set leaves the previous selection intact");
                var syntheticProgress = new ProgressStore(Path.Combine(output, "profile-test"), "old-player", "test-game");
                syntheticProgress.Award("wallet-10", DateTime.UtcNow); Set(form, "progress", syntheticProgress);
                ((TextBox)Get(form, "player")).Text = "Synthetic new profile";
                Check(Get(form, "progress") == null, "Editing the local profile clears another profile's displayed unlocks");
                form.Close();
            }
            using (var restored = new MainForm(false, false, Path.Combine(output, "app-data")))
                Check(((GameDefinition)Get(restored, "selectedGame")).Id == GameCatalog.WindWakerId, "Unknown saved catalog IDs safely fall back to the supported default");
            string persistentRoot = Path.Combine(output, "startup-progress");
            var profile = GameCatalog.Default.Profiles[0];
            var archived = new ProgressStore(Path.Combine(persistentRoot, "progress"), "Giocatore 1", profile.TitleId + "|" + profile.TitleVersion + "|" + profile.RpxHash, GameCatalog.Default.SetRevision);
            DateTime original = DateTime.UtcNow.AddDays(-2);
            archived.AwardMany(new[] { "story-heros-clothes", "story-telescope" }, original);
            using (var offline = new MainForm(false, false, persistentRoot))
            {
                var loaded = (ProgressStore)Get(offline, "progress");
                Check(loaded != null && loaded.Contains("story-heros-clothes") && loaded.Contains("story-telescope"), "Startup displays recorded achievements without requiring a running Cemu");
                Check(loaded.Get("story-heros-clothes").Utc == original && (int)offline.GetType().GetProperty("UnlockedCount", Private).GetValue(offline, null) == 2, "Offline view retains original award dates and completion count");
                Check((string)Get(offline, "dataRoot") == Path.GetFullPath(persistentRoot), "Explicit test/archive directory stays isolated from shared user data");
            }
            Console.WriteLine("SUCCESS " + checks + " UI checks; previews: " + output); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
