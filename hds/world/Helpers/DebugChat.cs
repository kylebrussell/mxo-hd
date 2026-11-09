using System;

namespace hds
{
    /// <summary>
    /// The developer chat lines the server sends about spawns, deletes and targets. They're off unless MXO_DEBUG_CHAT=1: a
    /// Debug build used to send one for every mob that came into or left view, dozens a second after a login or a teleport,
    /// and in that rush the client sometimes lost a packet and stopped processing the server's RPCs (no chat, missions,
    /// objectives) for the rest of the session, since lost RPCs aren't resent.
    /// </summary>
    public static class DebugChat
    {
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("MXO_DEBUG_CHAT") == "1";
    }
}
