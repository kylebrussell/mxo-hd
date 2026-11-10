using System;

namespace hds
{
    /// <summary>Dev: MXO_DROP_RPC_PACKET=n doesn't send the nth packet carrying RPCs, to see how the client handles a loss.</summary>
    public static class DropTest
    {
        private static readonly int dropAt = int.TryParse(Environment.GetEnvironmentVariable("MXO_DROP_RPC_PACKET"), out int n) ? n : 0;

        public static bool Enabled => dropAt > 0;

        public static bool ShouldDrop(int rpcPacketNumber) => dropAt > 0 && rpcPacketNumber == dropAt;
    }
}
