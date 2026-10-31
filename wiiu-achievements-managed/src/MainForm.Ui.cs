using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace WiiUAchievements
{
    // Custom-drawn interface in the style of the Wii U system menus: light striped backdrop,
    // rounded cards, pill buttons with GamePad glyphs and a cyan selection cursor. Layout uses
    // a 96-DPI logical canvas; every frame is drawn in OnPaint and animated by a 15 ms timer that
    // stops itself when nothing moves (ambient motion pauses while the game has focus).
    public sealed partial class MainForm
    {
        enum Kind { Button, Primary, Icon, Tab, Tile, GameTile, Toggle, Field, Dropdown, MenuItem }
        sealed class Widget
        {
            public string Id, Name, Help, Glyph;
            public Kind Kind;
            public RectangleF Bounds;
            public bool Enabled, Visible, Checked;
            public int Layer;
            public Action Click;
            public Action<Graphics, RectangleF, Color> Icon;
            public float Hover, Press, Value = -1;
            public Achievement Achievement;
            public TextBox Box;
        }

        // Motion timing shared by the whole window: quick feedback, short panel transitions,
        // all eased out. Reduced motion shortens them further and drops slides and zooms.
        const float TopH = 80, BottomH = 76, Pad = 20, BannerStatusH = 60, TileH = 132, Gap = 14, SheetW = 440, DrawerH = 260, CelebrationTime = 2.2f;
        const float PanelTime = 0.26f, MenuTime = 0.16f, HoverRate = 18, PressRate = 30;
        static readonly CultureInfo Italian = MakeCulture();
        readonly Dictionary<string, Widget> widgets = new Dictionary<string, Widget>();
        readonly List<Widget> visible = new List<Widget>();
        readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly OceanBanner banner = new OceanBanner();
        readonly Dictionary<string, float> celebrations = new Dictionary<string, float>();
        Bitmap measureBitmap, backdrop;
        Graphics measure;
        Widget hovered, pressed, focused;
        Achievement detail;
        float scale = 1, lastFrame, ambientTime, sheet, sheetTarget, drawer, drawerTarget, dialog, dialogTarget;
        float scroll, scrollTarget, logScroll, tabX = -1, tabW, shownRatio, shownRupees = -1, contentHeight, introStart = -10;
        int filter, categoryFilter, chapterFilter, unreadLogs;
        bool focusVisible, cloudPage;
        RectangleF bannerRect, gridRect, drawerRect, sheetRect, dialogRect, statusPill;

        // A minimised window has an empty client area: the layout keeps the fixed window size,
        // otherwise cards (and their cached bitmaps) would be sized to zero.
        bool Collapsed { get { return WindowState == FormWindowState.Minimized || ClientSize.Width <= 0 || ClientSize.Height <= 0; } }
        float W { get { return Collapsed ? WindowW : ClientSize.Width / scale; } }
        // The picture part of the banner gives way on short windows so more cards stay in view.
        float HeroH { get { return Math.Max(116, Math.Min(172, 116 + (H - 680) * 0.5f)); } }
        // Game look for banner-side content; buttons, bars and settings keep the system style.
        // The Games catalog is a system screen and keeps the neutral style.
        GameStyle Style { get { return libraryOpen ? GameStyle.Neutral : GameStyle.For(selectedGame.Id); } }
        GameStyle backdropStyle;
        int shownVideoFrame = -1;
        VideoBanner video;
        bool videoOpened;
        VideoBanner BannerVideo
        {
            get
            {
                if (!videoOpened) { videoOpened = true; video = GameStyle.For(selectedGame.Id).OpenBannerVideo(); }
                return video != null && !video.Failed ? video : null;
            }
        }
        void CloseBannerVideo() { if (video != null) video.Dispose(); video = null; videoOpened = false; }
        float H { get { return Collapsed ? WindowH : ClientSize.Height / scale; } }
        int ActiveLayer { get { return dialogTarget > 0 ? 2 : sheetTarget > 0 ? 1 : menuTarget > 0 ? 3 : 0; } }

        static CultureInfo MakeCulture()
        {
            try { return new CultureInfo("it-IT"); } catch (Exception) { return CultureInfo.InvariantCulture; }
        }

        void BuildUi()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            // Desktop DPI, read without creating this window's handle before its start position is applied.
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = Math.Max(1f, g.DpiX / 96f); } catch (Exception) { scale = 1; }
            Motion.Reduced = Motion.SystemPrefersReduced();
            measureBitmap = new Bitmap(1, 1);
            measure = Graphics.FromImage(measureBitmap);
            measure.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            foreach (var box in new[] { cemuPath, player, cloudEmail, cloudPassword, cloudProject, cloudApiKey })
            {
                box.BorderStyle = BorderStyle.None; box.BackColor = Color.White; box.ForeColor = Theme.Ink;
                box.Font = new Font("Segoe UI", 10.5f); box.Visible = false; box.TabStop = false;
                box.GotFocus += delegate { Invalidate(); };
                box.LostFocus += delegate { Invalidate(); };
                box.TextChanged += delegate { Invalidate(); };
                Controls.Add(box);
            }
            BuildToolbar();
            cemuPath.AccessibleName = "Percorso di Cemu.exe"; player.AccessibleName = "Profilo locale"; player.MaxLength = 64;
            cloudEmail.AccessibleName = "Email account cloud"; cloudPassword.AccessibleName = "Password account cloud";
            cloudProject.AccessibleName = "Project ID Firebase"; cloudApiKey.AccessibleName = "Web API key Firebase";
            cloudPassword.UseSystemPasswordChar = true;
            // 10 ms is the shortest WinForms timer; each tick then waits for the next display frame.
            animation.Interval = 10;
            animation.Tick += Animate;
            Application.Idle += OnIdle;
            Activated += delegate { FramePacing.Reset(); Kick(); };
            Deactivate += delegate { Kick(); };
            Resize += delegate
            {
                if (WindowState == FormWindowState.Maximized) { WindowState = FormWindowState.Normal; return; }
                if (Collapsed) return; // Restoring resizes again and rebuilds the layout.
                FramePacing.Reset(); ClearTileImages(); ClearLayers(); Arrange(); PlaceFields(); Kick();
            };
            Shown += delegate { introStart = Motion.Reduced ? -10 : (float)clock.Elapsed.TotalSeconds; Kick(); };
            VisibleChanged += delegate
            {
                if (!NativeMode) return;
                if (Visible) Kick();
                else { animation.Stop(); FramePacing.End(); if (video != null) video.SetPlaying(false); }
            };
            try { Icon = MakeIcon(); } catch (Exception) { }
            Disposed += delegate { Application.Idle -= OnIdle; if (scene != null) scene.Dispose(); FramePacing.End(); animation.Dispose(); banner.Dispose(); CloseBannerVideo(); ClearTileImages(); ClearLayers(); if (backdrop != null) backdrop.Dispose(); if (menuFrame != null) menuFrame.Dispose(); measure.Dispose(); measureBitmap.Dispose(); };
        }
        // One fixed window: the layout is designed for 1120×840 logical pixels (three card columns,
        // two full card rows under the banner and toolbar). It cannot be resized or maximised.
        // On screens too small for it at the desktop DPI, the whole interface is drawn smaller.
        const float WindowW = 1120, WindowH = 840;
        void FixWindowSize()
        {
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            var frame = SizeFromClientSize(Size.Empty);
            Rectangle area;
            try { area = Screen.FromPoint(Cursor.Position).WorkingArea; } catch (Exception) { area = Screen.PrimaryScreen.WorkingArea; }
            float fit = Math.Min((area.Width - frame.Width) / WindowW, (area.Height - frame.Height) / WindowH);
            if (fit < scale) scale = Math.Max(0.6f, (float)Math.Floor(fit * 20) / 20);
            ClientSize = new Size((int)Math.Round(WindowW * scale), (int)Math.Round(WindowH * scale));
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112, SC_MAXIMIZE = 0xF030;
            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_MAXIMIZE) return;
            base.WndProc(ref m);
        }
        static Icon MakeIcon()
        {
            if (BrandArt.Available) return Icon.ExtractAssociatedIcon(typeof(MainForm).Assembly.Location);
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.Fill(g, new RectangleF(1, 1, 30, 30), 8, Color.FromArgb(76, 204, 250), Theme.AccentDark);
                    Theme.Trophy(g, new RectangleF(5, 4, 22, 22));
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // ---------------------------------------------------------------- layout
        SizeF Measure(string text, Font font) { return Theme.Measure(measure, text, font); }
        Widget Put(string id, Kind kind, RectangleF bounds, string name, Action click, bool enabled, int layer)
        {
            Widget w;
            if (!widgets.TryGetValue(id, out w)) widgets[id] = w = new Widget { Id = id };
            w.Kind = kind; w.Bounds = bounds; w.Name = name; w.Click = click; w.Enabled = enabled; w.Layer = layer;
            w.Visible = true; w.Glyph = null; w.Help = null; w.Box = null; w.Achievement = null; w.Checked = false; w.Icon = null;
            visible.Add(w);
            return w;
        }
        float ButtonWidth(string text, string glyph) { return Measure(text, Theme.Font(15, Weight.Bold)).Width + (glyph != null ? 30 : 0) + 40; }
        float AddButton(string id, Kind kind, string text, string glyph, float x, float y, Action click, bool enabled, int layer, float height = 44, float width = 0)
        {
            if (width <= 0) width = ButtonWidth(text, glyph);
            Put(id, kind, new RectangleF(x, y, width, height), text, click, enabled, layer).Glyph = glyph;
            return x + width;
        }

        void Arrange()
        {
            foreach (var item in widgets.Values) item.Visible = false;
            visible.Clear();
            float w = W, h = H;
            Put("settings", Kind.Icon, new RectangleF(w - Pad - 92, 20, 40, 40), "Impostazioni", OpenSheet, true, 0).Help = "Percorso di Cemu, profilo, notifiche e movimento";
            Put("log", Kind.Icon, new RectangleF(w - Pad - 40, 20, 40, 40), "Registro attività", ToggleDrawer, true, 0).Help = unreadLogs + " nuove voci";
            var games = Put("games", Kind.Button, new RectangleF(w - Pad - 214, 24, 112, 32), "Giochi", ToggleLibrary, true, 0);
            games.Icon = Theme.GridIcon; games.Checked = libraryOpen; games.Help = libraryOpen ? "Torna agli obiettivi (G)" : "Catalogo dei giochi (G)";
            if (!libraryOpen)
            {
                bannerRect = new RectangleF(Pad, TopH + 14, w - Pad * 2, HeroH + BannerStatusH);
                float top = ArrangeToolbar(w, bannerRect.Bottom + 12);
                ArrangeGrid(w, h, top);
                float by = h - BottomH + 16;
                float x = AddButton("connect", Kind.Primary, NativeMode ? "Attiva obiettivi" : "Collega Cemu", "A", Pad, by, Connect, CanConnect, 0) + 10;
                x = AddButton("disconnect", Kind.Button, NativeMode ? "Disattiva" : "Scollega", "Y", x, by, () => Disconnect("Sessione scollegata."), CanDisconnect, 0) + 10;
                if (CanDisconnect) AddButton("pause", Kind.Button, paused ? "Riprendi" : "Pausa", "P", x, by, TogglePause, memory != null || nativeListening || demo, 0);
                else if (!NativeMode) AddButton("demo", Kind.Button, "Prova demo", "X", x, by, StartDemo, CanConnect, 0);
                ArrangeMenu(w, h);
            }
            else ArrangeLibrary(w, h);

            drawerRect = new RectangleF(Pad, h - BottomH - DrawerH * Theme.EaseOut(drawer), w - Pad * 2, DrawerH + 24);
            if (drawer > 0) Put("log-close", Kind.Icon, new RectangleF(drawerRect.Right - 54, drawerRect.Y + 14, 36, 36), "Chiudi registro", CloseDrawer, true, 0);

            float slide = Motion.Reduced ? 1 : Theme.EaseOut(sheet);
            sheetRect = new RectangleF(w - SheetW * slide, 0, SheetW, h);
            if (sheet > 0)
            {
                float sx = sheetRect.X;
                AddButton("back", Kind.Button, "Indietro", "B", sx + SheetW - 24 - ButtonWidth("Indietro", "B"), 13, CloseSheet, true, 1, 38);
                if (cloudPage)
                {
                    bool signedIn = cloud != null && cloud.IsSignedIn;
                    bool canAuthenticate = !cloudBusy && !signedIn;
                    string[] ids = { "field-project", "field-apikey", "field-email", "field-pass" };
                    string[] labels = { "Project ID Firebase", "Web API key", "Email account", "Password" };
                    TextBox[] boxes = { cloudProject, cloudApiKey, cloudEmail, cloudPassword };
                    for (int i = 0; i < boxes.Length; i++) { var box = boxes[i]; Put(ids[i], Kind.Field, new RectangleF(sx + 24, 134 + i * 78, SheetW - 48, 40), labels[i], () => FocusField(box), canAuthenticate, 1).Box = box; }
                    float bw = (SheetW - 72) / 3;
                    AddButton("cloud-signup", Kind.Button, "Registrati", null, sx + 24, 450, CloudSignUp, canAuthenticate, 1, 40, bw);
                    AddButton("cloud-login", Kind.Button, "Accedi", null, sx + 36 + bw, 450, CloudLogin, canAuthenticate, 1, 40, bw);
                    AddButton("cloud-logout", Kind.Button, "Esci", null, sx + 48 + bw * 2, 450, CloudLogout, !cloudBusy && signedIn, 1, 40, bw);
                }
                else
                {
                    Put("field-cemu", Kind.Field, new RectangleF(sx + 24, 124, SheetW - 48 - 112, 40), "Percorso di Cemu.exe", () => FocusField(cemuPath), FieldsEditable && !NativeMode, 1).Box = cemuPath;
                    AddButton("browse", Kind.Button, "Sfoglia…", null, sx + SheetW - 24 - 102, 124, Browse, FieldsEditable && !NativeMode, 1, 40, 102);
                    Put("field-player", Kind.Field, new RectangleF(sx + 24, 198, SheetW - 48, 40), "Profilo locale", () => FocusField(player), FieldsEditable, 1).Box = player;
                    Put("sound", Kind.Toggle, new RectangleF(sx + 24, 314, SheetW - 48, 44), "Suono di sblocco", ToggleSound, true, 1).Checked = soundOn;
                    Put("ambient", Kind.Toggle, new RectangleF(sx + 24, 362, SheetW - 48, 44), "Animazioni del banner", ToggleAmbient, true, 1).Checked = ambientOn;
                    Put("motion", Kind.Toggle, new RectangleF(sx + 24, 410, SheetW - 48, 44), "Movimento ridotto", ToggleMotion, true, 1).Checked = Motion.Reduced;
                    AddButton("test", Kind.Button, "Prova notifica", null, sx + 24, 466, TestToast, true, 1, 40);
                    AddButton("cloud-page", Kind.Button, "Account cloud…", null, sx + SheetW - 196, 466, OpenCloudPage, true, 1, 40, 172);
                }
            }
            if (dialog > 0 && detail != null) ArrangeDialog(w, h);
        }
        void PlaceFields()
        {
            bool show = sheet >= 1 && sheetTarget >= 1;
            foreach (var box in new[] { cemuPath, player, cloudEmail, cloudPassword, cloudProject, cloudApiKey })
            {
                Widget w;
                string id = box == cemuPath ? "field-cemu" : box == player ? "field-player" : box == cloudEmail ? "field-email" : box == cloudPassword ? "field-pass" : box == cloudProject ? "field-project" : "field-apikey";
                if (show && widgets.TryGetValue(id, out w) && w.Visible)
                {
                    var r = w.Bounds;
                    int height = box.PreferredHeight;
                    var device = new Rectangle((int)Math.Round((r.X + 13) * scale), (int)Math.Round(r.Y * scale + (r.Height * scale - height) / 2), (int)((r.Width - 26) * scale), height);
                    if (box.Bounds != device) box.Bounds = device;
                    box.ReadOnly = box == cemuPath || box == player ? !FieldsEditable || (box == cemuPath && NativeMode) : cloudBusy || (cloud != null && cloud.IsSignedIn);
                    box.BackColor = box.ReadOnly ? Color.FromArgb(240, 243, 245) : Color.White;
                    if (!box.Visible) box.Visible = true;
                }
                else if (box.Visible)
                {
                    if (box.Focused) { ActiveControl = null; Focus(); }
                    box.Visible = false;
                }
            }
            PlaceSearch();
        }

        // ---------------------------------------------------------------- animation
        void Kick()
        {
            if (animation.Enabled || IsDisposed || (NativeMode && !Visible)) return;
            lastFrame = (float)clock.Elapsed.TotalSeconds;
            FramePacing.Begin();
            animation.Start();
        }
        static bool Step(ref float value, float target, float delta)
        {
            if (value == target) return false;
            value = value < target ? Math.Min(target, value + delta) : Math.Max(target, value - delta);
            return true;
        }
        Rectangle Device(RectangleF r)
        {
            return Rectangle.FromLTRB((int)Math.Floor(r.Left * scale) - 2, (int)Math.Floor(r.Top * scale) - 2, (int)Math.Ceiling(r.Right * scale) + 2, (int)Math.Ceiling(r.Bottom * scale) + 2);
        }
        void Animate(object sender, EventArgs e) { RunFrame(); }
        // While animating and with no input waiting, frames run back to back, each paced by the
        // compositor; the timer covers the rest (and polls for new video frames).
        void OnIdle(object sender, EventArgs e)
        {
            for (int guard = 0; guard < 2000 && animation.Enabled && !IsDisposed && FramePacing.Available && !FramePacing.MessagePending(); guard++)
                if (!RunFrame()) break;
        }
        // One animation step: update, paint what changed, then wait for it to be presented.
        // Returns true when a presented frame was waited for.
        bool RunFrame()
        {
            if (tickLog != null) tickLog.Add(clock.Elapsed.TotalMilliseconds);
            var work = Stopwatch.StartNew();
            frameDirty = false;
            try { AnimateFrame(); }
            finally { animateMs = work.Elapsed.TotalMilliseconds; }
            if (!frameDirty || !Visible || WindowState == FormWindowState.Minimized || !IsHandleCreated) return false;
            var step = Stopwatch.StartNew();
            inFrame = true;
            try { Update(); } finally { inFrame = false; }
            updateMs = step.Elapsed.TotalMilliseconds; step.Restart();
            bool paced = FramePacing.WaitForFrame();
            waitMs = step.Elapsed.TotalMilliseconds;
            if (frameDetails != null) frameDetails.Add(new[] { clock.Elapsed.TotalMilliseconds, animateMs, updateMs, waitMs, paintMs });
            return paced;
        }
        double updateMs, waitMs;
        List<double> tickLog; // set by diagnostics and tests to record every animation tick
        List<double[]> frameDetails; // diagnostics: end time, animate, update, wait and paint ms
        bool frameDirty, inFrame;
        void Dirty(Rectangle r) { Invalidate(r); frameDirty = true; }
        void Dirty() { Invalidate(); frameDirty = true; }
        void AnimateFrame()
        {
            float now = (float)clock.Elapsed.TotalSeconds, dt = Math.Max(0.001f, Math.Min(0.05f, now - lastFrame));
            lastFrame = now;
            // Repaint only what moved: a whole-window repaint at ultrawide sizes costs several
            // frames' budget. "busy" means the layout of overlapping layers is changing.
            bool busy = false;
            var dirty = new List<RectangleF>();
            foreach (var w in widgets.Values)
            {
                float hv = Theme.Approach(w.Hover, w == hovered && w.Enabled && w.Visible ? 1 : 0, dt, HoverRate);
                float pr = Theme.Approach(w.Press, w == pressed && w.Enabled ? 1 : 0, dt, PressRate);
                if (hv != w.Hover || pr != w.Press)
                {
                    w.Hover = hv; w.Press = pr;
                    if (w.Visible) dirty.Add(Theme.Inflate(w.Bounds, 24, 24));
                }
                if (w.Kind != Kind.Toggle || !w.Visible) continue;
                float v = w.Value < 0 || Motion.Reduced ? (w.Checked ? 1 : 0) : Theme.Approach(w.Value, w.Checked ? 1 : 0, dt, 14);
                if (v != w.Value) { w.Value = v; dirty.Add(Theme.Inflate(w.Bounds, 8, 8)); }
            }
            // Panels: the same eased timing everywhere; reduced motion keeps a brief fade.
            float panel = Motion.Reduced ? 0.12f : PanelTime;
            float sheetBefore = sheet;
            busy |= Step(ref sheet, sheetTarget, dt / panel);
            busy |= Step(ref drawer, drawerTarget, dt / panel);
            busy |= Step(ref dialog, dialogTarget, dt / panel);
            busy |= Step(ref menuOpen, menuTarget, dt / (Motion.Reduced ? 0.1f : MenuTime));
            if (menuOpen <= 0 && menuTarget <= 0 && menu != null) { menu = null; busy = true; }
            if (dialog >= 1 && detail != null && (progress != null && progress.Contains(detail.Id) && !Motion.Reduced || now - detailSwap < DetailSlide + 0.05f))
                dirty.Add(Theme.Inflate(dialogRect, 44, 44));
            float s = Theme.Approach(scroll, scrollTarget, dt, 14);
            if (Math.Abs(s - scrollTarget) < 0.3f || Motion.Reduced) s = scrollTarget;
            bool scrolled = s != scroll;
            if (scrolled) scroll = s;
            Widget tab;
            if (!libraryOpen && widgets.TryGetValue("tab" + filter, out tab) && tab.Visible)
            {
                if (tabX < 0 || Motion.Reduced) { tabX = tab.Bounds.X; tabW = tab.Bounds.Width; }
                float nx = Theme.Approach(tabX, tab.Bounds.X, dt, 18), nw = Theme.Approach(tabW, tab.Bounds.Width, dt, 18);
                if (Math.Abs(nx - tab.Bounds.X) < 0.3f) nx = tab.Bounds.X;
                if (Math.Abs(nw - tab.Bounds.Width) < 0.3f) nw = tab.Bounds.Width;
                if (nx != tabX || nw != tabW) { tabX = nx; tabW = nw; dirty.Add(Theme.Inflate(toolbarRect, 4, 4)); }
            }
            float ratio = ActiveCount == 0 ? 0 : UnlockedCount / (float)ActiveCount;
            float nr = Motion.Reduced ? ratio : Theme.Approach(shownRatio, ratio, dt, 4.5f);
            if (nr != shownRatio) { shownRatio = nr; dirty.Add(Theme.Inflate(bannerRect, 22, 22)); }
            float rupees = current == null ? 0 : current.Rupees;
            if (shownRupees < 0) shownRupees = rupees;
            if (scrolled || nr != ratio) InvalidateScene();
            float nrp = Motion.Reduced ? rupees : Theme.Approach(shownRupees, rupees, dt, 7);
            if (Math.Abs(nrp - rupees) < 0.5f) nrp = rupees;
            if (nrp != shownRupees) { shownRupees = nrp; dirty.Add(Theme.Inflate(bannerRect, 22, 22)); }
            var gridBand = new RectangleF(0, gridRect.Y, W, gridRect.Height);
            if (now - introStart < IntroSpan + 0.05f) dirty.Add(libraryOpen ? new RectangleF(0, 0, W, H) : gridBand);
            if (celebrations.Count > 0)
            {
                foreach (var id in celebrations.Where(c => now - c.Value > CelebrationTime).Select(c => c.Key).ToList()) celebrations.Remove(id);
                dirty.Add(gridBand);
            }
            if (sheet != sheetBefore || sheet >= 1) { Arrange(); PlaceFields(); }
            else PlaceSearch();
            bool ambient = ambientOn && WindowState != FormWindowState.Minimized && ActiveForm == this;
            var playing = BannerVideo;
            if (playing != null) playing.SetPlaying(ambient);
            if (ambient) ambientTime += dt;
            if (busy) { Dirty(); return; }
            // One paint covers the bounding box of everything invalidated: while the grid
            // scrolls, the banner waits instead of stretching the box over the whole window.
            if (scrolled) dirty.Add(gridBand);
            bool idle = dirty.Count == 0;
            foreach (var r in dirty) Dirty(Device(r));
            if (scrolled) return;
            if (ambient)
            {
                // The banner only changes when the video has a new frame (or the drawn sea moves).
                int frames = playing != null && playing.HasFrame ? playing.Frames : -1;
                if (frames < 0 || frames != shownVideoFrame || libraryOpen)
                {
                    shownVideoFrame = frames;
                    Dirty(Device(libraryOpen ? Theme.Inflate(libraryArtRect, 2, 2) : new RectangleF(bannerRect.X, bannerRect.Y, bannerRect.Width, HeroH)));
                }
                idle = false;
            }
            // Only a connection in progress animates its status (a turning arc); steady states stay still.
            if (statusKind == StatusKind.Busy)
            {
                Dirty(Device(Theme.Inflate(statusPill, 4, 4)));
                if (!libraryOpen) Dirty(Device(StatusIconRect()));
                idle = false;
            }
            if (focusVisible && focused != null && focused.Visible && ContainsFocus) { Dirty(Device(Theme.Inflate(focused.Bounds, 16, 16))); idle = false; }
            // Quiet frames prepare upcoming cards, a few milliseconds at a time.
            if (celebrations.Count == 0 && now - introStart > IntroSpan && sheet <= 0 && dialog <= 0 && PrewarmTiles(now, 4)) idle = false;
            if (idle) { animation.Stop(); FramePacing.End(); }
        }
        void Celebrate(Achievement a) { celebrations[a.Id] = (float)clock.Elapsed.TotalSeconds; Kick(); }
        float Celebration(string id, float t)
        {
            float start;
            if (!celebrations.TryGetValue(id, out start)) return -1;
            float u = (t - start) / CelebrationTime;
            return u >= 1 ? -1 : Math.Max(0, u);
        }
        // The selection cursor breathes gently; with reduced motion it is steady.
        float FocusStrength(Widget w, float t)
        {
            if (!focusVisible || w != focused || !ContainsFocus) return 0;
            return Motion.Reduced ? 1 : 0.78f + 0.22f * (float)Math.Sin(t * 4.5f);
        }

        // ---------------------------------------------------------------- painting
        protected override void OnPaintBackground(PaintEventArgs e) { }
        // Frame timings, kept for the regression suite and diagnostics (no logging).
        readonly Stopwatch frameWatch = new Stopwatch();
        double paintMs, animateMs;
        protected override void OnPaint(PaintEventArgs e)
        {
            var c = e.ClipRectangle;
            frameWatch.Restart();
            Render(e.Graphics, new RectangleF(c.X / scale, c.Y / scale, c.Width / scale, c.Height / scale));
            paintMs = frameWatch.Elapsed.TotalMilliseconds;
        }
        // Used by the window and by the --preview rendering check of our own UI.
        public void SavePreview(string path)
        {
            using (var bitmap = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height)))
            {
                using (var g = Graphics.FromImage(bitmap)) Render(g, new RectangleF(0, 0, W, H));
                bitmap.Save(path, ImageFormat.Png);
            }
        }
        // Under the settings sheet and the detail card the page is frozen: it is drawn once into a
        // device-size picture and copied, so opening and animating a panel costs the panel alone.
        // sceneVersion moves whenever page content changes (status, progress, log, scroll).
        Bitmap scene;
        int sceneVersion, sceneBuilt = -1;
        void InvalidateScene() { sceneVersion++; }
        void Render(Graphics g, RectangleF clip)
        {
            Arrange();
            float t = (float)clock.Elapsed.TotalSeconds;
            if (sheet > 0 || (dialog > 0 && detail != null))
            {
                var size = ClientSize;
                if (scene == null || scene.Size != size || sceneBuilt != sceneVersion)
                {
                    if (scene == null || scene.Size != size)
                    {
                        if (scene != null) scene.Dispose();
                        scene = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
                    }
                    using (var page = Graphics.FromImage(scene)) RenderPage(page, new RectangleF(0, 0, W, H), t);
                    sceneBuilt = sceneVersion;
                }
                var mode = g.CompositingMode;
                g.CompositingMode = CompositingMode.SourceCopy; g.InterpolationMode = InterpolationMode.NearestNeighbor;
                var area = Rectangle.Intersect(Device(clip), new Rectangle(0, 0, size.Width, size.Height));
                if (area.Width > 0 && area.Height > 0) g.DrawImage(scene, area, area.X, area.Y, area.Width, area.Height, GraphicsUnit.Pixel);
                g.CompositingMode = mode; g.InterpolationMode = InterpolationMode.Bilinear;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(scale, scale);
                if (sheet > 0) DrawSheet(g, t);
                if (dialog > 0 && detail != null) DrawDialog(g, t);
                return;
            }
            if (scene != null) { scene.Dispose(); scene = null; }
            RenderPage(g, clip, t);
        }
        void RenderPage(Graphics g, RectangleF clip, float t)
        {
            DrawBackdrop(g, clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(scale, scale);
            if (libraryOpen) DrawLibrary(g, clip, t);
            else
            {
                if (clip.IntersectsWith(gridRect)) DrawGrid(g, clip, t);
                // The whole toolbar band, shadows included, is repainted as one: a small hover or
                // video repaint can touch only part of it (e.g. the lower filter row).
                if (clip.IntersectsWith(new RectangleF(0, bannerRect.Bottom, W, gridRect.Y - bannerRect.Bottom))) DrawToolbar(g, t);
                if (clip.IntersectsWith(Theme.Inflate(bannerRect, 22, 22))) DrawBanner(g, t);
                if (menuOpen > 0) DrawMenu(g, t);
            }
            if (clip.IntersectsWith(new RectangleF(0, 0, W, TopH + 10))) DrawTopBar(g, t);
            if (drawer > 0) DrawDrawer(g, t);
            if (clip.IntersectsWith(new RectangleF(0, H - BottomH - 12, W, BottomH + 12))) DrawBottomBar(g, t, clip.Bottom > H - BottomH + 12);
        }
        void DrawBackdrop(Graphics g, RectangleF clip)
        {
            var size = ClientSize;
            if (size.Width < 1 || size.Height < 1) return;
            if (backdrop == null || backdrop.Size != size || backdropStyle != Style)
            {
                backdropStyle = Style;
                if (backdrop != null) backdrop.Dispose();
                backdrop = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (var b = Graphics.FromImage(backdrop))
                {
                    b.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new LinearGradientBrush(new Rectangle(0, -1, size.Width, size.Height + 2), Style.BackTop, Style.BackBottom, 90f))
                        b.FillRectangle(brush, 0, 0, size.Width, size.Height);
                    Blob(b, new PointF(size.Width * 0.04f, size.Height * 0.42f), size.Width * 0.5f, Color.FromArgb(46, Style.Glow));
                    Blob(b, new PointF(size.Width * 0.98f, size.Height * 0.95f), size.Width * 0.55f, Color.FromArgb(40, Style.Accent));
                    if (!Style.Themed)
                        using (var pen = new Pen(Color.FromArgb(10, 0, 50, 80), 1.3f * scale))
                            for (float d = -size.Height; d < size.Width; d += 8 * scale) b.DrawLine(pen, d, size.Height, d + size.Height, 0);
                    Style.DecorateBackdrop(b, size, scale);
                }
            }
            var mode = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            var area = Rectangle.Intersect(Device(clip), new Rectangle(0, 0, size.Width, size.Height));
            if (area.Width > 0 && area.Height > 0) g.DrawImage(backdrop, area, area.X, area.Y, area.Width, area.Height, GraphicsUnit.Pixel);
            g.CompositingMode = mode;
            g.InterpolationMode = InterpolationMode.Bilinear;
        }
        static void Blob(Graphics g, PointF c, float radius, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(Theme.Square(c, radius * 2));
                using (var b = new PathGradientBrush(path) { CenterColor = color, SurroundColors = new[] { Color.FromArgb(0, color) } }) g.FillPath(b, path);
            }
        }

        string StatusShort()
        {
            switch (statusKind)
            {
                case StatusKind.Live: return "Collegato";
                case StatusKind.Demo: return "Demo attiva";
                case StatusKind.Paused: return "In pausa";
                case StatusKind.Busy: return "Collegamento…";
                case StatusKind.Error: return "Errore";
                default: return "Non collegato";
            }
        }
        Color StatusColor()
        {
            switch (statusKind)
            {
                case StatusKind.Live: return Theme.Success;
                case StatusKind.Demo: return Theme.Warning;
                case StatusKind.Paused: return Theme.Warning;
                case StatusKind.Busy: return Theme.Accent;
                case StatusKind.Error: return Theme.Danger;
                default: return Color.FromArgb(160, 170, 178);
            }
        }

        void DrawTopBar(Graphics g, float t)
        {
            float w = W;
            var style = Style;
            Layer(g, "top-bar", new RectangleF(0, 0, w, TopH + 8), style.GameId, b =>
            {
                using (var brush = new LinearGradientBrush(new RectangleF(0, -1, w, TopH + 2), style.BarTop, style.BarBottom, 90f)) b.FillRectangle(brush, 0, 0, w, TopH);
                using (var brush = new LinearGradientBrush(new RectangleF(0, TopH - 1, w, 10), Color.FromArgb(28, 20, 44, 64), Color.FromArgb(0, 20, 44, 64), 90f)) b.FillRectangle(brush, 0, TopH, w, 8);
                using (var pen = new Pen(style.BarLine)) b.DrawLine(pen, 0, TopH - 0.5f, w, TopH - 0.5f);
                style.DecorateTopBar(b, w, TopH);
                // Avatar and the brand (resampled from a large source) are static: they live in
                // the cached bar instead of being scaled again on every repaint.
                var face = new RectangleF(Pad, 20, 40, 40);
                using (var brush = new SolidBrush(Color.FromArgb(30, 20, 44, 64))) b.FillEllipse(brush, Theme.Inflate(face, 3, 3).X, face.Y - 1, face.Width + 6, face.Height + 6);
                using (var brush = new SolidBrush(Color.White)) b.FillEllipse(brush, Theme.Inflate(face, 3, 3));
                Theme.Avatar(b, face);
                // Symmetrical space is reserved for the brand, independent of profile/status text.
                if (!BrandArt.DrawLogo(b, new RectangleF(w / 2 - 104, 10, 208, 58)))
                    Theme.Text(b, "Wii U Achievements", Theme.Font(20, Weight.Heavy), Theme.Ink,
                        new RectangleF(w / 2 - 140, 20, 280, 40), StringAlignment.Center, StringAlignment.Center, false);
            });
            var avatar = new RectangleF(Pad, 20, 40, 40);
            // The status ring around the avatar repeats the connection state with a shape of its own.
            DrawAvatarRing(g, Theme.Inflate(avatar, 5, 5), t);
            string name = String.IsNullOrWhiteSpace(player.Text) ? "Profilo senza nome" : player.Text.Trim();
            var nameFont = Theme.Font(16, Weight.Bold);
            float profileWidth = Math.Min(240, w / 2 - 194);
            Theme.Text(g, name, nameFont, Theme.Ink, new RectangleF(Pad + 56, 18, profileWidth, 22));
            statusPill = new RectangleF(Pad + 56, 42, profileWidth, 20);
            using (var b = new SolidBrush(StatusColor())) g.FillEllipse(b, statusPill.X, statusPill.Y + 6, 8, 8);
            Theme.Text(g, StatusShort(), Theme.Font(12.5f, Weight.Bold), Theme.Mix(StatusColor(), Theme.Ink, 0.45f),
                new RectangleF(statusPill.X + 14, statusPill.Y, profileWidth - 14, 20));

            DrawButton(g, widgets["games"], t);
            DrawIconButton(g, widgets["settings"], t, (gg, r, c) => Theme.Gear(gg, r, c));
            var log = widgets["log"];
            DrawIconButton(g, log, t, (gg, r, c) => Theme.ListIcon(gg, r, c));
            if (unreadLogs > 0)
            {
                string n = unreadLogs > 99 ? "99+" : unreadLogs.ToString(CultureInfo.InvariantCulture);
                var f = Theme.Font(11, Weight.Heavy);
                float bw = Math.Max(20, Measure(n, f).Width + 10);
                var badge = new RectangleF(log.Bounds.Right - bw + 6, log.Bounds.Y - 4, bw, 20);
                Theme.Fill(g, Theme.Inflate(badge, 1.5f, 1.5f), 11.5f, Color.White, Color.White);
                Theme.Fill(g, badge, 10, Color.FromArgb(244, 96, 104), Theme.Danger);
                Theme.Text(g, n, f, Color.White, badge, StringAlignment.Center, StringAlignment.Center, false);
            }
        }
        void DrawAvatarRing(Graphics g, RectangleF r, float t)
        {
            Color c = StatusColor();
            if (statusKind == StatusKind.Busy && !Motion.Reduced)
                using (var pen = new Pen(c, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, r, (t * 300) % 360, 250);
            else if (statusKind == StatusKind.Paused)
                using (var pen = new Pen(c, 2.2f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(pen, r);
            else if (statusKind != StatusKind.Idle)
                using (var pen = new Pen(c, 2.2f)) g.DrawEllipse(pen, r);
        }
        void DrawIconButton(Graphics g, Widget w, float t, Action<Graphics, RectangleF, Color> icon)
        {
            var r = w.Bounds;
            if (w.Press > 0) r = Theme.Inflate(r, -1.5f * w.Press, -1.5f * w.Press);
            float hv = w.Enabled ? w.Hover : 0;
            float alpha = w.Enabled ? 1 : 0.45f;
            using (var b = new SolidBrush(Color.FromArgb((int)((28 + 26 * hv) * alpha), 20, 44, 64))) g.FillEllipse(b, r.X, r.Y + 2, r.Width, r.Height);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(r);
                using (var b = new LinearGradientBrush(Theme.Inflate(r, 0, 1), Theme.Alpha(Theme.Mix(Style.ButtonTop, Color.White, hv * 0.5f), alpha), Theme.Alpha(Style.ButtonBottom, alpha), 90f)) g.FillPath(b, path);
            }
            using (var pen = new Pen(Theme.Alpha(Theme.Mix(Style.Themed ? Style.ButtonEdge : Color.FromArgb(196, 205, 212), Style.Accent, hv), alpha), (Style.Themed ? 1.4f : 1) + hv)) g.DrawEllipse(pen, r);
            icon(g, Theme.Inflate(r, -r.Width * 0.27f, -r.Height * 0.27f), Theme.Alpha(Theme.Mix(Style.Themed ? Style.GlyphFill : Color.FromArgb(108, 118, 126), Style.AccentDark, hv), alpha));
            Theme.Cursor(g, r, r.Width / 2, FocusStrength(w, t));
        }

        // Device-resolution bitmaps rebuilt only when their key changes; frames copy them 1:1.
        sealed class LayerImage { public Bitmap Bitmap; public string Key; }
        readonly Dictionary<string, LayerImage> layers = new Dictionary<string, LayerImage>();
        int layerBuilds;
        void ClearLayers() { foreach (var layer in layers.Values) layer.Bitmap.Dispose(); layers.Clear(); }
        void Layer(Graphics g, string id, RectangleF area, string key, Action<Graphics> paint)
        {
            key += string.Format(CultureInfo.InvariantCulture, "|{0:0.0},{1:0.0},{2:0.0},{3:0.0}@{4}", area.X, area.Y, area.Width, area.Height, scale);
            LayerImage layer;
            if (!layers.TryGetValue(id, out layer) || layer.Key != key)
            {
                if (layer != null) layer.Bitmap.Dispose();
                var bitmap = new Bitmap(Math.Max(1, (int)Math.Ceiling(area.Width * scale)), Math.Max(1, (int)Math.Ceiling(area.Height * scale)), PixelFormat.Format32bppPArgb);
                try
                {
                    using (var b = Graphics.FromImage(bitmap))
                    {
                        // Text over transparency must use greyscale antialiasing (ClearType needs a backdrop).
                        b.SmoothingMode = SmoothingMode.AntiAlias; b.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; b.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        b.ScaleTransform(scale, scale); b.TranslateTransform(-area.X, -area.Y);
                        paint(b);
                    }
                }
                catch { bitmap.Dispose(); layers.Remove(id); throw; }
                layers[id] = layer = new LayerImage { Bitmap = bitmap, Key = key };
                layerBuilds++;
            }
            var interpolation = g.InterpolationMode; var offset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(layer.Bitmap, (float)Math.Round(area.X * scale) / scale, (float)Math.Round(area.Y * scale) / scale, layer.Bitmap.Width / scale, layer.Bitmap.Height / scale);
            g.InterpolationMode = interpolation; g.PixelOffsetMode = offset;
        }

        RectangleF StatusIconRect() { return new RectangleF(bannerRect.X + 18, bannerRect.Y + HeroH + 12, 36, 36); }
        void DrawBanner(Graphics g, float t)
        {
            var r = bannerRect;
            Layer(g, "banner-shadow", Theme.Inflate(r, 24, 24), "shadow", b => Theme.Shadow(b, r, 18, 9, 48, 4));
            var playing = BannerVideo;
            bool drawn = false;
            var hero = new RectangleF(r.X, r.Y, r.Width, HeroH);
            var state = g.Save();
            using (var clip = Theme.Round(r, 18)) g.SetClip(clip, CombineMode.Intersect);
            // Keep the picture and its logos separate from status text/live values.
            try { drawn = playing != null && playing.Draw(g, hero, 0); } catch (Exception) { drawn = false; }
            if (!drawn) banner.Draw(g, hero, 0, ambientTime, scale);
            g.Restore(state);
            // Everything above the moving picture changes only with the session state.
            string key = Style.GameId + drawn + statusKind + statusTitle + "|" + statusDetail + "|" + selectedGame.Id + "|" + UnlockedCount + "/" + ActiveCount + "|" + EarnedPoints + "|" +
                shownRatio.ToString("0.0000", CultureInfo.InvariantCulture) + "|" + (current == null ? "-" : Math.Round(shownRupees) + "," + current.Health + "," + current.MaxHealth);
            Layer(g, "banner-overlay", Theme.Inflate(r, 12, 12), key, b => DrawBannerOverlay(b, drawn));
            DrawStatusIcon(g, StatusIconRect(), t);
        }
        void DrawBannerOverlay(Graphics g, bool drawn)
        {
            var r = bannerRect;
            var style = Style;
            var hero = new RectangleF(r.X, r.Y, r.Width, HeroH);
            var strip = new RectangleF(r.X, hero.Bottom, r.Width, BannerStatusH);
            var state = g.Save();
            using (var clip = Theme.Round(r, 18)) g.SetClip(clip, CombineMode.Intersect);
            if (drawn)
            {
                using (var b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, 422, hero.Height), Color.FromArgb(205, 3, 38, 65), Color.FromArgb(0, 3, 38, 65), 0f))
                    g.FillRectangle(b, r.X, r.Y, 420, hero.Height);
            }
            Theme.Fill(g, strip, 0, style.BarTop, style.BarBottom);
            style.Parchment(g, strip, 0, 0.3f);
            // A thin sea-and-foam seam between the picture and the status: the shoreline.
            if (style.Themed) style.WaveBandCached(g, new RectangleF(r.X, strip.Y - 1, r.Width, 8), 0, 0.85f);
            else using (var pen = new Pen(Color.FromArgb(110, style.TrimDark), 1)) g.DrawLine(pen, r.X, strip.Y, r.Right, strip.Y);
            g.Restore(state);
            Theme.Stroke(g, r, 18, style.Themed ? style.ButtonEdge : Theme.Line, 1.2f);
            style.BannerCorners(g, r);

            var logoArea = new RectangleF(hero.X + 34, hero.Y + 12, 248, hero.Height - 24);
            if (!style.DrawArt(g, "logo.png", logoArea, 1))
                Theme.ShadowText(g, selectedGame.Title, Theme.Font(29, Weight.Heavy), Color.White, logoArea, StringAlignment.Center, StringAlignment.Center, true, 90);
            float radius = Math.Min(61, hero.Height / 2 - 8);
            DrawRing(g, new PointF(hero.Right - 30 - radius, hero.Y + hero.Height / 2), radius);

            // Status on the left; on the right the live values or the local score and configuration.
            float right = current != null ? 334 : 360;
            float tx = strip.X + 66, textW = strip.Width - 66 - right - 16;
            Theme.Text(g, statusTitle, Theme.Font(15, Weight.Heavy), Theme.Ink, new RectangleF(tx, strip.Y + 8, textW, 22));
            Theme.Text(g, statusDetail, Theme.Font(12.5f, Weight.Regular), style.Muted, new RectangleF(tx, strip.Y + 31, textW, 22));
            if (current != null) DrawChips(g, strip.Right - 328, strip.Y + 9);
            else
            {
                var profile = selectedGame.Profiles[0];
                float x = strip.Right - 22;
                var cfg = new RectangleF(x - 176, strip.Y + 9, 176, 22);
                Theme.Text(g, profile.Label + " · Cemu " + profile.CemuVersion, Theme.Font(13, Weight.Bold), Theme.Ink, cfg, StringAlignment.Far, StringAlignment.Center, false);
                Theme.Text(g, "Playthrough TEST · sola lettura", Theme.Font(11.5f, Weight.Regular), style.Muted, new RectangleF(x - 200, strip.Y + 31, 200, 18), StringAlignment.Far, StringAlignment.Center, false);
                using (var pen = new Pen(Color.FromArgb(110, style.Themed ? style.ButtonEdge : Theme.Line), 1)) g.DrawLine(pen, x - 190, strip.Y + 12, x - 190, strip.Bottom - 12);
                int total = rules.Where(a => !a.IsPlanned).Sum(a => a.Points);
                Metric(g, x - 340, strip.Y + 9, (gg, rr) => Theme.Star(gg, rr, Color.FromArgb(255, 228, 118), Color.FromArgb(226, 152, 20)), EarnedPoints + " / " + total, "PUNTI LOCALI", 130);
            }
        }
        // Connection state as a coloured disc with its own symbol: the state reads by shape too.
        void DrawStatusIcon(Graphics g, RectangleF r, float t)
        {
            Color c = statusKind == StatusKind.Idle ? Theme.LockedInk : StatusColor();
            Theme.Shadow(g, r, r.Width / 2, 3, 30, 1.5f);
            using (var b = new LinearGradientBrush(Theme.Inflate(r, 0, 1), Theme.Light(c, 0.2f), Theme.Dark(c, 0.12f), 90f)) g.FillEllipse(b, r);
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f)) g.DrawEllipse(pen, Theme.Inflate(r, -1, -1));
            var i = Theme.Inflate(r, -r.Width * 0.27f, -r.Height * 0.27f);
            switch (statusKind)
            {
                case StatusKind.Live: Theme.Check(g, Theme.Inflate(i, 2, 2), Color.White, 2.6f); break;
                case StatusKind.Demo: using (var path = new GraphicsPath()) { path.AddPolygon(new[] { new PointF(i.X + i.Width * 0.22f, i.Y + 1), new PointF(i.Right - 1, i.Y + i.Height / 2), new PointF(i.X + i.Width * 0.22f, i.Bottom - 1) }); Theme.Fill(g, path, Color.White); } break;
                case StatusKind.Paused:
                    using (var b = new SolidBrush(Color.White)) { g.FillRectangle(b, i.X + i.Width * 0.18f, i.Y + 1, i.Width * 0.22f, i.Height - 2); g.FillRectangle(b, i.X + i.Width * 0.6f, i.Y + 1, i.Width * 0.22f, i.Height - 2); }
                    break;
                case StatusKind.Busy:
                    using (var pen = new Pen(Color.White, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, Theme.Inflate(i, 1, 1), Motion.Reduced ? 0 : (t * 320) % 360, 270);
                    break;
                case StatusKind.Error:
                    using (var pen = new Pen(Color.White, 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, i.X + i.Width / 2, i.Y + 1, i.X + i.Width / 2, i.Y + i.Height * 0.58f);
                    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, Theme.Square(new PointF(i.X + i.Width / 2, i.Bottom - 2), 3.6f));
                    break;
                default:
                    // Ready: a plug waiting for its socket.
                    using (var pen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(pen, i.X + i.Width * 0.36f, i.Y, i.X + i.Width * 0.36f, i.Y + i.Height * 0.3f);
                        g.DrawLine(pen, i.X + i.Width * 0.64f, i.Y, i.X + i.Width * 0.64f, i.Y + i.Height * 0.3f);
                        g.DrawLine(pen, i.X + i.Width / 2, i.Y + i.Height * 0.78f, i.X + i.Width / 2, i.Bottom + 1);
                    }
                    using (var body = Theme.Round(new RectangleF(i.X + i.Width * 0.18f, i.Y + i.Height * 0.3f, i.Width * 0.64f, i.Height * 0.48f), i.Width * 0.14f)) Theme.Fill(g, body, Color.White);
                    break;
            }
        }
        static string Hearts(int quarters)
        {
            string[] parts = { "", "¼", "½", "¾" };
            int whole = quarters / 4, rest = quarters % 4;
            return whole == 0 && rest > 0 ? parts[rest] : whole + parts[rest];
        }
        void DrawChips(Graphics g, float x, float y)
        {
            Metric(g, x, y, (gg, r) => Theme.Gem(gg, r, Theme.GemColor(0), 0), ((int)Math.Round(shownRupees)).ToString(CultureInfo.InvariantCulture), "RUPIE", 80);
            Metric(g, x + 106, y, (gg, r) => Theme.HeartQuarters(gg, r, 4), Hearts(current.Health) + "/" + Math.Max(1, current.MaxHealth / 4), "CUORI", 80);
            Metric(g, x + 212, y, (gg, r) => Theme.Star(gg, r, Color.FromArgb(255, 228, 118), Color.FromArgb(226, 152, 20)), EarnedPoints.ToString(CultureInfo.InvariantCulture), "PUNTI", 80);
        }
        void Metric(Graphics g, float x, float y, Action<Graphics, RectangleF> icon, string value, string label, float width)
        {
            icon(g, new RectangleF(x, y + 4, 21, 23));
            Theme.Text(g, value, Theme.Font(18, Weight.Heavy), Theme.Ink, new RectangleF(x + 28, y - 1, width, 26));
            Theme.Text(g, label, Theme.Font(11, Weight.Heavy), Style.Muted, new RectangleF(x + 28, y + 24, width, 16));
        }
        void DrawRing(Graphics g, PointF c, float R)
        {
            float k = R / 61f;
            var disc = Theme.Square(c, R * 2);
            Theme.Shadow(g, disc, R, 8, 42, 2);
            using (var b = new LinearGradientBrush(Theme.Inflate(disc, 0, 1), Color.FromArgb(246, Style.CardTop), Color.FromArgb(238, Style.Themed ? Style.CardBottom : Color.FromArgb(231, 241, 246)), 90f)) g.FillEllipse(b, disc);
            Style.CompassRing(g, c, R);
            var arc = Theme.Square(c, 96 * k);
            using (var pen = new Pen(Style.Themed ? Style.TrackTop : Color.FromArgb(217, 227, 234), 8 * k)) g.DrawEllipse(pen, arc);
            bool full = shownRatio >= 0.999f;
            float sweep = 360 * shownRatio;
            if (sweep > 0.5f)
                using (var brush = new LinearGradientBrush(Theme.Inflate(arc, 8, 8), full ? Color.FromArgb(255, 220, 90) : Style.Glow, full ? Color.FromArgb(226, 146, 16) : Style.AccentDark, 45f))
                using (var pen = new Pen(brush, 8 * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, arc, -90, Math.Max(1, sweep));
            Theme.Text(g, ((int)Math.Round(shownRatio * 100)).ToString(CultureInfo.InvariantCulture) + "%", Theme.Font(26 * Math.Max(0.85f, k), Weight.Heavy), Theme.Ink, new RectangleF(c.X - 48, c.Y - 30 * k, 96, 37 * k), StringAlignment.Center, StringAlignment.Center, false);
            Theme.Text(g, UnlockedCount + " di " + ActiveCount, Theme.Font(12, Weight.Bold), Style.Muted, new RectangleF(c.X - 48, c.Y + 6 * k, 96, 18), StringAlignment.Center, StringAlignment.Center, false);
            if (k > 0.9f) Theme.Text(g, "ATTIVI", Theme.Font(10, Weight.Heavy), Style.AccentDark, new RectangleF(c.X - 48, c.Y + 23, 96, 14), StringAlignment.Center, StringAlignment.Center, false);
        }

        void DrawBottomBar(Graphics g, float t, bool contents = true)
        {
            float w = W, y = H - BottomH;
            using (var b = new LinearGradientBrush(new RectangleF(0, y - 10, w, 11), Color.FromArgb(0, 20, 44, 64), Color.FromArgb(22, 20, 44, 64), 90f)) g.FillRectangle(b, 0, y - 9, w, 9);
            var style = Style;
            Layer(g, "bottom-bar", new RectangleF(0, y - 12, w, BottomH + 12), style.GameId, b =>
            {
                using (var brush = new LinearGradientBrush(new RectangleF(0, y - 1, w, BottomH + 2), style.FootTop, style.FootBottom, 90f)) b.FillRectangle(brush, 0, y, w, BottomH);
                using (var pen = new Pen(style.FootLine)) b.DrawLine(pen, 0, y + 0.5f, w, y + 0.5f);
                using (var pen = new Pen(Color.White)) b.DrawLine(pen, 0, y + 1.5f, w, y + 1.5f);
                style.DecorateFooter(b, new RectangleF(0, y, w, BottomH), 0);
            });
            if (!contents) return;
            if (libraryOpen)
            {
                DrawButton(g, widgets["library-back"], t);
                float lx = widgets["library-back"].Bounds.Right + 24;
                Theme.Info(g, new RectangleF(lx, y + 29, 18, 18), Theme.Muted);
                Theme.Text(g, "Gioco selezionato: " + selectedGame.Title + ". Il cambio di gioco è possibile solo da scollegati.", Theme.Font(13, Weight.Bold), Theme.Muted,
                    new RectangleF(lx + 26, y + 12, w - lx - 26 - Pad, 52), StringAlignment.Near, StringAlignment.Center, true);
                return;
            }
            string last = CanDisconnect ? "pause" : "demo";
            foreach (var id in new[] { "connect", "disconnect", last }) DrawButton(g, widgets[id], t);
            float hx = widgets[last].Bounds.Right + 26;
            if (w - Pad - hx > 140)
            {
                Theme.Info(g, new RectangleF(hx, y + 29, 18, 18), style.Muted);
                Theme.Text(g, hint, Theme.Font(13, Weight.Regular), Theme.Mix(style.Muted, Theme.Ink, 0.25f), new RectangleF(hx + 26, y + 10, w - Pad - hx - 26, BottomH - 18), StringAlignment.Near, StringAlignment.Center, true);
            }
        }
        void DrawButton(Graphics g, Widget w, float t)
        {
            bool primary = w.Kind == Kind.Primary, on = w.Checked && w.Kind == Kind.Button;
            var style = Style;
            float hv = w.Enabled ? w.Hover : 0;
            var r = w.Bounds;
            if (w.Press > 0) r.Offset(0, 1.5f * w.Press);
            float radius = r.Height / 2;
            if (w.Enabled) Theme.Shadow(g, r, radius, 5 + 3 * hv, (int)(46 + 30 * hv), 2.5f - w.Press);
            Color top, bottom, edge, ink, glyph, glyphInk;
            if (primary || on)
            {
                top = Theme.Mix(style.PrimaryTop, Theme.Light(style.PrimaryTop, 0.3f), hv); bottom = Theme.Mix(style.PrimaryBottom, Theme.Dark(style.PrimaryBottom, 0.15f), w.Press);
                edge = Theme.Mix(style.PrimaryEdge, style.Themed ? style.Trim : style.PrimaryEdge, hv); ink = style.PrimaryInk; glyph = style.PrimaryGlyph; glyphInk = style.PrimaryGlyphInk;
            }
            else
            {
                top = Theme.Mix(style.ButtonTop, Color.White, hv * 0.6f); bottom = Theme.Mix(style.ButtonBottom, Theme.Dark(style.ButtonBottom, 0.06f), w.Press);
                edge = Theme.Mix(style.ButtonEdge, style.Themed ? style.Trim : Theme.Accent, hv); ink = style.ButtonInk; glyph = style.GlyphFill; glyphInk = style.GlyphInk;
            }
            if (!w.Enabled && primary)
            {
                top = Color.FromArgb(178, 212, 228); bottom = Color.FromArgb(152, 192, 212); edge = Color.FromArgb(140, 180, 200);
                ink = Color.FromArgb(240, 255, 255, 255); glyph = Color.FromArgb(220, 255, 255, 255); glyphInk = Color.FromArgb(140, 180, 200);
            }
            else if (!w.Enabled)
            {
                top = Color.FromArgb(244, 246, 248); bottom = Color.FromArgb(230, 234, 238); edge = Color.FromArgb(208, 214, 219);
                ink = Color.FromArgb(146, 155, 162); glyph = Color.FromArgb(196, 203, 209); glyphInk = Color.White;
            }
            Theme.Fill(g, r, radius, top, bottom);
            using (var gloss = Theme.Round(new RectangleF(r.X + 3, r.Y + 2, r.Width - 6, r.Height * 0.46f), radius - 3))
                Theme.Fill(g, gloss, Color.FromArgb(primary || on ? 56 : 110, 255, 255, 255));
            Theme.Stroke(g, r, radius, edge, style.Themed ? 1.6f + 0.4f * hv : primary || hv > 0.05f ? 1.4f : 1.1f);
            var font = Theme.Font(r.Height < 36 ? 14 : 15, Weight.Bold);
            bool lead = w.Glyph != null || w.Icon != null;
            float lead_w = w.Icon != null ? 24 : 30;
            float textW = Measure(w.Name, font).Width + 2, content = textW + (lead ? lead_w : 0), x = r.X + (r.Width - content) / 2;
            if (w.Icon != null) { w.Icon(g, new RectangleF(x, r.Y + (r.Height - 16) / 2, 16, 16), primary || on ? style.PrimaryInk : Theme.Mix(style.GlyphFill, style.AccentDark, hv)); x += lead_w; }
            else if (w.Glyph != null)
            {
                Theme.Glyph(g, new RectangleF(x, r.Y + (r.Height - 24) / 2, 24, 24), w.Glyph, glyph, glyphInk);
                x += lead_w;
            }
            Theme.Text(g, w.Name, font, ink, new RectangleF(x, r.Y, textW + 4, r.Height), StringAlignment.Near, StringAlignment.Center, false);
            Theme.Cursor(g, r, radius, FocusStrength(w, t));
        }

        void DrawDrawer(Graphics g, float t)
        {
            var r = drawerRect;
            Theme.Shadow(g, r, 18, 14, 90, -2);
            using (var path = Theme.TopRound(r, 18)) Theme.Fill(g, path, Style.Drawer);
            if (Style.Themed) Style.WaveBandCached(g, new RectangleF(r.X + 18, r.Y - 7, r.Width - 36, 9), 0, 1);
            using (var path = Theme.TopRound(new RectangleF(r.X, r.Y, r.Width, 62), 18))
            using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, 64), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(b, path);
            Theme.ListIcon(g, new RectangleF(r.X + 22, r.Y + 19, 18, 18), Color.FromArgb(150, 214, 240));
            Theme.Text(g, "Registro attività", Theme.Font(17, Weight.Heavy), Color.White, new RectangleF(r.X + 48, r.Y + 14, 320, 26));
            Theme.Text(g, (logLines.Count == 1 ? "1 voce" : logLines.Count + " voci") + " · rotella per scorrere", Theme.Font(12.5f, Weight.Bold), Color.FromArgb(166, 190, 206), new RectangleF(r.X + 48, r.Y + 39, 360, 18));
            Widget close;
            if (widgets.TryGetValue("log-close", out close) && close.Visible)
                DrawIconButton(g, close, t, (gg, b, c) => Theme.Close(gg, b, c, 2.4f));
            var area = new RectangleF(r.X + 22, r.Y + 68, r.Width - 44, DrawerH - 80);
            const float lineH = 21;
            int fit = Math.Max(1, (int)(area.Height / lineH));
            logScroll = Math.Max(0, Math.Min(Math.Max(0, logLines.Count - fit), logScroll));
            if (logLines.Count == 0) Theme.Text(g, "Nessuna attività registrata in questa sessione.", Theme.Font(13.5f, Weight.Regular), Color.FromArgb(166, 190, 206), area);
            var mono = Theme.Mono(13);
            int last = logLines.Count - 1 - (int)logScroll;
            for (int k = 0; k < fit && last - k >= 0; k++)
            {
                string line = logLines[last - k];
                float y = area.Bottom - (k + 1) * lineH;
                if (k % 2 == 0) using (var b = new SolidBrush(Color.FromArgb(14, 255, 255, 255))) g.FillRectangle(b, area.X - 8, y, area.Width + 16, lineH);
                int split = Math.Min(line.Length, 8);
                string message = line.Substring(split).Trim();
                // Unlocks and test/demo lines carry a coloured marker as well as their text tag.
                Color mark = message.Contains("Sbloccato") || message.Contains("Riconosciuto") ? Color.FromArgb(132, 224, 120) : message.StartsWith("[") ? Color.FromArgb(255, 196, 90) : Color.Transparent;
                if (mark.A > 0) using (var b = new SolidBrush(mark)) g.FillEllipse(b, area.X + 70, y + lineH / 2 - 3, 6, 6);
                Theme.Text(g, line.Substring(0, split), mono, Color.FromArgb(110, 214, 255), new RectangleF(area.X, y + 1, 80, lineH));
                Theme.Text(g, message, mono, Color.FromArgb(226, 234, 240), new RectangleF(area.X + 84, y + 1, area.Width - 84, lineH));
            }
        }

        void DrawSheet(Graphics g, float t)
        {
            float e = Theme.EaseOut(sheet);
            using (var b = new SolidBrush(Color.FromArgb((int)(96 * e), 10, 26, 42))) g.FillRectangle(b, 0, 0, W, H);
            var r = sheetRect;
            var style = Style;
            var sheetState = g.Save();
            using (var b = new LinearGradientBrush(new RectangleF(r.X - 19, 0, 20, H), Color.FromArgb(0, 10, 20, 30), Color.FromArgb(60, 10, 20, 30), 0f)) g.FillRectangle(b, r.X - 18, 0, 18, H);
            using (var b = new LinearGradientBrush(new RectangleF(r.X, -1, r.Width, H + 2), style.SheetTop, style.SheetBottom, 90f)) g.FillRectangle(b, r);
            using (var b = new LinearGradientBrush(new RectangleF(r.X, -1, r.Width, 66), style.BarTop, style.BarBottom, 90f)) g.FillRectangle(b, r.X, 0, r.Width, 64);
            using (var pen = new Pen(style.BarLine)) g.DrawLine(pen, r.X, 63.5f, r.Right, 63.5f);
            if (style.Themed)
            {
                style.Parchment(g, r, 0, 0.4f);
                using (var pen = new Pen(style.BarLine, 1)) g.DrawLine(pen, r.X + 0.5f, 0, r.X + 0.5f, H);
                style.WaveBandCached(g, new RectangleF(r.X, 62, r.Width, 8), 0, 0.8f);
            }
            float sx = r.X + 24, cw = SheetW - 48;
            Theme.Gear(g, new RectangleF(sx, 20, 24, 24), style.AccentDark);
            Theme.Text(g, cloudPage ? "Account cloud" : "Impostazioni", Theme.Font(22, Weight.Heavy), Theme.Ink, new RectangleF(sx + 34, 0, 230, 64), StringAlignment.Near, StringAlignment.Center, false);
            DrawButton(g, widgets["back"], t);

            if (cloudPage)
            {
                Section(g, "ACCOUNT CLOUD · OPZIONALE", sx, 84, cw);
                string[] ids = { "field-project", "field-apikey", "field-email", "field-pass" };
                string[] labels = { "Project ID Firebase", "Web API key", "Email", "Password" };
                for (int i = 0; i < ids.Length; i++)
                {
                    Theme.Text(g, labels[i], Theme.Font(14, Weight.Bold), Theme.Ink, new RectangleF(sx, 108 + i * 78, cw, 22));
                    DrawField(g, widgets[ids[i]], t);
                }
                DrawButton(g, widgets["cloud-signup"], t); DrawButton(g, widgets["cloud-login"], t); DrawButton(g, widgets["cloud-logout"], t);
                bool signed = cloud != null && cloud.IsSignedIn;
                var note = new RectangleF(sx, 504, cw, 60);
                Theme.Fill(g, note, 12, Color.FromArgb(150, Color.White), Color.FromArgb(110, Color.White));
                Theme.Stroke(g, note, 12, Color.FromArgb(140, style.Themed ? style.ButtonEdge : Theme.Line), 1);
                Theme.Info(g, new RectangleF(note.X + 12, note.Y + 12, 18, 18), cloudBusy ? style.AccentDark : signed ? Theme.UnlockedInk : style.Muted);
                Theme.Text(g, cloudMessage, Theme.Font(13, Weight.Bold), Theme.Ink, new RectangleF(note.X + 38, note.Y + 8, note.Width - 48, note.Height - 12), StringAlignment.Near, StringAlignment.Center, true);
                Theme.Text(g, signed ? "Account connesso. I progressi si sincronizzano automaticamente. Per usare un altro account, premi Esci." : "Accedi o registrati per sincronizzare i progressi. I progressi restano locali anche senza rete.", Theme.Font(12.5f, Weight.Regular), style.Muted, new RectangleF(sx, 578, cw, 80), StringAlignment.Near, StringAlignment.Near, true);
                g.Restore(sheetState);
                return;
            }
            Section(g, "COLLEGAMENTO", sx, 80, cw);
            Theme.Text(g, "Percorso di Cemu.exe", Theme.Font(14, Weight.Bold), Theme.Ink, new RectangleF(sx, 101, cw, 22));
            DrawField(g, widgets["field-cemu"], t);
            DrawButton(g, widgets["browse"], t);
            Theme.Text(g, "Profilo locale", Theme.Font(14, Weight.Bold), Theme.Ink, new RectangleF(sx, 175, cw, 22));
            DrawField(g, widgets["field-player"], t);
            Theme.Text(g, FieldsEditable ? "Ogni profilo ha progressi separati: usane uno nuovo per una nuova partita." : NativeMode ? "Disattiva gli obiettivi per cambiare profilo. La partita in Cemu può continuare." : "Scollega Cemu o la demo per modificare percorso e profilo.",
                Theme.Font(12.5f, Weight.Regular), FieldsEditable ? style.Muted : Theme.ExperimentalInk, new RectangleF(sx, 244, cw, 40), StringAlignment.Near, StringAlignment.Near, true);

            Section(g, "NOTIFICHE E MOVIMENTO", sx, 290, cw);
            DrawToggle(g, widgets["sound"], "Jingle originale, diverso per ogni rarità", t);
            DrawToggle(g, widgets["ambient"], "Mare e video; si fermano mentre giochi in Cemu", t);
            DrawToggle(g, widgets["motion"], motionChoice == null ? "Segue l'impostazione di Windows" : "Dissolvenze brevi al posto di zoom e scorrimenti", t);
            DrawButton(g, widgets["test"], t);
            DrawButton(g, widgets["cloud-page"], t);

            Section(g, "CONFIGURAZIONE SUPPORTATA", sx, 520, cw);
            var info = new RectangleF(sx, 542, cw, 68);
            Theme.Fill(g, info, 12, style.CardTop, style.Themed ? style.CardBottom : Color.FromArgb(246, 249, 250));
            Theme.Stroke(g, info, 12, style.Themed ? style.CardEdge : Theme.Line, 1);
            style.Parchment(g, info, 12, 0.3f);
            style.SimpleFrame(g, info, true, 10);
            var f = Theme.Font(12.5f, Weight.Regular);
            Theme.Text(g, selectedGame.Title + " · " + selectedGame.Profiles[0].Label + " · Cemu " + selectedGame.Profiles[0].CemuVersion, Theme.Font(13.5f, Weight.Bold), Theme.Ink, new RectangleF(info.X + 14, info.Y + 9, info.Width - 28, 20));
            Theme.Text(g, "Title ID " + selectedGame.Profiles[0].TitleId, f, style.Muted, new RectangleF(info.X + 14, info.Y + 29, info.Width - 28, 18));
            Theme.Text(g, "RPX " + selectedGame.Profiles[0].RpxHash + " · sola lettura della memoria", f, style.Muted, new RectangleF(info.X + 14, info.Y + 46, info.Width - 28, 18));
            Theme.Text(g, "Loghi e illustrazioni forniti dal proprietario del progetto. Suoni originali sintetizzati. Font Nunito © The Nunito Project Authors, SIL OFL 1.1.",
                Theme.Font(11.5f, Weight.Regular), style.Muted, new RectangleF(sx, Math.Max(622, H - 56), cw, 40), StringAlignment.Near, StringAlignment.Near, true);
            g.Restore(sheetState);
        }
        void Section(Graphics g, string text, float x, float y, float width)
        {
            var font = Theme.Font(12, Weight.Heavy);
            float tw = Measure(text, font).Width + 10;
            Theme.Text(g, text, font, Style.AccentDark, new RectangleF(x, y, tw + 4, 18));
            using (var pen = new Pen(Color.FromArgb(90, Style.Themed ? Style.ButtonEdge : Theme.Line), 1)) g.DrawLine(pen, x + tw + 4, y + 10, x + width, y + 10);
        }
        static string FieldDisplayText(TextBox box) { return box.UseSystemPasswordChar ? new string('●', box.TextLength) : box.Text; }
        void DrawField(Graphics g, Widget w, float t)
        {
            var r = w.Bounds;
            bool active = w.Box.Focused;
            Theme.Fill(g, r, 10, w.Enabled ? Color.White : Color.FromArgb(240, 243, 245), w.Enabled ? Color.White : Color.FromArgb(240, 243, 245));
            Theme.Stroke(g, r, 10, active ? Theme.Accent : Theme.Mix(Color.FromArgb(192, 201, 209), Theme.Accent, w.Hover * 0.7f), active ? 2 : 1.2f);
            if (active) Theme.Cursor(g, r, 10, 0.55f);
            if (!w.Box.Visible) Theme.Text(g, FieldDisplayText(w.Box), Theme.Font(14, Weight.Regular), w.Enabled ? Theme.Ink : Theme.Muted, Theme.Inflate(r, -13, 0), StringAlignment.Near, StringAlignment.Center, false);
            Theme.Cursor(g, r, 10, FocusStrength(w, t));
        }
        void DrawToggle(Graphics g, Widget w, string help, float t)
        {
            var r = w.Bounds;
            Theme.Fill(g, r, 12, Theme.Mix(Style.CardTop, Style.Soft, w.Hover * 0.6f), Theme.Mix(Style.Themed ? Style.CardBottom : Color.FromArgb(246, 249, 250), Style.Soft, w.Hover * 0.6f));
            Theme.Stroke(g, r, 12, Theme.Mix(Style.Themed ? Style.CardEdge : Theme.Line, Style.Accent, w.Hover * 0.6f), 1);
            Theme.Text(g, w.Name, Theme.Font(14.5f, Weight.Bold), Theme.Ink, new RectangleF(r.X + 14, r.Y + 4, r.Width - 90, 20));
            Theme.Text(g, help, Theme.Font(12, Weight.Regular), Style.Muted, new RectangleF(r.X + 14, r.Y + 23, r.Width - 90, 18));
            var track = new RectangleF(r.Right - 66, r.Y + (r.Height - 28) / 2, 52, 28);
            float on = w.Value < 0 ? (w.Checked ? 1 : 0) : w.Value;
            Theme.Fill(g, track, 14, Theme.Mix(Style.Themed ? Style.BubbleOffTop : Color.FromArgb(200, 208, 214), Style.Glow, on), Theme.Mix(Style.Themed ? Style.BubbleOffBottom : Color.FromArgb(184, 193, 200), Style.AccentDark, on));
            // "I"/"O" marks inside the track: the state reads without its colour.
            using (var pen = new Pen(Color.FromArgb((int)(220 * on), 255, 255, 255), 2) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, track.X + 15, track.Y + 9, track.X + 15, track.Bottom - 9);
            using (var pen = new Pen(Color.FromArgb((int)(220 * (1 - on)), 255, 255, 255), 1.8f)) g.DrawEllipse(pen, track.Right - 20, track.Y + 9.5f, 9, 9);
            var knob = Theme.Square(new PointF(track.X + 14 + 24 * on, track.Y + 14), 22);
            using (var b = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(b, knob.X, knob.Y + 1.5f, knob.Width, knob.Height);
            using (var b = new LinearGradientBrush(Theme.Inflate(knob, 0, 1), Color.White, Color.FromArgb(232, 237, 241), 90f)) g.FillEllipse(b, knob);
            Theme.Cursor(g, r, 12, FocusStrength(w, t));
        }

        // ---------------------------------------------------------------- input
        PointF Logical(Point p) { return new PointF(p.X / scale, p.Y / scale); }
        Widget HitTest(Point device)
        {
            Arrange();
            var p = Logical(device);
            int layer = ActiveLayer;
            bool overDrawer = drawer > 0 && drawerRect.Contains(p) && p.Y < H - BottomH;
            for (int i = visible.Count - 1; i >= 0; i--)
            {
                var w = visible[i];
                if (w.Layer != layer || !w.Bounds.Contains(p)) continue;
                if (overDrawer && w.Id != "log-close") continue;
                if (w.Kind == Kind.Tile && !gridRect.Contains(p)) continue;
                if (w.Kind == Kind.GameTile && !libraryGridRect.Contains(p)) continue;
                return w;
            }
            return null;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var w = HitTest(e.Location);
            if (w == hovered) return;
            hovered = w;
            Cursor = w != null && w.Enabled ? Cursors.Hand : Cursors.Default;
            Kick();
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = null; Kick();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            focusVisible = false;
            pressed = HitTest(e.Location);
            var p = Logical(e.Location);
            if ((pressed == null || pressed.Kind != Kind.Field) && ActiveControl is TextBox) { ActiveControl = null; Focus(); }
            if (pressed == null)
            {
                if (dialogTarget > 0) { if (!dialogRect.Contains(p)) CloseDialog(); }
                else if (menuTarget > 0) { if (!menuRect.Contains(p)) CloseMenu(); }
                else if (sheetTarget > 0 && !sheetRect.Contains(p)) CloseSheet();
            }
            Kick();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var w = HitTest(e.Location);
            var down = pressed;
            pressed = null;
            if (w != null && w == down) Press(w);
            Kick();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var p = Logical(e.Location);
            if (menuTarget > 0) { CloseMenu(); return; }
            if (drawer > 0 && drawerRect.Contains(p) && p.Y < H - BottomH) logScroll += e.Delta / 120f * 3;
            else if (ActiveLayer == 0 && libraryOpen) libraryScroll -= e.Delta / 120f * 96;
            else if (ActiveLayer == 0) scrollTarget -= e.Delta / 120f * 96;
            if (ActiveLayer == 0 && !libraryOpen && !(drawer > 0 && drawerRect.Contains(p)))
                Invalidate(Device(new RectangleF(0, gridRect.Y, W, gridRect.Height)));
            else Invalidate();
            Kick();
        }
        void Press(Widget w)
        {
            if (w == null || !w.Enabled || w.Click == null) return;
            w.Click();
            Invalidate(); Kick();
        }
        List<Widget> Focusables()
        {
            Arrange();
            int layer = ActiveLayer;
            return visible.Where(w => w.Layer == layer && w.Enabled && (w.Kind != Kind.Tile || !(drawer > 0 && drawerRect.IntersectsWith(w.Bounds)))).ToList();
        }
        void SetFocus(Widget w)
        {
            focused = w; focusVisible = true;
            if (w.Box == search) FocusSearch();
            else if (w.Kind == Kind.Field && w.Box != null && w.Box.Visible) w.Box.Focus();
            else if (ActiveControl != null) { ActiveControl = null; Focus(); }
            if (w.Kind == Kind.GameTile)
            {
                if (w.Bounds.Top < libraryGridRect.Top + 8) libraryScroll -= libraryGridRect.Top + 8 - w.Bounds.Top;
                else if (w.Bounds.Bottom > libraryGridRect.Bottom - 8) libraryScroll += w.Bounds.Bottom - libraryGridRect.Bottom + 8;
            }
            if (w.Kind == Kind.Tile)
            {
                var b = w.Bounds;
                if (b.Y < gridRect.Y + 8) scrollTarget -= gridRect.Y + 12 - b.Y;
                else if (b.Bottom > gridRect.Bottom - 8) scrollTarget += b.Bottom - gridRect.Bottom + 14;
            }
            Invalidate(); Kick();
        }
        void MoveFocus(int direction)
        {
            var list = Focusables();
            if (list.Count == 0) return;
            int index = focused == null ? -1 : list.IndexOf(focused);
            if (index < 0) index = direction > 0 ? -1 : 0;
            SetFocus(list[((index + direction) % list.Count + list.Count) % list.Count]);
        }
        void Navigate(Keys key)
        {
            var list = Focusables();
            if (list.Count == 0) return;
            if (focused == null || !list.Contains(focused) || !focusVisible) { SetFocus(list.FirstOrDefault(w => w.Kind == Kind.Tile || w.Kind == Kind.GameTile) ?? list[0]); return; }
            var from = Theme.Center(focused.Bounds);
            float dx = key == Keys.Left ? -1 : key == Keys.Right ? 1 : 0, dy = key == Keys.Up ? -1 : key == Keys.Down ? 1 : 0;
            Widget best = null;
            float bestScore = float.MaxValue;
            foreach (var w in list)
            {
                if (w == focused) continue;
                var c = Theme.Center(w.Bounds);
                float vx = c.X - from.X, vy = c.Y - from.Y, along = vx * dx + vy * dy;
                if (along <= 1) continue;
                float score = along + (Math.Abs(vx * dy) + Math.Abs(vy * dx)) * 2.5f;
                if (score < bestScore) { bestScore = score; best = w; }
            }
            if (best != null) SetFocus(best);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool modified = (keyData & (Keys.Control | Keys.Alt)) != 0, shift = (keyData & Keys.Shift) != 0;
            bool typing = ActiveControl is TextBox && ActiveControl.Focused;
            bool searching = typing && ActiveControl == search;
            if (!modified && key == Keys.Tab) { MoveFocus(shift ? -1 : 1); return true; }
            if ((keyData & Keys.Control) != 0 && (keyData & Keys.Alt) == 0 && key == Keys.F && !libraryOpen && ActiveLayer == 0) { FocusSearch(); return true; }
            // Escape in the search box first clears it, then leaves it.
            if (!modified && key == Keys.Escape && searching)
            {
                if (search.TextLength > 0) search.Clear(); else { ActiveControl = null; Focus(); }
                return true;
            }
            if (!modified && key == Keys.Escape && CloseTop()) return true;
            if (typing)
            {
                if (key == Keys.Enter)
                {
                    var first = searching ? Focusables().FirstOrDefault(w => w.Kind == Kind.Tile) : null;
                    if (first != null) SetFocus(first); else MoveFocus(1);
                    return true;
                }
                if (searching && key == Keys.Down) { var first = Focusables().FirstOrDefault(w => w.Kind == Kind.Tile); if (first != null) SetFocus(first); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (modified || shift) return base.ProcessCmdKey(ref msg, keyData);
            switch (key)
            {
                case Keys.Left: case Keys.Right:
                    if (ActiveLayer == 2) { StepDetail(key == Keys.Left ? -1 : 1); return true; }
                    Navigate(key); return true;
                case Keys.Up: case Keys.Down: Navigate(key); return true;
                case Keys.Enter: case Keys.Space:
                    if (focused != null && focused.Visible && focused.Layer == ActiveLayer) Press(focused);
                    return true;
                case Keys.B: case Keys.Back: CloseTop(); return true;
                case Keys.A: if (!libraryOpen && ActiveLayer == 0 && CanConnect) Connect(); return true;
                case Keys.X: if (!NativeMode && !libraryOpen && ActiveLayer == 0 && CanConnect) StartDemo(); return true;
                case Keys.C: if (!libraryOpen && ActiveLayer == 0) NextCategory(); return true;
                case Keys.F: if (!libraryOpen && ActiveLayer == 0) NextChapter(); return true;
                case Keys.S: if (!libraryOpen && ActiveLayer == 0) NextSort(); return true;
                case Keys.R: if (!libraryOpen && ActiveLayer == 0) NextRarity(); return true;
                case Keys.Y: if (ActiveLayer == 0 && CanDisconnect) Disconnect("Sessione scollegata."); return true;
                case Keys.P: if (!libraryOpen && ActiveLayer == 0 && CanDisconnect) TogglePause(); return true;
                case Keys.G: if (ActiveLayer == 0) ToggleLibrary(); return true;
                case Keys.PageDown: if (ActiveLayer == 0) { if (libraryOpen) libraryScroll += libraryGridRect.Height * 0.8f; else scrollTarget += gridRect.Height * 0.8f; Kick(); } return true;
                case Keys.PageUp: if (ActiveLayer == 0) { if (libraryOpen) libraryScroll -= libraryGridRect.Height * 0.8f; else scrollTarget -= gridRect.Height * 0.8f; Kick(); } return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------------------------------------------------------- actions
        void OpenCloudPage() { cloudPage = true; focused = hovered = null; Arrange(); PlaceFields(); Invalidate(); Kick(); }
        // Panels freeze the page as it is now, without the hover of the card or button just used.
        void SettleHover() { foreach (var w in widgets.Values) { w.Hover = 0; w.Press = 0; } hovered = pressed = null; InvalidateScene(); }
        void OpenSheet() { SettleHover(); menu = null; menuOpen = menuTarget = 0; cloudPage = false; sheetTarget = 1; hovered = null; focused = null; PlaceSearch(); Kick(); }
        void CloseSheet() { if (cloudPage) { cloudPage = false; cloudPassword.Clear(); Arrange(); PlaceFields(); Invalidate(); return; } cloudPassword.Clear(); sheetTarget = 0; focused = null; PlaceFields(); Kick(); }
        void ToggleDrawer() { CloseMenu(); drawerTarget = drawerTarget > 0 ? 0 : 1; if (drawerTarget > 0) { unreadLogs = 0; logScroll = 0; } Kick(); }
        void CloseDrawer() { drawerTarget = 0; Kick(); }
        void OpenDetail(Achievement a) { SettleHover(); detail = a; detailSwap = -10; dialogTarget = 1; if (Motion.Reduced) dialog = Math.Max(dialog, 0.5f); hovered = null; PlaceSearch(); Kick(); }
        void CloseDialog() { dialogTarget = 0; RevealDetailTile(); Kick(); }
        bool CloseTop()
        {
            if (dialogTarget > 0) CloseDialog();
            else if (menuTarget > 0) CloseMenu();
            else if (sheetTarget > 0) CloseSheet();
            else if (drawerTarget > 0) CloseDrawer();
            else if (libraryOpen) CloseLibrary();
            else return false;
            return true;
        }
        void SetFilter(int value)
        {
            if (filter == value) return;
            filter = value;
            FilterChanged();
        }
        void FocusField(TextBox box) { if (box.Visible) box.Focus(); }
        void Browse()
        {
            using (var picker = new OpenFileDialog { Filter = "Cemu|Cemu.exe", Title = "Seleziona Cemu.exe" })
                if (picker.ShowDialog(this) == DialogResult.OK) cemuPath.Text = picker.FileName;
        }
        void ToggleSound() { soundOn = !soundOn; TrySaveSettings(); if (soundOn) Chime.Play(); }
        void ToggleAmbient() { ambientOn = !ambientOn; TrySaveSettings(); }
        void ToggleMotion()
        {
            Motion.Reduced = !Motion.Reduced; motionChoice = Motion.Reduced ? "reduced" : "full";
            ClearTileImages(); InvalidateScene(); TrySaveSettings(); Invalidate();
        }
        void TrySaveSettings()
        {
            try { SaveSettings(); } catch (Exception ex) { Log("Impostazioni non salvate: " + ex.Message); }
        }
        int testTier;
        void TestToast()
        {
            // Each press previews the next rarity, preferring an objective with the owner's artwork.
            var tier = (AchievementRarity)(testTier++ % 3);
            var sample = rules.Where(a => a.Rarity == tier).OrderByDescending(a => arts[a.Id].HasIcon).ThenByDescending(a => a.Points).FirstOrDefault()
                ?? rules.OrderByDescending(a => a.Points).First();
            toasts.Enqueue(new PendingToast { Achievement = sample, Header = "ANTEPRIMA · NOTIFICA DI PROVA" });
            PumpToasts();
        }

        // ---------------------------------------------------------------- accessibility
        string Describe(Widget w)
        {
            if (w.Kind == Kind.Tile && w.Achievement != null)
            {
                if (w.Achievement.IsPlanned) return w.Help + " Obiettivo non attivo: nessuno sblocco automatico.";
                var unlock = progress == null ? null : progress.Get(w.Achievement.Id);
                return w.Help + " " + w.Achievement.Points + " punti. " +
                    (unlock != null ? "Sbloccato il " + unlock.Utc.ToLocalTime().ToString("g", Italian) + "." : "Da sbloccare.");
            }
            return w.Help;
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new SurfaceAccessible(this); }
        sealed class SurfaceAccessible : ControlAccessibleObject
        {
            readonly MainForm form;
            public SurfaceAccessible(MainForm owner) : base(owner) { form = owner; }
            List<Widget> Items()
            {
                form.Arrange();
                int layer = form.ActiveLayer;
                return form.visible.Where(w => w.Layer == layer).ToList();
            }
            public override int GetChildCount() { return Items().Count; }
            public override AccessibleObject GetChild(int index)
            {
                var items = Items();
                if (index < 0 || index >= items.Count) return null;
                var w = items[index];
                if (w.Box != null && w.Box.Visible) return w.Box.AccessibilityObject;
                return new WidgetAccessible(form, w, this);
            }
        }
        sealed class WidgetAccessible : AccessibleObject
        {
            readonly MainForm form;
            readonly Widget widget;
            readonly AccessibleObject parent;
            public WidgetAccessible(MainForm form, Widget widget, AccessibleObject parent) { this.form = form; this.widget = widget; this.parent = parent; }
            public override string Name { get { return widget.Name; } set { } }
            public override string Description { get { return form.Describe(widget); } }
            public override AccessibleObject Parent { get { return parent; } }
            public override Rectangle Bounds { get { return form.RectangleToScreen(form.Device(widget.Bounds)); } }
            public override string DefaultAction { get { return widget.Kind == Kind.Toggle ? "Attiva o disattiva" : widget.Kind == Kind.Dropdown ? "Apri il menu" : widget.Kind == Kind.MenuItem ? "Scegli" : "Premi"; } }
            public override AccessibleRole Role
            {
                get
                {
                    switch (widget.Kind)
                    {
                        case Kind.Tile: case Kind.GameTile: return AccessibleRole.ListItem;
                        case Kind.Tab: return AccessibleRole.PageTab;
                        case Kind.Dropdown: return AccessibleRole.ButtonDropDown;
                        case Kind.MenuItem: return AccessibleRole.MenuItem;
                        case Kind.Toggle: return AccessibleRole.CheckButton;
                        case Kind.Field: return AccessibleRole.Text;
                        default: return AccessibleRole.PushButton;
                    }
                }
            }
            public override AccessibleStates State
            {
                get
                {
                    var s = AccessibleStates.Focusable;
                    if (!widget.Enabled) s |= AccessibleStates.Unavailable;
                    if (widget.Checked) s |= AccessibleStates.Checked;
                    if (form.focused == widget) s |= AccessibleStates.Focused;
                    return s;
                }
            }
            public override void DoDefaultAction() { form.BeginInvoke(new Action(() => form.Press(widget))); }
        }
    }
}
