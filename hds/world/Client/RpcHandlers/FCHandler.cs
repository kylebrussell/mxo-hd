using System;
using System.Data.Common;
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

            uint crewId = GroupId(client, SocialPackets.GroupCrew);
            factionName = factionName.Trim();
            (uint FactionId, uint Refusal) result;
            try
            {
                result = Store.dbManager.WorldDbHandler.FoundFaction(client.playerData.getCharID(), crewId, factionName);
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[SOCIAL] Faction founding rolled back for {handle} crew={crewId}: {error.Message}");
                RefuseFounding(client, factionName, "The faction could not be created. Your crew was kept.");
                return;
            }
            if (result.Refusal != SocialPackets.ResultOk)
            {
                RefuseFounding(client, factionName, result.Refusal == SocialPackets.FactionInvalidTeamName
                    ? "That faction name is invalid or already in use."
                    : "Only the captain of an unaffiliated crew can found a faction.");
                return;
            }

            uint factionId = result.FactionId;
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

        private static void RefuseFounding(WorldClient client, string name, string text)
        {
            SocialHandler.Send(client, ServerPackets.BuildFactionCreateError(name.Length <= 41 ? name : string.Empty));
            SocialHandler.Send(client, ServerPackets.BuildSystemChatMessage(text, "BROADCAST"));
        }

        /// <summary>
        /// Client 78 &lt;type 1=faction 2=crew&gt; &lt;amount u32&gt; &lt;1=deposit 0=withdraw&gt;, answered with 79 to every
        /// online member (two-person capture 09:43:35: "78 02 64 00 00 00 01" then "79 02 64 00 00 00 &lt;char&gt; 01").
        /// </summary>
        public void ProcessDepositMoney(ref byte[] rpcData)
        {
            if (rpcData == null || rpcData.Length != 6 || rpcData[5] > 1) return;
            byte type = rpcData[0];
            if (type != SocialPackets.GroupFaction && type != SocialPackets.GroupCrew) return;
            uint amount = BitConverter.ToUInt32(rpcData, 1);
            bool deposit = rpcData[5] == 1;
            WorldClient client = Store.currentClient;

            lock (client.playerData.craftingSync)
            {
                var publication = new AbilityAcquisition.Publication(client);
                uint group = GroupId(client, type);
                (uint Cash, string Refusal) result;
                try
                {
                    result = Store.dbManager.WorldDbHandler.TransferGroupMoney(publication.Character, type, group, amount, deposit);
                }
                catch (DbException error)
                {
                    Output.WriteDebugLog($"[GROUP BANK] Transfer rolled back char={publication.Character} type={type} " +
                        $"group={group} amount={amount} deposit={deposit}: {error.Message}");
                    result = (0, "The transfer could not be saved. Your information was kept.");
                }
                if (result.Refusal != null)
                {
                    if (publication.Publish(client, ServerPackets.BuildSystemChatMessage(result.Refusal, "BROADCAST")))
                        client.FlushQueue();
                    return;
                }

                byte[] ledger = ServerPackets.BuildGroupBankUpdate(publication.Character, type, amount, deposit);
                byte[] wallet = ServerPackets.BuildInfoCurrent(result.Cash, ServerPackets.InfoReasonLoad);
                bool published = GroupId(client, type) == group ? publication.PublishPurchase(client, result.Cash, ledger, wallet)
                    : publication.PublishPurchase(client, result.Cash, wallet);
                if (published) client.FlushQueue();
                foreach (WorldClient member in SocialHandler.OnlineWhere(other => !ReferenceEquals(other, client) && GroupId(other, type) == group))
                    SocialHandler.Send(member, ledger);
                Output.WriteDebugLog($"[GROUP BANK] Transfer committed char={publication.Character} type={type} " +
                    $"group={group} amount={amount} deposit={deposit} cash={result.Cash}");
            }
        }

        private static uint GroupId(WorldClient client, byte type) => NumericalUtils.ByteArrayToUint32(
            (type == SocialPackets.GroupFaction ? client.playerInstance.FactionID : client.playerInstance.CrewID).getValue(), 1);

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
