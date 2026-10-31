using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WiiUAchievements
{
    public sealed class Region
    {
        public ulong Address, AllocationBase, Size;
        public uint State, Protection, Type;
        public bool Readable
        {
            get
            {
                uint p = Protection & 0xff;
                return State == 0x1000 && (Protection & 0x100) == 0 &&
                    (p == 2 || p == 4 || p == 8 || p == 0x20 || p == 0x40 || p == 0x80);
            }
        }
    }

    // Deliberately exposes no write, injection, breakpoint or process-control API.
    public sealed class ProcessMemory : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Mbi
        {
            public IntPtr BaseAddress, AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State, Protect, Type;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr read);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UIntPtr VirtualQueryEx(IntPtr process, IntPtr address, out Mbi info, UIntPtr size);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        IntPtr handle;
        public int Pid { get; private set; }
        public ProcessMemory(int pid)
        {
            if (IntPtr.Size != 8) throw new InvalidOperationException("Eseguire la versione a 64 bit.");
            Pid = pid;
            handle = OpenProcess(0x0400 | 0x0010, false, pid); // QUERY_INFORMATION | VM_READ
            if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossibile leggere il processo selezionato.");
        }
        public byte[] Read(ulong address, int size)
        {
            if (handle == IntPtr.Zero) throw new ObjectDisposedException("ProcessMemory");
            if (size < 1 || size > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException("size");
            byte[] buffer = new byte[size];
            UIntPtr read;
            if (!ReadProcessMemory(handle, new IntPtr(checked((long)address)), buffer, (UIntPtr)(uint)size, out read) || read.ToUInt64() != (ulong)size)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Memoria non disponibile: 0x" + address.ToString("X"));
            return buffer;
        }
        public List<Region> Regions()
        {
            var result = new List<Region>();
            ulong cursor = 0;
            while (cursor < 0x00007FFFFFFF0000)
            {
                Mbi m;
                if (VirtualQueryEx(handle, new IntPtr((long)cursor), out m, (UIntPtr)(uint)Marshal.SizeOf(typeof(Mbi))) == UIntPtr.Zero) break;
                ulong start = (ulong)m.BaseAddress.ToInt64(), size = m.RegionSize.ToUInt64();
                result.Add(new Region { Address = start, Size = size, AllocationBase = (ulong)m.AllocationBase.ToInt64(), State = m.State, Protection = m.Protect, Type = m.Type });
                if (size == 0 || start + size <= cursor) break;
                cursor = start + size;
            }
            return result;
        }
        // Cemu 2.6 reserves one continuous 4 GiB virtual address space (MMU.cpp).
        // AllocationBase groups both committed and reserved pages in that reservation.
        public List<ulong> FindGuestBases()
        {
            var result = new List<ulong>();
            foreach (var group in Regions().Where(r => r.AllocationBase != 0).GroupBy(r => r.AllocationBase))
            {
                ulong start = group.Key;
                if (group.Max(r => r.Address + r.Size) - start != 0x100000000UL) continue;
                // Guest code and MEM2 must both have readable committed pages.
                if (!group.Any(r => r.Readable && r.Address <= start + 0x02000000 && r.Address + r.Size > start + 0x02000000)) continue;
                if (!group.Any(r => r.Readable && r.Address < start + 0x40000000 && r.Address + r.Size > start + 0x10000000)) continue;
                result.Add(start);
            }
            return result;
        }
        public void Dispose()
        {
            if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; }
        }
    }

    public sealed class Observation
    {
        public int Rupees, Health, MaxHealth;
        public bool Matches(byte[] b, int offset)
        {
            return offset >= 0 && offset + 6 <= b.Length &&
                GameSnapshot.U16(b, offset) == MaxHealth && GameSnapshot.U16(b, offset + 2) == Health &&
                GameSnapshot.U16(b, offset + 4) == Rupees;
        }
        public bool Same(Observation other)
        {
            return other != null && Rupees == other.Rupees && Health == other.Health && MaxHealth == other.MaxHealth;
        }
        public void Validate()
        {
            if (Rupees < 0 || Rupees > 5000 || MaxHealth < 12 || MaxHealth > 80 || MaxHealth % 4 != 0 || Health < 0 || Health > MaxHealth)
                throw new ArgumentException("Valori di gioco non validi.");
        }
    }

    public sealed class Candidate
    {
        public ulong GuestBase;
        public uint SaveAddress;
        public override string ToString() { return "0x" + SaveAddress.ToString("X8"); }
    }

    public static class Calibration
    {
        public static List<Candidate> Scan(ProcessMemory memory, Observation observation, CancellationToken cancel, Action<int> progress)
        {
            observation.Validate();
            var bases = memory.FindGuestBases();
            if (bases.Count == 0) throw new InvalidOperationException("Spazio Wii U non trovato. Avvia il gioco in Cemu 2.6 e carica un salvataggio.");
            var regions = memory.Regions();
            var hits = new List<Candidate>();
            const int chunk = 1024 * 1024;
            int done = 0;
            var ranges = new List<Tuple<ulong, ulong, ulong>>();
            foreach (ulong guestBase in bases)
                foreach (var region in regions.Where(r => r.Readable && r.AllocationBase == guestBase))
                {
                    ulong begin = Math.Max(region.Address, guestBase + 0x10000000UL);
                    ulong end = Math.Min(region.Address + region.Size, guestBase + 0x40000000UL);
                    if (end > begin) ranges.Add(Tuple.Create(guestBase, begin, end));
                }
            long total = ranges.Sum(r => (long)(r.Item3 - r.Item2));
            long completed = 0;
            foreach (var range in ranges)
            {
                for (ulong address = range.Item2; address < range.Item3; address += chunk)
                {
                    cancel.ThrowIfCancellationRequested();
                    int size = (int)Math.Min((ulong)(chunk + 5), range.Item3 - address);
                    byte[] data;
                    try { data = memory.Read(address, size); }
                    catch (Win32Exception) { completed += Math.Min(chunk, size); continue; }
                    for (int i = 0; i + 6 <= size && i < chunk; i++)
                    {
                        // The save header is 16-bit aligned in the Wii U address space.
                        if (((address + (ulong)i - range.Item1) & 1) != 0 || !observation.Matches(data, i)) continue;
                        hits.Add(new Candidate { GuestBase = range.Item1, SaveAddress = checked((uint)(address + (ulong)i - range.Item1)) });
                        if (hits.Count > 5000) throw new InvalidOperationException("Troppi risultati. Cambia il numero di rupie e ripeti la ricerca.");
                    }
                    completed += Math.Min(chunk, size);
                    int percent = total == 0 ? 100 : (int)(completed * 100 / total);
                    if (percent != done) { done = percent; progress(percent); }
                }
            }
            progress(100);
            return hits;
        }
        public static List<Candidate> Filter(ProcessMemory memory, List<Candidate> candidates, Observation observation)
        {
            observation.Validate();
            var result = new List<Candidate>();
            foreach (var c in candidates)
            {
                try { if (observation.Matches(memory.Read(c.GuestBase + c.SaveAddress, 6), 0)) result.Add(c); }
                catch (Win32Exception) { }
            }
            return result;
        }
    }
}
