using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace WiiUAchievements
{
    // Session logic. The custom-drawn interface lives in MainForm.Ui.cs.
    public sealed partial class MainForm : Form
    {
        enum StatusKind { Idle, Busy, Live, Demo, Paused, Error }
        sealed class PendingToast { public Achievement Achievement; public string Header; public bool Retry; }

        readonly TextBox cemuPath = new TextBox(), player = new TextBox(), cloudEmail = new TextBox(), cloudPassword = new TextBox(), cloudProject = new TextBox(), cloudApiKey = new TextBox();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Queue<PendingToast> toasts = new Queue<PendingToast>();
        readonly List<string> logLines = new List<string>();
        readonly string root, dataRoot;
        readonly string[] legacyDataDirectories;
        List<Achievement> rules;
        GameDefinition selectedGame = GameCatalog.Default;
        GameProfile activeProfile;
        IGameMemoryReader gameReader;
        readonly Dictionary<string, Art> arts = new Dictionary<string, Art>();
        ProcessMemory memory;
        GameSession session;
        ProgressStore progress;
        AchievementEngine engine;
        CloudSync cloud;
        bool cloudBusy;
        DateTime nextCloudSync;
        bool autoReconnect;
        int connectionGeneration;
        readonly System.Windows.Forms.Timer connectionWatch = new System.Windows.Forms.Timer();
        string cloudMessage = "Accedi o registrati per sincronizzare i progressi.";
        readonly System.Windows.Forms.Timer cloudTimer = new System.Windows.Forms.Timer();
        GameSnapshot current;
        ulong guestBase;
        DateTime identityCheck;
        bool demo, recording, connecting, paused, soundOn = true, ambientOn = true;
        string motionChoice; // null follows the Windows animation setting
        int engineStateVersion;
        int demoTick;
        ToastForm activeToast;
        string statusTitle = "", statusDetail = "", hint = "";
        StatusKind statusKind;

        public MainForm(bool startDemo, bool autoConnect)
            : this(startDemo, autoConnect, null) { }
        public MainForm(bool startDemo, bool autoConnect, string isolatedDataDirectory)
            : this(startDemo, autoConnect, isolatedDataDirectory, null) { }
        public MainForm(bool startDemo, bool autoConnect, string isolatedDataDirectory, NativeCemuFeed nativeSource)
        {
            nativeFeed = nativeSource;
            root = nativeSource == null ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(typeof(MainForm).Assembly.Location);
            dataRoot = Path.GetFullPath(isolatedDataDirectory ?? UserData.DefaultDirectory);
            legacyDataDirectories = isolatedDataDirectory == null ? UserData.LegacyDirectories(root) : new string[0];
            if (isolatedDataDirectory == null) UserData.ImportSettings(dataRoot, legacyDataDirectories);
            cloud = new CloudSync(dataRoot);
            cloudProject.Text = cloud.ProjectId ?? CloudProject.Id; cloudApiKey.Text = cloud.ApiKey ?? CloudProject.WebApiKey; cloudEmail.Text = cloud.Email ?? "";
            if (cloud.Warning != null) cloudMessage = cloud.Warning;
            else if (cloud.IsSignedIn) cloudMessage = "Account connesso. Sincronizzazione automatica attiva.";
            cloudTimer.Interval = 30000; cloudTimer.Tick += delegate { SyncCloud(); }; cloudTimer.Start();
            rules = selectedGame.LoadRules(root);
            foreach (var a in rules) arts[a.Id] = Art.For(a, rules, selectedGame.Id);
            Text = "Wii U Achievements — " + selectedGame.Title;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.BackBottom; ForeColor = Theme.Ink;
            BuildUi();
            FixWindowSize();
            cemuPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Cemu_2.6", "Cemu.exe");
            player.Text = "Giocatore 1";
            LoadSettings();
            RestoreProgressView();
            player.TextChanged += delegate
            {
                InvalidateScene();
                if (!FieldsEditable) return;
                progress = null; engine = null; current = null;
                RefreshList();
            };
            connectionWatch.Interval = 1000; connectionWatch.Tick += delegate { WatchConnection(); }; connectionWatch.Start();
            timer.Interval = 100;
            timer.Tick += Tick;
            Shown += delegate { if (startDemo) StartDemo(); else if (autoConnect) Connect(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (NativeMode && !nativeShutdown && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
                connectionGeneration++; timer.Stop(); cloudTimer.Stop(); cloudTimer.Dispose(); connectionWatch.Stop(); connectionWatch.Dispose(); if (memory != null) memory.Dispose(); if (activeToast != null) activeToast.Close();
            };
            RefreshList();
            hint = DefaultHint;
            SetStatus("Pronto al collegamento", "Avvia il gioco e carica un salvataggio, poi premi Collega Cemu.", StatusKind.Idle);
            if (NativeMode) ConnectNative();
        }
        void LoadSettings()
        {
            string path = Path.Combine(dataRoot, "settings.xml");
            if (!File.Exists(path)) return;
            try
            {
                var doc = SafeXml.Load(path);
                cemuPath.Text = (string)doc.Root.Element("cemu") ?? cemuPath.Text; player.Text = (string)doc.Root.Element("player") ?? player.Text;
                soundOn = (string)doc.Root.Element("sound") != "false"; ambientOn = (string)doc.Root.Element("ambient") != "false";
                string motion = (string)doc.Root.Element("motion");
                if (motion == "reduced" || motion == "full") { motionChoice = motion; Motion.Reduced = motion == "reduced"; }
                var savedGame = GameCatalog.Games.FirstOrDefault(g => g.Id == (string)doc.Root.Element("game") && g.Available);
                if (savedGame != null && savedGame != selectedGame) ApplyGameSelection(savedGame);
            }
            catch (Exception ex) { Log("Impostazioni non caricate: " + ex.Message); }
        }
        void SaveSettings()
        {
            SafeXml.AtomicSave(new XDocument(new XElement("settings", new XElement("cemu", cemuPath.Text), new XElement("player", player.Text), new XElement("game", selectedGame.Id),
                new XElement("sound", soundOn ? "true" : "false"), new XElement("ambient", ambientOn ? "true" : "false"),
                motionChoice == null ? null : new XElement("motion", motionChoice))), Path.Combine(dataRoot, "settings.xml"));
        }
        ProgressStore OpenProgress(string fingerprint)
        {
            var saved = new ProgressStore(Path.Combine(dataRoot, "progress"), player.Text, fingerprint, selectedGame.SetRevision);
            int imported = UserData.ImportProgress(saved, legacyDataDirectories, rules.Where(a => !a.IsPlanned).Select(a => a.Id));
            if (imported > 0) Log("Recuperati " + imported + " sblocchi dalle versioni precedenti, con le date originali.");
            return saved;
        }
        void RestoreProgressView()
        {
            if (selectedGame.Profiles.Count != 1) return;
            var profile = selectedGame.Profiles[0];
            try { progress = OpenProgress(profile.TitleId + "|" + profile.TitleVersion + "|" + profile.RpxHash); }
            catch (Exception ex) { progress = null; Log("Progressi non caricati: " + ex.Message); }
        }
        void SetStatus(string title, string subtitle, StatusKind kind)
        {
            statusTitle = title; statusDetail = subtitle; statusKind = kind; InvalidateScene();
            AccessibleDescription = title + ". " + subtitle;
            Invalidate(); Kick();
        }
        void Log(string message)
        {
            logLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (logLines.Count > 500) logLines.RemoveAt(0);
            if (drawerTarget == 0) unreadLogs++;
            InvalidateScene();
            Invalidate();
        }
        void SetConnected(bool value)
        {
            player.ReadOnly = value; cemuPath.ReadOnly = value;
            PlaceFields(); Invalidate(); Kick();
        }
        const string DefaultHint = "Caricamenti e selezione file: attesa e ripresa automatiche. Puoi lasciare l'app collegata.";
        bool FieldsEditable { get { return memory == null && !nativeListening && !demo && !connecting && !autoReconnect; } }
        bool CanConnect { get { return !connecting && memory == null && !nativeListening && !demo && !autoReconnect; } }
        bool CanDisconnect { get { return nativeListening || memory != null || demo || autoReconnect; } }

        async void Connect()
        {
            if (NativeMode) { ConnectNative(); return; }
            if (connecting || memory != null || demo) return;
            int generation = ++connectionGeneration;
            connecting = true; SetConnected(true);
            SetStatus("Collegamento…", "Identificazione della sessione e verifica dello spazio Wii U.", StatusKind.Busy);
            ProcessMemory attached = null;
            try
            {
                if (String.IsNullOrWhiteSpace(player.Text)) throw new InvalidOperationException("Inserisci un nome per il profilo locale.");
                string path = Path.GetFullPath(cemuPath.Text.Trim().Trim('"'));
                cemuPath.Text = path;
                var processes = Process.GetProcessesByName("Cemu").Where(p => SamePath(p, path)).ToList();
                if (processes.Count != 1) throw new InvalidOperationException("Deve essere aperta una sola sessione Cemu dal percorso selezionato.");
                session = GameSession.FromLog(path, processes[0].Id);
                activeProfile = selectedGame.RequireProfile(session);
                gameReader = activeProfile.CreateReader();
                attached = new ProcessMemory(session.Pid);
                var bases = await Task.Run(() => attached.FindGuestBases());
                if (generation != connectionGeneration || IsDisposed || Disposing) { attached.Dispose(); attached = null; return; }
                if (!bases.Contains(session.GuestBase)) throw new InvalidOperationException("Il log e la memoria del processo non corrispondono. Ricollega dopo l'avvio del gioco.");
                guestBase = session.GuestBase;
                var sample = gameReader.Read(attached, guestBase);
                progress = OpenProgress(session.Fingerprint);
                engine = new AchievementEngine(rules, progress, AchievementRecognition.IncludeExistingSave);
                engine.Update(sample);
                SaveSettings();
                memory = attached; attached = null; demo = false; paused = false; recording = true;
                identityCheck = DateTime.MinValue;
                connecting = false;
                autoReconnect = false;
                SetConnected(true); if (!engine.WaitingForGameplay) UpdateStats(sample); else current = null; RefreshList(); timer.Start();
                SetRecordingStatus();
                hint = DefaultHint;
                Log(selectedGame.Title + " · " + activeProfile.Label + " riconosciuta. " + (engine.WaitingForGameplay ? "Attesa automatica della partita." : sample.Rupees + " rupie, " + (sample.Health / 4m) + "/" + (sample.MaxHealth / 4m) + " cuori."));
                if (progress.Warning != null) Log(progress.Warning);
                SyncCloud();
            }
            catch (Exception ex)
            {
                if (attached != null) attached.Dispose();
                if (generation != connectionGeneration || IsDisposed || Disposing) return;
                engine = null; progress = null; current = null; gameReader = null; activeProfile = null;
                connecting = false;
                SetConnected(false);
                SetStatus("Collegamento non riuscito", ex.Message, StatusKind.Error); Log(ex.Message);
            }
            finally { if (generation == connectionGeneration) connecting = false; if (!IsDisposed && !Disposing) Invalidate(); }
        }
        static bool SamePath(Process process, string expected)
        {
            try { return String.Equals(Path.GetFullPath(process.MainModule.FileName), expected, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
        void Disconnect(string reason)
        {
            nativeListening = false;
            connectionGeneration++; connecting = false; autoReconnect = false;
            // Demo unlocks live in a throwaway store and should not linger on screen.
            if (demo) progress = null;
            timer.Stop(); recording = false; demo = false; paused = false;
            if (memory != null) { memory.Dispose(); memory = null; }
            if (engine != null) engine.Reset();
            gameReader = null; activeProfile = null;
            toasts.Clear(); if (activeToast != null) { activeToast.Close(); activeToast = null; }
            current = null; hint = DefaultHint;
            SetConnected(false); SetStatus("Scollegato", reason, StatusKind.Idle); Log(reason);
        }
        void StartDemo()
        {
            if (NativeMode) return;
            if (memory != null || demo || connecting) return;
            try
            {
                // Demo has its own temporary directory, never the real player's progress.
                string temporary = Path.Combine(dataRoot, "demo", Guid.NewGuid().ToString("N"));
                progress = new ProgressStore(temporary, "DEMO", "simulation|" + selectedGame.Id, selectedGame.SetRevision); engine = new AchievementEngine(rules, progress);
                current = null; demo = true; paused = false; recording = true; demoTick = 0;
                SetConnected(true); RefreshList();
                SetRecordingStatus();
                hint = "La demo aumenta le rupie automaticamente. Premi Scollega per tornare al gioco reale.";
                timer.Start(); Log("Demo avviata: nessun collegamento a Cemu.");
            }
            catch (Exception ex) { Log(ex.Message); }
        }
        void SetRecordingStatus()
        {
            if (!demo && engine != null && engine.WaitingForGameplay) SetAutomaticWaiting(engine.GameplayMessage);
            else if (demo) SetStatus("DEMO · simulazione", "Dati simulati. I progressi della tua partita restano separati.", StatusKind.Demo);
            else SetStatus(NativeMode ? "Cemu · obiettivi attivi" : "Collegato · registrazione attiva", selectedGame.Title + " · " + activeProfile.Label + " · progressi locali", StatusKind.Live);
        }
        void TogglePause()
        {
            if (memory == null && !nativeListening && !demo || connecting) return;
            paused = !paused; recording = !paused;
            engine.Reset(); current = null; identityCheck = DateTime.MinValue;
            if (paused)
            {
                timer.Stop();
                toasts.Clear(); if (activeToast != null) { activeToast.Close(); activeToast = null; }
                SetStatus("Registrazione in pausa", "Carica la partita, poi premi Riprendi. Gli obiettivi già soddisfatti non verranno assegnati.", StatusKind.Paused);
                hint = "La pausa ferma gli achievement, non il gioco. Per una nuova partita usa un altro profilo locale.";
            }
            else { timer.Start(); SetRecordingStatus(); hint = DefaultHint; }
            Log(paused ? "Registrazione in pausa: condizioni in corso azzerate." : "Registrazione ripresa con una nuova baseline.");
            RefreshList();
        }
        void SelectGame(GameDefinition game)
        {
            if (!game.Available || (game != selectedGame && !CanConnect)) return;
            if (game != selectedGame)
            {
                ApplyGameSelection(game); TrySaveSettings();
                SetStatus("Pronto al collegamento", "Avvia il gioco selezionato nella configurazione supportata.", StatusKind.Idle);
            }
            CloseLibrary(); RefreshList();
        }
        void ApplyGameSelection(GameDefinition game)
        {
            // Load successfully before changing selection. Existing progress on disk is untouched.
            var loaded = game.LoadRules(root);
            var loadedArt = loaded.ToDictionary(rule => rule.Id, rule => Art.For(rule, loaded, game.Id));
            ClearTileImages();
            selectedGame = game; rules = loaded; progress = null; engine = null; current = null;
            arts.Clear(); foreach (var pair in loadedArt) arts.Add(pair.Key, pair.Value);
            if (backdrop != null) { backdrop.Dispose(); backdrop = null; } // The backdrop follows the game palette.
            CloseBannerVideo();
            celebrations.Clear(); filter = categoryFilter = chapterFilter = sortMode = rarityFilter = detectionFilter = iconFilter = 0; search.Clear(); scroll = scrollTarget = 0; tabX = -1;
            Text = "Wii U Achievements — " + game.Title;
        }
        void Tick(object sender, EventArgs e)
        {
            if (NativeMode) { TickNative(); return; }
            if (!recording || engine == null) return;
            try
            {
                GameSnapshot sample;
                if (demo)
                {
                    demoTick++;
                    sample = new GameSnapshot { MaxHealth = demoTick < 180 ? 12 : 16, Health = 12, Rupees = Math.Min(1200, (demoTick / 20) * 5), Utc = DateTime.UtcNow };
                }
                else
                {
                    if ((DateTime.UtcNow - identityCheck).TotalSeconds >= 1)
                    {
                        var live = GameSession.FromLog(session.CemuPath, session.Pid);
                        if (live.Fingerprint != session.Fingerprint || live.GuestBase != guestBase) throw new InvalidOperationException("Il gioco o la sessione Cemu sono cambiati. Ricollega l'app.");
                        identityCheck = DateTime.UtcNow;
                    }
                    sample = gameReader.Read(memory, guestBase);

                }
                ProcessSample(sample);
            }
            catch (Exception ex)
            {
                // Invalid samples/disconnections cannot carry an armed condition across a gap.
                Disconnect("Sessione non disponibile: " + ex.Message);
                autoReconnect = true;
                SetAutomaticWaiting("In attesa della sessione Cemu supportata; ricollegamento automatico.");
            }
        }
        void ProcessSample(GameSnapshot sample)
        {
                var awarded = engine.Update(sample);
                if (engine.WaitingForGameplay) { SetAutomaticWaiting(engine.GameplayMessage); return; }
                if (statusTitle == "Attesa automatica") SetRecordingStatus();
                foreach (var a in awarded)
                {
                    bool fromSave = engine.RecognizedFromSave(a.Id);
                    Log((demo ? "[DEMO] " : a.Experimental ? "[TEST] " : "") + (fromSave ? "Riconosciuto dal salvataggio: " : "Sbloccato: ") + a.Title + " (+" + a.Points + ")");

                    toasts.Enqueue(new PendingToast { Achievement = a, Header = demo ? "DEMO · OBIETTIVO SBLOCCATO" : fromSave ? "RICONOSCIUTO DAL SALVATAGGIO" : a.Experimental ? "SBLOCCATO · SPERIMENTALE" : "OBIETTIVO SBLOCCATO" });
                    Celebrate(a); RefreshList();
                }
                UpdateStats(sample);
                PumpToasts();
                if (!demo && awarded.Count > 0) SyncCloud();
        }
        void SetAutomaticWaiting(string message)
        {
            current = null;
            if (statusTitle != "Attesa automatica" || statusDetail != message)
            {
                toasts.Clear(); if (activeToast != null) { activeToast.Close(); activeToast = null; }
                SetStatus("Attesa automatica", message, StatusKind.Busy); RefreshList();
            }
        }
        void WatchConnection()
        {
            if (NativeMode) return;
            if (!autoReconnect || connecting || demo || memory != null || IsDisposed || Disposing) return;
            try
            {
                var matches = Process.GetProcessesByName("Cemu").Where(p => SamePath(p, cemuPath.Text)).ToList();
                if (matches.Count != 1) return;
                var candidate = GameSession.FromLog(cemuPath.Text, matches[0].Id);
                selectedGame.RequireProfile(candidate); // Never attach another game/version automatically.
                Connect();
            }
            catch { } // Wait for the matching title/log; user can cancel with Scollega.
        }
        void PumpToasts()
        {
            if (activeToast != null || toasts.Count == 0 || IsDisposed) return;
            var next = toasts.Dequeue();
            ToastForm toast;
            // A popup failure must not stop recording: the unlock is already saved and listed.
            try { toast = new ToastForm(next.Achievement, arts[next.Achievement.Id], next.Header, ToastScreen()); }
            catch (Exception ex) { Log("Notifica non mostrata: " + ex.Message); return; }
            toast.FormClosed += delegate
            {
                if (activeToast == toast) activeToast = null;
                // A rejected layered window falls back to the plain popup for the same unlock.
                if (toast.Failed && !next.Retry && !paused && !IsDisposed && !Disposing)
                { next.Retry = true; var retry = new Queue<PendingToast>(new[] { next }.Concat(toasts)); toasts.Clear(); foreach (var p in retry) toasts.Enqueue(p); }
                if (!IsDisposed && !Disposing && IsHandleCreated) BeginInvoke(new Action(PumpToasts));
            };
            activeToast = toast;
            try { toast.Show(); }
            catch (Exception ex) { activeToast = null; toast.Dispose(); Log("Notifica non mostrata: " + ex.Message); return; }
            if (soundOn && !next.Retry) Chime.Play(arts[next.Achievement.Id].Tier);
        }
        Screen ToastScreen()
        {
            try
            {
                if (session != null && (memory != null || NativeMode))
                {
                    IntPtr window = Process.GetProcessById(session.Pid).MainWindowHandle;
                    if (window != IntPtr.Zero) return Screen.FromHandle(window);
                }
            }
            catch (Exception) { }
            return Screen.FromControl(this);
        }
        void UpdateStats(GameSnapshot s)
        {
            bool changed = current == null || current.Rupees != s.Rupees || current.Health != s.Health || current.MaxHealth != s.MaxHealth || s.ProgressionDiffersFrom(current);
            if (engine != null && engineStateVersion != engine.StateVersion) { changed = true; engineStateVersion = engine.StateVersion; }
            current = s;
            if (changed) { InvalidateScene(); Invalidate(); Kick(); }
        }
        int ActiveCount { get { return rules.Count(a => !a.IsPlanned); } }
        int UnlockedCount { get { return rules.Count(a => !a.IsPlanned && progress != null && progress.Contains(a.Id)); } }
        int EarnedPoints { get { return rules.Where(a => !a.IsPlanned && progress != null && progress.Contains(a.Id)).Sum(a => a.Points); } }
        void RefreshList()
        {
            InvalidateScene(); Invalidate(); Kick();
        }
        void CloudLogin() { AuthenticateCloud(false); }
        void CloudSignUp() { AuthenticateCloud(true); }
        async void AuthenticateCloud(bool signup)
        {
            if (cloudBusy || cloud.IsSignedIn) return;
            string email = cloudEmail.Text.Trim(), password = cloudPassword.Text;
            string key = cloudApiKey.Text.Trim(), project = cloudProject.Text.Trim();
            cloudPassword.Clear();
            if (String.IsNullOrWhiteSpace(email) || String.IsNullOrEmpty(password)) { cloudMessage = "Inserisci email e password."; Invalidate(); return; }
            cloudBusy = true; cloudMessage = "Accesso in corso…"; PlaceFields(); Invalidate();
            try
            {
                await Task.Run(() => { cloud.Configure(key, project); if (signup) cloud.SignUp(email, password); else cloud.SignIn(email, password); });
                if (IsDisposed || Disposing) return;
                cloudMessage = "Account connesso. Sincronizzazione dei progressi in corso.";
                nextCloudSync = DateTime.MinValue;
            }
            catch (Exception) { if (!IsDisposed) cloudMessage = "Accesso non riuscito. Verifica configurazione, credenziali e connessione."; }
            finally { password = null; cloudBusy = false; if (!IsDisposed) { PlaceFields(); Invalidate(); } }
            if (!IsDisposed) SyncCloud();
        }
        async void CloudLogout()
        {
            if (cloudBusy) return;
            cloudBusy = true; cloudPassword.Clear(); PlaceFields();
            try { await Task.Run(() => cloud.SignOut()); if (!IsDisposed) cloudMessage = "Account disconnesso; credenziali rimosse."; }
            catch (Exception) { if (!IsDisposed) cloudMessage = "Account disconnesso; impossibile rimuovere alcuni file delle credenziali."; }
            finally { cloudBusy = false; if (!IsDisposed) { PlaceFields(); Invalidate(); } }
        }
        async void SyncCloud()
        {
            if (cloudBusy || cloud == null || !cloud.IsSignedIn || demo || progress == null || DateTime.UtcNow < nextCloudSync) return;
            var target = progress; var entries = target.Snapshot(); var allowed = rules.Where(a => !a.IsPlanned).Select(a => a.Id).ToArray();
            cloudBusy = true; nextCloudSync = DateTime.UtcNow.AddSeconds(30);
            try
            {
                var remote = await Task.Run(() => cloud.Synchronize(target.Identity, entries, allowed));
                if (IsDisposed || Disposing || progress != target || demo) return;
                if (target.Merge(remote, allowed)) { if (engine != null) engine.Reset(); RefreshList(); }
                cloudMessage = "Progressi sincronizzati · " + DateTime.Now.ToString("HH:mm");
            }
            catch (Exception) { if (!IsDisposed) cloudMessage = "Cloud non disponibile: progressi locali conservati, nuovo tentativo automatico."; }
            finally { cloudBusy = false; if (!IsDisposed) { PlaceFields(); Invalidate(); } }
        }
    }
}
