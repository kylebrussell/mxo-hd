using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    /// <summary>
    /// Mission teams, crews and factions: invites (client 6e / 80 8c), answers (6f), leaving (71), disbanding (77),
    /// team loot mode (80 90) and group chat. The order of packets each player gets follows the retail captures
    /// named in SocialPackets.
    /// </summary>
    public class SocialHandler
    {
        private static SocialGroups Groups => SocialGroups.Instance;

        // ---- helpers ------------------------------------------------------------------------------------------

        internal static string HandleOf(WorldClient client) =>
            StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());

        private static UInt32 CrewIdOf(WorldClient client) =>
            NumericalUtils.ByteArrayToUint32(client.playerInstance.CrewID.getValue(), 1);

        private static UInt32 FactionIdOf(WorldClient client) =>
            NumericalUtils.ByteArrayToUint32(client.playerInstance.FactionID.getValue(), 1);

        internal static WorldClient FindOnline(Func<WorldClient, bool> match)
        {
            lock (WorldServer.Clients)
            {
                return WorldServer.Clients.Values.FirstOrDefault(c =>
                    c != null && c.playerData != null && c.playerData.getOnWorld() && match(c));
            }
        }

        internal static WorldClient FindOnlineByHandle(string handle) =>
            FindOnline(c => string.Equals(HandleOf(c), handle, StringComparison.OrdinalIgnoreCase));

        internal static WorldClient FindOnlineByCharId(UInt32 charId) =>
            FindOnline(c => c.playerData.getCharID() == charId);

        internal static List<WorldClient> OnlineWhere(Func<WorldClient, bool> match)
        {
            lock (WorldServer.Clients)
            {
                return WorldServer.Clients.Values.Where(c =>
                    c != null && c.playerData != null && c.playerData.getOnWorld() && match(c)).ToList();
            }
        }

        internal static void Send(WorldClient client, byte[] rpc)
        {
            if (client == null)
            {
                return;
            }

            client.messageQueue.addRpcMessage(rpc);
            client.FlushQueue();
        }

        private static void SendToAll(IEnumerable<WorldClient> clients, byte[] rpc)
        {
            foreach (WorldClient client in clients)
            {
                Send(client, rpc);
            }
        }

        /// <summary>Sets group ids on the player and sends them to the player's own view (02 00 02 ...).
        /// Pass null to leave an id unchanged.</summary>
        internal static void SetOwnGroupIds(WorldClient client, UInt32? factionId, UInt32? crewId, UInt32? teamId)
        {
            List<Attribute> changed = new List<Attribute>();
            if (factionId.HasValue)
            {
                client.playerInstance.FactionID.setValue(factionId.Value);
                changed.Add(client.playerInstance.FactionID);
            }

            if (crewId.HasValue)
            {
                client.playerInstance.CrewID.setValue(crewId.Value);
                changed.Add(client.playerInstance.CrewID);
            }

            if (teamId.HasValue)
            {
                client.playerInstance.MissionTeamID.setValue(teamId.Value);
                changed.Add(client.playerInstance.MissionTeamID);
            }

            if (changed.Count == 0)
            {
                return;
            }

            byte[] block = client.playerInstance.GetSelfUpdateAttributes(changed, false);
            client.messageQueue.addObjectMessage(SocialPackets.SelfViewUpdate(block), false);
            client.FlushQueue();

            // Players who can see me get the change on their view of me (crew/faction name tags).
            byte[] others = SocialPackets.OtherViewUpdate(client.playerInstance.GetUpdateAttributes(changed));
            Store.world.SendViewPacketToAllPlayers(others, client.playerData.getCharID(),
                NumericalUtils.ByteArrayToUint16(client.playerInstance.GetGoid(), 1), client.playerData.getEntityId());
        }

        private static void Notice(WorldClient client, string text)
        {
            new ServerPackets().sendSystemChatMessage(client, text, "SYSTEM");
        }

        // ---- mission teams ------------------------------------------------------------------------------------

        /// <summary>The player's team, created (80 8d + MissionTeamID) if they have none. Retail sent 80 8d in answer
        /// to the mission list request (two-person capture 09:34:12).</summary>
        public static LiveTeam EnsureTeam(WorldClient client)
        {
            LiveTeam team = Groups.FindTeamOf(client.playerData.getCharID());
            if (team != null)
            {
                return team;
            }

            team = Groups.CreateTeam(client.playerData.getCharID(), HandleOf(client));
            Send(client, SocialPackets.TeamCreated(team.Id, team.Name));
            SetOwnGroupIds(client, null, null, team.Id);
            Output.WriteDebugLog("[SOCIAL] Created " + team.Name + " (0x" + team.Id.ToString("x8") + ")");
            return team;
        }

        private static void LeaveTeam(WorldClient client, bool tellLeaver)
        {
            UInt32 charId = client.playerData.getCharID();
            LiveTeam team = Groups.FindTeamOf(charId);
            if (team == null)
            {
                return;
            }

            List<UInt32> before = team.MemberIds();
            LiveTeam after = Groups.RemoveTeamMember(team.Id, charId);
            byte[] left = SocialPackets.LeftGroup(SocialPackets.GroupTeam, charId);
            // Rajko capture: the leaver got 72 03 with its own char id; the others are told the same.
            foreach (UInt32 memberId in before.Where(id => id != charId))
            {
                Send(FindOnlineByCharId(memberId), left);
            }

            if (tellLeaver)
            {
                Send(client, left);
                SetOwnGroupIds(client, null, null, 0);
            }
        }

        // ---- invites ------------------------------------------------------------------------------------------

        /// <summary>Client 6e: invite a player to my mission team (03), crew (02) or faction (01).</summary>
        public void ProcessGroupInvite(ref byte[] rpcData)
        {
            SocialRequests.GroupInvite invite;
            if (!SocialRequests.TryParseGroupInvite(rpcData, out invite))
            {
                Output.WriteDebugLog("[SOCIAL] Unreadable group invite " + StringUtils.bytesToString(rpcData));
                return;
            }

            Invite(Store.currentClient, invite.GroupType, invite.InviteeHandle, invite.GroupName);
        }

        /// <summary>Client 80 8c: "/invite handle" (mission team). Retail answered a bad handle with 80 8d carrying
        /// 0x0d000031 and a good one by creating the inviter's team (80 8d) and inviting.</summary>
        public void ProcessMissionTeamInvite(ref byte[] rpcData)
        {
            // 80 8c 07 00 00 00 04 08 00 "Warboy": a two-byte header, so offset 7 is rpcData[5].
            string handle;
            if (rpcData.Length < 3 || !SocialRequests.TryReadSized(rpcData, (rpcData[0] | (rpcData[1] << 8)) - 2, out handle))
            {
                return;
            }

            WorldClient inviter = Store.currentClient;
            WorldClient invitee = FindOnlineByHandle(handle);
            if (invitee == null || invitee == inviter)
            {
                Send(inviter, SocialPackets.TeamRequestFailed(SocialPackets.TeamInvalidCharacterName));
                return;
            }

            Invite(inviter, SocialPackets.GroupTeam, handle, null);
        }

        private static void Invite(WorldClient inviter, byte groupType, string inviteeHandle, string groupName)
        {
            string inviterHandle = HandleOf(inviter);
            WorldClient invitee = FindOnlineByHandle(inviteeHandle);
            Output.WriteDebugLog("[SOCIAL] " + inviterHandle + " invites " + inviteeHandle + " to group type " + groupType +
                                 (groupName != null ? " '" + groupName + "'" : "") + (invitee == null ? " (not online)" : ""));
            if (invitee == null || invitee == inviter)
            {
                // Nobody by that handle is in the world (offline or no such character).
                Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.InvalidCharacterNameCode(groupType), inviteeHandle));
                return;
            }

            inviteeHandle = HandleOf(invitee);
            PendingInvite pending = new PendingInvite
            {
                GroupType = groupType,
                InviterCharId = inviter.playerData.getCharID(),
                InviterHandle = inviterHandle,
                InviteeHandle = inviteeHandle
            };

            byte[] invitePacket;
            switch (groupType)
            {
                case SocialPackets.GroupTeam:
                {
                    LiveTeam team = EnsureTeam(inviter);
                    if (team.LeaderCharId != inviter.playerData.getCharID())
                    {
                        Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.TeamNotTeamLeader, inviteeHandle));
                        return;
                    }

                    if (team.HasMember(invitee.playerData.getCharID()))
                    {
                        Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.TeamCurrentMember, inviteeHandle));
                        return;
                    }

                    pending.GroupName = team.Name;
                    invitePacket = SocialPackets.TeamInvite(inviterHandle, team.Name);
                    break;
                }

                case SocialPackets.GroupCrew:
                {
                    if (CrewIdOf(invitee) != 0)
                    {
                        Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.CrewCurrentMember, inviteeHandle));
                        return;
                    }

                    UInt32 crewId = CrewIdOf(inviter);
                    byte org;
                    if (crewId != 0)
                    {
                        Crew crew = Store.dbManager.WorldDbHandler.GetCrewData(crewId);
                        pending.GroupName = crew.crewName;
                        org = (byte) crew.org;
                    }
                    else
                    {
                        // A player without a crew founds one by inviting someone; the client sends the new name.
                        string name = (groupName ?? "").Trim();
                        if (name.Length == 0 || name.Length > 31 || Store.dbManager.WorldDbHandler.CrewNameExists(name))
                        {
                            Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.CrewInvalidTeamName, inviteeHandle));
                            return;
                        }

                        pending.GroupName = name;
                        org = inviter.playerInstance.OrganizationID.getValue()[0];
                    }

                    invitePacket = SocialPackets.CrewInvite(inviterHandle, pending.GroupName, org);
                    break;
                }

                case SocialPackets.GroupFaction:
                {
                    UInt32 factionId = FactionIdOf(inviter);
                    Faction faction = factionId != 0 ? Store.dbManager.WorldDbHandler.FetchFaction(factionId) : null;
                    if (faction == null || !string.Equals(faction.masterPlayerHandle, inviterHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.FactionNotAuthorized, inviteeHandle));
                        return;
                    }

                    UInt32 inviteeCrew = CrewIdOf(invitee);
                    Crew crew = inviteeCrew != 0 ? Store.dbManager.WorldDbHandler.GetCrewData(inviteeCrew) : null;
                    if (crew == null || !string.Equals(crew.characterMasterName, inviteeHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        // Factions are made of crews; only a crew captain can bring a crew in.
                        Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.FactionInvalidMemberCode, inviteeHandle));
                        return;
                    }

                    if (crew.factionId != 0)
                    {
                        Send(inviter, SocialPackets.InviteAnswer(groupType, SocialPackets.FactionCurrentMember, inviteeHandle));
                        return;
                    }

                    pending.GroupName = faction.name;
                    invitePacket = SocialPackets.FactionInvite(inviterHandle, faction.name);
                    break;
                }

                default:
                    return;
            }

            if (!Groups.AddInvite(pending, DateTime.UtcNow))
            {
                Send(inviter, SocialPackets.InviteAnswer(groupType, PreviousRequestCode(groupType), inviteeHandle));
                return;
            }

            Send(invitee, invitePacket);
        }

        private static UInt32 PreviousRequestCode(byte groupType)
        {
            switch (groupType)
            {
                case SocialPackets.GroupCrew: return SocialPackets.CrewPreviousRequestInProgress;
                case SocialPackets.GroupFaction: return 0x0d00000c; // FACTIONPREVIOUSREQUESTINPROGRESS
                default: return SocialPackets.TeamPreviousRequestInProgress;
            }
        }

        /// <summary>Client 6f: the invitee's answer. Declines go back to the inviter as 6f with the invitee's code.</summary>
        public void ProcessInviteAnswer(ref byte[] rpcData)
        {
            SocialRequests.InviteAnswer answer;
            if (!SocialRequests.TryParseInviteAnswer(rpcData, out answer))
            {
                return;
            }

            WorldClient invitee = Store.currentClient;
            PendingInvite invite = Groups.TakeInvite(HandleOf(invitee), answer.GroupType, answer.InviterHandle, DateTime.UtcNow);
            if (invite == null)
            {
                Output.WriteDebugLog("[SOCIAL] Answer from " + HandleOf(invitee) + " to no pending invite from " + answer.InviterHandle);
                return;
            }

            WorldClient inviter = FindOnlineByCharId(invite.InviterCharId);
            if (!answer.Accepted)
            {
                UInt32 code = answer.Result != 0 ? answer.Result : SocialPackets.InviteDeclinedCode(answer.GroupType);
                Send(inviter, SocialPackets.InviteAnswer(answer.GroupType, code, HandleOf(invitee)));
                return;
            }

            switch (answer.GroupType)
            {
                case SocialPackets.GroupTeam:
                    JoinTeam(invitee, invite);
                    break;
                case SocialPackets.GroupCrew:
                    JoinCrew(invitee, inviter, invite);
                    break;
                case SocialPackets.GroupFaction:
                    JoinFaction(invitee, invite);
                    break;
            }
        }

        private static void JoinTeam(WorldClient invitee, PendingInvite invite)
        {
            LiveTeam team = Groups.FindTeamOf(invite.InviterCharId);
            if (team == null)
            {
                Notice(invitee, "That mission team no longer exists.");
                return;
            }

            UInt32 charId = invitee.playerData.getCharID();
            // A player is on one team: leave the old one (usually the solo team made for the mission list).
            LiveTeam old = Groups.FindTeamOf(charId);
            if (old != null && old.Id != team.Id)
            {
                LeaveTeam(invitee, false);
            }

            int place = Groups.AddTeamMember(team.Id, charId, HandleOf(invitee));
            // Two-person capture 09:40:44-45: the newcomer gets 80 8e and MissionTeamID, the others 70 03.
            Send(invitee, SocialPackets.TeamInfo(charId, team.Id, team.LeaderCharId, team.Name, team.Members.ToList()));
            SetOwnGroupIds(invitee, null, null, team.Id);
            byte[] joined = SocialPackets.JoinedGroup(SocialPackets.GroupTeam, charId, (byte) place, HandleOf(invitee));
            foreach (UInt32 memberId in team.MemberIds().Where(id => id != charId))
            {
                Send(FindOnlineByCharId(memberId), joined);
            }
        }

        private static void JoinCrew(WorldClient invitee, WorldClient inviter, PendingInvite invite)
        {
            var db = Store.dbManager.WorldDbHandler;
            UInt32 crewId = inviter != null ? CrewIdOf(inviter) : db.GetCrewIdByInviterHandle(invite.InviterHandle);
            bool founded = false;
            if (crewId == 0)
            {
                if (inviter == null || db.CrewNameExists(invite.GroupName))
                {
                    Notice(invitee, "That crew could not be created.");
                    return;
                }

                crewId = db.CreateCrew(invite.GroupName, invite.InviterHandle, inviter.playerInstance.OrganizationID.getValue()[0]);
                db.AddMemberToCrew(invite.InviterCharId, crewId, 0, 1, 0);
                founded = true;
                Output.WriteDebugLog("[SOCIAL] " + invite.InviterHandle + " founded crew '" + invite.GroupName + "' (" + crewId + ")");
            }

            Crew crew = db.GetCrewData(crewId);
            UInt32 charId = invitee.playerData.getCharID();
            db.AddMemberToCrew(charId, crewId, crew.factionId, 0, 0);

            // Two-person capture 09:41:13: the newcomer gets FactionID + CrewID on its own view, the member list
            // (80 86) and, when the crew is in a faction, the faction (7c mode 1 and 2); the others get 70 02.
            SetOwnGroupIds(invitee, crew.factionId, crewId, null);
            SendCrewInfo(invitee, crewId);
            if (crew.factionId != 0)
            {
                SendFactionInfo(invitee, crew.factionId);
            }

            if (founded)
            {
                SetOwnGroupIds(inviter, crew.factionId, crewId, null);
                SendCrewInfo(inviter, crewId);
                return;
            }

            byte[] joined = SocialPackets.JoinedGroup(SocialPackets.GroupCrew, charId, 0, HandleOf(invitee));
            SendToAll(OnlineWhere(c => CrewIdOf(c) == crewId && c != invitee), joined);
        }

        private static void JoinFaction(WorldClient captain, PendingInvite invite)
        {
            var db = Store.dbManager.WorldDbHandler;
            Faction faction = db.FetchFaction(FactionIdOfHandle(invite));
            UInt32 crewId = CrewIdOf(captain);
            if (faction == null || faction.factionId == 0 || crewId == 0)
            {
                Notice(captain, "That faction no longer exists.");
                return;
            }

            db.AddCrewToFaction(faction.factionId, crewId);
            db.SetFactionOfCrewMembers(crewId, faction.factionId);
            // ProxyM2_m2_packets 18:55:42: after "6f 01 ... 00 00 00 00" the invitee got 7c mode 1 and mode 2.
            foreach (WorldClient member in OnlineWhere(c => CrewIdOf(c) == crewId))
            {
                SetOwnGroupIds(member, faction.factionId, null, null);
            }

            foreach (WorldClient member in OnlineWhere(c => FactionIdOf(c) == faction.factionId))
            {
                SendFactionInfo(member, faction.factionId);
            }
        }

        private static UInt32 FactionIdOfHandle(PendingInvite invite)
        {
            WorldClient inviter = FindOnlineByCharId(invite.InviterCharId);
            if (inviter != null)
            {
                return FactionIdOf(inviter);
            }

            Crew crew = Store.dbManager.WorldDbHandler.GetCrewData(Store.dbManager.WorldDbHandler.GetCrewIdByInviterHandle(invite.InviterHandle));
            return crew.factionId;
        }

        internal static void SendCrewInfo(WorldClient client, UInt32 crewId)
        {
            if (crewId == 0)
            {
                Send(client, SocialPackets.NoCrewInfo(client.playerData.getCharID()));
                return;
            }

            Crew crew = Store.dbManager.WorldDbHandler.GetCrewData(crewId);
            List<CrewMember> members = Store.dbManager.WorldDbHandler.GetCrewMembersForCrewId(crewId);
            UInt32 captain = members.Where(m => m.isCaptain).Select(m => m.charId).FirstOrDefault();
            UInt32 firstMate = members.Where(m => m.isFirstMate).Select(m => m.charId).FirstOrDefault();
            foreach (CrewMember member in members)
            {
                // The DB flag goes stale after a crash; the live client list is the truth.
                member.isOnline = (ushort) (member.charId == client.playerData.getCharID() ||
                                            FindOnlineByCharId(member.charId) != null ? 1 : 0);
            }

            Send(client, ServerPackets.BuildCrewInfoPacket(client.playerData.getCharID(), crew, members, captain, firstMate));
        }

        internal static void SendFactionInfo(WorldClient client, UInt32 factionId)
        {
            Faction faction = Store.dbManager.WorldDbHandler.FetchFaction(factionId);
            faction.masterPlayerCharId = Store.dbManager.WorldDbHandler.GetCharIdByHandle(faction.masterPlayerHandle);
            Send(client, ServerPackets.BuildFactionInfoPacket(client.playerData.getCharID(), faction.factionId,
                ServerPackets.FactionAlignmentDefault, faction.name, faction.masterPlayerCharId, faction.money));
            Send(client, ServerPackets.BuildFactionCrewsPacket(client.playerData.getCharID(), faction));
        }

        // ---- leaving and disbanding ---------------------------------------------------------------------------

        /// <summary>Client 71 &lt;type&gt;: leave my faction / crew / team.</summary>
        public void ProcessLeaveGroup(ref byte[] rpcData)
        {
            if (rpcData.Length < 1)
            {
                return;
            }

            WorldClient client = Store.currentClient;
            UInt32 charId = client.playerData.getCharID();
            var db = Store.dbManager.WorldDbHandler;
            switch (rpcData[0])
            {
                case SocialPackets.GroupTeam:
                    LeaveTeam(client, true);
                    break;

                case SocialPackets.GroupCrew:
                {
                    UInt32 crewId = CrewIdOf(client);
                    if (crewId == 0)
                    {
                        return;
                    }

                    db.RemoveMemberFromCrew(charId, crewId);
                    List<CrewMember> remaining = db.GetCrewMembersForCrewId(crewId);
                    if (remaining.Count == 0)
                    {
                        db.DisbandCrew(crewId);
                    }
                    else if (!remaining.Any(m => m.isCaptain))
                    {
                        CrewMember next = remaining.FirstOrDefault(m => m.isFirstMate) ?? remaining[0];
                        db.SetCrewCaptain(crewId, next.charId, next.handle);
                    }

                    // Two-person capture 09:43:53-54: the leaver's own view got FactionID 0 and CrewID 0, the
                    // remaining members 72 02 <leaver>.
                    SetOwnGroupIds(client, 0, 0, null);
                    SendToAll(OnlineWhere(c => CrewIdOf(c) == crewId && c != client),
                        SocialPackets.LeftGroup(SocialPackets.GroupCrew, charId));
                    break;
                }

                case SocialPackets.GroupFaction:
                {
                    // A crew leaves its faction; only its captain can do that. (No retail capture of this.)
                    UInt32 factionId = FactionIdOf(client);
                    UInt32 crewId = CrewIdOf(client);
                    Crew crew = crewId != 0 ? db.GetCrewData(crewId) : null;
                    if (factionId == 0 || crew == null ||
                        !string.Equals(crew.characterMasterName, HandleOf(client), StringComparison.OrdinalIgnoreCase))
                    {
                        Send(client, SocialPackets.InviteAnswer(SocialPackets.GroupFaction, SocialPackets.FactionNotAuthorized, HandleOf(client)));
                        return;
                    }

                    db.RemoveCrewFromFaction(factionId, crewId);
                    db.SetFactionOfCrewMembers(crewId, 0);
                    foreach (WorldClient member in OnlineWhere(c => CrewIdOf(c) == crewId))
                    {
                        SetOwnGroupIds(member, 0, null, null);
                        Send(member, SocialPackets.GroupDisbanded(SocialPackets.GroupFaction));
                    }

                    foreach (WorldClient member in OnlineWhere(c => FactionIdOf(c) == factionId))
                    {
                        SendFactionInfo(member, factionId);
                    }

                    break;
                }
            }
        }

        /// <summary>Client 77 &lt;type&gt;: disband my team / crew / faction. Retail answered a leader's 77 03 by sending
        /// 77 03 and MissionTeamID 0 to every member, the leader included.</summary>
        public void ProcessDisbandGroup(ref byte[] rpcData)
        {
            if (rpcData.Length < 1)
            {
                return;
            }

            WorldClient client = Store.currentClient;
            string handle = HandleOf(client);
            var db = Store.dbManager.WorldDbHandler;
            byte type = rpcData[0];
            switch (type)
            {
                case SocialPackets.GroupTeam:
                {
                    LiveTeam team = Groups.FindTeamOf(client.playerData.getCharID());
                    if (team == null)
                    {
                        return;
                    }

                    if (team.LeaderCharId != client.playerData.getCharID())
                    {
                        Send(client, SocialPackets.InviteAnswer(type, SocialPackets.TeamNotTeamLeader, handle));
                        return;
                    }

                    Groups.DisbandTeam(team.Id);
                    foreach (UInt32 memberId in team.MemberIds())
                    {
                        WorldClient member = FindOnlineByCharId(memberId);
                        if (member != null)
                        {
                            Send(member, SocialPackets.GroupDisbanded(type));
                            SetOwnGroupIds(member, null, null, 0);
                        }
                    }

                    break;
                }

                case SocialPackets.GroupCrew:
                {
                    // Crew_Button_Disband. No retail capture: same pattern as the team, plus the faction id goes.
                    UInt32 crewId = CrewIdOf(client);
                    Crew crew = crewId != 0 ? db.GetCrewData(crewId) : null;
                    if (crew == null || !string.Equals(crew.characterMasterName, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    List<WorldClient> members = OnlineWhere(c => CrewIdOf(c) == crewId);
                    db.DisbandCrew(crewId);
                    foreach (WorldClient member in members)
                    {
                        Send(member, SocialPackets.GroupDisbanded(type));
                        SetOwnGroupIds(member, 0, 0, null);
                    }

                    break;
                }

                case SocialPackets.GroupFaction:
                {
                    UInt32 factionId = FactionIdOf(client);
                    Faction faction = factionId != 0 ? db.FetchFaction(factionId) : null;
                    if (faction == null || !string.Equals(faction.masterPlayerHandle, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    List<WorldClient> members = OnlineWhere(c => FactionIdOf(c) == factionId);
                    db.DisbandFaction(factionId);
                    foreach (WorldClient member in members)
                    {
                        Send(member, SocialPackets.GroupDisbanded(type));
                        SetOwnGroupIds(member, 0, null, null);
                    }

                    break;
                }
            }
        }

        /// <summary>Client 80 90 &lt;mode&gt;: the leader changed the loot mode; retail echoed it to every member.</summary>
        public void ProcessLootMode(ref byte[] rpcData)
        {
            if (rpcData.Length < 1)
            {
                return;
            }

            LiveTeam team = Groups.FindTeamOf(Store.currentClient.playerData.getCharID());
            if (team == null || team.LeaderCharId != Store.currentClient.playerData.getCharID())
            {
                return;
            }

            team.LootMode = rpcData[0];
            foreach (UInt32 memberId in team.MemberIds())
            {
                Send(FindOnlineByCharId(memberId), SocialPackets.LootMode(rpcData[0]));
            }
        }

        /// <summary>A player left the world: take them off their team.</summary>
        public static void PlayerLeftWorld(WorldClient client)
        {
            try
            {
                LeaveTeam(client, false);
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[SOCIAL] Team cleanup failed: " + e.Message);
            }

            try
            {
                ChatRoomHandler.PlayerLeftWorld(client);
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[SOCIAL] Chat room cleanup failed: " + e.Message);
            }
        }

        // ---- chat ---------------------------------------------------------------------------------------------

        /// <summary>Crew (12), faction (13) and team (15) chat go to the other online members. Retail did not echo a
        /// group line back to its sender (the client prints its own line).</summary>
        public static bool TrySendGroupChat(WorldClient sender, byte channel, string text)
        {
            UInt32 charId = sender.playerData.getCharID();
            List<WorldClient> recipients;
            switch (channel)
            {
                case SocialPackets.ChatCrew:
                {
                    UInt32 crewId = CrewIdOf(sender);
                    if (crewId == 0)
                    {
                        Notice(sender, "You are not in a crew.");
                        return true;
                    }

                    recipients = OnlineWhere(c => CrewIdOf(c) == crewId && c != sender);
                    break;
                }

                case SocialPackets.ChatFaction:
                {
                    UInt32 factionId = FactionIdOf(sender);
                    if (factionId == 0)
                    {
                        Notice(sender, "You are not in a faction.");
                        return true;
                    }

                    recipients = OnlineWhere(c => FactionIdOf(c) == factionId && c != sender);
                    break;
                }

                case SocialPackets.ChatTeam:
                {
                    LiveTeam team = Groups.FindTeamOf(charId);
                    if (team == null)
                    {
                        Notice(sender, "You are not on a mission team.");
                        return true;
                    }

                    List<UInt32> ids = team.MemberIds();
                    recipients = OnlineWhere(c => c != sender && ids.Contains(c.playerData.getCharID()));
                    break;
                }

                default:
                    return false;
            }

            SendToAll(recipients, SocialPackets.ChatMessage(channel, charId, HandleOf(sender), text));
            Output.WriteDebugLog("[SOCIAL] Chat channel 0x" + channel.ToString("x2") + " from " + HandleOf(sender) +
                                 " to " + recipients.Count + " member(s)");
            return true;
        }
    }
}
