using System;
using System.Collections.Generic;
using System.Linq;

namespace hds.world.Structures
{
    /// <summary>A live mission team. Teams are not stored in the database; they last while their members are online.</summary>
    public class LiveTeam
    {
        public UInt32 Id;
        public string Name;
        public UInt32 LeaderCharId;
        public byte LootMode = SocialPackets.DefaultLootMode;
        public readonly List<KeyValuePair<UInt32, string>> Members = new List<KeyValuePair<UInt32, string>>();

        public bool HasMember(UInt32 charId) => Members.Any(m => m.Key == charId);
        public List<UInt32> MemberIds() => Members.Select(m => m.Key).ToList();
    }

    /// <summary>An invite the invitee has not answered yet.</summary>
    public class PendingInvite
    {
        public byte GroupType;
        public UInt32 InviterCharId;
        public string InviterHandle;
        public string InviteeHandle;
        /// <summary>Crew or faction name the inviter typed; for a crew that does not exist yet it names the new crew.</summary>
        public string GroupName;
        public DateTime CreatedUtc;
    }

    /// <summary>Mission teams and pending invites, shared by all client threads.</summary>
    public class SocialGroups
    {
        public static readonly SocialGroups Instance = new SocialGroups();

        /// <summary>Retail's team ids looked like 0x02000069 / 0x030000b8: a counter in the low bits.</summary>
        private const UInt32 TeamIdBase = 0x02000000;

        /// <summary>An unanswered invite blocks a second one to the same player for this long.</summary>
        public static readonly TimeSpan InviteLifetime = TimeSpan.FromSeconds(60);

        private readonly object sync = new object();
        private readonly Dictionary<UInt32, LiveTeam> teams = new Dictionary<UInt32, LiveTeam>();
        private readonly Dictionary<string, PendingInvite> invites = new Dictionary<string, PendingInvite>(StringComparer.OrdinalIgnoreCase);
        private UInt32 teamCounter;

        public LiveTeam CreateTeam(UInt32 leaderCharId, string leaderHandle)
        {
            lock (sync)
            {
                teamCounter++;
                LiveTeam team = new LiveTeam
                {
                    Id = TeamIdBase | (teamCounter & 0x00ffffff),
                    Name = leaderHandle + "'s Mission Team",
                    LeaderCharId = leaderCharId
                };
                team.Members.Add(new KeyValuePair<UInt32, string>(leaderCharId, leaderHandle));
                teams[team.Id] = team;
                return team;
            }
        }

        public LiveTeam FindTeam(UInt32 teamId)
        {
            lock (sync)
            {
                LiveTeam team;
                return teams.TryGetValue(teamId, out team) ? team : null;
            }
        }

        public LiveTeam FindTeamOf(UInt32 charId)
        {
            lock (sync)
            {
                return teams.Values.FirstOrDefault(t => t.HasMember(charId));
            }
        }

        /// <summary>Adds the player and returns their place in the member list (0 = leader), or -1 when the team is gone.</summary>
        public int AddTeamMember(UInt32 teamId, UInt32 charId, string handle)
        {
            lock (sync)
            {
                LiveTeam team;
                if (!teams.TryGetValue(teamId, out team))
                {
                    return -1;
                }

                int existing = team.Members.FindIndex(m => m.Key == charId);
                if (existing >= 0)
                {
                    return existing;
                }

                team.Members.Add(new KeyValuePair<UInt32, string>(charId, handle));
                return team.Members.Count - 1;
            }
        }

        /// <summary>Removes the player. A team left with nobody is deleted; if the leader left, the next member leads.
        /// Returns the team as it is afterwards (null when deleted or not found).</summary>
        public LiveTeam RemoveTeamMember(UInt32 teamId, UInt32 charId)
        {
            lock (sync)
            {
                LiveTeam team;
                if (!teams.TryGetValue(teamId, out team))
                {
                    return null;
                }

                team.Members.RemoveAll(m => m.Key == charId);
                if (team.Members.Count == 0)
                {
                    teams.Remove(teamId);
                    return null;
                }

                if (team.LeaderCharId == charId)
                {
                    team.LeaderCharId = team.Members[0].Key;
                }

                return team;
            }
        }

        public LiveTeam DisbandTeam(UInt32 teamId)
        {
            lock (sync)
            {
                LiveTeam team;
                if (teams.TryGetValue(teamId, out team))
                {
                    teams.Remove(teamId);
                }

                return team;
            }
        }

        /// <summary>Records an invite; false when the invitee already has an unanswered one (retail: "previous request in progress").</summary>
        public bool AddInvite(PendingInvite invite, DateTime nowUtc)
        {
            lock (sync)
            {
                PendingInvite existing;
                if (invites.TryGetValue(invite.InviteeHandle, out existing) && nowUtc - existing.CreatedUtc < InviteLifetime)
                {
                    return false;
                }

                invite.CreatedUtc = nowUtc;
                invites[invite.InviteeHandle] = invite;
                return true;
            }
        }

        /// <summary>Takes (removes) the invitee's pending invite of this type from this inviter, if it is still valid.</summary>
        public PendingInvite TakeInvite(string inviteeHandle, byte groupType, string inviterHandle, DateTime nowUtc)
        {
            lock (sync)
            {
                PendingInvite invite;
                if (!invites.TryGetValue(inviteeHandle, out invite) || invite.GroupType != groupType ||
                    !string.Equals(invite.InviterHandle, inviterHandle, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                invites.Remove(inviteeHandle);
                return nowUtc - invite.CreatedUtc < InviteLifetime ? invite : null;
            }
        }
    }
}
