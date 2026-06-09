using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    public partial class ServerPackets
    {
        public void SendFactionName(WorldClient client, UInt32 factionID, string factionName)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_FACTION_NAME_RESPONSE, 0);
            pak.AddUint32(factionID, 1);
            // Add 42 Bytes long faction name 
            pak.AddStringWithFixedSized(factionName, 42);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }
        
        public void sendCrewAndFactionEnableWindow(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_CREW_MEMBERS_LIST,0);
            pak.AddHexBytes("15A0070000000000000000000000000000000000000000210000000000230000000000");
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

        }

        public void SendCrewInfo(WorldClient client, Crew crew, List<CrewMember> members)
        {
            byte[] packet;

            if (crew.crewId == 0)
            {
                PacketContent emptyPak = new PacketContent();
                emptyPak.AddUint16((UInt16) RPCResponseHeaders.SERVER_CREW_MEMBERS_LIST, 0);
                emptyPak.AddHexBytes("15A0070000000000000000000000000000000000000000210000000000230000000000");
                packet = emptyPak.ReturnFinalPacket();

                // Finalize the packet - we have now crew data
                /*
                pak.addUintShort(0);
                pak.addUint32(0,1);
                pak.addHexBytes("0000000000000000000000210000000000230000000000");
                */
            }
            else
            {
                UInt32 charIdCaptain = 0;
                UInt32 charIdFM = 0;
                foreach (CrewMember member in members)
                {
                    if (member.isCaptain)
                    {
                        charIdCaptain = member.charId;
                    }

                    if (member.isFirstMate)
                    {
                        charIdFM = member.charId;
                    }
                }

                packet = BuildCrewInfoPacket(client.playerData.getCharID(), crew, members, charIdCaptain, charIdFM);
            }

            client.messageQueue.addRpcMessage(packet);
        }

        // Pure layout builder for the 80 86 / SERVER_CREW_MEMBERS_LIST packet (populated crew variant).
        // Extracted from SendCrewInfo so the decoded wire layout can be golden-tested without a live Store/WorldClient.
        // Decoded field order (see docs/PACKET-RESEARCH.md, 80 86 decode):
        //   opcode 80 86, char id (u32), crew/org id (u32), org reputation byte, crew-name offset (u16),
        //   captain char id (u32), first-mate char id (u32), money (u32), member-list offset (u16),
        //   constant 14 02 00 00, full size (u16), then member-data block.
        internal static byte[] BuildCrewInfoPacket(UInt32 charId, Crew crew, List<CrewMember> members,
            UInt32 charIdCaptain, UInt32 charIdFM)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_CREW_MEMBERS_LIST, 0);

            pak.AddUint32(charId, 1);
            pak.AddUint32(crew.crewId, 1);
            pak.AddUShort(crew.org);
            pak.AddUint16(33, 1); // CrewName Offset

            pak.AddUint32(charIdCaptain, 1);
            pak.AddUint32(charIdFM, 1);
            pak.AddUint32(crew.money, 1);

            UInt16 offsetMemberList =
                (ushort) (33 + crew.crewName.Length +
                          3); // baseoffset + full crewname size (inkl. size byte and 0 termination)
            pak.AddUint16(offsetMemberList, 1);
            // 14 02 00 00 : decode-confirmed stable constant in every sampled 80 86 log
            // (medanon, sonyblack, afterwhoruneo). Follows the member-list offset, precedes the full-size u16.
            pak.AddHexBytes("14020000");
            // pak.addUint16(calculatedFullSize,1);


            PacketContent memberData = new PacketContent();
            memberData.AddSizedTerminatedString(crew.crewName);
            memberData.AddUint16((ushort) members.Count,1);
            foreach (CrewMember member in members)
            {
                memberData.AddByte(0x00);
                memberData.AddUint32(member.charId,1);
                memberData.AddStringWithFixedSized(member.handle,31);
                memberData.AddUShort(member.isOnline);
            }

            memberData.AddByteArray(new byte[]{0x00, 0x00, 0x00});

            UInt16 finalFullSize = (UInt16) (pak.ReturnFinalPacket().Length + memberData.ReturnFinalPacket().Length);
            pak.AddUint16(finalFullSize, 1);
            pak.AddByteArray(memberData.ReturnFinalPacket());

            return pak.ReturnFinalPacket();
        }

        // Alignment of the faction-info mode-1 payload. 1=Zion, 3=Mero (decode confirms sampled 7c logs
        // carry only 1 and 3; 2=Machine is presumed but unobserved). The Faction data model carries no
        // alignment/side field yet, so we emit the Zion default; wire this to faction data once the model
        // gains an alignment property. See docs/PACKET-RESEARCH.md (7c SERVER_FACTION_PLAYER_INFO mode 1).
        internal const ushort FactionAlignmentDefault = 1;

        public void SendFactionInfo(WorldClient client, Faction faction, bool sendToAllMembers)
        {
            byte[] packet = BuildFactionInfoPacket(
                Store.currentClient.playerData.getCharID(),
                faction.factionId,
                FactionAlignmentDefault,
                faction.name,
                faction.masterPlayerCharId,
                faction.money);

            if (sendToAllMembers)
            {
                Store.world.SendRPCToFactionMembers(faction.factionId, client, packet, true);
            }
            else
            {
                client.messageQueue.addRpcMessage(packet);
            }

        }

        // Pure layout builder for the 7c / SERVER_FACTION_PLAYER_INFO packet, mode 1 (faction summary).
        // Extracted from SendFactionInfo so the decoded wire layout can be golden-tested without a live Store.
        // Decoded field order (see docs/PACKET-RESEARCH.md, 7c mode-1 decode):
        //   opcode 7c, char id (u32), faction id (u32), mode-1 prefix+flag, data-length (u16),
        //   alignment (1 byte), faction name (fixed 42), master char id (u32), money (u32).
        internal static byte[] BuildFactionInfoPacket(UInt32 charId, UInt32 factionId, ushort alignment,
            string factionName, UInt32 masterPlayerCharId, UInt32 money)
        {
            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort) RPCResponseHeaders.SERVER_FACTION_PLAYER_INFO);
            pak.AddUint32(charId, 1);
            pak.AddUint32(factionId, 1);
            // 01 00 00 01 0F 00 : decoded mode-1 prefix (01 00 00 01) + count/flag (0F 00) from the
            // 7c SERVER_FACTION_PLAYER_INFO decode. Not unknown - this is the fixed mode-1 header.
            pak.AddHexBytes("010000010F00");
            // Decoded data-length field for the mode-1 payload that follows. Constant 52 here because the
            // trailing name is a fixed-size 42-byte field (no length prefix), so the payload length is fixed.
            pak.AddUint16(52, 1);
            // Alignment: 1=Zion, 3=Mero (see FactionAlignmentDefault). AddUShort emits a single byte.
            pak.AddUShort(alignment);
            pak.AddStringWithFixedSized(factionName,
                42); // 32 is more realistic - but after that there is much "dummy" data which differs

            pak.AddUint32(masterPlayerCharId, 1);
            pak.AddUint32(money, 1);
            return pak.ReturnFinalPacket();
        }

        public void SendFactionCreationError(WorldClient client)
        {
            // We don't finally know the full packet format for the error but the header so we send something
            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort) RPCResponseHeaders.SERVER_FACTION_CREATE_ERROR);
            pak.AddUint32(0,1);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        public void SendFactionCrews(WorldClient client, Faction faction, bool sendToAllMembers)
        {
            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort) RPCResponseHeaders.SERVER_FACTION_PLAYER_INFO);
            pak.AddUint32(Store.currentClient.playerData.getCharID(), 1);
            pak.AddUint32(faction.factionId, 1);
            pak.AddHexBytes("020001020F00"); // Currently unknown but the 02 should tell "hey this are the crew data"
                                    //020000020F00  
            // Add the size for the next Data until end (we know that 81 bytes for each crewData so we you just multiply it)
            pak.AddUint16((ushort) (faction.crews.Count * 81), 1);
            foreach (Crew theCrew in faction.crews)
            {
                pak.AddUint32(theCrew.crewId, 1);
                pak.AddStringWithFixedSized(theCrew.crewName, 38);
                //pak.addHexBytes("0000000000E6DC");
                pak.AddUint32(theCrew.masterPlayerCharId, 1);
                pak.AddStringWithFixedSized(theCrew.characterMasterName, 31);
                pak.AddUShort(theCrew.masterIsOnline); // ToDo: Should be an online flag (if leader is online ? )
                pak.AddUShort(theCrew.factionRank); // This is rank
                
            }

            if (sendToAllMembers)
            {
                Store.world.SendRPCToFactionMembers(faction.factionId, client, pak.ReturnFinalPacket(), true);
            }
            else
            {
                client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());    
            }
        }

        public void SendMoneyUpdateFactionCrew(WorldClient client, ushort type, UInt32 moneyAmount, ushort IsMoneyGiving)
        {
            // We found only ONE Example of this reponse in the 020_mxoemu_2_persons_actions_with_afterwhoruneo
            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort) RPCResponseHeaders.SERVER_FACTION_UPDATE_MONEY);
            pak.AddUShort(type);
            pak.AddUint32(moneyAmount,1);
            pak.AddUint32(Store.currentClient.playerData.getCharID(),1);
            pak.AddUShort(IsMoneyGiving);

            switch (type)
            {
                case 1:
                    Store.world.SendRPCToFactionMembers(NumericalUtils.ByteArrayToUint32(client.playerInstance.FactionID.getValue(),1), client, pak.ReturnFinalPacket(), true);
                    break;
                case 2:
                    Store.world.SendRPCToCrewMembers(NumericalUtils.ByteArrayToUint32(client.playerInstance.CrewID.getValue(),1), client, pak.ReturnFinalPacket(), true);
                    break;
            }
        }

        public void SendCrewInviteToPlayer(string playerHandle, string crewName)
        {
            // ToDo: fix the name display issue ?
            string charname =
                StringUtils.charBytesToString_NZ(Store.currentClient.playerInstance.CharacterName.getValue());
            UInt16 crewOffset = (UInt16) (charname.Length + 7 + 3);
            PacketContent pak = new PacketContent();
            pak.AddUint16((ushort) RPCResponseHeaders.SERVER_CREW_INVITE,0);
            pak.AddUint16(7, 1); // Start Offset for Charactername
            pak.AddUint16(crewOffset, 1);
            pak.AddByte(0x01); // ToDo: Grab Org from Crew and place it here (this tells which reputation you get then)  
            pak.AddSizedTerminatedString(
                StringUtils.charBytesToString_NZ(Store.currentClient.playerInstance.CharacterName.getValue()));
            pak.AddSizedTerminatedString(crewName);
            Store.world.SendRPCToOnePlayerByHandle(pak.ReturnFinalPacket(), playerHandle);
        }

        public void SendJoinedGroup(uint type, UInt32 charOrGroupId, UInt32 groupId, string joinerName)
        {
            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort) RPCResponseHeaders.SERVER_JOIN_GROUP);
            pak.AddUint32(charOrGroupId,1);
            pak.AddUint16(9,1); // Todo: Research if other group types could have more data so that this offset needs to be recalculated
            pak.AddUShort(0);
            pak.AddSizedTerminatedString(joinerName);
            
            PacketContent myselfStateData = new PacketContent();
            myselfStateData.AddUint16(1,1);
            PacketContent viewResetStateData = new PacketContent();
            
            switch (type)
            {
                case 1:
                    // Faction (crew joined the faction)

                    Store.world.SendRPCToFactionMembers(groupId, Store.currentClient, pak.ReturnFinalPacket(), true);
                    break;
                
                case 2:
                    // Crew (player joined crew)
                    
                    List<Attribute> updateFCAttributes = new List<Attribute>();
                    updateFCAttributes.Add(Store.currentClient.playerInstance.FactionID);
                    updateFCAttributes.Add(Store.currentClient.playerInstance.CrewID);
                    
                    viewResetStateData.AddByteArray(Store.currentClient.playerInstance.GetUpdateAttributes(updateFCAttributes));
                    myselfStateData.AddByteArray(Store.currentClient.playerInstance.GetSelfUpdateAttributes(updateFCAttributes, true));
                    
                    Store.world.SendRPCToCrewMembers(groupId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    
                    break;
                    
                case 3:
                    // Mission (player joines mission team)
                    Store.currentClient.playerInstance.MissionTeamID.setValue(groupId);
                    
                    List<Attribute> updateMissionAttributes = new List<Attribute>();
                    updateMissionAttributes.Add(Store.currentClient.playerInstance.MissionTeamID);
                    
                    viewResetStateData.AddByteArray(Store.currentClient.playerInstance.GetUpdateAttributes(updateMissionAttributes));
                    myselfStateData.AddByteArray(Store.currentClient.playerInstance.GetSelfUpdateAttributes(updateMissionAttributes, true));
                    
                    Store.world.SendRPCToMissionTeamMembers(groupId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;
            }

            if (viewResetStateData.ReturnFinalPacket().Length > 0)
            {
                // Send the ViewStateData
                Store.world.SendViewPacketToAllPlayers(viewResetStateData.ReturnFinalPacket(), Store.currentClient.playerData.getCharID(), NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.GetGoid(), 1), Store.currentClient.playerData.getEntityId());
            
                // Send StateData to myself
                Store.currentClient.messageQueue.addObjectMessage(myselfStateData.ReturnFinalPacket(),false);    
            }
            
        }

        public void SendLeaveGroup(uint type, UInt32 charId, UInt32 groupId)
        {
            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort) RPCResponseHeaders.SERVER_LEAVE_GROUP);
            pak.AddUShort((ushort) type);
            pak.AddUint32(charId,1);

            PacketContent myselfStateData = new PacketContent();
            myselfStateData.AddUint16(1,1);
            PacketContent viewResetStateData = new PacketContent();
            
            switch (type)
            {
                case 1:
                    // This removes the faction flag (but as it is from the crew packet it may set faction and crew to zero)
                    // This is just a simple ViewStateUpdate on GRoup 5 (4 times 80 skipped) and set CrewId to 0
                    Store.currentClient.playerInstance.FactionID.setValue(0);
                    
                    List<Attribute> updateAttributes = new List<Attribute>();
                    updateAttributes.Add(Store.currentClient.playerInstance.FactionID);

                    viewResetStateData.AddByteArray(Store.currentClient.playerInstance.GetUpdateAttributes(updateAttributes));
                    myselfStateData.AddByteArray(Store.currentClient.playerInstance.GetSelfUpdateAttributes(updateAttributes, true));
                    Store.world.SendRPCToFactionMembers(groupId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;
                
                case 2:
                    // This removes the faction flag (but as it is from the crew packet it may set faction and crew to zero)
                    // This is just a simple ViewStateUpdate on GRoup 5 (4 times 80 skipped) and set CrewId to 0
                    Store.currentClient.playerInstance.FactionID.setValue(0);
                    Store.currentClient.playerInstance.CrewID.setValue(0);
                    
                    List<Attribute> updateFCAttributes = new List<Attribute>();
                    updateFCAttributes.Add(Store.currentClient.playerInstance.FactionID);
                    updateFCAttributes.Add(Store.currentClient.playerInstance.CrewID);
                    
                    viewResetStateData.AddByteArray(Store.currentClient.playerInstance.GetUpdateAttributes(updateFCAttributes));
                    myselfStateData.AddByteArray(Store.currentClient.playerInstance.GetSelfUpdateAttributes(updateFCAttributes, true));
                    
                    Store.world.SendRPCToCrewMembers(groupId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;
                    
                case 3:
                    // ToDo: we MAYBE not send it to ourself (needs testing)
                    Store.currentClient.playerInstance.MissionTeamID.setValue(0);
                    
                    List<Attribute> updateMissionAttributes = new List<Attribute>();
                    updateMissionAttributes.Add(Store.currentClient.playerInstance.MissionTeamID);
                    
                    viewResetStateData.AddByteArray(Store.currentClient.playerInstance.GetUpdateAttributes(updateMissionAttributes));
                    myselfStateData.AddByteArray(Store.currentClient.playerInstance.GetSelfUpdateAttributes(updateMissionAttributes, true));
                    
                    Store.world.SendRPCToMissionTeamMembers(groupId, Store.currentClient, pak.ReturnFinalPacket(), false);
                    break;
            }

            Store.world.SendViewPacketToAllPlayers(viewResetStateData.ReturnFinalPacket(), Store.currentClient.playerData.getCharID(), NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.GetGoid(), 1), Store.currentClient.playerData.getEntityId());

            Store.currentClient.messageQueue.addObjectMessage(myselfStateData.ReturnFinalPacket(),false);
            
        }
    }
}