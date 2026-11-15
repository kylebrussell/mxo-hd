using System;
using System.Linq;
using hds.shared;

namespace hds
{
    class RegionHandler
    {
        public RegionHandler()
        {

        }

        public void ProcessRegionLoaded(ref byte[] packet)
        {

            // Client 621b3f00: ObjID u32 and ObjPos 3xf32 (opcode already removed).
            if (packet == null || packet.Length < 16) return;

            byte[] objectIDBytes = new byte[4];
            byte[] sectorIDBytes = new byte[2];
            ArrayUtils.copyTo(packet, 0, objectIDBytes, 0, 4);
            ArrayUtils.copyTo(objectIDBytes, 2, sectorIDBytes, 0, 2);
            
            PacketReader pakReader = new PacketReader(packet);
            UInt16 sectorID = pakReader.ReadUInt16(1);
            UInt16 objectID = pakReader.ReadUInt16(1);
            float xPos = pakReader.ReadFloat(1);
            float yPos = pakReader.ReadFloat(1);
            float zPos = pakReader.ReadFloat(1);
            
            DataLoader objectLoader = DataLoader.getInstance();
            StaticWorldObject objectValues = objectLoader.getObjectValues(NumericalUtils.ByteArrayToUint32(objectIDBytes,1),
                (UInt16)Store.currentClient.playerData.getDistrictId());

            // Omitted or unknown source keys have no authoritative object to spawn.
            // Keep the existing unsupported-object no-reply behavior; never trust ObjPos as a replacement.
            if (objectValues?.type == null || objectValues.type.Length < 2)
            {
                Output.WriteDebugLog("[REGION] Unavailable source object; request ignored");
                return;
            }
            UInt16 goId = NumericalUtils.ByteArrayToUint16(objectValues.type, 1);
            
            ServerPackets pak = new ServerPackets();
            string regionNote = "Region Object ID " + objectID + " (GoType ID: " + goId + ") in Sector ID" + sectorID + " X:" + xPos + "Y:" + yPos + "Z:" + zPos;
            if (Output.ChatDebugEnabled)
            {
                pak.sendSystemChatMessage(Store.currentClient, regionNote, "BROADCAST");
            }
            else
            {
                Output.WriteDebugLog(regionNote);
            }

            // TRy to spawn
            switch (goId)
            {
                case 8400:
                    // Spawn Signpost
                    ObjectAttributes8400 signpost = new ObjectAttributes8400("SIGNPOST",goId);
                    signpost.SetRelatedStaticObjId(objectValues.mxoStaticId);

                    var signPosts = from signPost in DataLoader.getInstance().Signposts
                        where signPost.mxoStaticId == objectValues.mxoStaticId
                        select signPost;
                    if (signPosts.Count() > 0)
                    {
                        foreach (NPC_Singpost signpostNpc in signPosts)
                        {
                            
                            signpost.DisableAllAttributes();
                            signpost.Position.enable();
                            signpost.SignpostNameString.enable();
                            signpost.AnimationID0.enable();
                            signpost.SignpostOrgID.enable();
                            signpost.DescriptionID.enable();
                            signpost.SignpostReqReputation.enable();
                            signpost.SignpostReqLevel.enable();
                            signpost.Orientation.enable();

                            signpost.Position.setValue(NumericalUtils.doublesToLtVector3d(signpostNpc.xPos, signpostNpc.yPos, signpostNpc.zPos));
                            signpost.SignpostNameString.setValue(signpostNpc.SingpostNameString);
                            signpost.AnimationID0.setValue(signpostNpc.AnimationID0);
                            signpost.SignpostOrgID.setValue(signpostNpc.SingpostOrgId);
                            signpost.DescriptionID.setValue(signpostNpc.DescriptionRez_ID);
                            signpost.SignpostReqReputation.setValue(signpostNpc.SingpostReqReputation);
                            signpost.SignpostReqLevel.setValue(signpostNpc.SingpostReqLevel);
                        
                            signpost.Orientation.setValue(StringUtils.hexStringToBytes(objectValues.quat));
                            //signpost.Orientation.setValue(NumericalUtils.floatsToQuaternion(signpostNpc.wQuad, signpostNpc.xQuad, signpostNpc.yQuad, signpostNpc.zQuad));
                    
                            String entityMxOHackString = "" + objectValues.metrId + "" + objectValues.mxoStaticId;
                            UInt64 entityId = UInt64.Parse(entityMxOHackString);
                    
                            pak.SendSpawnGameObject(Store.currentClient,signpost,entityId);    
                        }
                        
                    }
                   
                    break;
                
                default:
                    Output.WriteDebugLog("Region Object ID " + objectID + " (GoType ID: " + goId + ") in Sector ID" + sectorID + " X:" + xPos + "Y:" + yPos + "Z:" + zPos);
                    break;

            }
           
        }
    }
}
