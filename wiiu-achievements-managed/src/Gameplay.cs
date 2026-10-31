using System;
using System.Linq;
using System.Text;
namespace WiiUAchievements
{
    public enum GameplayState { Unknown, Menu, Loading, Ready }
    // Guest addresses researched from HD source and observed on EUR v0/cdc68acd.
    public sealed class GameplayContext
    {
        public const uint StageAddress = 0x104741E4, PlayerAddress = 0x10474BDC;
        public GameplayState State { get; private set; }
        public string Stage { get; private set; }
        public uint Player { get; private set; }
        public bool RequestedTransition { get; private set; }
        public string Key { get { return Stage + ":" + Player.ToString("X8"); } }
        public GameplayContext(GameplayState state, string stage, uint player, bool requestedTransition = false) { State = state; Stage = stage; Player = player; RequestedTransition = requestedTransition; }
        static string Name(byte[] bytes, int offset)
        {
            int length = 0; while (length < 8 && bytes[offset + length] != 0) length++;
            if (length == 0 || bytes.Skip(offset).Take(length).Any(b => !(b >= 65 && b <= 90 || b >= 97 && b <= 122 || b >= 48 && b <= 57 || b == 95))) return null;
            if (bytes.Skip(offset + length).Take(8 - length).Any(b => b != 0)) return null;
            return Encoding.ASCII.GetString(bytes, offset, length);
        }
        static uint U32(byte[] b, int i) { return ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3]; }
        public static GameplayContext Parse(byte[] stage, byte[] players)
        {
            if (stage == null || stage.Length != 26 || players == null || players.Length != 16) return new GameplayContext(GameplayState.Unknown, null, 0);
            string name = Name(stage, 0), next = Name(stage, 12); uint player = U32(players, 0), second = U32(players, 8);
            if (name == null || next == null || stage[24] > 1) return new GameplayContext(GameplayState.Unknown, name, player);
            if (name == "Name" || name == "sea_T") return new GameplayContext(GameplayState.Menu, name, player);
            if (stage[24] != 0 || player == 0 || second == 0) return new GameplayContext(GameplayState.Loading, name, player, stage[24] == 1 && next != "Name" && next != "sea_T");
            if (player != second || player < 0x10000000 || player >= 0x50000000 || (player & 3) != 0) return new GameplayContext(GameplayState.Unknown, name, player);
            return new GameplayContext(GameplayState.Ready, name, player);
        }
    }
    public sealed class GameplayGate
    {
        string key, activeKey; DateTime first, previous;
        bool preserve;
        public bool Waiting { get; private set; }
        public bool PreserveArming { get { return preserve; } }
        public bool ResumingTransition { get; private set; }
        public string Message { get; private set; }
        public void Reset() { key = activeKey = null; first = previous = DateTime.MinValue; Waiting = preserve = ResumingTransition = false; Message = null; }
        public bool Accept(GameSnapshot sample)
        {
            if (sample.Context == null) { Reset(); return true; }
            ResumingTransition = false;
            if (previous != DateTime.MinValue && (sample.Utc <= previous || (sample.Utc - previous).TotalSeconds > 1))
            { key = activeKey = null; preserve = false; }
            previous = sample.Utc;
            var context = sample.Context;
            if (context.State != GameplayState.Ready)
            {
                if (context.State == GameplayState.Loading && activeKey != null && context.RequestedTransition) preserve = true;
                if (context.State != GameplayState.Loading) { preserve = false; activeKey = null; }
                // A load lacking an observed in-world request never inherits arming.
                key = null; Waiting = true;
                Message = context.State == GameplayState.Menu ? "Selezione salvataggio: attesa automatica." : "Caricamento o stato non disponibile: attesa automatica.";
                return false;
            }
            if (key != context.Key) { key = context.Key; first = sample.Utc; }
            Waiting = (sample.Utc - first).TotalMilliseconds < 750;
            Message = Waiting ? "Partita in stabilizzazione; ripresa automatica." : null;
            if (!Waiting) { ResumingTransition = preserve; preserve = false; activeKey = key; }
            return !Waiting;
        }
    }
}
