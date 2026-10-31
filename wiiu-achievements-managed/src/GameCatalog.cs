using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace WiiUAchievements
{
    // A catalog entry is not support by itself: available games require a reader
    // and an exact, validated profile. Never guess an adapter for an unknown title.
    public interface IGameMemoryReader
    {
        GameSnapshot Read(ProcessMemory memory, ulong guestBase);
    }

    public sealed class WindWakerMemoryReader : IGameMemoryReader
    {
        public static int HeroClothesFromFlags(byte flags) { return (flags & 0x80) != 0 ? 1 : 0; }
        public GameSnapshot Read(ProcessMemory memory, ulong guestBase)
        {
            // Observe context on both sides of the save read: a scene change cannot
            // combine the old context with the newly loaded save (or vice versa).
            try
            {
                byte[] beforeStage = memory.Read(checked(guestBase + GameplayContext.StageAddress), 26);
                byte[] beforePlayers = memory.Read(checked(guestBase + GameplayContext.PlayerAddress), 16);
                var context = GameplayContext.Parse(beforeStage, beforePlayers);
                if (context.State != GameplayState.Ready) return new GameSnapshot { Utc = DateTime.UtcNow, Context = context };
                byte[] save = memory.Read(checked(guestBase + WindWakerProgression.SaveAddress), WindWakerProgression.ReadSize);
                byte[] afterStage = memory.Read(checked(guestBase + GameplayContext.StageAddress), 26);
                byte[] afterPlayers = memory.Read(checked(guestBase + GameplayContext.PlayerAddress), 16);
                if (!beforeStage.SequenceEqual(afterStage) || !beforePlayers.SequenceEqual(afterPlayers))
                    return new GameSnapshot { Utc = DateTime.UtcNow, Context = new GameplayContext(GameplayState.Loading, null, 0) };
                var sample = WindWakerProgression.Parse(save); sample.Context = context; return sample;
            }
            catch (System.ComponentModel.Win32Exception) { return new GameSnapshot { Utc = DateTime.UtcNow, Context = new GameplayContext(GameplayState.Unknown, null, 0) }; }
            catch (ArgumentException) { return new GameSnapshot { Utc = DateTime.UtcNow, Context = new GameplayContext(GameplayState.Unknown, null, 0) }; }
        }
    }

    public sealed class GameProfile
    {
        public string Label { get; private set; }
        public string CemuVersion { get; private set; }
        public string TitleId { get; private set; }
        public string TitleVersion { get; private set; }
        public string RpxHash { get; private set; }
        readonly Func<IGameMemoryReader> createReader;

        public GameProfile(string label, string cemuVersion, string titleId, string titleVersion, string rpxHash, Func<IGameMemoryReader> reader)
        {
            if (new[] { label, cemuVersion, titleId, titleVersion, rpxHash }.Any(String.IsNullOrWhiteSpace) || reader == null)
                throw new ArgumentException("Profilo del gioco incompleto.");
            Label = label; CemuVersion = cemuVersion; TitleId = titleId; TitleVersion = titleVersion; RpxHash = rpxHash;
            createReader = reader;
        }
        public bool Matches(GameSession session)
        {
            return session != null && session.CemuVersion == CemuVersion && session.Version == TitleVersion &&
                String.Equals(session.TitleId, TitleId, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(session.RpxHash, RpxHash, StringComparison.OrdinalIgnoreCase);
        }
        public IGameMemoryReader CreateReader() { return createReader(); }
    }

    public sealed class GameDefinition
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string RulesFile { get; private set; }
        public string PendingFile { get; private set; }
        public string ExtraRulesFile { get; private set; }
        public string PresentationFile { get; private set; }
        public string SetRevision { get; private set; }
        public ReadOnlyCollection<GameProfile> Profiles { get; private set; }
        public bool Available { get { return Profiles.Count > 0; } }

        public GameDefinition(string id, string title, string description, string rulesFile, string revision, params GameProfile[] profiles)
            : this(id, title, description, rulesFile, revision, null, profiles) { }
        public GameDefinition(string id, string title, string description, string rulesFile, string revision, string pendingFile, params GameProfile[] profiles)
            : this(id, title, description, rulesFile, revision, pendingFile, null, profiles) { }
        public GameDefinition(string id, string title, string description, string rulesFile, string revision, string pendingFile, string extraRulesFile, params GameProfile[] profiles)
            : this(id, title, description, rulesFile, revision, pendingFile, extraRulesFile, null, profiles) { }
        public GameDefinition(string id, string title, string description, string rulesFile, string revision, string pendingFile, string extraRulesFile, string presentationFile, params GameProfile[] profiles)
        {
            if (String.IsNullOrWhiteSpace(id) || String.IsNullOrWhiteSpace(title) || profiles == null || profiles.Any(p => p == null))
                throw new ArgumentException("Voce del catalogo non valida.");
            if (profiles.Length > 0 && (String.IsNullOrWhiteSpace(rulesFile) || String.IsNullOrWhiteSpace(revision)))
                throw new ArgumentException("Un gioco disponibile richiede un set achievement.");
            Id = id; Title = title; Description = description; RulesFile = rulesFile; SetRevision = revision;
            PendingFile = pendingFile;
            ExtraRulesFile = extraRulesFile;
            PresentationFile = presentationFile;
            Profiles = Array.AsReadOnly((GameProfile[])profiles.Clone());
        }
        public GameProfile RequireProfile(GameSession session)
        {
            var profile = Profiles.FirstOrDefault(p => p.Matches(session));
            if (profile == null) throw new InvalidOperationException("Configurazione non supportata per " + Title + ". Controlla le versioni nella sezione Giochi.");
            return profile;
        }
        public List<Achievement> LoadRules(string root)
        {
            if (!Available) throw new InvalidOperationException("Questo gioco non è ancora disponibile.");
            var loaded = AchievementSet.Load(Path.Combine(root, RulesFile), SetRevision);
            if (ExtraRulesFile != null)
            {
                var extra = AchievementSet.Load(Path.Combine(root, ExtraRulesFile), SetRevision);
                if (extra.Any(a => a.IsPlanned)) throw new InvalidDataException("Le regole aggiuntive devono essere attive.");
                loaded.AddRange(extra);
            }
            if (loaded.Select(a => a.Id).Distinct().Count() != loaded.Count)
                throw new InvalidDataException("ID achievement duplicati nei file attivi.");
            var pending = PendingFile == null ? new List<Achievement>() : AchievementSet.Load(Path.Combine(root, PendingFile), SetRevision);
            if (pending.Any(a => !a.IsPlanned) || loaded.Concat(pending).Select(a => a.Id).Distinct().Count() != loaded.Count + pending.Count)
                throw new InvalidDataException("Catalogo da verificare non valido o con ID duplicati.");
            var results = loaded.Concat(pending).ToList();
            if (PresentationFile != null) AchievementPresentation.Apply(results, Path.Combine(root, PresentationFile), SetRevision);
            return results.OrderBy(a => a.Order).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();
        }
    }

    public static class GameCatalog
    {
        public const string WindWakerId = "wind-waker-hd";
        static readonly ReadOnlyCollection<GameDefinition> games = Array.AsReadOnly(new[] {
            new GameDefinition(WindWakerId, "The Wind Waker HD",
                "Obiettivi di storia, equipaggiamento, esplorazione e secondarie. Attivi e da verificare sono distinti.", "achievements.xml", AchievementSet.Revision, "achievements-planned.xml", "achievements-story.xml", "achievement-presentation.xml",
                new GameProfile("EUR v0", "2.6", GameSession.EurTitleId, "v0", "cdc68acd", () => new WindWakerMemoryReader())),
            new GameDefinition("future-games", "Altri giochi", "Non ancora disponibili. Il catalogo si amplierà con i prossimi giochi supportati.", null, null)
        });
        public static ReadOnlyCollection<GameDefinition> Games { get { return games; } }
        public static GameDefinition Default { get { return games.First(g => g.Id == WindWakerId); } }
    }
}
