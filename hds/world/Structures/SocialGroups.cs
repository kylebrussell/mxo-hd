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

        /// <summary>Sent from the crew creation window (80 84): the inviter hears the outcome as 80 85, not 6f.</summary>
        public bool FromCrewCreation;

        /// <summary>The organisation the creation window picked.</summary>
        public byte Org;
    }

    /// <summary>A player on the "looking for team" list (client 80 91).</summary>
    public class TeamSeeker
    {
        public UInt32 CharId;
        public string Handle;
        public byte Level;
        public byte Org;
        public UInt32 FactionId;
        public UInt32 BestDiscipline;
    }

    /// <summary>The filters of a team search (client 80 92).</summary>
    public class SeekerSearch
    {
        public byte MinLevel;
        public byte MaxLevel;
        public bool MyFactionOnly;
        public UInt32 FactionId;
        public List<UInt32> Disciplines = new List<UInt32>();
        public List<byte> Orgs = new List<byte>();
    }

    /// <summary>Faction ranks per crew: 1 leads, 5 is the bottom (client.dll offers Promote above rank 1 and Demote
    /// below 5, and only to a captain whose crew ranks higher than the target).</summary>
    public static class FactionRanks
    {
        /// <summary>The stored rank, or for crews from before ranks were kept (0): the faction master's crew leads, the
        /// rest are at the bottom.</summary>
        public static ushort Effective(ushort stored, bool masterCrew)
        {
            if (stored >= SocialPackets.TopFactionRank && stored <= SocialPackets.BottomFactionRank)
            {
                return stored;
            }

            return masterCrew ? SocialPackets.TopFactionRank : SocialPackets.BottomFactionRank;
        }

        /// <summary>0 and the new rank, or one of the client's refusal codes (80 81).</summary>
        public static uint Change(ushort actorRank, ushort targetRank, bool promote, out ushort newRank)
        {
            newRank = targetRank;
            if (actorRank >= targetRank)
            {
                return SocialPackets.FactionPromoterNotHigher;
            }

            int wanted = promote ? targetRank - 1 : targetRank + 1;
            if (wanted < SocialPackets.TopFactionRank || wanted > SocialPackets.BottomFactionRank)
            {
                return SocialPackets.FactionIllegalRanks;
            }

            newRank = (ushort) wanted;
            return SocialPackets.ResultOk;
        }
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

        /// <summary>Makes a member the team's leader; false when they aren't on it.</summary>
        public bool SetLeader(UInt32 teamId, UInt32 charId)
        {
            lock (sync)
            {
                LiveTeam team;
                if (!teams.TryGetValue(teamId, out team) || !team.HasMember(charId))
                {
                    return false;
                }

                team.LeaderCharId = charId;
                return true;
            }
        }

        // ---- looking for team (80 91 / 80 92) ------------------------------------------------------------------

        private readonly Dictionary<UInt32, TeamSeeker> seekers = new Dictionary<UInt32, TeamSeeker>();

        public void AddSeeker(TeamSeeker seeker)
        {
            lock (sync)
            {
                seekers[seeker.CharId] = seeker;
            }
        }

        public bool RemoveSeeker(UInt32 charId)
        {
            lock (sync)
            {
                return seekers.Remove(charId);
            }
        }

        public bool IsSeeking(UInt32 charId)
        {
            lock (sync)
            {
                return seekers.ContainsKey(charId);
            }
        }

        /// <summary>Seekers other than the searcher that pass the filters, by level then handle. A level range of 0..0
        /// takes every level; an empty discipline or organisation list takes all.</summary>
        public List<TeamSeeker> FindSeekers(UInt32 searcherCharId, SeekerSearch search)
        {
            lock (sync)
            {
                byte min = search.MinLevel, max = search.MaxLevel;
                if (min > max)
                {
                    byte swap = min;
                    min = max;
                    max = swap;
                }

                return seekers.Values.Where(s => s.CharId != searcherCharId &&
                                                 (max == 0 || (s.Level >= min && s.Level <= max)) &&
                                                 (!search.MyFactionOnly || (search.FactionId != 0 && s.FactionId == search.FactionId)) &&
                                                 (search.Disciplines.Count == 0 || search.Disciplines.Contains(s.BestDiscipline)) &&
                                                 (search.Orgs.Count == 0 || search.Orgs.Contains(s.Org)))
                    .OrderBy(s => s.Level).ThenBy(s => s.Handle, StringComparer.OrdinalIgnoreCase).ToList();
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
