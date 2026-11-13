using System;
using System.Reflection;
using hds.shared;

namespace hds
{
    public class ObjectInteractionHandler
    {
        private enum objectTypesStatic : int
        {
            COLLECTOR = 0x01,
            HUMAN_NPC = 0x02,
            DOOR = 0x03,
            HARDLINE_UPLOAD = 0x04,
            HARDLINE_LAEXIT = 0x05,
            HARDLINE_SYNC = 0x08, // Request Hardline Sync
            ENVIROMENT = 0x00, // Bench too
        }

        private const UInt32 SubwayCarType = 6568; // ViewVisibleThread.SubwayTypeId

        private enum objectTypesDynamic : int
        {
            SUBWAY = 0x02,
            LOOT = 0x05,
            TALK = 0x09, // double-click on an NPC: 80 c7 <view u16> <spawn u16> 09 00
            GET = 0x0d,  // right-click Get on a mission NPC
            GIVE = 0x0e  // right-click Give on a mission NPC
        }

        private static Mob InteractedTutorialMob(ClientView view)
        {
            if (view == null)
            {
                return null;
            }

            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob.getEntityId() == view.entityId)
                    {
                        return mob;
                    }
                }
            }

            return null;
        }

        private static Mob InteractedMissionMob(ClientView view)
        {
            return view == null ? null : MissionRuntime.FindMissionMob(Store.currentClient.playerData, view.entityId);
        }

        /// <summary>
        /// Client 66 &lt;Slot u8&gt; &lt;TargetID u32&gt;: a bag item dragged onto an NPC (client.dll 0x621eb080, the trade
        /// sender's branch for objects that take items). The client sends it only for a mission item whose key matches
        /// the NPC's, prints "You hand over the %s." itself and leaves the slot alone; otherwise it prints "The NPC will not
        /// accept your trade request." and sends nothing. No capture has one, so it gets the same answer as the NPC menu's
        /// Give (80 c7 ... 0e), which retail answered with 5f, 81 22 and the goal (80 a0). TargetID is
        /// &lt;view u16&gt; &lt;spawn id&gt; 00, as for trades.
        /// </summary>
        public static void ProcessGiveToNpc(WorldClient client, byte[] rpcData)
        {
            if (rpcData.Length < 5)
            {
                return;
            }

            byte slot = rpcData[0];
            UInt16 viewId = (UInt16)(rpcData[1] | (rpcData[2] << 8));
            ClientView view = client.viewMan.getViewById(viewId);
            Mob npc = InteractedMissionMob(view);
            Output.WriteDebugLog("[MISSION] Item in slot " + slot + " dragged onto view " + viewId + (npc != null ? " (" + npc.getName() + ")" : ""));
            bool taken = MissionRuntime.OnGive(client, npc);
            TutorialMission.Of(client)?.OnGive(InteractedTutorialMob(view));
            if (!taken && TutorialMission.Of(client) == null)
            {
                // Ours: the client already said it handed the item over, and the item is still in the bag
                new ServerPackets().sendSystemChatMessage(client, "They don't want that.", "SYSTEM");
            }
        }

        public void processTarget(ref byte[] packet)
        {
            byte[] viewBytes = {packet[0], packet[1]};
            UInt16 viewID = NumericalUtils.ByteArrayToUint16(viewBytes, 1);
        }

        public void processVendorOpen(ref byte[] objectID)
        {
            // ToDo: Dynamic Shop Vendors
            Vendor vendor = DataLoader.getInstance()
                .getVendorByGoIDandMetrId(NumericalUtils.ByteArrayToUint32(objectID, 1), (UInt16)Store.currentClient.playerData.getDistrictId());

            if (vendor != null)
            {
                // ToDo: send the Packet (and give it the items ?)
                ServerPackets serverPacket = new ServerPackets();
                serverPacket.SendVendorWindow(Store.currentClient, vendor);
            }
        }

        public void processObjectInteraction(StaticWorldObject staticWorldObject, GameObjectItem item)
        {
            UInt16 typeId = NumericalUtils.ByteArrayToUint16(staticWorldObject.type, 1);
            Output.WriteDebugLog("[DOOR] Use of static " + staticWorldObject.mxoStaticId + " type " + typeId +
                                 " (" + item.getName() + ")");

            FieldInfo objectField = typeof(GameObjectDefinitions).GetField("Object" + typeId);
            Type doorClass = objectField?.FieldType;
            GameObject door;
            if (doorClass == typeof(AttributeClass363))
            {
                AttributeClass363 d = new AttributeClass363("DOOR363", typeId);
                door = PrepareDoor(d, d.Position, d.Orientation, d.CurrentState, staticWorldObject);
            }
            else if (doorClass == typeof(AttributeClass364))
            {
                AttributeClass364 d = new AttributeClass364("DOOR364", typeId);
                door = PrepareDoor(d, d.Position, d.Orientation, d.CurrentState, staticWorldObject);
            }
            else if (doorClass == typeof(AttributeClass365))
            {
                AttributeClass365 d = new AttributeClass365("DOOR365", typeId);
                door = PrepareDoor(d, d.Position, d.Orientation, d.CurrentState, staticWorldObject);
            }
            else
            {
                new ServerPackets().sendSystemChatMessage(Store.currentClient,
                    "Unknown Object Interaction with Object Type " + typeId + " and Name " + item.getName(), "MODAL");
                return;
            }

            // ToDo: We make a little Entity "Hack" so that we have a unique id : metrid + fullmxostatic_id is entity
            UInt64 entityId = UInt64.Parse("" + staticWorldObject.metrId + staticWorldObject.mxoStaticId);
            new ServerPackets().SendSpawnGameObject(Store.currentClient, door, entityId);

            ClientView view = Store.currentClient.viewMan.FindViewForEntity(entityId);
            if (view != null)
            {
                DoorLifecycle.Opened(Store.currentClient, view.ViewID, entityId, typeId);
            }

            if (typeId == MissionSites.ElevatorDoor1 || typeId == MissionSites.ElevatorDoor2)
            {
                MissionStaging.OnElevator(Store.currentClient, staticWorldObject);
            }
        }

        private static GameObject PrepareDoor(GameObject door, Attribute position, Attribute orientation,
            Attribute currentState, StaticWorldObject staticWorldObject)
        {
            door.SetRelatedStaticObjId(staticWorldObject.mxoStaticId);
            door.DisableAllAttributes();
            orientation.enable();
            position.enable();
            currentState.enable();
            position.setValue(NumericalUtils.doublesToLtVector3d(staticWorldObject.pos_x,
                staticWorldObject.pos_y, staticWorldObject.pos_z));
            orientation.setValue(StringUtils.hexStringToBytes(staticWorldObject.quat));
            currentState.setValue(NumericalUtils.uint32ToByteArray(DoorLifecycle.StateOpen, 1));
            return door;
        }

        public void processObjectStatic(ref byte[] packet)
        {
            byte[] objectID = new byte[4];
            byte[] sectorID = new byte[2];
            ArrayUtils.copyTo(packet, 0, objectID, 0, 4);
            ArrayUtils.copyTo(objectID, 2, sectorID, 0, 2);


            UInt32 numericObjectId = NumericalUtils.ByteArrayToUint32(objectID, 1);

            // Ok sector Bytes are something like 30 39 (reversed but the result must be 39 03)
            UInt16 numericSectorId = NumericalUtils.ByteArrayToUint16(sectorID, 1);
            // strip out object id
            string id = StringUtils.bytesToString_NS(objectID);


            // get the type 
            byte[] objectType = new byte[1];
            ArrayUtils.copy(packet, 4, objectType, 0, 1);
            int objectTypeID = packet[4];

            DataLoader objectLoader = DataLoader.getInstance();
            StaticWorldObject objectValues =
                objectLoader.getObjectValues(NumericalUtils.ByteArrayToUint32(objectID, 1),
                    (UInt16)Store.currentClient.playerData.getDistrictId());


            // create a new System message but fill it in the switch block
            ServerPackets pak = new ServerPackets();

            // The tutorial's wardrobe and hardline aren't in our static object data
            if (TutorialMission.Of(Store.currentClient)?.OnStaticUse(numericObjectId, (byte)objectTypeID) == true)
            {
                return;
            }

            if ((objectTypeID == (int)objectTypesStatic.COLLECTOR || objectTypeID == (int)objectTypesStatic.HUMAN_NPC) &&
                CollectorHandler.TryOpen(Store.currentClient, objectValues))
            {
                return;
            }

            // Hardline booths: 03 jack out, 04 upload, 05 exit to the Loading Area (retail 80 c8 <booth> 04 00)
            if (objectValues != null && objectValues.type != null &&
                HardlineBooths.IsBooth(NumericalUtils.ByteArrayToUint16(objectValues.type, 1)))
            {
                TeleportHandler.ProcessBoothAction(Store.currentClient, (byte)objectTypeID, objectValues);
                return;
            }

            if (Elevators.IsPanel(objectValues))
            {
                Elevators.OnPanelUsed(Store.currentClient, objectValues);
                return;
            }

            switch (objectTypeID)
            {
                // Case 03 is not only a staticWorldObject ...
                case (int) objectTypesStatic.DOOR:

                    Output.writeToLogForConsole("[OI HELPER] INTERACT WITH DOOR | Object ID :" + id + " Sector ID : " +
                                                numericSectorId);

                    // just a test

                    if (objectValues != null && objectValues.type != null)
                    {
                        GameObjectItem item =
                            objectLoader.getGameObjectItemById(NumericalUtils.ByteArrayToUint16(objectValues.type, 1));
                        //Store.currentClient.messageQueue.addRpcMessage(PacketsUtils.createSystemMessage("[OI HELPER] Door StrType: !" + item.getName() + " with Type ID " + StringUtils.bytesToString_NS(objectValues.type), Store.currentClient));
                        this.processObjectInteraction(objectValues, item);
                    }

                    break;

                case (int) objectTypesStatic.HARDLINE_SYNC:

                    pak.sendSystemChatMessage(Store.currentClient, "Hardline Interaction(not done yet)!", "MODAL");
                    break;

                case (int) objectTypesStatic.HARDLINE_UPLOAD:
                    // Retail moved the bag's Code Bits and fragments into code storage here
                    Store.currentClient.playerData.lastClickedObjectId = numericObjectId;
                    CodeStorageHandler.ProcessUploadRequest(Store.currentClient);
                    break;

                case (int) objectTypesStatic.HARDLINE_LAEXIT:
                    // Exit LA
                    pak.sendSystemChatMessage(Store.currentClient, "Exit to LA Dialog should popup", "MODAL");
                    new TeleportHandler().processHardlineExitRequest();
                    break;

                case (int) objectTypesStatic.HUMAN_NPC:
                    pak.sendSystemChatMessage(Store.currentClient,
                        "Open Vendor Dialog for Object ID" + StringUtils.bytesToString_NS(objectID), "MODAL");
                    this.processVendorOpen(ref objectID);
                    break;

                case (int) objectTypesStatic.COLLECTOR:
                    pak.sendSystemChatMessage(Store.currentClient, "Collector Interaction (not done yet)!", "MODAL");
                    break;

                case (int) objectTypesStatic.ENVIROMENT:
                    GameObjectItem enviromentItem =
                        objectLoader.getGameObjectItemById(NumericalUtils.ByteArrayToUint16(objectValues.type, 1));
                    UInt16 enviromentTypeID = NumericalUtils.ByteArrayToUint16(objectValues.type, 1);
                    switch (enviromentTypeID)
                    {
                        case 6581: 
                            // its a bench
                        case 6952:
                            // ToDo: implement Elevator Panel
                            pak.SendElevatorPanel(Store.currentClient, objectValues);
                            break;
                        default:
                            pak.sendSystemChatMessage(Store.currentClient,
                                "Enviroment Type ID " + objectValues.type + " name " + enviromentItem.getName(),
                                "MODAL");
                            break;
                    }

                    pak.sendSystemChatMessage(Store.currentClient,
                        "Enviroment Type ID " + NumericalUtils.ByteArrayToUint16(objectValues.type, 1) + " name " +
                        enviromentItem.getName(), "MODAL");
                    break;


                default:
                    pak.sendSystemChatMessage(Store.currentClient,
                        "[OI HELPER] Unknown Object Type : " + objectTypeID.ToString() + "| Object ID :" + id, "MODAL");
                    break;
            }
        }

        public void processObjectDynamic(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);

            byte[] objectID = new byte[4];
            byte[] sectorID = new byte[2];
            ArrayUtils.copyTo(packet, 0, objectID, 0, 4);
            ArrayUtils.copyTo(objectID, 2, sectorID, 0, 2);

            UInt32 numericObjectId = NumericalUtils.ByteArrayToUint32(objectID, 1);


            // Ok sector Bytes are something like 30 39 (reversed but the result must be 39 03)
            UInt16 numericSectorId = NumericalUtils.ByteArrayToUint16(sectorID, 1);
            // strip out object id
            string id = StringUtils.bytesToString_NS(objectID);


            // get the type 
            byte[] objectType = new byte[1];
            ArrayUtils.copy(packet, 4, objectType, 0, 1);
            int objectTypeID = packet[4];


            // create a new System message but fill it in the switch block
            ServerPackets pak = new ServerPackets();

            // A hardline booth view (80 c7 <view> <spawn> 03|04|05 00 in retail)
            UInt16 interactedViewId = (UInt16)(packet[0] | (packet[1] << 8));
            ClientView interactedView = Store.currentClient.viewMan.getViewById(interactedViewId);
            Output.WriteDebugLog("[OBJECT-USE] char="+Store.currentClient.playerData.getCharID()+" view="+interactedViewId+
                " receivedSpawn="+(numericObjectId>>16)+" expectedSpawn="+interactedView?.spawnId+" goid="+interactedView?.GoID+" use="+objectTypeID);
            if (interactedView != null && TutorialMission.Of(Store.currentClient)?.OnViewUse(interactedView.entityId, (byte)objectTypeID) == true)
            {
                return;
            }

            if (interactedView != null && interactedView.spawnId == (numericObjectId >> 16) &&
                PvpArchives.OnUse(Store.currentClient, interactedView, (byte)objectTypeID)) return;

            if (interactedView != null && DataMining.Use(Store.currentClient, interactedView, (byte)objectTypeID,(ushort)(numericObjectId>>16))) return;

            // Pick Lock, Disable Security Devices and locked mission objects run their retail rule first (UtilityAbilities)
            if (interactedView != null && UtilityAbilities.OnObjectUse(Store.currentClient, interactedView.entityId, (byte)objectTypeID))
            {
                return;
            }

            // Objects the server follows (ObjectStates.Track) move to the state retail's script gives the use
            bool followed = interactedView != null &&
                            ObjectStates.OnUse(Store.currentClient, interactedView.entityId, (byte)objectTypeID);

            if (interactedView != null && MissionRuntime.OnViewUse(Store.currentClient, interactedView.entityId))
            {
                return;
            }

            if (followed)
            {
                Store.currentClient.FlushQueue();
                return;
            }

            if (MissionContactNpcs.OnUse(Store.currentClient, interactedView, (byte)objectTypeID))
            {
                return;
            }

            if (interactedView != null && HardlineBooths.IsBooth(interactedView.GoID) && interactedView.staticObject != null)
            {
                if (interactedView.spawnId != (numericObjectId >> 16)) return;
                TeleportHandler.ProcessBoothAction(Store.currentClient, (byte)objectTypeID, interactedView.staticObject);
                return;
            }

#if DEBUG
            if (DebugChat.Enabled)
            pak.sendSystemChatMessage(Store.currentClient,
                "Object Type ID IS " + objectTypeID.ToString() + " Dynamic Object RPC : " +
                StringUtils.bytesToString_NS(packet), "BROADCAST");
#endif

            switch (objectTypeID)
            {
                case (int) objectTypesDynamic.SUBWAY:
                    // 02 is retail's generic "use" (on NPCs it opened vendors: 81 9c + 81 0d), so only a subway
                    // car view gets the subway answer. 81 3a / 81 3e never appear in the retail captures.
                    if (interactedView == null || interactedView.GoID != SubwayCarType)
                    {
                        Output.WriteDebugLog("[OI HELPER] Use (02) of dynamic view " + interactedViewId +
                                             (interactedView != null ? " type " + interactedView.GoID : "") + " not handled");
                        break;
                    }

                    // The map opens on the nearest station's connector (SubwayHandler, docs/CLIENT-ACTIONS.md)
                    SubwayHandler.OpenMap(Store.currentClient);

                    break;
                case (int) objectTypesDynamic.LOOT:
                    NpcLoot.Open(Store.currentClient,
                        interactedView != null && interactedView.spawnId == (numericObjectId >> 16)
                            ? interactedView.entityId : 0);
                    break;

                case (int) objectTypesDynamic.TALK:
                    Mob talkMob = InteractedMissionMob(interactedView);
                    Output.WriteDebugLog("[MISSION] Talk to view " + interactedViewId + (talkMob != null ? " (" + talkMob.getName() + ")" : ""));
                    MissionRuntime.OnTalk(Store.currentClient, talkMob);
                    TutorialMission.Of(Store.currentClient)?.OnTalk(InteractedTutorialMob(interactedView));
                    break;

                case (int) objectTypesDynamic.GET:
                    Mob getMob = InteractedMissionMob(interactedView);
                    Output.WriteDebugLog("[MISSION] Get from view " + interactedViewId + (getMob != null ? " (" + getMob.getName() + ")" : ""));
                    MissionRuntime.OnGet(Store.currentClient, getMob);
                    break;

                case (int) objectTypesDynamic.GIVE:
                    Mob giveMob = InteractedMissionMob(interactedView);
                    Output.WriteDebugLog("[MISSION] Give to view " + interactedViewId + (giveMob != null ? " (" + giveMob.getName() + ")" : ""));
                    MissionRuntime.OnGive(Store.currentClient, giveMob);
                    TutorialMission.Of(Store.currentClient)?.OnGive(InteractedTutorialMob(interactedView));
                    break;

                case 0x04: // hardline upload, retail 80 c7 <view> <spawn> 04 00: Code Bits and fragments go into code storage
                    CodeStorageHandler.ProcessUploadRequest(Store.currentClient);
                    break;

                default:
                    pak.sendSystemChatMessage(Store.currentClient,
                        "[OI HELPER] Unknown Object Type : " + objectTypeID.ToString() + "| Object ID :" + id, "MODAL");
                    break;
            }
        }
    }
}
