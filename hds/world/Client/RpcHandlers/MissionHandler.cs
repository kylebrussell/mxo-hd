using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hds.shared;

namespace hds
{
    public class MissionHandler
    {
        /// <summary>
        /// Client RPC 0x809b: <c>&lt;txn u16&gt; 01</c>. Retail sent no mission id here; the mission is the one
        /// whose info the client asked for last. (The second byte, always 01, used to be read as the id.)
        /// </summary>
        public void ProcessMissionaccept(ref byte[] packet)
        {
            #if DEBUG
            Output.WriteRpcLog("Mission Accept Data:" + StringUtils.bytesToString(packet));
            #endif
            PacketReader reader = new PacketReader(packet);
            UInt16 txn = reader.ReadUInt16(1);

            ClientData data = Store.currentClient.playerData;
            List<Mission> list = data.currentMissionList;
            if (list == null || data.selectedMissionIndex < 0 || data.selectedMissionIndex >= list.Count)
            {
                Output.WriteDebugLog("[MISSION] Accept without a selected mission ignored");
                return;
            }

            Mission mission = list[data.selectedMissionIndex];
            new TeamHandler().checkAndCreateMissionTeam(Store.currentClient);

            data.missionInstanceCounter++;
            data.activeMission = mission;
            ServerPackets pak = new ServerPackets();
            pak.sendMissionAccept(Store.currentClient, txn, data.missionInstanceCounter, mission);

            // Retail sends every objective at once, numbered from 0, state 0 (open).
            List<MissionObjective> objectives = mission.phases.Count > 0
                ? mission.phases[0].missionObjectives
                : new List<MissionObjective>();
            for (int i = 0; i < objectives.Count; i++)
            {
                pak.SendSetMissionObjective((ushort)i, 0, objectives[i].description, Store.currentClient);
            }
            Store.currentClient.FlushQueue();
            Output.WriteDebugLog("[MISSION] Accepted '" + mission.title + "' with " + objectives.Count + " objectives");
        }

        public void processMissionList(ref byte[] packet)
        {
            
            new TeamHandler().checkAndCreateMissionTeam(Store.currentClient);
            PacketReader reader = new PacketReader(packet);

            // Retail: the first u16 is a per-session request number the server copies back into every
            // reply (5, 6, 7, ...), not a contact id.
            UInt16 txn = reader.ReadUInt16(1);
            uint orgID = reader.ReadUint8();

            ServerPackets pak = new ServerPackets();
            Store.currentClient.playerData.currentMissionList = DataLoader.getInstance().FindMissions(txn, orgID);
            Store.currentClient.playerData.selectedMissionIndex = -1;
            pak.sendMissionList(txn, orgID, Store.currentClient.playerData.currentMissionList, Store.currentClient);
            Output.WriteDebugLog("[MISSION] Listed " + Store.currentClient.playerData.currentMissionList.Count + " missions");
        }

        public void processInvitePlayerToMissionTeam(ref byte[] packet)
        {
            // Read the Data
            PacketReader reader = new PacketReader(packet);
            UInt32 unknownUint = reader.ReadUInt32(1); // Maybe its just an offset of uint8 - we dont care and know lol
            reader.IncrementOffsetByValue(1);
            String handleToInvite = reader.ReadSizedZeroTerminatedString();
            // ToDo: implement the right response for the player who get the invite
        }

        public void processLoadMissionInfo(ref byte[] packet)
        {
            // Client RPC 0x8098: <txn u16> <mission index u16> 01 <difficulty u8> 00
            PacketReader reader = new PacketReader(packet);
            UInt16 txn = reader.ReadUInt16(1);
            UInt16 missionIndexFromList = reader.ReadUInt16(1);
            uint missionTime = reader.ReadUint8();
            byte difficulty = (byte)reader.ReadUint8();

            List<Mission> list = Store.currentClient.playerData.currentMissionList;
            if (list == null || missionIndexFromList >= list.Count)
            {
                // This used to index the list unchecked and throw.
                Output.WriteDebugLog("[MISSION] Info for unknown mission index " + missionIndexFromList);
                return;
            }

            Store.currentClient.playerData.selectedMissionIndex = missionIndexFromList;
            ServerPackets pak = new ServerPackets();
            pak.sendMissionInfo(list[missionIndexFromList], txn, difficulty, Store.currentClient);
        }

        public void processAbortMission(ref byte[] packet)
        {
            Store.currentClient.playerData.activeMission = null;
            ServerPackets pak = new ServerPackets();
            pak.SendMissionAbort(Store.currentClient);
        }
    }
}
