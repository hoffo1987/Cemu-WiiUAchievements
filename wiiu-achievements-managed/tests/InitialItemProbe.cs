using System;
using System.Diagnostics;
using System.Linq;
using WiiUAchievements;

// Manual read-only research. Clothing has live evidence; the series decoders
// are executable experiments, not certified mappings for a complete game set.
class InitialItemProbe
{
    static int Main()
    {
        try
        {
            var processes = Process.GetProcessesByName("Cemu");
            if (processes.Length != 1) throw new InvalidOperationException("Apri una sola sessione Cemu.");
            var process = processes[0];
            var session = GameSession.FromLog(process.MainModule.FileName, process.Id);
            var profile = GameCatalog.Default.RequireProfile(session);
            using (var memory = new ProcessMemory(process.Id))
            {
                if (!memory.FindGuestBases().Contains(session.GuestBase))
                    throw new InvalidOperationException("Log e spazio guest non corrispondono.");
                var sample = profile.CreateReader().Read(memory, session.GuestBase);
                Console.WriteLine("Cemu 2.6 / EUR v0 / cdc68acd; sola lettura, nessun file personale salvato.");
                Console.WriteLine("Cuori: {0}/4 su {1}/4; rupie: {2}", sample.Health, sample.MaxHealth, sample.Rupees);
                Console.WriteLine("IPOTESI cannocchiale (+0x51): {0}", memory.Read(checked(session.GuestBase + 0x145B7BD1U), 1)[0]);
                Console.WriteLine("IPOTESI spada (+0xB4): {0}", memory.Read(checked(session.GuestBase + 0x145B7C34U), 1)[0]);
                Console.WriteLine("IPOTESI scudo (+0xB5): {0}", memory.Read(checked(session.GuestBase + 0x145B7C35U), 1)[0]);
                // GameCube decompilation suggests save + 0x624, event 0x2A80.
                // HD layout and acquisition/load semantics MUST be observed live.
                byte clothes = memory.Read(checked(session.GuestBase + 0x145B81CEU), 1)[0];
                Console.WriteLine("IPOTESI tunica (+0x64E, bit 0x80): {0}; bit={1}", clothes, (clothes & 0x80) != 0 ? 1 : 0);
                Console.WriteLine("Serie sperimentali: valori disponibili {0}/{1}", WindWakerProgression.Fields.Count(f => sample.HasValue(f.Name)), WindWakerProgression.Fields.Count);
                foreach (var field in WindWakerProgression.Fields)
                    Console.WriteLine("TEST " + field.Name + ": " + (sample.HasValue(field.Name) ? sample.Value(field.Name).ToString() : "non disponibile"));
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
