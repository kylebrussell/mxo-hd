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

        public void ProcessDepositMoney(ref byte[] rpcData)
        {
            PacketReader reader = new PacketReader(rpcData);
            uint type = reader.ReadUint8();
            UInt32 moneyToDepositOrTake = reader.ReadUInt32(1);
            uint IsGivingMoney = reader.ReadUint8();

            ServerPackets packet = new ServerPackets();
            switch (type)
            {
                case 1:
                    // This is too faction
                    if (IsGivingMoney==1)
                    {
                        UInt32 newMoneyAmount = (UInt32) Store.currentClient.playerData.getInfo() - moneyToDepositOrTake;
                        // Update Info
                        Store.dbManager.WorldDbHandler.IncreaseFactionMoney(NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.FactionID.getValue(),1), moneyToDepositOrTake);
                        Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient,newMoneyAmount);
                        Store.currentClient.playerData.setInfo(newMoneyAmount);
                    }
                    else
                    {
                        UInt32 newMoneyAmount = (UInt32) Store.currentClient.playerData.getInfo() + moneyToDepositOrTake;
                        // Update Info
                        Store.dbManager.WorldDbHandler.DecreaseFactionMoney(NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.FactionID.getValue(),1), moneyToDepositOrTake);
                        Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient,newMoneyAmount);
                        Store.currentClient.playerData.setInfo(newMoneyAmount);
                    }

                    //ProcessFactionInfoUpdate();
                    break;
                case 2:
                    // This is to crew
                    if (IsGivingMoney==1)
                    {
                        UInt32 newMoneyAmount = (UInt32) Store.currentClient.playerData.getInfo() - moneyToDepositOrTake;
                        // Update Info
                        Store.dbManager.WorldDbHandler.IncreaseCrewMoney(NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.CrewID.getValue(),1), moneyToDepositOrTake);
                        Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient,newMoneyAmount);
                        Store.currentClient.playerData.setInfo(newMoneyAmount);
                    }
                    else
                    {
                        UInt32 newMoneyAmount = (UInt32) Store.currentClient.playerData.getInfo() + moneyToDepositOrTake;
                        // Update Info
                        Store.dbManager.WorldDbHandler.DecreaseCrewMoney(NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.CrewID.getValue(),1), moneyToDepositOrTake);
                        Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient,newMoneyAmount);
                        Store.currentClient.playerData.setInfo(newMoneyAmount);
                    }
                    //ProcessCrewInfoUpdate();
                    break;
            }

            packet.SendMoneyUpdateFactionCrew(Store.currentClient, (ushort) type, moneyToDepositOrTake, (ushort) IsGivingMoney);
            packet.SendInfoCurrent(Store.currentClient, (UInt32)Store.currentClient.playerData.getInfo());
            
        }

        
    }
}
