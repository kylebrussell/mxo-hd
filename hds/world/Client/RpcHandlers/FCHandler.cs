using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    class FCHandler
    {
        public void ProcessLoadFactionName(ref byte[] packet)
        {

            PacketReader packetReader = new PacketReader(packet);
            UInt32 factionId = packetReader.ReadUInt32(1);

            string factionName = Store.dbManager.WorldDbHandler.GetFactionNameById(factionId);
            
            ServerPackets pak = new ServerPackets();
            pak.SendFactionName(Store.currentClient, factionId, factionName);
        }


        /// <summary>
        /// Client 7a: found a faction and invite another crew captain to it:
        /// 7a &lt;invitee offset u16&gt; &lt;faction name offset u16&gt; &lt;sized faction name&gt; &lt;sized handle&gt;.
        /// No retail capture of this RPC; the faction is created with the founder's crew in it, and the other
        /// captain gets the retail faction invite (6e 01), so the faction exists even if they decline.
        /// </summary>
        public void ProcessInvitePlayerToNewFaction(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            string handle = SocialHandler.HandleOf(client);
            if (packet.Length < 4)
            {
                return;
            }

            string factionName;
            string inviteeHandle;
            if (!SocialRequests.TryReadSized(packet, (packet[2] | (packet[3] << 8)) - 1, out factionName) ||
                !SocialRequests.TryReadSized(packet, (packet[0] | (packet[1] << 8)) - 1, out inviteeHandle))
            {
                return;
            }

            var db = Store.dbManager.WorldDbHandler;
            UInt32 crewId = NumericalUtils.ByteArrayToUint32(client.playerInstance.CrewID.getValue(), 1);
            Crew crew = crewId != 0 ? db.GetCrewData(crewId) : null;
            factionName = factionName.Trim();
            if (crew == null || crew.factionId != 0 ||
                !string.Equals(crew.characterMasterName, handle, StringComparison.OrdinalIgnoreCase))
            {
                SocialHandler.Send(client, SocialPackets.InviteAnswer(SocialPackets.GroupFaction, SocialPackets.FactionInvalidFounder, inviteeHandle));
                return;
            }

            if (factionName.Length == 0 || factionName.Length > 41 || !db.IsFactionnameAvailable(factionName))
            {
                SocialHandler.Send(client, SocialPackets.InviteAnswer(SocialPackets.GroupFaction, SocialPackets.FactionInvalidTeamName, inviteeHandle));
                return;
            }

            UInt32 factionId = db.CreateFaction(factionName, handle);
            db.AddCrewToFaction(factionId, crewId);
            db.SetFactionOfCrewMembers(crewId, factionId);
            Output.WriteDebugLog("[SOCIAL] " + handle + " founded faction '" + factionName + "' (" + factionId + ")");
            foreach (WorldClient member in SocialHandler.OnlineWhere(c =>
                         NumericalUtils.ByteArrayToUint32(c.playerInstance.CrewID.getValue(), 1) == crewId))
            {
                SocialHandler.SetOwnGroupIds(member, factionId, null, null);
                SocialHandler.SendFactionInfo(member, factionId);
            }

            if (inviteeHandle.Trim().Length > 0)
            {
                byte[] invite = SocialRequests.BuildGroupInvite(SocialPackets.GroupFaction, inviteeHandle, factionName);
                new SocialHandler().ProcessGroupInvite(ref invite);
            }
        }

        /// <summary>
        /// Client 78 &lt;type 1=faction 2=crew&gt; &lt;amount u32&gt; &lt;1=deposit 0=withdraw&gt;, answered with 79 to every
        /// online member (two-person capture 09:43:35: "78 02 64 00 00 00 01" then "79 02 64 00 00 00 &lt;char&gt; 01").
        /// </summary>
        private static readonly object BankLock = new object();

        public void ProcessDepositMoney(ref byte[] rpcData)
        {
            if (rpcData.Length < 6)
            {
                return;
            }

            byte type = rpcData[0];
            UInt32 amount = BitConverter.ToUInt32(rpcData, 1);
            bool deposit = rpcData[5] == 1;
            WorldClient client = Store.currentClient;
            var db = Store.dbManager.WorldDbHandler;

            UInt32 groupId = NumericalUtils.ByteArrayToUint32(
                (type == SocialPackets.GroupFaction ? client.playerInstance.FactionID : client.playerInstance.CrewID).getValue(), 1);
            if (groupId == 0 || (type != SocialPackets.GroupFaction && type != SocialPackets.GroupCrew))
            {
                return;
            }

            // Check and move under one lock: two members withdrawing at once both passed the balance check.
            long newInfo;
            lock (BankLock)
            {
                long bank = type == SocialPackets.GroupFaction ? db.FetchFaction(groupId).money : db.GetCrewData(groupId).money;
                long info = client.playerData.getInfo();
                if (!CanMoveMoney(deposit, info, bank, amount))
                {
                    // The client has LTTRGS_ codes for this (e.g. CREWBANKINSUFFICIENTFUNDS) but no capture shows how they
                    // were delivered, so say it in plain text; nothing is moved.
                    new ServerPackets().sendSystemChatMessage(client,
                        deposit ? "You do not have that much information." : "The bank does not hold that much information.", "SYSTEM");
                    return;
                }

                newInfo = deposit ? info - amount : info + amount;
                if (type == SocialPackets.GroupFaction)
                {
                    if (deposit) db.IncreaseFactionMoney(groupId, amount); else db.DecreaseFactionMoney(groupId, amount);
                }
                else
                {
                    if (deposit) db.IncreaseCrewMoney(groupId, amount); else db.DecreaseCrewMoney(groupId, amount);
                }
            }

            db.SaveInfo(client, newInfo);
            client.playerData.setInfo(newInfo);

            ServerPackets packet = new ServerPackets();
            packet.SendMoneyUpdateFactionCrew(client, type, amount, (ushort) (deposit ? 1 : 0));
            packet.SendInfoCurrent(client, (UInt32) newInfo);
        }

        /// <summary>A deposit needs that much information on the player, a withdrawal that much in the bank.</summary>
        internal static bool CanMoveMoney(bool deposit, long playerInfo, long bank, UInt32 amount)
        {
            if (amount == 0)
            {
                return false;
            }

            return deposit ? playerInfo >= amount : bank >= amount;
        }

        
    }
}
