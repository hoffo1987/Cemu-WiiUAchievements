using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WiiUAchievements
{
    public sealed class GameSnapshot
    {
        public int Rupees, Health, MaxHealth;
        public GameplayContext Context;
        public int? HeroClothes;
        readonly Dictionary<string, int> progression = new Dictionary<string, int>();
        public void SetProgression(string field, int value)
        {
            var spec = WindWakerProgression.Find(field);
            if (spec == null || value < 0 || value > spec.Maximum) throw new ArgumentException("Valore progressione non valido: " + field);
            progression[field] = value;
        }
        public DateTime Utc;
        public void Validate()
        {
            new Observation { MaxHealth = MaxHealth, Health = Health, Rupees = Rupees }.Validate();
            if (HeroClothes.HasValue && HeroClothes != 0 && HeroClothes != 1)
                throw new ArgumentException("Flag tunica non valido.");
            if (Utc.Kind != DateTimeKind.Utc || Utc == DateTime.MinValue)
                throw new ArgumentException("Il campione deve avere un timestamp UTC valido.");
        }
        public static int U16(byte[] bytes, int offset) { return (bytes[offset] << 8) | bytes[offset + 1]; }
        public static GameSnapshot Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 6) throw new InvalidDataException("Campione di memoria incompleto.");
            var s = new GameSnapshot { MaxHealth = U16(bytes, 0), Health = U16(bytes, 2), Rupees = U16(bytes, 4), Utc = DateTime.UtcNow };
            s.Validate();
            return s;
        }
        public int Value(string field)
        {
            int observed;
            if (progression.TryGetValue(field, out observed)) return observed;
            switch (field)
            {
                case "rupees": return Rupees;
                case "max_health": return MaxHealth;
                case "hero_clothes":
                    if (!HeroClothes.HasValue) throw new InvalidDataException("Dato tunica non disponibile.");
                    return HeroClothes.Value;
                default: throw new InvalidDataException("Campo sconosciuto: " + field);
            }
        }
        public bool HasValue(string field)
        { return field == "hero_clothes" ? HeroClothes.HasValue : field == "rupees" || field == "max_health" || progression.ContainsKey(field); }
        public bool RegressesFrom(GameSnapshot old)
        {
            return old != null && (MaxHealth < old.MaxHealth || (HeroClothes.HasValue && old.HeroClothes.HasValue && HeroClothes < old.HeroClothes) ||
                progression.Any(pair => pair.Key != "grandma_and_soup" && old.HasValue(pair.Key) && pair.Value < old.Value(pair.Key)));
        }

        // Stronger signal used only for automatic load detection.
        // Counts permanent progression fields that dropped (excluding consumables).
        public int PermanentRegressionCount(GameSnapshot old)
        {
            if (old == null) return 0;
            int count = MaxHealth < old.MaxHealth ? 1 : 0;
            if (HeroClothes.HasValue && old.HeroClothes.HasValue && HeroClothes < old.HeroClothes) count++;
            foreach (var pair in progression)
            {
                if (pair.Key == "grandma_and_soup") continue;
                if (old.HasValue(pair.Key) && pair.Value < old.Value(pair.Key)) count++;
            }
            return count;
        }
        public bool ProgressionDiffersFrom(GameSnapshot old)
        { return old == null || HeroClothes != old.HeroClothes || progression.Count != old.progression.Count || progression.Any(p => !old.progression.ContainsKey(p.Key) || old.progression[p.Key] != p.Value); }
        internal GameSnapshot CopyProgression()
        {
            var copy = new GameSnapshot { HeroClothes = HeroClothes, MaxHealth = MaxHealth };
            foreach (var pair in progression) copy.progression.Add(pair.Key, pair.Value);
            return copy;
        }
    }

    public enum AchievementRarity { Bronze, Silver, Gold, Platinum }
    public sealed class Achievement
    {
        public string Id, Title, Description, Field;
        public string Category = "Crescita", Requirement;
        public string Chapter = "Extra";
        public int Order = 10000;
        public bool Experimental;
        public AchievementRarity Rarity = AchievementRarity.Bronze;
        public int Threshold, Points;
        public bool IsPlanned { get { return Requirement != null; } }
        public bool Met(GameSnapshot s)
        {
            if (IsPlanned) throw new InvalidOperationException("Obiettivo in attesa di verifica: " + Id);
            return s.Value(Field) >= Threshold;
        }
    }
    public static class AchievementSet
    {
        public const string Revision = "wwhd-alpha-1";
        public static readonly System.Collections.ObjectModel.ReadOnlyCollection<string> Categories =
            Array.AsReadOnly(new[] { "Storia", "Equipaggiamento", "Esplorazione", "Collezioni", "Secondarie", "Crescita" });
        public static List<Achievement> Load(string path)
        {
            return Load(path, Revision);
        }
        public static List<Achievement> Load(string path, string revision)
        {
            var doc = SafeXml.Load(path);
            if (String.IsNullOrWhiteSpace(revision) || doc.Root == null || doc.Root.Name != "achievement-set" || (string)doc.Root.Attribute("revision") != revision)
                throw new InvalidDataException("Set achievement incompatibile.");
            if (doc.Root.Elements().Any(x => x.Name != "achievement" && x.Name != "planned"))
                throw new InvalidDataException("Elemento sconosciuto nel set achievement.");
            var results = new List<Achievement>();
            foreach (var x in doc.Root.Elements())
            {
                bool planned = x.Name == "planned";
                string[] attributes = planned ? new[] { "id", "title", "description", "category", "requirement", "points", "chapter", "order" } :
                    new[] { "id", "title", "description", "field", "threshold", "points", "category", "experimental", "chapter", "order" };
                if (x.HasElements || x.Attributes().Any(attribute => !attributes.Contains(attribute.Name.ToString())))
                    throw new InvalidDataException("Condizione achievement non supportata.");
                int threshold = 0, points;
                if ((!planned && !Int32.TryParse((string)x.Attribute("threshold"), NumberStyles.None, CultureInfo.InvariantCulture, out threshold)) ||
                    !Int32.TryParse((string)x.Attribute("points"), NumberStyles.None, CultureInfo.InvariantCulture, out points))
                    throw new InvalidDataException("Soglia o punteggio achievement non valido.");
                var a = new Achievement {
                    Id = (string)x.Attribute("id"), Title = (string)x.Attribute("title"),
                    Description = (string)x.Attribute("description"), Field = (string)x.Attribute("field"),
                    Category = (string)x.Attribute("category") ?? (planned ? null : "Crescita"), Requirement = planned ? (string)x.Attribute("requirement") : null,
                    Threshold = threshold, Points = points };
                int order;
                if (x.Attribute("order") != null && (!Int32.TryParse((string)x.Attribute("order"), NumberStyles.None, CultureInfo.InvariantCulture, out order) || order > 10000))
                    throw new InvalidDataException("Ordine obiettivo non valido.");
                a.Order = x.Attribute("order") == null ? 10000 : Int32.Parse((string)x.Attribute("order"), CultureInfo.InvariantCulture);
                a.Chapter = (string)x.Attribute("chapter") ?? (a.Category == "Crescita" ? "15 · Crescita" : "14 · Extra");
                if (String.IsNullOrWhiteSpace(a.Chapter) || a.Chapter.Length > 48 ||
                    (x.Attribute("experimental") != null && (string)x.Attribute("experimental") != "true" && (string)x.Attribute("experimental") != "false"))
                    throw new InvalidDataException("Metadati obiettivo non validi.");
                a.Experimental = (string)x.Attribute("experimental") == "true";
                if (String.IsNullOrWhiteSpace(a.Id) || String.IsNullOrWhiteSpace(a.Title) ||
                    String.IsNullOrWhiteSpace(a.Description) || a.Id != a.Id.Trim() || a.Id.Length > 128 ||
                    results.Any(t => t.Id == a.Id) || !Categories.Contains(a.Category) || a.Points <= 0 ||
                    (planned ? String.IsNullOrWhiteSpace(a.Requirement) :
                    ((a.Field != "rupees" && a.Field != "max_health" && a.Field != "hero_clothes" && !WindWakerProgression.ValidThreshold(a.Field, a.Threshold)) || a.Threshold <= 0 ||
                    (WindWakerProgression.Find(a.Field) != null && !a.Experimental) ||
                    (a.Field == "hero_clothes" && a.Threshold != 1) ||
                    (a.Field == "rupees" && a.Threshold > 5000) ||
                    (a.Field == "max_health" && (a.Threshold < 12 || a.Threshold > 80 || a.Threshold % 4 != 0)))))
                    throw new InvalidDataException("Definizione achievement non valida.");
                results.Add(a);
            }
            if (results.Count == 0) throw new InvalidDataException("Il set non contiene obiettivi.");
            return results;
        }
    }

    // Editorial metadata never changes conditions, points, IDs or progress identity.
    public static class AchievementPresentation
    {
        public static void Apply(List<Achievement> rules, string path, string revision)
        {
            var doc = SafeXml.Load(path);
            if (doc.Root == null || doc.Root.Name != "achievement-presentation" || (string)doc.Root.Attribute("revision") != revision ||
                doc.Root.Attributes().Any(a => a.Name != "revision")) throw new InvalidDataException("Presentazione obiettivi incompatibile.");
            var seen = new HashSet<string>();
            foreach (var node in doc.Root.Elements())
            {
                string[] allowed = { "id", "rarity", "chapter", "order", "title", "description" };
                string id = (string)node.Attribute("id"), rarity = (string)node.Attribute("rarity");
                var rule = rules.SingleOrDefault(a => a.Id == id);
                if (node.Name != "entry" || node.HasElements || node.Attributes().Any(a => !allowed.Contains(a.Name.ToString())) || rule == null || !seen.Add(id))
                    throw new InvalidDataException("Voce di presentazione sconosciuta o duplicata.");
                AchievementRarity value;
                switch (rarity)
                {
                    case "bronze": value = AchievementRarity.Bronze; break;
                    case "silver": value = AchievementRarity.Silver; break;
                    case "gold": value = AchievementRarity.Gold; break;
                    case "platinum":
                        if (id != "completion-platinum" || !rule.IsPlanned) throw new InvalidDataException("Platino riservato al futuro completamento finale del set.");
                        value = AchievementRarity.Platinum; break;
                    default: throw new InvalidDataException("Rarità obiettivo non valida.");
                }
                rule.Rarity = value;
                foreach (string attribute in new[] { "chapter", "title", "description" })
                {
                    var text = node.Attribute(attribute);
                    if (text == null) continue;
                    if (String.IsNullOrWhiteSpace(text.Value) || (attribute == "chapter" && text.Value.Length > 48)) throw new InvalidDataException("Testo di presentazione non valido.");
                    if (attribute == "chapter") rule.Chapter = text.Value;
                    else if (attribute == "title") rule.Title = text.Value;
                    else rule.Description = text.Value;
                }
                int order;
                if (node.Attribute("order") != null)
                {
                    if (!Int32.TryParse((string)node.Attribute("order"), NumberStyles.None, CultureInfo.InvariantCulture, out order) || order > 10000)
                        throw new InvalidDataException("Ordine di presentazione non valido.");
                    rule.Order = order;
                }
            }
            if (seen.Count != rules.Count) throw new InvalidDataException("Ogni obiettivo deve avere una rarità esplicita.");
        }
    }

    public static class SafeXml
    {
        public static XDocument Load(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 };
            using (var reader = XmlReader.Create(path, settings)) return XDocument.Load(reader);
        }
        public static void AtomicSave(XDocument doc, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                doc.Save(temp);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public sealed class Unlock
    {
        public string Id;
        public DateTime Utc;
    }
    public sealed class ProgressStore
    {
        readonly string path, identity;
        readonly Dictionary<string, Unlock> unlocked = new Dictionary<string, Unlock>();
        public string Warning { get; private set; }
        public ProgressStore(string directory, string player, string gameIdentity)
            : this(directory, player, gameIdentity, AchievementSet.Revision) { }
        public ProgressStore(string directory, string player, string gameIdentity, string revision)
        {
            if (String.IsNullOrWhiteSpace(player)) throw new ArgumentException("Inserisci un nome giocatore.");
            if (String.IsNullOrWhiteSpace(gameIdentity) || String.IsNullOrWhiteSpace(revision)) throw new ArgumentException("Identità del gioco o revisione mancante.");
            identity = player.Trim() + "|" + gameIdentity + "|" + revision;
            string key;
            using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 24);
            path = Path.Combine(directory, key + ".xml");
            if (!File.Exists(path)) return;
            try { Load(path); }
            catch (Exception ex)
            {
                if (!(ex is XmlException || ex is InvalidDataException || ex is FormatException || ex is ArgumentException)) throw;
                unlocked.Clear();
                // Do not silently overwrite corrupt progress with an empty document.
                string damaged = path + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Copy(path, damaged, false);
                if (File.Exists(path + ".bak"))
                {
                    try { Load(path + ".bak"); Warning = "Progressi recuperati dal backup; originale danneggiato conservato."; }
                    catch { unlocked.Clear(); throw new InvalidDataException("Progressi e backup non leggibili. Copie conservate: " + damaged); }
                }
                else throw new InvalidDataException("Progressi non leggibili. Originale conservato: " + damaged);
            }
        }
        void Load(string file)
        {
            var entries = ReadRecords(file);
            unlocked.Clear();
            foreach (var item in entries) unlocked.Add(item.Key, item.Value);
        }
        Dictionary<string, Unlock> ReadRecords(string file)
        {
            var doc = SafeXml.Load(file);
            if (doc.Root == null || doc.Root.Name != "progress" || (string)doc.Root.Attribute("identity") != identity)
                throw new InvalidDataException("Identità dei progressi non valida.");
            var temp = new Dictionary<string, Unlock>();
            foreach (var x in doc.Root.Elements("unlock"))
            {
                string id = (string)x.Attribute("id");
                if (String.IsNullOrWhiteSpace(id) || temp.ContainsKey(id)) throw new InvalidDataException("Sblocco non valido.");
                temp.Add(id, new Unlock { Id = id, Utc = DateTime.Parse((string)x.Attribute("utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime() });
            }
            return temp;
        }
        public bool ImportLegacy(string directory, IEnumerable<string> allowedIds)
        {
            string source = Path.Combine(directory, Path.GetFileName(path));
            if (String.Equals(Path.GetFullPath(source), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) || !File.Exists(source)) return false;
            var allowed = new HashSet<string>(allowedIds, StringComparer.Ordinal);
            // Parse against this exact profile/game/revision, without rewriting donors.
            var entries = ReadRecords(source).Values.Where(x => allowed.Contains(x.Id));
            return Merge(entries, allowed);
        }
        public bool Contains(string id) { return unlocked.ContainsKey(id); }
        public string Identity { get { return identity; } }
        public Unlock[] Snapshot() { return unlocked.Values.Select(x => new Unlock { Id = x.Id, Utc = x.Utc }).ToArray(); }
        public bool Merge(IEnumerable<Unlock> incoming, IEnumerable<string> allowedIds)
        {
            var allowed = new HashSet<string>(allowedIds, StringComparer.Ordinal);
            var additions = new Dictionary<string, Unlock>();
            foreach (var item in incoming)
            {
                if (item == null || !allowed.Contains(item.Id) || item.Utc.Kind != DateTimeKind.Utc || item.Utc == DateTime.MinValue || item.Utc > DateTime.UtcNow.AddMinutes(5))
                    throw new InvalidDataException("Sblocco cloud non valido.");
                if (!Contains(item.Id) && !additions.ContainsKey(item.Id)) additions.Add(item.Id, new Unlock { Id = item.Id, Utc = item.Utc });
            }
            if (additions.Count == 0) return false;
            var entries = unlocked.Values.Concat(additions.Values).OrderBy(x => x.Utc).ThenBy(x => x.Id, StringComparer.Ordinal);
            SafeXml.AtomicSave(new XDocument(new XElement("progress", new XAttribute("identity", identity),
                entries.Select(x => new XElement("unlock", new XAttribute("id", x.Id), new XAttribute("utc", x.Utc.ToString("o", CultureInfo.InvariantCulture)))))), path);
            foreach (var item in additions) unlocked.Add(item.Key, item.Value);
            return true;
        }
        public Unlock Get(string id)
        {
            Unlock value;
            return unlocked.TryGetValue(id, out value) ? new Unlock { Id = value.Id, Utc = value.Utc } : null;
        }
        public void Award(string id, DateTime utc)
        {
            AwardMany(new[] { id }, utc);
        }
        public void AwardMany(IEnumerable<string> ids, DateTime utc)
        {
            if (ids == null) throw new ArgumentNullException("ids");
            if (utc.Kind != DateTimeKind.Utc || utc == DateTime.MinValue) throw new ArgumentException("Timestamp sblocco non valido.");
            var requested = ids.Distinct().ToList();
            if (requested.Any(String.IsNullOrWhiteSpace)) throw new ArgumentException("ID sblocco non valido.");
            var additions = requested.Where(id => !Contains(id)).Select(id => new Unlock { Id = id, Utc = utc }).ToList();
            if (additions.Count == 0) return;
            var entries = unlocked.Values.Concat(additions).OrderBy(x => x.Utc).ThenBy(x => x.Id, StringComparer.Ordinal);
            var doc = new XDocument(new XElement("progress", new XAttribute("identity", identity),
                entries.Select(x => new XElement("unlock", new XAttribute("id", x.Id), new XAttribute("utc", x.Utc.ToString("o", CultureInfo.InvariantCulture))))));
            SafeXml.AtomicSave(doc, path); // All awards from a sample commit together, before publishing any.
            foreach (var item in additions) unlocked.Add(item.Id, item);
        }
    }

    public enum AchievementPhase { WaitingForBaseline, Stabilizing, AlreadySatisfied, Tracking, Confirming, Unlocked, PendingValidation }
    public enum AchievementRecognition { NewEventsOnly, IncludeExistingSave }
    public sealed class AchievementStatus
    {
        public AchievementPhase Phase { get; internal set; }
        public int Confirmations { get; internal set; }
    }

    public sealed class AchievementEngine
    {
        readonly List<Achievement> achievements;
        readonly ProgressStore store;
        readonly AchievementRecognition recognition;
        readonly HashSet<string> recognizedExisting = new HashSet<string>();
        readonly Dictionary<string, bool> armed = new Dictionary<string, bool>();
        readonly Dictionary<string, int> streak = new Dictionary<string, int>();
        readonly Dictionary<string, int> below = new Dictionary<string, int>();
        readonly Dictionary<string, AchievementStatus> states = new Dictionary<string, AchievementStatus>();
        public const int RequiredSamples = 3;
        public int StateVersion { get; private set; }
        DateTime? lastSample;
        GameSnapshot lastProgression;
        readonly GameplayGate gameplay = new GameplayGate();
        public bool WaitingForGameplay { get { return gameplay.Waiting; } }
        public string GameplayMessage { get { return gameplay.Message; } }
        public AchievementEngine(List<Achievement> rules, ProgressStore progress)
            : this(rules, progress, AchievementRecognition.NewEventsOnly) { }
        public AchievementEngine(List<Achievement> rules, ProgressStore progress, AchievementRecognition recognition)
        {
            if (rules == null || progress == null) throw new ArgumentNullException(rules == null ? "rules" : "progress");
            if (rules.Any(a => a == null || String.IsNullOrWhiteSpace(a.Id)) || rules.Select(a => a.Id).Distinct().Count() != rules.Count)
                throw new ArgumentException("Regole duplicate o non valide.");
            if (!Enum.IsDefined(typeof(AchievementRecognition), recognition)) throw new ArgumentException("Modalità di riconoscimento non valida.");
            achievements = new List<Achievement>(rules); store = progress; this.recognition = recognition; Reset();
        }
        public bool RecognizedFromSave(string id) { return recognizedExisting.Contains(id); }
        void SetState(string id, AchievementPhase phase, int confirmations)
        {
            AchievementStatus old;
            if (states.TryGetValue(id, out old) && old.Phase == phase && old.Confirmations == confirmations) return;
            states[id] = new AchievementStatus { Phase = phase, Confirmations = confirmations };
            StateVersion++;
        }
        public AchievementStatus Status(string id)
        {
            var state = states[id];
            return new AchievementStatus { Phase = state.Phase, Confirmations = state.Confirmations };
        }
        public void Reset() { gameplay.Reset(); ResetConditions(); }
        void ResetConditions()
        {
            recognizedExisting.Clear();
            lastSample = null;
            lastProgression = null;
            foreach (var a in achievements)
            {
                armed[a.Id] = false; streak[a.Id] = 0; below[a.Id] = 0;
                SetState(a.Id, a.IsPlanned ? AchievementPhase.PendingValidation : store.Contains(a.Id) ? AchievementPhase.Unlocked : AchievementPhase.WaitingForBaseline, 0);
            }
        }
        public List<Achievement> Update(GameSnapshot s)
        {
            recognizedExisting.Clear();
            try
            {
                if (s == null) throw new ArgumentNullException("s");
                if (s.Utc.Kind != DateTimeKind.Utc || s.Utc == DateTime.MinValue) throw new ArgumentException("Timestamp non valido.");
                if (!gameplay.Accept(s))
                {
                    if (gameplay.PreserveArming)
                    {
                        lastSample = null;
                        foreach (var a in achievements)
                        {
                            streak[a.Id] = below[a.Id] = 0;
                            if (!a.IsPlanned && !store.Contains(a.Id)) SetState(a.Id, AchievementPhase.WaitingForBaseline, 0);
                        }
                    }
                    else ResetConditions();
                    return new List<Achievement>();
                }
                if (gameplay.ResumingTransition && s.RegressesFrom(lastProgression)) ResetConditions();
                s.Validate();
            }
            catch { Reset(); throw; }
            if (lastSample.HasValue && (s.Utc <= lastSample.Value || (s.Utc - lastSample.Value).TotalSeconds > 1 || s.RegressesFrom(lastProgression))) Reset();
            var awards = new List<Achievement>();
            foreach (var a in achievements)
            {
                if (a.IsPlanned || store.Contains(a.Id)) continue;
                if (!s.HasValue(a.Field))
                {
                    armed[a.Id] = false; streak[a.Id] = 0; below[a.Id] = 0;
                    SetState(a.Id, AchievementPhase.WaitingForBaseline, 0); continue;
                }
                bool met = a.Met(s);
                if (!met)
                {
                    streak[a.Id] = 0;
                    below[a.Id] = Math.Min(RequiredSamples, below[a.Id] + 1);
                    if (below[a.Id] >= RequiredSamples) armed[a.Id] = true;
                    SetState(a.Id, armed[a.Id] ? AchievementPhase.Tracking : AchievementPhase.Stabilizing, 0);
                    continue;
                }
                below[a.Id] = 0;
                bool existing = !armed[a.Id];
                if (existing && (recognition != AchievementRecognition.IncludeExistingSave || s.Context == null))
                { SetState(a.Id, AchievementPhase.AlreadySatisfied, 0); continue; }
                streak[a.Id]++;
                SetState(a.Id, AchievementPhase.Confirming, streak[a.Id]);
                if (streak[a.Id] < RequiredSamples) continue;
                awards.Add(a);
                if (existing) recognizedExisting.Add(a.Id);
            }
            try { store.AwardMany(awards.Select(a => a.Id), s.Utc); }
            catch { Reset(); throw; }
            foreach (var a in awards) SetState(a.Id, AchievementPhase.Unlocked, 0);
            lastSample = s.Utc;
            lastProgression = s.CopyProgression();
            return awards;
        }
    }

    public static class GameIdentity
    {
        public static bool IsWindWaker(string title)
        {
            if (String.IsNullOrWhiteSpace(title)) return false;
            return title.IndexOf("wind waker", StringComparison.OrdinalIgnoreCase) >= 0 &&
                title.IndexOf("HD", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
