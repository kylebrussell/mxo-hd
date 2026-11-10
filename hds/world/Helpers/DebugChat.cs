using System;

namespace hds
{
    /// <summary>
    /// The developer chat lines the server sends about spawns, deletes and targets, off unless MXO_CHAT_DEBUG=1 (the
    /// server's debug chat flag). A Debug build used to send one for every mob that came into or left view, dozens a second
    /// after a login or a teleport, and in that rush the client lost packets. Lost RPCs are now resent, but the flood is
    /// still pointless traffic.
    /// </summary>
    public static class DebugChat
    {
        public static bool Enabled => Output.ChatDebugEnabled;
    }
}
