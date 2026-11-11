using System;

namespace hds
{
    /// <summary>
    /// Who may fight whom. <see cref="IsOpponent"/> is the client's own test for two players (client.dll 0x625ed830,
    /// read from the code: docs/PVP.md), so the server allows exactly the attacks the client offers. What the server
    /// adds on top (<see cref="Check"/>) is ours and marked so.
    /// </summary>
    public static class PvpRules
    {
        /// <summary>
        /// Values of the client's PvPServer setting. Retail sent 6 on its hostile server and 2 elsewhere (captures:
        /// died_in_pvp and some_pvp have 6, the ProxyM2 one 2). The client treats 2, 4 and 6 alike.
        /// </summary>
        public const int ModeOff = 0, ModeByLevel = 1, ModeByFlag = 2, ModeEveryone = 3, ModeEveryoneButTeam = 5, ModeHostile = 6;

        /// <summary>One player as the rule sees them.</summary>
        public class Subject
        {
            public UInt32 CharId;
            public int Level;
            /// <summary>OrganizationID: 0 for a player with no organisation.</summary>
            public UInt32 Organization;
            /// <summary>ConditionStateFlags bit 0x01000000.</summary>
            public bool PvpFlag;
            public UInt32 DuelId;
            /// <summary>Stands in a sector whose data has the PvP Zone property.</summary>
            public bool InPvpZone;
            public UInt32 MissionTeamId;
            public bool Dead;
        }

        /// <summary>The client's DuelID test (0x625ed7e0): both set, equal but for bit 0.</summary>
        public static bool DuelOpponents(UInt32 a, UInt32 b)
        {
            return a != 0 && b != 0 && ((a ^ b) & ~1u) == 0 && ((a ^ b) & 1u) != 0;
        }

        /// <summary>
        /// The client's IsOpponent for two players: duel partners always; otherwise the server mode must make the target
        /// fair game (their PvP flag in modes 2, 4 and 6; both levels above the safe level in mode 1) or both must stand
        /// in PvP sectors, and then they must not share a mission team and must belong to different organisations. The
        /// attacker's own flag is never read, and players without an organisation fight nobody outside a duel.
        /// </summary>
        public static bool IsOpponent(Subject self, Subject target, int mode, int maxSafeLevel)
        {
            if (self.CharId != 0 && self.CharId == target.CharId)
            {
                return false;
            }
            if (DuelOpponents(self.DuelId, target.DuelId))
            {
                return true;
            }

            bool fairGame = false, force = false;
            switch (mode)
            {
                case ModeByLevel:
                    fairGame = self.Level > maxSafeLevel && target.Level > maxSafeLevel;
                    break;
                case 2:
                case 4:
                case 6:
                    fairGame = target.PvpFlag;
                    break;
                case ModeEveryone:
                    return true;
                case ModeEveryoneButTeam:
                    fairGame = force = true;
                    break;
            }
            if (!(fairGame || (self.InPvpZone && target.InPvpZone)))
            {
                return false;
            }
            if (self.MissionTeamId != 0 && self.MissionTeamId == target.MissionTeamId)
            {
                return false;
            }
            if (force)
            {
                return true;
            }
            if (self.Organization == 0 || target.Organization == 0)
            {
                return false;
            }
            return self.Organization != target.Organization;
        }

        public enum Verdict
        {
            Allowed,
            /// <summary>RPC 4d reason 2 (dead), for the attacker or about the target.</summary>
            AttackerDead,
            TargetDead,
            /// <summary>RPC 4d reason 24, "Target is not an opponent."</summary>
            NotOpponent,
        }

        /// <summary>
        /// The server's answer to an attack on another player. Beyond the client's test: nobody dead fights (ours; the
        /// client has the messages for it), and a player in a duel fights only the duel partner (ours, after the
        /// client's own lock: in a duel it lets the player select nobody else).
        /// </summary>
        public static Verdict Check(Subject attacker, Subject target, int mode, int maxSafeLevel)
        {
            if (attacker.Dead)
            {
                return Verdict.AttackerDead;
            }
            if (target.Dead)
            {
                return Verdict.TargetDead;
            }
            bool duel = DuelOpponents(attacker.DuelId, target.DuelId);
            if (!duel && (attacker.DuelId != 0 || target.DuelId != 0))
            {
                return Verdict.NotOpponent;
            }
            return IsOpponent(attacker, target, mode, maxSafeLevel) ? Verdict.Allowed : Verdict.NotOpponent;
        }

        /// <summary>
        /// Whether a hostile server (mode 6) flags this player by itself. Retail's hostile server set the flag right
        /// after world entry and again whenever a fight started (some_pvp capture); PvPMaxSafeLevel kept new characters
        /// out of it. Which levels exactly retail spared isn't in any capture: this uses the client's own comparison
        /// for mode 1, level above the safe level.
        /// </summary>
        public static bool HostileServerFlags(int level, int maxSafeLevel)
        {
            return level > maxSafeLevel;
        }
    }
}
