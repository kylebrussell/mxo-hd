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
            Accept(Store.currentClient, mission, txn);
        }

        internal static bool Accept(WorldClient client, Mission mission, UInt16 txn)
        {
            ClientData data = client.playerData;
            lock (data.missionSync)
            {
                if (!MissionTickets.CanStart(client, mission))
                {
                    return false;
                }
                MissionProgress openFloorProgress=null;MissionOpenFloorAdmission.Plan openFloorPlan=null;
                if(MissionOpenFloorAdmission.Requires(mission.phases.FirstOrDefault()))
                {
                    openFloorProgress=new MissionProgress(mission);
                    if(!MissionOpenFloorAdmission.TryPrepare(client,openFloorProgress,0,data.missionProgress,out openFloorPlan,out _,out string refusal)||!openFloorPlan.BeforeCurrent(client))
                    {
                        Output.WriteDebugLog("[MISSION] Box acceptance refused before mutation: "+refusal);
                        client.messageQueue.addRpcMessage(ServerPackets.BuildSystemChatMessage("This mission area cannot be placed safely. Please try again later.","BROADCAST"));
                        client.FlushQueue();return false;
                    }
                }
                SocialHandler.EnsureTeam(client);

                ushort previousInstance=data.missionInstanceCounter;
                data.missionInstanceCounter++;
                // Acceptance, NPCs and briefing publish only after their durable checkpoint (retail's order).
                if(openFloorPlan==null) {if(!MissionRuntime.Start(client,mission,txn))return false;}
                else if(!MissionRuntime.StartOpenFloor(client,openFloorProgress,openFloorPlan,txn)) {data.missionInstanceCounter=previousInstance;return false;}
                UtilityAbilities.OnMissionAccepted(client);
                client.FlushQueue();
                Output.WriteDebugLog("[MISSION] Accepted '" + mission.title + "'");
                return true;
            }
        }

        public void processMissionList(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            PacketReader reader = new PacketReader(packet);

            // Retail: the first u16 is a per-session request number the server copies back into every
            // reply (5, 6, 7, ...), not a contact id.
            UInt16 txn = reader.ReadUInt16(1);
            uint orgID = reader.ReadUint8();

            // Org contacts only: retail's neighbourhood lists came from using the contact (MissionContactNpcs.OnUse), and no
            // capture has a request naming one.
            MissionContact contact = MissionContacts.ForOrg(orgID, MissionContacts.Reputation(client, orgID));
            MissionContactNpcs.SendList(client, contact, txn);
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
            TutorialMission tutorial = TutorialMission.Of(Store.currentClient);
            if (tutorial != null)
            {
                tutorial.OnSkip();
                return;
            }

            if(!MissionRuntime.End(Store.currentClient))return;
            ServerPackets pak = new ServerPackets();
            pak.SendMissionAbort(Store.currentClient);
        }
    }
}
