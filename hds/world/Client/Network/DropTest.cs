using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Dev: MXO_DROP_RPC_PACKET=n doesn't send the nth packet carrying RPCs; MXO_DROP_SSEQS=4,5,6 drops the listed
    /// outbound SSEQ values (modulo 4096) once, to verify recovery from startup/in-flight datagram loss.
    /// </summary>
    public static class DropTest
    {
        private static readonly int dropAt = int.TryParse(Environment.GetEnvironmentVariable("MXO_DROP_RPC_PACKET"), out int n) ? n : 0;
        private static readonly HashSet<ushort> dropSseqs = ParseSseqs(Environment.GetEnvironmentVariable("MXO_DROP_SSEQS"));

        public static bool Enabled => dropAt > 0 || dropSseqs != null;

        public static bool ShouldDrop(int rpcPacketNumber) => dropAt > 0 && rpcPacketNumber == dropAt;

        public static bool ShouldDropSseq(ushort sseq) => dropSseqs != null && dropSseqs.Contains((ushort)(sseq & 0xFFF));

        private static HashSet<ushort> ParseSseqs(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            HashSet<ushort> parsed = null;
            foreach (string part in raw.Split(','))
            {
                if (ushort.TryParse(part.Trim(), out ushort seq))
                {
                    if (parsed == null) parsed = new HashSet<ushort>();
                    parsed.Add((ushort)(seq & 0xFFF));
                }
            }
            return parsed;
        }
    }
}
