using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace WiiUAchievements
{
    public enum NativeCemuState { Stopped = 0, Running = 1, Paused = 2 }

    // A consumed observation, never a reference into emulated memory. Reset is
    // sticky across queue replacement so an unseen stop/load cannot join streaks.
    public sealed class NativeCemuSample
    {
        public GameSnapshot Snapshot;
        public NativeCemuState State;
        public ulong Generation, MonotonicMilliseconds;
        public bool Reset;
    }

    public sealed class NativeCemuFeed
    {
        public const int PacketSize = 1952;
        public const uint Protocol = 1;
        public const ulong SupportedTitleId = 0x0005000010143600UL;
        public const uint SupportedRpxHash = 0xCDC68ACD;
        public const int FreshnessMilliseconds = 1000, MinimumSampleMilliseconds = 100;
        readonly object sync = new object();
        readonly Func<long> clock;
        readonly long frequency;
        byte[] pending, previousContext;
        bool haveHeader, haveReceipt, resetPending = true, boundaryPending, staleSent;
        NativeCemuState lastState;
        ulong lastGeneration, lastNative, lastQueued;
        long lastReceipt;
        DateTime pendingUtc;

        public NativeCemuFeed() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency) { }
        // Injectable monotonic clock keeps freshness regressions deterministic.
        public NativeCemuFeed(Func<long> timestamp, long ticksPerSecond)
        {
            if (timestamp == null || ticksPerSecond <= 0) throw new ArgumentException("Orologio nativo non valido.");
            clock = timestamp; frequency = ticksPerSecond;
        }
        static uint U32(byte[] b, int i)
        { return (uint)b[i] | ((uint)b[i + 1] << 8) | ((uint)b[i + 2] << 16) | ((uint)b[i + 3] << 24); }
        static ulong U64(byte[] b, int i) { return U32(b, i) | ((ulong)U32(b, i + 4) << 32); }
        static bool Equal(byte[] a, int ai, byte[] b, int bi, int length)
        { for (int i = 0; i < length; i++) if (a[ai + i] != b[bi + i]) return false; return true; }
        static byte[] Part(byte[] source, int start, int count)
        { var result = new byte[count]; Buffer.BlockCopy(source, start, result, 0, count); return result; }
        double Age(long now) { return (now - (double)lastReceipt) * 1000.0 / frequency; }
        bool Fresh(long now) { double age = Age(now); return haveReceipt && age >= 0 && age <= FreshnessMilliseconds; }
        public bool IsFresh
        { get { lock (sync) return lastState == NativeCemuState.Running && Fresh(clock()); } }

        // Called from the emulator: bounded copy, header checks and one-slot
        // replacement only. Decoding, engine, persistence and UI run at consume.
        public bool Submit(byte[] packet)
        {
            lock (sync)
            {
                if (packet == null || packet.Length != PacketSize || U32(packet, 0) != Protocol || U32(packet, 4) > 2)
                { Invalidate(); return false; }
                var state = (NativeCemuState)U32(packet, 4);
                if (state != NativeCemuState.Stopped &&
                    (U64(packet, 24) != SupportedTitleId || U32(packet, 32) != 0 || U32(packet, 36) != SupportedRpxHash))
                { Invalidate(); return false; }
                ulong generation = U64(packet, 8), milliseconds = U64(packet, 16);
                long now = clock();
                bool segment = !haveHeader || generation != lastGeneration || state != lastState;
                if (!segment && milliseconds <= lastNative)
                {
                    // Duplicate/frozen frames do not renew freshness. A reversal
                    // invalidates continuity rather than reordering observations.
                    if (milliseconds < lastNative) Invalidate();
                    return false;
                }
                if (segment || (haveHeader && milliseconds - lastNative > FreshnessMilliseconds) ||
                    (haveReceipt && !Fresh(now))) resetPending = true;
                // Context changes in an overwritten packet must still be seen
                // as a boundary even when the UI has not consumed the queue yet.
                if (previousContext != null && (!Equal(packet, 40, previousContext, 0, 42) ||
                    !Equal(packet, 1910, previousContext, 42, 42))) resetPending = true;
                previousContext = new byte[84];
                Buffer.BlockCopy(packet, 40, previousContext, 0, 42);
                Buffer.BlockCopy(packet, 1910, previousContext, 42, 42);
                haveHeader = haveReceipt = true; staleSent = false;
                lastGeneration = generation; lastNative = milliseconds; lastState = state; lastReceipt = now;
                // Lifecycle events bypass cadence throttling, even if their
                // timestamps match the last running packet.
                if (!segment && state == NativeCemuState.Running && milliseconds - lastQueued < MinimumSampleMilliseconds)
                    return false;
                // Dropped observations may contain a contradictory condition or
                // invalid save header even when the scene/player stayed equal.
                // Never join confirmation streaks across an unread replacement.
                if (pending != null) resetPending = true;
                pending = (byte[])packet.Clone(); pendingUtc = DateTime.UtcNow; lastQueued = milliseconds;
                boundaryPending = false;
                if (state != NativeCemuState.Running) resetPending = true;
                return true;
            }
        }

        void Invalidate()
        {
            pending = previousContext = null; haveHeader = haveReceipt = false;
            lastState = NativeCemuState.Stopped; resetPending = boundaryPending = true; staleSent = false;
        }
        public void Reset() { lock (sync) Invalidate(); }
        NativeCemuSample Boundary()
        {
            return new NativeCemuSample { State = NativeCemuState.Stopped, Reset = true,
                Generation = lastGeneration, MonotonicMilliseconds = lastNative };
        }
        public bool TryRead(out NativeCemuSample sample)
        {
            lock (sync)
            {
                sample = null;
                if (boundaryPending) { boundaryPending = false; sample = Boundary(); return true; }
                if (lastState == NativeCemuState.Running && haveReceipt && !Fresh(clock()))
                {
                    pending = null; resetPending = true;
                    if (staleSent) return false;
                    staleSent = true; sample = Boundary(); return true;
                }
                if (pending == null) return false;
                byte[] packet = pending; pending = null;
                sample = new NativeCemuSample { State = (NativeCemuState)U32(packet, 4),
                    Generation = U64(packet, 8), MonotonicMilliseconds = U64(packet, 16), Reset = resetPending };
                resetPending = false;
                if (sample.State != NativeCemuState.Running) return true;
                DateTime utc = pendingUtc;
                if (!Equal(packet, 40, packet, 1910, 42))
                {
                    sample.Reset = true;
                    sample.Snapshot = new GameSnapshot { Utc = utc, Context = new GameplayContext(GameplayState.Loading, null, 0) };
                    return true;
                }
                var context = GameplayContext.Parse(Part(packet, 40, 26), Part(packet, 66, 16));
                if (context.State != GameplayState.Ready)
                {
                    sample.Snapshot = new GameSnapshot { Utc = utc, Context = context };
                    return true;
                }
                try
                {
                    sample.Snapshot = WindWakerProgression.Parse(Part(packet, 82, WindWakerProgression.ReadSize));
                    sample.Snapshot.Utc = utc; sample.Snapshot.Context = context;
                }
                catch (ArgumentException)
                {
                    sample.Reset = true;
                    sample.Snapshot = new GameSnapshot { Utc = utc, Context = new GameplayContext(GameplayState.Unknown, null, 0) };
                }
                return true;
            }
        }
    }

    public static class NativeCemuBridge
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void RegisterCallback(IntPtr callback);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int SnapshotCallback(int command, IntPtr packet, int size);
        static readonly SnapshotCallback callback = Callback;
        static readonly object initializeLock = new object();
        static ulong registeredPointer;
        public static readonly NativeCemuFeed Feed = new NativeCemuFeed();

        // CLR ExecuteInDefaultAppDomain requires public static int Method(string).
        public static int Initialize(string registerPointerHex)
        {
            ulong address;
            string value = registerPointerHex == null ? "" : registerPointerHex.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value.Substring(2);
            if (IntPtr.Size != 8 || !UInt64.TryParse(value, NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out address) || address == 0) return -1;
            lock (initializeLock)
            {
                if (registeredPointer != 0) return registeredPointer == address ? 0 : -1;
                try
                {
                    NativeCemuUi.Start();
                    var register = (RegisterCallback)Marshal.GetDelegateForFunctionPointer(new IntPtr(unchecked((long)address)), typeof(RegisterCallback));
                    register(Marshal.GetFunctionPointerForDelegate(callback));
                    registeredPointer = address;
                    return 0;
                }
                catch { Feed.Reset(); return -2; }
            }
        }
        static int Callback(int command, IntPtr packet, int size)
        {
            // Exceptions must never cross the unmanaged C ABI boundary.
            try
            {
                if (command == 1)
                {
                    if (packet == IntPtr.Zero || size != NativeCemuFeed.PacketSize) { Feed.Reset(); return -1; }
                    var bytes = new byte[NativeCemuFeed.PacketSize]; Marshal.Copy(packet, bytes, 0, bytes.Length);
                    return Feed.Submit(bytes) ? 0 : 1;
                }
                if (command == 2) { NativeCemuUi.Show(); return 0; }
                if (command == 3) { Feed.Reset(); NativeCemuUi.Stop(); return 0; }
                return -1;
            }
            catch { Feed.Reset(); return -2; }
        }
    }
}
