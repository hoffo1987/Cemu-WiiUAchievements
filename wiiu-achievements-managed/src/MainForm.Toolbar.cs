using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace WiiUAchievements
{
    // Toolbar of the objectives section: status tabs, search, category/chapter/sort menus, the
    // rarity/detection/image filters and a reset. All of it only selects and orders what is shown; nothing reaches engine or progress.
    public sealed partial class MainForm
    {
        const float RowH = 40, ToolbarPad = 10, MenuChipH = 36;
        static readonly string[] SortNames = { "Percorso", "Rarità", "Punti", "Nome", "Sbloccati di recente" };
        static readonly string[] TabNames = { "Tutti", "Sbloccati", "Da sbloccare", "Non attivi" };
        readonly TextBox search = new TextBox();
        int sortMode;
        bool searchEditing;
        string menu;
        float menuOpen, menuTarget;
        RectangleF toolbarRect, menuRect, menuAnchor, legendRect;
        bool menuAbove;
        readonly List<KeyValuePair<string, PointF>> menuGroups = new List<KeyValuePair<string, PointF>>();

        void BuildToolbar()
        {
            search.BorderStyle = BorderStyle.None; search.BackColor = Color.White; search.ForeColor = Theme.Ink;
            search.Font = new Font("Segoe UI", 10.5f); search.Visible = false; search.TabStop = false; search.MaxLength = 64;
            search.AccessibleName = "Cerca obiettivi";
            search.GotFocus += delegate { searchEditing = true; Invalidate(); };
            search.LostFocus += delegate { searchEditing = false; PlaceSearch(); Invalidate(); };
            search.TextChanged += delegate { scroll = scrollTarget = 0; focused = null; Invalidate(); Kick(); };
            Controls.Add(search);
        }

        // ---------------------------------------------------------------- filtering and order
        static readonly string[] RarityNames = { "Tutte", "Bronzo", "Argento", "Oro", "Platino" };
        static readonly string[] DetectionNames = { "Tutti", "Standard", "Sperimentali", "Non implementati" };
        static readonly string[] ImageNames = { "Tutte", "Personalizzate", "Senza icona" };
        int rarityFilter, detectionFilter, iconFilter;
        string Query { get { return search.Text.Trim(); } }
        // Search key: lower case, no accents, punctuation as spaces ("L'eredità" -> "l eredita").
        static string SearchKey(string text)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in (text ?? "").Normalize(System.Text.NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    result.Append(Char.IsLetterOrDigit(c) ? Char.ToLowerInvariant(c) : ' ');
            return result.ToString();
        }
        static string[] SearchWords(string text) { return SearchKey(text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); }
        // Italian/English item names and nicknames, so a search finds an objective by what the
        // player calls it. Rebuilt whenever the game's rules change.
        static readonly string[] SearchSynonyms = { "spada lama sword", "arco freccia frecce faretra bow arrow", "bomba bombe bomb artificiere", "rupie rupia portafoglio denaro wallet rupee",
            "cuore cuori salute vita heart health", "canzone canto inno sonata melodia note musica song", "foto fotografia fotocamera immaginografo pictobox picto", "rampino grappling",
            "arpione hookshot", "vela sail", "magia magic", "calzari stivali piombo ferro boots", "superbracciali bracciali bracelets", "corazza armatura armor", "tempio santuario temple",
            "tifone ciclone gales", "mappa mappe carta carte chart", "ampolla ampolle bottiglia bottiglie zuppa soup bottle", "borsello bottino spoils", "fagotto esche bait", "cartella delivery",
            "portafelicita pendagli pendant", "famire medli", "macore makar", "kuznar gohdan", "kardemos kalle demos", "elmaroc helmaroc", "jay harla jalhalla", "gayla molgera",
            "triganon puppet", "sensei ken orca", "arraffaele salvatore", "mina mila", "maggy maggie", "fagalore hoskit", "gill tott", "mastro miniato carlov", "mingle knuckle",
            "primula outset", "taura windfall", "octoboss piovra octo", "minitendo gallery galleria" };
        static readonly string[] BossIds = { "story-din-pearl", "story-farore-pearl", "story-gohdan", "story-helmaroc", "story-jalhalla", "story-molgera", "story-puppet-ganon", "story-ganondorf" };
        readonly Dictionary<string, string> searchIndex = new Dictionary<string, string>();
        List<Achievement> indexedRules;
        string SearchText(Achievement a)
        {
            if (!ReferenceEquals(indexedRules, rules))
            {
                indexedRules = rules; searchIndex.Clear();
                foreach (var rule in rules)
                {
                    string text = " " + SearchKey(rule.Title + " " + rule.Description + " " + rule.Category + " " + rule.Chapter + " " + rule.Id + " " + rule.Field + " " +
                        Art.TierName((Tier)rule.Rarity)) + " ";
                    var words = new HashSet<string>(text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    foreach (string group in SearchSynonyms)
                        if (SearchWords(group).Any(words.Contains)) text += SearchKey(group) + " ";
                    if (BossIds.Contains(rule.Id)) text += "boss ";
                    searchIndex[rule.Id] = text;
                }
            }
            string indexed;
            return searchIndex.TryGetValue(a.Id, out indexed) ? indexed : SearchKey(a.Title);
        }
        // Layout runs on every frame, so the catalog query is remembered until something it
        // depends on changes (rules, progress, filters, order or search). This keeps animation
        // frames free of large allocations and the garbage collections they cause.
        string catalogKey;
        List<Achievement> catalogScoped, catalogFiltered, chapterSource;
        List<string> chapterList;
        string wordsText;
        string[] wordsCache = new string[0];
        string CatalogKey()
        {
            int unlocked = 0;
            if (progress != null) foreach (var a in rules) if (!a.IsPlanned && progress.Contains(a.Id)) unlocked++;
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rules) + "|" + (progress == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(progress)) + "|" +
                unlocked + "|" + filter + "|" + categoryFilter + "|" + chapterFilter + "|" + sortMode + "|" + rarityFilter + "|" + detectionFilter + "|" + iconFilter + "|" + search.Text;
        }
        void RefreshCatalog()
        {
            string key = CatalogKey();
            if (key == catalogKey) return;
            catalogScoped = Scoped(true, true, true).ToList();
            catalogFiltered = FilterAndSort(catalogScoped);
            catalogKey = key;
        }
        IEnumerable<Achievement> CategoryRules() { RefreshCatalog(); return catalogScoped; }
        string[] QueryWords()
        {
            string text = search.Text;
            if (text != wordsText) { wordsText = text; wordsCache = SearchWords(text.Trim()); }
            return wordsCache;
        }
        // Rules after category, chapter, the extra filters and search; the status tabs count from
        // here. A menu counts its own options with its own filter left out.
        IEnumerable<Achievement> Scoped(bool byCategory, bool byChapter, bool byMore)
        {
            var chapters = Chapters();
            string[] words = QueryWords();
            return rules.Where(a => (!byCategory || categoryFilter == 0 || a.Category == AchievementSet.Categories[categoryFilter - 1]) &&
                (!byChapter || chapterFilter == 0 || chapterFilter > chapters.Count || a.Chapter == chapters[chapterFilter - 1]) &&
                (!byMore || MoreMatches(a, -1)) &&
                (words.Length == 0 || words.All(word => SearchText(a).Contains(word))));
        }
        // Rarity (group 0), detection (1) and image (2) filters; `skip` leaves one group out.
        bool MoreMatches(Achievement a, int skip)
        {
            return (skip == 0 || rarityFilter == 0 || (int)a.Rarity == rarityFilter - 1) &&
                (skip == 1 || detectionFilter == 0 || (detectionFilter == 1 ? !a.IsPlanned && !a.Experimental : detectionFilter == 2 ? !a.IsPlanned && a.Experimental : a.IsPlanned)) &&
                (skip == 2 || iconFilter == 0 || arts[a.Id].HasIcon == (iconFilter == 1));
        }
        List<string> Chapters()
        {
            if (!ReferenceEquals(chapterSource, rules)) { chapterSource = rules; chapterList = rules.Select(a => a.Chapter).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(); }
            return chapterList;
        }
        bool Unlocked(Achievement a) { return !a.IsPlanned && progress != null && progress.Contains(a.Id); }
        // The shown list (a shared, read-only result: callers never modify it).
        List<Achievement> Filtered() { RefreshCatalog(); return catalogFiltered; }
        List<Achievement> FilterAndSort(List<Achievement> scoped)
        {
            var items = scoped.Where(a => filter == 0 || (filter == 3 ? a.IsPlanned : !a.IsPlanned && Unlocked(a) == (filter == 1)));
            switch (sortMode)
            {
                case 1: items = items.OrderByDescending(a => (int)a.Rarity); break;
                case 2: items = items.OrderByDescending(a => a.IsPlanned ? -1 : a.Points); break;
                case 3: items = items.OrderBy(a => a.Title, StringComparer.Create(Italian, true)); break;
                case 4: items = items.OrderByDescending(a => Unlocked(a) ? progress.Get(a.Id).Utc.Ticks : a.IsPlanned ? -2 : -1); break;
            }
            return items.ToList();
        }
        bool MoreActive { get { return rarityFilter != 0 || detectionFilter != 0 || iconFilter != 0; } }
        bool FiltersActive { get { return categoryFilter != 0 || chapterFilter != 0 || sortMode != 0 || MoreActive || Query.Length > 0; } }
        string CategoryName { get { return categoryFilter == 0 ? "Tutte" : AchievementSet.Categories[categoryFilter - 1]; } }
        string ChapterName { get { var c = Chapters(); return chapterFilter == 0 || chapterFilter > c.Count ? "Tutte" : c[chapterFilter - 1]; } }
        string MoreName
        {
            get
            {
                var parts = new List<string>();
                if (rarityFilter != 0) parts.Add(RarityNames[rarityFilter]);
                if (detectionFilter != 0) parts.Add(DetectionNames[detectionFilter]);
                if (iconFilter != 0) parts.Add(ImageNames[iconFilter]);
                return parts.Count == 0 ? "Nessuno" : String.Join(" · ", parts);
            }
        }

        // A filter change lays the grid out again with a short staggered entrance.
        void FilterChanged()
        {
            scroll = scrollTarget = 0; focused = null;
            introStart = Motion.Reduced ? -10 : (float)clock.Elapsed.TotalSeconds;
            Invalidate(); Kick();
        }
        void NextChapter() { chapterFilter = (chapterFilter + 1) % (Chapters().Count + 1); FilterChanged(); }
        void NextCategory() { categoryFilter = (categoryFilter + 1) % (AchievementSet.Categories.Count + 1); FilterChanged(); }
        void NextSort() { sortMode = (sortMode + 1) % SortNames.Length; FilterChanged(); }
        void NextRarity() { rarityFilter = (rarityFilter + 1) % RarityNames.Length; FilterChanged(); }
        void ResetFilters()
        {
            categoryFilter = chapterFilter = sortMode = rarityFilter = detectionFilter = iconFilter = 0;
            if (search.TextLength > 0) search.Clear();
            FilterChanged();
        }
        void FocusSearch()
        {
            if (libraryOpen || ActiveLayer != 0) return;
            searchEditing = true; PlaceSearch();
            if (search.Visible) { search.Focus(); search.SelectionStart = search.TextLength; }
            Invalidate(); Kick();
        }
        void ClearSearch() { search.Clear(); if (search.Visible) search.Focus(); }

        // ---------------------------------------------------------------- layout
        float TabWidth(int i, int count)
        {
            return Measure(TabNames[i], Theme.Font(14.5f, Weight.Bold)).Width + Measure(count.ToString(CultureInfo.InvariantCulture), Theme.Font(12, Weight.Heavy)).Width + 74;
        }
        float DropdownWidth(string caption, string value)
        {
            float text = Math.Max(Measure(caption, Theme.Font(11, Weight.Heavy)).Width, Measure(value, Theme.Font(14.5f, Weight.Bold)).Width);
            return Math.Max(146, Math.Min(250, text + 82));
        }
        // Lays out the toolbar from `top` and returns where the grid starts.
        float ArrangeToolbar(float w, float top)
        {
            var scoped = CategoryRules().ToList();
            int unlocked = scoped.Count(Unlocked), pending = scoped.Count(a => a.IsPlanned);
            int[] counts = { scoped.Count, unlocked, scoped.Count - pending - unlocked, pending };
            float[] tabW = new float[4];
            for (int i = 0; i < 4; i++) tabW[i] = TabWidth(i, counts[i]);
            float tabsW = tabW.Sum() + 8;
            string[] captions = { "CATEGORIA", "TAPPA", "ORDINA PER", "ALTRI FILTRI" };
            string[] values = { CategoryName, ChapterName, SortNames[sortMode], MoreName };
            float[] dropW = new float[4];
            for (int i = 0; i < 4; i++) dropW[i] = DropdownWidth(captions[i], values[i]);
            float dropsW = dropW.Sum() + 24, resetW = FiltersActive ? ButtonWidth("Azzera", null) - 6 : 0;
            float inner = w - Pad * 2 - ToolbarPad * 2;
            bool single = tabsW + 14 + dropsW + (resetW > 0 ? resetW + 10 : 0) + 14 + 250 <= inner;
            int rows = single ? 1 : 2;
            toolbarRect = new RectangleF(Pad, top, w - Pad * 2, ToolbarPad * 2 + rows * RowH + (rows - 1) * 8);
            float x = toolbarRect.X + ToolbarPad, y = top + ToolbarPad;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var tab = Put("tab" + i, Kind.Tab, new RectangleF(x + 4, y + 4, tabW[i], RowH - 8), TabNames[i], () => SetFilter(index), true, 0);
                tab.Checked = filter == i; tab.Glyph = counts[i].ToString(CultureInfo.InvariantCulture); tab.Help = counts[i] + " obiettivi";
                x += tabW[i];
            }
            float searchX = toolbarRect.X + ToolbarPad + tabsW + 14, searchRight = toolbarRect.Right - ToolbarPad;
            float dx = single ? searchX : toolbarRect.X + ToolbarPad, dy = single ? y : y + RowH + 8;
            string[] ids = { "category", "chapter", "sort", "more" };
            string[] glyphs = { "C", "F", "S", "R" };
            string[] helps = { "Filtra per categoria (tasto C per scorrere)", "Filtra per tappa del percorso (tasto F per scorrere)", "Cambia l'ordine degli obiettivi (tasto S per scorrere)",
                "Filtra per rarità, rilevamento e immagine (tasto R per scorrere la rarità)" };
            for (int i = 0; i < 4; i++)
            {
                string id = ids[i];
                var d = Put(id, Kind.Dropdown, new RectangleF(dx, dy, dropW[i], RowH), captions[i].Substring(0, 1) + captions[i].Substring(1).ToLowerInvariant() + ": " + values[i],
                    () => OpenMenu(id), true, 0);
                d.Glyph = glyphs[i]; d.Help = helps[i]; d.Checked = menu == id && menuTarget > 0;
                dx += dropW[i] + 8;
            }
            if (resetW > 0)
            {
                AddButton("reset-filters", Kind.Button, "Azzera", null, dx + 2, dy + 4, ResetFilters, true, 0, 32, resetW);
                widgets["reset-filters"].Help = "Riporta categoria, tappa, ordine, altri filtri e ricerca ai valori iniziali";
                dx += resetW + 12;
            }
            // Where there is room, a key to the four states (icon + word) sits in the toolbar: on
            // the second row, or on very wide windows between the menus and a capped search box.
            float legendW = LegendWidth();
            legendRect = RectangleF.Empty;
            if (single)
            {
                searchX = dx + 6;
                const float searchMax = 480;
                if (searchRight - searchX > searchMax + legendW + 40)
                {
                    searchX = searchRight - searchMax;
                    legendRect = new RectangleF(searchX - 24 - legendW, y, legendW, RowH);
                }
            }
            else if (toolbarRect.Right - ToolbarPad - legendW > dx + 8)
                legendRect = new RectangleF(toolbarRect.Right - ToolbarPad - legendW, dy, legendW, RowH);
            var field = Put("search", Kind.Field, new RectangleF(searchX, y, Math.Max(120, searchRight - searchX), RowH), "Cerca obiettivi", FocusSearch, true, 0);
            field.Box = search; field.Help = "Cerca per nome, descrizione, categoria, tappa o rarità (Ctrl+F)";
            if (search.TextLength > 0)
                Put("search-clear", Kind.Icon, new RectangleF(field.Bounds.Right - 36, y + 6, 28, 28), "Cancella ricerca", ClearSearch, true, 0);
            return toolbarRect.Bottom + 4;
        }
        void PlaceSearch()
        {
            Widget w;
            bool show = searchEditing && !libraryOpen && ActiveLayer == 0 && drawer <= 0.001f && widgets.TryGetValue("search", out w) && w.Visible;
            if (show)
            {
                var r = widgets["search"].Bounds;
                float right = r.Right - (search.TextLength > 0 ? 40 : 14) - SearchCountWidth();
                int height = search.PreferredHeight;
                var device = new Rectangle((int)Math.Round((r.X + 40) * scale), (int)Math.Round(r.Y * scale + (r.Height * scale - height) / 2),
                    Math.Max(20, (int)((right - r.X - 44) * scale)), height);
                if (search.Bounds != device) search.Bounds = device;
                search.BackColor = SearchFill;
                if (!search.Visible) search.Visible = true;
            }
            else if (search.Visible)
            {
                if (search.Focused) { ActiveControl = null; Focus(); }
                search.Visible = false;
            }
        }
        Color SearchFill { get { return Style.Themed ? Color.FromArgb(255, 254, 249) : Color.White; } }
        string SearchCount()
        {
            int shown = Filtered().Count;
            return Query.Length > 0 || filter != 0 || categoryFilter != 0 || chapterFilter != 0 ? shown + " di " + rules.Count : rules.Count + " obiettivi";
        }
        float SearchCountWidth() { return Measure(SearchCount(), Theme.Font(12.5f, Weight.Bold)).Width + 10; }

        // ---------------------------------------------------------------- menus
        sealed class MenuOption { public string Label, Group; public int Count; public Action Pick; public bool Checked; }
        List<MenuOption> MenuOptions()
        {
            var list = new List<MenuOption>();
            if (menu == "category")
            {
                var scope = Scoped(false, true, true).ToList();
                list.Add(new MenuOption { Label = "Tutte", Count = scope.Count, Checked = categoryFilter == 0 });
                for (int i = 0; i < AchievementSet.Categories.Count; i++)
                {
                    string c = AchievementSet.Categories[i];
                    list.Add(new MenuOption { Label = c, Count = scope.Count(a => a.Category == c), Checked = categoryFilter == i + 1 });
                }
                for (int i = 0; i < list.Count; i++) { int value = i; list[i].Pick = () => { categoryFilter = value; FilterChanged(); }; }
            }
            else if (menu == "chapter")
            {
                var scope = Scoped(true, false, true).ToList();
                var chapters = Chapters();
                list.Add(new MenuOption { Label = "Tutte", Count = scope.Count, Checked = chapterFilter == 0 });
                for (int i = 0; i < chapters.Count; i++)
                {
                    string c = chapters[i];
                    list.Add(new MenuOption { Label = c, Count = scope.Count(a => a.Chapter == c), Checked = chapterFilter == i + 1 });
                }
                for (int i = 0; i < list.Count; i++) { int value = i; list[i].Pick = () => { chapterFilter = value; FilterChanged(); }; }
            }
            else if (menu == "more")
            {
                var scope = Scoped(true, true, false).ToList();
                string[][] names = { RarityNames, DetectionNames, ImageNames };
                string[] groups = { "Rarità", "Rilevamento", "Immagine" };
                for (int group = 0; group < 3; group++)
                    for (int i = 0; i < names[group].Length; i++)
                    {
                        int g = group, value = i;
                        int current = g == 0 ? rarityFilter : g == 1 ? detectionFilter : iconFilter;
                        var counted = scope.Where(a => MoreMatches(a, g)).ToList();
                        int count = i == 0 ? counted.Count : counted.Count(a => g == 0 ? (int)a.Rarity == i - 1 :
                            g == 1 ? (i == 1 ? !a.IsPlanned && !a.Experimental : i == 2 ? !a.IsPlanned && a.Experimental : a.IsPlanned) : arts[a.Id].HasIcon == (i == 1));
                        list.Add(new MenuOption { Label = names[g][i], Group = groups[g], Count = count, Checked = current == i, Pick = () =>
                        {
                            if (g == 0) rarityFilter = value; else if (g == 1) detectionFilter = value; else iconFilter = value;
                            FilterChanged();
                        } });
                    }
            }
            else if (menu == "sort")
                for (int i = 0; i < SortNames.Length; i++) { int value = i; list.Add(new MenuOption { Label = SortNames[i], Count = -1, Checked = sortMode == i, Pick = () => { sortMode = value; FilterChanged(); } }); }
            return list;
        }
        void OpenMenu(string id)
        {
            if (menu == id && menuTarget > 0) { CloseMenu(); return; }
            bool keyboard = focusVisible;
            menu = id; menuTarget = 1; if (Motion.Reduced) menuOpen = 0.6f;
            hovered = null;
            Arrange();
            if (keyboard)
            {
                Widget pick = visible.FirstOrDefault(w => w.Kind == Kind.MenuItem && w.Checked) ?? visible.FirstOrDefault(w => w.Kind == Kind.MenuItem);
                if (pick != null) { focused = pick; focusVisible = true; }
            }
            else focused = null;
            PlaceSearch(); Invalidate(); Kick();
        }
        void CloseMenu()
        {
            if (menuTarget <= 0) return;
            string from = menu;
            menuTarget = 0; if (Motion.Reduced) menuOpen = Math.Min(menuOpen, 0.4f);
            if (focusVisible && from != null && widgets.ContainsKey(from)) focused = widgets[from]; else focused = null;
            Invalidate(); Kick();
        }
        void PickMenu(MenuOption option)
        {
            CloseMenu();
            option.Pick();
        }
        void ArrangeMenu(float w, float h)
        {
            Widget anchor;
            if (menu == null || (menuOpen <= 0 && menuTarget <= 0) || !widgets.TryGetValue(menu, out anchor) || !anchor.Visible) return;
            menuAnchor = anchor.Bounds;
            var options = MenuOptions();
            var labelFont = Theme.Font(14, Weight.Bold);
            menuGroups.Clear();
            if (options.Any(o => o.Group != null)) { ArrangeGroupedMenu(w, h, options, labelFont); return; }
            float chipW = 0;
            foreach (var o in options) chipW = Math.Max(chipW, Measure(o.Label, labelFont).Width + (o.Count >= 0 ? 44 : 0) + 46);
            chipW = Math.Max(chipW, 132);
            const float head = 34, pad = 12, gap = 6;
            float below = h - BottomH - 10 - (menuAnchor.Bottom + 8), above = menuAnchor.Y - TopH - 10;
            float maxW = w - Pad * 2;
            int maxCols = Math.Max(1, (int)((maxW - pad * 2 + gap) / (chipW + gap)));
            Func<float, int> rowsFor = space => Math.Max(1, (int)((space - head - pad * 2 + gap) / (MenuChipH + gap)));
            int cols = Math.Min(maxCols, (options.Count + rowsFor(below) - 1) / rowsFor(below));
            menuAbove = false;
            if ((options.Count + cols - 1) / cols > rowsFor(below) || cols > maxCols)
            {
                int colsAbove = Math.Min(maxCols, (options.Count + rowsFor(above) - 1) / rowsFor(above));
                if (above > below) { menuAbove = true; cols = colsAbove; }
            }
            cols = Math.Max(1, Math.Min(maxCols, cols));
            int rows = (options.Count + cols - 1) / cols;
            float mw = Math.Max(menuAnchor.Width, pad * 2 + cols * chipW + (cols - 1) * gap), mh = head + pad + rows * MenuChipH + (rows - 1) * gap + pad;
            float mx = Math.Max(Pad, Math.Min(w - Pad - mw, menuAnchor.X));
            float my = menuAbove ? menuAnchor.Y - 8 - mh : menuAnchor.Bottom + 8;
            menuRect = new RectangleF(mx, my, mw, mh);
            chipW = (mw - pad * 2 - (cols - 1) * gap) / cols;
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                // Column-major order reads top to bottom like a list.
                int col = i / rows, row = i % rows;
                var item = Put("menu:" + i, Kind.MenuItem, new RectangleF(mx + pad + col * (chipW + gap), my + head + pad + row * (MenuChipH + gap), chipW, MenuChipH),
                    option.Label, () => PickMenu(option), true, 3);
                item.Checked = option.Checked;
                item.Glyph = option.Count >= 0 ? option.Count.ToString(CultureInfo.InvariantCulture) : null;
                item.Help = option.Count >= 0 ? option.Count + " obiettivi" : "Ordine";
            }
        }

        // Grouped menu: one labelled row of chips per group, each chip as wide as its words.
        void ArrangeGroupedMenu(float w, float h, List<MenuOption> options, Font labelFont)
        {
            const float head = 34, pad = 12, gap = 6, label = 18, groupGap = 6;
            var groups = options.Select(o => o.Group).Distinct().ToList();
            var widths = options.Select(o => Measure(o.Label, labelFont).Width + 84).ToList();
            float rowW = groups.Max(name => options.Select((o, i) => o.Group == name ? widths[i] + gap : 0).Sum() - gap);
            float mw = Math.Min(w - Pad * 2, Math.Max(menuAnchor.Width, rowW + pad * 2));
            float mh = head + pad + groups.Count * (label + MenuChipH) + (groups.Count - 1) * groupGap + pad;
            float below = h - BottomH - 10 - (menuAnchor.Bottom + 8), above = menuAnchor.Y - TopH + 4;
            menuAbove = mh > below && above > below;
            float mx = Math.Max(Pad, Math.Min(w - Pad - mw, menuAnchor.X));
            float my = menuAbove ? menuAnchor.Y - 8 - mh : menuAnchor.Bottom + 8;
            menuRect = new RectangleF(mx, my, mw, mh);
            for (int g = 0; g < groups.Count; g++)
            {
                float y = my + head + pad + g * (label + MenuChipH + groupGap), x = mx + pad;
                menuGroups.Add(new KeyValuePair<string, PointF>(groups[g], new PointF(x + 2, y)));
                for (int i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    if (option.Group != groups[g]) continue;
                    var item = Put("menu:" + i, Kind.MenuItem, new RectangleF(x, y + label, widths[i], MenuChipH), option.Group + ": " + option.Label, () => PickMenu(option), true, 3);
                    item.Checked = option.Checked;
                    item.Glyph = option.Count.ToString(CultureInfo.InvariantCulture);
                    item.Help = option.Count + " obiettivi";
                    x += widths[i] + gap;
                }
            }
        }

        // ---------------------------------------------------------------- painting
        void DrawToolbar(Graphics g, float t)
        {
            var style = Style;
            var panel = toolbarRect;
            var t0 = widgets["tab0"].Bounds; var t3 = widgets["tab3"].Bounds;
            var track = new RectangleF(t0.X - 4, t0.Y - 4, t3.Right - t0.X + 8, t0.Height + 8);
            Layer(g, "toolbar-panel", Theme.Inflate(panel, 16, 16), style.GameId + "|" + panel.Height + "|" + track, b =>
            {
                Theme.Shadow(b, panel, 16, 8, 30, 3);
                Theme.Fill(b, panel, 16, Color.FromArgb(style.Themed ? 250 : 236, style.CardTop), Color.FromArgb(style.Themed ? 246 : 226, style.Themed ? style.CardBottom : style.Soft));
                style.Parchment(b, panel, 16, 0.55f);
                if (!style.SimpleFrame(b, panel, true, 10)) Theme.Stroke(b, panel, 16, Color.FromArgb(150, Color.White), 1.2f);
                // The tab track: a recessed groove holding the four state tabs.
                Theme.Fill(b, track, track.Height / 2, style.TrackTop, style.TrackBottom);
                style.Parchment(b, track, track.Height / 2, 0.3f);
                using (var inset = new LinearGradientBrush(new RectangleF(track.X, track.Y - 1, track.Width, 8), Color.FromArgb(34, 20, 44, 64), Color.FromArgb(0, 20, 44, 64), 90f))
                using (var path = Theme.Round(track, track.Height / 2)) { var s = b.Save(); b.SetClip(path, CombineMode.Intersect); b.FillRectangle(inset, track.X, track.Y, track.Width, 7); b.Restore(s); }
                Theme.Stroke(b, track, track.Height / 2, style.TrackEdge, 1.1f);
            });
            DrawTabs(g, t);
            foreach (var id in new[] { "category", "chapter", "sort", "more" }) DrawDropdown(g, widgets[id], t);
            Widget reset;
            if (widgets.TryGetValue("reset-filters", out reset) && reset.Visible) DrawButton(g, reset, t);
            DrawSearch(g, widgets["search"], t);
            if (legendRect.Width > 0) DrawLegend(g, legendRect);
        }
        static readonly CardState[] LegendStates = { CardState.Unlocked, CardState.Locked, CardState.Experimental, CardState.Planned };
        float LegendWidth()
        {
            var font = Theme.Font(12.5f, Weight.Bold);
            return LegendStates.Sum(s => 22 + Measure(StateLabel(s), font).Width + 14) - 14 + 4;
        }
        void DrawLegend(Graphics g, RectangleF r)
        {
            var font = Theme.Font(12.5f, Weight.Bold);
            float x = r.X;
            foreach (var state in LegendStates)
            {
                Color ink = StateInk(state);
                var icon = new RectangleF(x, r.Y + r.Height / 2 - 9, 18, 18);
                using (var b = new SolidBrush(Color.FromArgb(34, ink))) g.FillEllipse(b, icon);
                StateIcon(state)(g, Theme.Inflate(icon, -4, -4), ink);
                float lw = Measure(StateLabel(state), font).Width + 2;
                Theme.Text(g, StateLabel(state), font, ink, new RectangleF(x + 22, r.Y, lw + 2, r.Height), StringAlignment.Near, StringAlignment.Center, false);
                x += 22 + lw + 12;
            }
        }
        static void TabIcon(Graphics g, int index, RectangleF r, Color color)
        {
            switch (index)
            {
                case 0: Theme.GridIcon(g, Theme.Inflate(r, -1.5f, -1.5f), color); break;
                case 1:
                    using (var pen = new Pen(color, 1.7f)) g.DrawEllipse(pen, Theme.Inflate(r, -0.5f, -0.5f));
                    Theme.Check(g, Theme.Inflate(r, -1, -1), color, 2); break;
                case 2: Theme.Lock(g, Theme.Inflate(r, -0.5f, 0), color); break;
                default: Theme.Pending(g, r, color); break;
            }
        }
        void DrawTabs(Graphics g, float t)
        {
            var style = Style;
            var sel = widgets["tab" + filter].Bounds;
            if (tabX < 0) { tabX = sel.X; tabW = sel.Width; }
            var highlight = new RectangleF(tabX, sel.Y, tabW, sel.Height);
            Theme.Shadow(g, highlight, highlight.Height / 2, 4, 50, 1.5f);
            Theme.Fill(g, highlight, highlight.Height / 2, style.ButtonTop, style.Themed ? Theme.Light(style.ButtonBottom, 0.4f) : Color.FromArgb(242, 246, 249));
            Theme.Stroke(g, highlight, highlight.Height / 2, style.Themed ? style.Trim : Theme.Mix(style.Accent, Color.White, 0.35f), 1.6f);
            var nameFont = Theme.Font(14.5f, Weight.Bold);
            var countFont = Theme.Font(12, Weight.Heavy);
            for (int i = 0; i < 4; i++)
            {
                var w = widgets["tab" + i];
                bool on = filter == i;
                float nw = Measure(w.Name, nameFont).Width, cw = Measure(w.Glyph, countFont).Width + 14;
                float x = w.Bounds.X + (w.Bounds.Width - (18 + 7 + nw + 8 + cw)) / 2, cy = w.Bounds.Y + w.Bounds.Height / 2;
                if (w.Press > 0) cy += w.Press;
                Color ink = on ? style.AccentDark : Theme.Mix(Theme.LockedInk, Theme.Ink, w.Hover);
                TabIcon(g, i, new RectangleF(x, cy - 8, 16, 16), ink);
                Theme.Text(g, w.Name, nameFont, ink, new RectangleF(x + 25, cy - 12, nw + 6, 24), StringAlignment.Near, StringAlignment.Center, false);
                var bubble = new RectangleF(x + 25 + nw + 8, cy - 10, cw, 20);
                Theme.Fill(g, bubble, 10, on ? style.BubbleTop : style.BubbleOffTop, on ? style.BubbleBottom : style.BubbleOffBottom);
                Theme.Text(g, w.Glyph, countFont, Color.White, bubble, StringAlignment.Center, StringAlignment.Center, false);
                Theme.Cursor(g, w.Bounds, w.Bounds.Height / 2, FocusStrength(w, t));
            }
        }
        void DrawDropdown(Graphics g, Widget w, float t)
        {
            var style = Style;
            var r = w.Bounds;
            float hv = w.Hover, open = w.Checked ? Theme.EaseOut(menuOpen) : 0;
            if (w.Press > 0) r.Offset(0, 1.2f * w.Press);
            bool active = w.Id == "category" ? categoryFilter != 0 : w.Id == "chapter" ? chapterFilter != 0 : w.Id == "more" ? MoreActive : sortMode != 0;
            Theme.Shadow(g, r, 12, 4 + 3 * hv, 34 + (int)(26 * hv), 2 - w.Press);
            Theme.Fill(g, r, 12, Theme.Mix(style.ButtonTop, Color.White, hv * 0.5f), Theme.Mix(style.ButtonBottom, style.Soft, open * 0.6f));
            style.Parchment(g, r, 12, 0.25f);
            Color edge = active || open > 0 ? style.Accent : Theme.Mix(style.ButtonEdge, style.Themed ? style.Trim : style.Accent, hv);
            Theme.Stroke(g, r, 12, edge, active || open > 0 ? 1.8f : 1.2f + 0.4f * hv);
            Theme.Glyph(g, new RectangleF(r.X + 10, r.Y + (r.Height - 22) / 2, 22, 22), w.Glyph, active ? style.Accent : style.GlyphFill, style.Themed && !active ? style.GlyphInk : Color.White);
            string caption = w.Id == "category" ? "CATEGORIA" : w.Id == "chapter" ? "TAPPA" : w.Id == "more" ? "ALTRI FILTRI" : "ORDINA PER";
            string value = w.Id == "category" ? CategoryName : w.Id == "chapter" ? ChapterName : w.Id == "more" ? MoreName : SortNames[sortMode];
            float tx = r.X + 40, tw = r.Width - 40 - 30;
            Theme.Text(g, caption, Theme.Font(11, Weight.Heavy), active ? style.AccentDark : style.Muted, new RectangleF(tx, r.Y + 3, tw, 16));
            Theme.Text(g, value, Theme.Font(14.5f, Weight.Bold), active ? style.AccentDark : style.ButtonInk, new RectangleF(tx, r.Y + 17, tw, 21));
            // The chevron turns as the menu unfolds.
            var c = new PointF(r.Right - 18, r.Y + r.Height / 2);
            var state = g.Save();
            g.TranslateTransform(c.X, c.Y); g.RotateTransform(180 * open); g.TranslateTransform(-c.X, -c.Y);
            Theme.Chevron(g, Theme.Square(c, 14), Theme.Mix(style.Muted, style.AccentDark, Math.Max(hv, open)), 0, 2.2f);
            g.Restore(state);
            Theme.Cursor(g, r, 12, FocusStrength(w, t));
        }
        void DrawSearch(Graphics g, Widget w, float t)
        {
            var style = Style;
            var r = w.Bounds;
            bool editing = searchEditing && search.Visible;
            Theme.Fill(g, r, r.Height / 2, SearchFill, SearchFill);
            using (var inset = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, 9), Color.FromArgb(26, 20, 44, 64), Color.FromArgb(0, 20, 44, 64), 90f))
            using (var path = Theme.Round(r, r.Height / 2)) { var s = g.Save(); g.SetClip(path, CombineMode.Intersect); g.FillRectangle(inset, r.X, r.Y, r.Width, 8); g.Restore(s); }
            Color edge = editing ? style.Accent : Theme.Mix(style.Themed ? style.ButtonEdge : Color.FromArgb(192, 201, 209), style.Accent, w.Hover * 0.7f);
            Theme.Stroke(g, r, r.Height / 2, edge, editing ? 2 : 1.2f);
            if (editing) Theme.Cursor(g, r, r.Height / 2, 0.5f);
            Theme.Search(g, new RectangleF(r.X + 15, r.Y + (r.Height - 17) / 2, 17, 17), editing ? style.AccentDark : style.Muted);
            string count = SearchCount();
            var countFont = Theme.Font(12.5f, Weight.Bold);
            float cw = SearchCountWidth(), right = r.Right - (search.TextLength > 0 ? 40 : 14);
            if (r.Width > 220) Theme.Text(g, count, countFont, Query.Length > 0 || filter != 0 ? style.AccentDark : style.Muted, new RectangleF(right - cw, r.Y, cw, r.Height), StringAlignment.Far, StringAlignment.Center, false);
            else cw = 0;
            if (!editing || !search.Visible)
            {
                bool empty = search.TextLength == 0;
                Theme.Text(g, empty ? "Cerca per nome, luogo o tappa…" : search.Text, Theme.Font(14, empty ? Weight.Regular : Weight.Bold), empty ? style.Muted : Theme.Ink,
                    new RectangleF(r.X + 42, r.Y, right - cw - r.X - 46, r.Height), StringAlignment.Near, StringAlignment.Center, false);
            }
            Widget clear;
            if (widgets.TryGetValue("search-clear", out clear) && clear.Visible)
            {
                var c = clear.Bounds;
                using (var b = new SolidBrush(Theme.Mix(Color.FromArgb(226, 232, 236), style.Soft, clear.Hover))) g.FillEllipse(b, c);
                Theme.Close(g, Theme.Inflate(c, -8, -8), Theme.Mix(style.Muted, style.AccentDark, clear.Hover), 2);
                Theme.Cursor(g, c, c.Width / 2, FocusStrength(clear, t));
            }
            Theme.Cursor(g, r, r.Height / 2, FocusStrength(w, t));
        }

        Bitmap menuFrame;
        void DrawMenu(Graphics g, float t)
        {
            if (menu == null || menuOpen <= 0) return;
            float e = Theme.EaseOut(menuOpen);
            if (e >= 0.999f) { DrawMenuPanel(g, t); return; }
            // While unfolding, the panel is composed offscreen and faded in from its anchor.
            var area = Theme.Inflate(menuRect, 24, 24);
            int pw = Math.Max(1, (int)Math.Ceiling(area.Width * scale)), ph = Math.Max(1, (int)Math.Ceiling(area.Height * scale));
            if (menuFrame == null || menuFrame.Width != pw || menuFrame.Height != ph)
            {
                if (menuFrame != null) menuFrame.Dispose();
                menuFrame = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
            }
            using (var b = Graphics.FromImage(menuFrame))
            {
                b.Clear(Color.Transparent);
                b.SmoothingMode = SmoothingMode.AntiAlias; b.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; b.PixelOffsetMode = PixelOffsetMode.HighQuality;
                b.ScaleTransform(scale, scale); b.TranslateTransform(-area.X, -area.Y);
                DrawMenuPanel(b, t);
            }
            float k = Motion.Reduced ? 1 : 0.96f + 0.04f * e, shift = Motion.Reduced ? 0 : (menuAbove ? 8 : -8) * (1 - e);
            float ox = Math.Max(menuRect.X, Math.Min(menuRect.Right, menuAnchor.X + menuAnchor.Width / 2)), oy = menuAbove ? menuRect.Bottom : menuRect.Y;
            var dest = new RectangleF(ox + (area.X - ox) * k, oy + (area.Y - oy) * k + shift, area.Width * k, area.Height * k);
            Theme.DrawFaded(g, menuFrame, dest, e);
        }
        void DrawMenuPanel(Graphics g, float t)
        {
            var style = Style;
            var r = menuRect;
            Theme.Shadow(g, r, 16, 14, 70, 5);
            Theme.Fill(g, r, 16, style.CardTop, style.Themed ? style.CardBottom : Color.FromArgb(240, 245, 248));
            style.Parchment(g, r, 16, 0.5f);
            if (!style.SimpleFrame(g, r, false, 12)) Theme.Stroke(g, r, 16, Theme.Line, 1);
            // A small notch points back at the menu's button.
            float nx = Math.Max(r.X + 24, Math.Min(r.Right - 24, menuAnchor.X + 40));
            using (var notch = new GraphicsPath())
            {
                if (menuAbove) notch.AddPolygon(new[] { new PointF(nx - 8, r.Bottom - 1), new PointF(nx + 8, r.Bottom - 1), new PointF(nx, r.Bottom + 7) });
                else notch.AddPolygon(new[] { new PointF(nx - 8, r.Y + 1), new PointF(nx + 8, r.Y + 1), new PointF(nx, r.Y - 7) });
                Theme.Fill(g, notch, style.Themed ? style.Trim : style.CardTop);
            }
            string title = menu == "category" ? "Scegli una categoria" : menu == "chapter" ? "Scegli una tappa del viaggio" : menu == "more" ? "Filtra per rarità, rilevamento e immagine" : "Ordina gli obiettivi per";
            Action<Graphics, RectangleF, Color> icon = Theme.SortIcon;
            if (menu == "category" || menu == "more") icon = Theme.Tag; else if (menu == "chapter") icon = Theme.Compass;
            icon(g, new RectangleF(r.X + 16, r.Y + 13, 16, 16), style.AccentDark);
            Theme.Text(g, title, Theme.Font(13.5f, Weight.Heavy), style.AccentDark, new RectangleF(r.X + 40, r.Y + 10, r.Width - 56, 22));
            var font = Theme.Font(14, Weight.Bold);
            var countFont = Theme.Font(11.5f, Weight.Heavy);
            foreach (var group in menuGroups)
                Theme.Text(g, group.Key.ToUpperInvariant(), Theme.Font(11.5f, Weight.Heavy), style.Muted, new RectangleF(group.Value.X, group.Value.Y, r.Width - 32, 18));
            foreach (var w in visible)
            {
                if (w.Kind != Kind.MenuItem) continue;
                var c = w.Bounds;
                if (w.Press > 0) c.Offset(0, w.Press);
                if (w.Checked) Theme.Fill(g, c, 10, style.PrimaryTop, style.PrimaryBottom);
                else if (w.Hover > 0.01f) Theme.Fill(g, c, 10, Theme.Alpha(style.Soft, w.Hover), Theme.Alpha(Theme.Mix(style.Soft, style.Glow, 0.25f), w.Hover));
                if (!w.Checked) Theme.Stroke(g, c, 10, Theme.Alpha(style.Themed ? style.ButtonEdge : Theme.Line, 0.55f + 0.45f * w.Hover), 1);
                Color ink = w.Checked ? style.PrimaryInk : Theme.Mix(Theme.Ink, style.AccentDark, w.Hover);
                float x = c.X + 12;
                if (w.Checked) { Theme.Check(g, new RectangleF(x - 2, c.Y + (c.Height - 18) / 2, 18, 18), Color.White, 2.4f); }
                string name = w.Name.Contains(": ") ? w.Name.Substring(w.Name.IndexOf(": ", StringComparison.Ordinal) + 2) : w.Name;
                Theme.Text(g, name, font, ink, new RectangleF(x + 20, c.Y, c.Width - 32 - (w.Glyph != null ? 40 : 0), c.Height), StringAlignment.Near, StringAlignment.Center, false);
                if (w.Glyph != null)
                {
                    float bw = Measure(w.Glyph, countFont).Width + 14;
                    var bubble = new RectangleF(c.Right - 10 - bw, c.Y + (c.Height - 20) / 2, bw, 20);
                    bool zero = w.Glyph == "0";
                    Theme.Fill(g, bubble, 10, w.Checked ? Color.FromArgb(70, 255, 255, 255) : zero ? Color.FromArgb(226, 230, 233) : style.BubbleOffTop,
                        w.Checked ? Color.FromArgb(70, 255, 255, 255) : zero ? Color.FromArgb(214, 219, 223) : style.BubbleOffBottom);
                    Theme.Text(g, w.Glyph, countFont, zero && !w.Checked ? Theme.LockedInk : Color.White, bubble, StringAlignment.Center, StringAlignment.Center, false);
                }
                Theme.Cursor(g, c, 10, FocusStrength(w, t));
            }
        }
    }
}
