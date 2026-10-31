using System;
using System.Diagnostics;
using WiiUAchievements;

class Probe
{
    static int Main(string[] args)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("Cemu"))
            {
                Console.WriteLine("Cemu PID: " + process.Id);
                Console.WriteLine("Titolo: " + process.MainWindowTitle);
                using (var memory = new ProcessMemory(process.Id))
                {
                    var bases = memory.FindGuestBases();
                    Console.WriteLine("Spazi guest: " + bases.Count);
                    foreach (ulong b in bases)
                    {
                        Console.WriteLine("Base: 0x" + b.ToString("X"));
                        foreach (uint address in new uint[] { 0x145B7B80, 0x1506B500 })
                        {
                            try
                            {
                                var s = GameSnapshot.Parse(memory.Read(b + address, 6));
                                Console.WriteLine(String.Format("0x{0:X8}: cuori {1}/4 / {2}/4; rupie {3}", address, s.Health, s.MaxHealth, s.Rupees));
                            }
                            catch (Exception ex) { Console.WriteLine("0x" + address.ToString("X8") + ": " + ex.Message); }
                        }
                    }
                }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); return 1; }
    }
}
