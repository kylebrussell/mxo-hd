using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hds.databases;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    public partial class ServerPackets 
    {
        

        public void sendDeleteViewPacket(WorldClient client, UInt16 viewIdToDelete)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(1, 1); // Master View
            pak.AddByte(0x01);
            pak.AddUint16(1,1); // Num Views to delete - current just this one (maybe later more)
            pak.AddUint16(viewIdToDelete, 1);
            // Nothing after the view id: retail's next view update starts right here
            // (01 00 01 01 00 <view> <next view id> ...). An extra 00 used to make the client read the
            // next view id one byte off and drop the rest of the packet (e.g. the NPC death after combat).

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();

        }

        public void sendEmotePerform(WorldClient client, UInt32 emoteID)
        {
            double x = 0; double y = 0; double z = 0;
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            int rotation = (int)Store.currentClient.playerInstance.YawInterval.getValue()[0];
            float xPos = (float)x;
            float yPos = (float)y;
            float zPos = (float)z;

            /*
            byte sampleEmoteMsg[] =
            {
                0x03, 0x02, 0x00, 0x01, 0x28, 0xAA, 0x40, 0x00, 0x25, 0x01, 0x00, 0x00, 0x10, 0xBB, 0xBB, 0xBB,
                0xBB, 0xCC, 0xCC, 0xCC, 0xCC, 0xDD, 0xDD, 0xDD, 0xDD, 0x2A, 0x9F, 0x1E, 0x20, 0x00, 0x00,
            };*/

            // Example REsponse for /rolldice  02 03 02 00 01 28 01 40 00 25 <ID> 00 00 10 cd a7 65 c7 00 00 be 42 33 ff 72 46 b9 51 32 22 00 00
            // Example REsponse for /rolldice  02 03 02 00 01 28 01 40 00 25 5c 00 00 10 cd a7 65 c7 00 00 be 42 33 ff 72 46 b9 51 32 22 00 00
            // Example Response for ClapEmote: 02 03 02 00 01 28 06 40 00 25 04 00 00 10 34 49 84 c7 00 00 be 42 27 a4 7f 46 b3 a6 5e 18 00 00 
            // Python Example self.emotePacket="\x02\x03\x02\x00\x01\x28<emoteNum>\x40\x00\x25<emoteID>\x00\x00\x10<coordX><coordY><coordZ>\x2a\x9f\x1e\x20\x00\x00"
            //                                 02 03 02 00 01 28 01 40 00 25 01 00 00 ea 33 47 00 00 be 42 80 55 bd c7 b9 51 32 22 00 00 


            // ToDo: parse list and get the fucking id from uint
            DataLoader objectLoader = DataLoader.getInstance();
            byte emoteByte = new byte();
            emoteByte = objectLoader.findEmoteByLongId(emoteID);
            

            if(emoteByte>0)
            {
                client.playerData.emoteCounter++;
                client.messageQueue.addObjectMessage(BuildEmoteUpdate(client.playerData.emoteCounter, emoteByte,
                    xPos, yPos, zPos, (UInt32)(TimeUtils.GetSimTimeSeconds() * 1000)), false);
                client.FlushQueue();
            }
            

        }

        /// <summary>
        /// Own-view emote: <c>02 00 01 28 &lt;counter&gt; 40 00 25 &lt;emote&gt; 00 00 10 &lt;x y z f32&gt; &lt;start sim ms u32&gt;</c>
        /// (retail clap: <c>28 06 40 00 25 04 00 00 10 &lt;pos&gt; b3 a6 5e 18</c>). The start time is server sim time
        /// in milliseconds; this used to be a fixed value copied from one capture (b9 51 32 22), far from the
        /// sim clock, so the animation didn't play, and a stray 00 followed the entry.
        /// </summary>
        public static byte[] BuildEmoteUpdate(byte counter, byte emote, float x, float y, float z, UInt32 simMs)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByteArray(new byte[] { 0x01, 0x28, counter, 0x40, 0x00, 0x25, emote, 0x00, 0x00, 0x10 });
            pak.AddFloatLtVector3f(x, y, z);
            pak.AddUint32(simMs, 1);
            return pak.ReturnFinalPacket();
        }

        public void sendMoodChange(WorldClient client, byte moodByte)
        {
            // ToDo: should be more dynamic ? How to announce to another players ?
            byte[] moodPak = { 0x02, 0x00, 0x01, 0x01, 0x00, moodByte, 0x00, 0x00 };
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x02);
            pak.AddByteArray(moodPak);
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();
        }

        public void sendAppeareanceUpdate(WorldClient client, byte[] rsibytes)
        {
            client.messageQueue.addObjectMessage(BuildAppearanceUpdate(rsibytes), false);
            client.FlushQueue();
        }

        /// <summary>
        /// Own-view EquippedItemID (attribute 31: group 4, bit 3), as retail sent it after a weapon
        /// equip/unequip: <c>02 00 02 80 80 80 80 08 &lt;item goid u32 LE&gt;</c> (0 = nothing in hand).
        /// </summary>
        public static byte[] BuildEquippedItemUpdate(UInt32 goid)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByteArray(new byte[] { 0x02, 0x80, 0x80, 0x80, 0x80, 0x08 });
            pak.AddUint32(goid, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Own-view CombatantMode (attribute 43: group 6, bit 1), as retail sent it after RPC 0x3d:
        /// <c>02 00 02 80 80 80 80 80 80 02 &lt;mode&gt;</c> (0x21 weapon drawn, 0x22 at rest).
        /// </summary>
        public static byte[] BuildCombatantModeUpdate(byte mode)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByteArray(new byte[] { 0x02, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 });
            pak.AddByte(mode);
            return pak.ReturnFinalPacket();
        }

        /// <summary>RSIDescription is 15 bytes; PacketsUtils.getRSIBytes packs the parts into 13.</summary>
        public const int RsiDescriptionSize = 15;

        /// <summary>
        /// Own-view RSIDescription update, as retail sent it: <c>02 00 02 80 01 &lt;15 RSI bytes&gt;</c>
        /// (group 1, bit 0 = own-view attribute 7). The packed parts are zero-padded to 15 bytes; retail
        /// values end in zeros too. This used to send the 13 packed bytes under group bytes 80 89, which
        /// also claim MovementScale and another group after it, so the client read past the update.
        /// </summary>
        public static byte[] BuildAppearanceUpdate(byte[] rsibytes)
        {
            byte[] rsi = new byte[RsiDescriptionSize];
            Array.Copy(rsibytes, rsi, Math.Min(rsibytes.Length, RsiDescriptionSize));

            PacketContent pak = new PacketContent();
            pak.AddUint16(2,1);
            pak.AddByteArray(new byte[] { 0x02, 0x80, 0x01 });
            pak.AddByteArray(rsi);
            return pak.ReturnFinalPacket();
        }

        public void sendAttribute(WorldClient client, UInt16 attributeValue, byte type)
        {
            // Byte-exact with the previous inline build:
            //   80 b2 | <type> | <attributeValue big-endian> | 00 08 02
            byte[] message = AttributeBundle.Build80b2Attribute(type, attributeValue);
            client.messageQueue.addRpcMessage(message);
        }

        /// <summary>
        /// Own view at death, retail (F1 pvp death): 02 00 02 80 80 80 c1 01 00 00 80 80 01 00 00 00 01 =
        /// IsDead (own-view attribute 21) = 1, Health = 0, ConditionStateFlags = 0x01000000.
        /// </summary>
        public static byte[] BuildSelfDeathUpdate()
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("020002" + "808080c1" + "01" + "0000" + "8080" + "01" + "00000001");
            return pak.ReturnFinalPacket();
        }

        /// <summary>2e: the death system message (string id 0x60000092), byte-identical in all three retail deaths.</summary>
        public static byte[] BuildDeathMessage()
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("2e67" + "0000000000000000000000" + "92000060" + "00000000000000000000000000000000000000");
            return pak.ReturnFinalPacket();
        }

        /// <summary>Own view on respawn: Health = max and ConditionStateFlags cleared (02 00 02 80 80 80 c0 &lt;hp&gt; 80 80 01 00000000).</summary>
        public static byte[] BuildSelfRespawnHeal(UInt16 maxHealth)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("020002808080c0");
            pak.AddUint16(maxHealth, 1);
            pak.AddHexBytes("8080" + "01" + "00000000");
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 fd 00 00 00 00 01 01: the death variant of the jack-out finish (00 00 = quit).</summary>
        public static byte[] BuildRespawnToLoadingArea()
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_JACKOUT_FINISH, 0);
            pak.AddHexBytes("000000000101");
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 80 fd 00 00 00 00 01 00: answer to "Exit to Loading Area" at a hardline (client 80 fc 01). The client
        /// confirms with 80 fe 01 and the server then loads the Loading Area (crashover2k
        /// proxy_second_pc_001_exit_to_loadingarea, packets 13-16).
        /// </summary>
        public static byte[] BuildJackoutToLoadingArea()
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_JACKOUT_FINISH, 0);
            pak.AddHexBytes("000000000100");
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 e5: the player's absolute experience, sent while loading the world.</summary>
        public void sendEXPCurrent(WorldClient client, UInt32 exp)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_EXP, 0);
            pak.AddUint32(exp, 1);
            pak.AddHexBytes("00000000");
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        /// <summary>
        /// 80 e6 &lt;gain u32&gt; 01 00 00 00: an experience gain; the client adds it and shows "You gained N
        /// experience". Retail after a 391-EXP kill: 80 e6 87 01 00 00 01 00 00 00. This used to carry the
        /// player's total (also at login), so every kill announced the whole total as gained.
        /// </summary>
        public static byte[] BuildEXPGain(UInt32 gain, byte gainType = ExpGainFighting)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_EXP_ANIM, 0);
            pak.AddUint32(gain, 1);
            pak.AddByte(gainType);
            pak.AddHexBytes("000000");
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 e6 gain types: 01 = "from fighting" (kills), 02 = mission rewards (retail mission completions).</summary>
        public const byte ExpGainFighting = 0x01;
        public const byte ExpGainMission = 0x02;

        public void SendEXPGain(WorldClient client, UInt32 gain, byte gainType = ExpGainFighting)
        {
            client.messageQueue.addRpcMessage(BuildEXPGain(gain, gainType));
        }

        /// <summary>Reason codes the retail server put on 80 e7 / 80 e4 cash messages.</summary>
        public const UInt32 InfoReasonLoad = 0x00;
        public const UInt32 InfoReasonVendorSell = 0x07;
        public const UInt32 InfoReasonVendorBuy = 0x08;
        public const UInt32 InfoReasonRecycle = 0x0f;

        public void SendInfoCurrent(WorldClient client, UInt32 info, UInt32 reason = InfoReasonLoad)
        {
            client.messageQueue.addRpcMessage(BuildInfoCurrent(info, reason));
        }

        /// <summary>
        /// 80 e4: new absolute cash (u32 LE), then a reason code (u32 LE). Retail only used reason 0
        /// while loading the world; every mid-session change carried a nonzero reason and came right
        /// after an 80 e7. Retail FM-13SK sell: "80 e4 97 e1 7a 00 07 00 00 00" (8,053,143, reason 7).
        /// </summary>
        internal static byte[] BuildInfoCurrent(UInt32 info, UInt32 reason)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_INFO, 0);
            pak.AddUint32(info, 1);
            pak.AddUint32(reason, 1);
            return pak.ReturnFinalPacket();
        }

        public void SendInfoDelta(WorldClient client, long delta, UInt32 reason)
        {
            client.messageQueue.addRpcMessage(BuildInfoDelta(delta, reason));
        }

        /// <summary>
        /// 80 e7: signed cash change (i64 LE), then the reason code (u32 LE). Retail FM-13SK sell:
        /// "80 e7 88 13 00 00 00 00 00 00 07 00 00 00" (+5000, reason 7).
        /// </summary>
        internal static byte[] BuildInfoDelta(long delta, UInt32 reason)
        {
            byte[] deltaBytes = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(deltaBytes, delta);

            PacketContent pak = new PacketContent();
            pak.AddUint16(0x80e7, 0);
            pak.AddByteArray(deltaBytes);
            pak.AddUint32(reason, 1);
            return pak.ReturnFinalPacket();
        }

        // Updates the Inner Strenght Value
        /// <summary>
        /// The player's own view (2): <c>02 00 02 80 80 80 40 &lt;health u16&gt;</c>. Own-view attribute 27 is
        /// Health (group 3, bit 6); retail sent exactly this during combat.
        /// </summary>
        public static byte[] BuildSelfHealthUpdate(UInt16 health)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x02);
            pak.AddByteArray(new byte[] { 0x80, 0x80, 0x80 });
            pak.AddByte(0x40);
            pak.AddUint16(health, 1);
            return pak.ReturnFinalPacket();
        }

        public void sendISCurrent(WorldClient client, UInt16 innerStrengthValue )
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x02);
            pak.AddByteArray(new byte[]{0x80,0x80,0x80});
            pak.AddByte(0x10);
            pak.AddUint16(innerStrengthValue,1);
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();

        }

        public void SendPlayerFriendList(WorldClient client)
        {
            ArrayList friends =  Store.dbManager.WorldDbHandler.FetchFriendList(Store.currentClient.playerData.getCharID());

            // mxopackets1_entilsar_enti_lastday2 21:43:23: the list (3b = online, 3c = offline) and 80 de for
            // each online friend. Online is taken from the live client list; the DB flag goes stale after a crash.
            List<string> online = new List<string>();
            foreach (Hashtable friend in friends)
            {
                string handle = "SOE+MXO+" + Store.worldConfig.serverName + "+" + friend["handle"];
                bool isOnline = SocialHandler.FindOnlineByHandle(friend["handle"].ToString()) != null;
                client.messageQueue.addRpcMessage(SocialPackets.FriendListStatus(
                    isOnline ? SocialPackets.FriendOnlineListed : SocialPackets.FriendListed, handle));
                if (isOnline)
                {
                    online.Add(handle);
                }
            }

            foreach (string handle in online)
            {
                client.messageQueue.addRpcMessage(SocialPackets.FriendOnline(handle));
            }
        }

        public void sendPlayerMoveAnim(WorldClient client, byte animationId)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x02);
            pak.AddByteArray(new byte[] { 0x01, 0x02 });
            pak.AddByte(animationId);
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();
        }

        public void sendPlayerAnimation(WorldClient client, String hexAnimation)
        {
            // See animations.txt - hex animation is the first Id
            // Some samples:
            /*
             * 7312 1273 = Stand_Abil_ProgLauSelectivePhage2s_A
               7412 1274 = Stand_Abil_ProgLauSelectivePhage4s_A
               7512 1275 = Stand_Abil_ProgLauSelectivePhage6s_A
               7612 1276 = Stand_Abil_ProgLauSelectivePhage8s_A
             */
            Random rand = new Random();
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            double x = 0; 
            double y = 0; 
            double z = 0;
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            float xPos = (float)x;
            float yPos = (float)y;
            float zPos = (float)z;

            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByteArray(new byte[]{0x01,0x28});
            pak.AddUShort((ushort)rand.Next(0, 255));
            pak.AddByteArray(new byte[] { 0x40, 00 });
            pak.AddByte(0x29);
            pak.AddHexBytes(hexAnimation);
            pak.AddByteArray(new byte[] { 0x00, 0x01 });
            pak.AddFloat(xPos,1);
            pak.AddFloat(yPos,1);
            pak.AddFloat(zPos,1);
            pak.AddByteArray(new byte[] {0x20, 0x9f, 0x1e, 0x20});
            pak.AddUint16(0, 1);
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();
        }

        public void sendPlayerAttributes(WorldClient client)
        {
            // ToDo: Load dynamic if we know what to load
            
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc15002f0000f70300000802000000000000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc15002f0000f70300000802000000000000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc1500300000f70300000702ecffffff0000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc1500310000f70300005004000000000000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc1500320000f7030000f403000000000000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc1500330000f70300005104f6ffffff0000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc56002a0000b3a900000904090000000100000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80b23a0400000802"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80b2110011000802"));
            
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("81A500000700052300687474703A2F2F6D786F656D752E696E666F2F666F72756D2F696E6465782E70687000")); // Has forum url - but is not flash traffic - or ? 
            // TODO: lead-only family (80 bc 55 00 super-jump), not yet modeled byte-exact.
            client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc55005100000b0000003702330000000000000000")); // this adds super jump points dude
            // Code Write bonuses
            // Was: 80b225000f001900  (80 b2 attribute form with a non-default 00 19 00 tail)
            client.messageQueue.addRpcMessage(AttributeBundle.Build80b2Attribute(0x25, 0x000f, new byte[] { 0x00, 0x19, 0x00 }));
            // Was: 80b2ca0300000802  (80 b2 short-tail attribute form)
            client.messageQueue.addRpcMessage(AttributeBundle.Build80b2Attribute(0xca, 0x0300));
            // Was: 80bc4503ca030025000000ca03000000000000000000  (45 03 family, byte-9 field1 repeat)
            client.messageQueue.addRpcMessage(AttributeBundle.Build80bc4503(0x03ca, 0x25));
            // TODO: lead-only family (80 bc 45 00, no byte-9 self-repeat), not yet modeled byte-exact.
            client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45004d0000250000008902300000000000000000"));
            //client.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("2E070000000000000000002400000000000000000000000000000000000000000000000011005768617427732075702062726F736B6900"));
            
            createFlashTraffic(client, "http://mxo.hardlinedreams.com");
        }

        public void SendPlayerSpawn(WorldClient receiverClient, WorldClient otherClient, UInt16 viewId)
        {
            PacketContent pak = new PacketContent();
            byte[] spawnPaket = new BootingHelperRsi().generatePlayerSpawnPacket(otherClient, receiverClient.playerData.assignSpawnIdCounter());
            pak.AddByteArray(spawnPaket);
            pak.AddUint16(viewId,1);
            pak.AddByte(0x00);
            receiverClient.messageQueue.addObjectMessage(pak.ReturnFinalPacket(),false);
            receiverClient.FlushQueue();
        }

        public void sendSaveCharDataMessage(WorldClient client, string handle)
        {

            PacketContent pak = new PacketContent();
            pak.AddByte((byte)RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
            pak.AddHexBytes("0700000000000000000000006400002E00240000000000000000000000000000000000");
            pak.AddSizedTerminatedString(handle);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

        }

        public void sendGetBackgroundMessage(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_GET_BACKGROUND,0);
            pak.AddByte(0x05);
            pak.AddUint16(0,1);

            // Get Data from DB and save
            Hashtable characterData = Store.dbManager.WorldDbHandler.GetCharInfo(client.playerData.getCharID());
            String backgroundTextt = characterData["background"].ToString();
            pak.AddSizedTerminatedString(backgroundTextt);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

        }

        public void SendPlayerGetDetails(WorldClient client, Hashtable charData)
        {
            // ToDo: Research real packet and replace the content with it 
            UInt16 handleOffset = 29;
            UInt16 firstNameOffset = (ushort) (handleOffset + (charData["handle"].ToString().Length + 3));
            UInt16 lastNameOffset = (ushort) (firstNameOffset + (charData["firstname"].ToString().Length + 3));
            
            UInt16 crewNameOffset = 0;
            if (charData["crew_name"] != null)
            {
                crewNameOffset = (ushort) (lastNameOffset + (charData["lastname"].ToString().Length + 3));
            }
            
            UInt16 factionNameOffset = 0;
            if (charData["faction_name"] != null)
            {
                if (crewNameOffset > 0)
                {
                    factionNameOffset = (ushort) (crewNameOffset + (charData["faction_name"].ToString().Length + 3));    
                }
                else
                {
                    factionNameOffset = (ushort) (lastNameOffset + (charData["lastname"].ToString().Length + 3));
                }
            }

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_PLAYER_GET_DETAILS, 0);
            pak.AddUint32((UInt32)charData["charId"],1);
            pak.AddUint16(handleOffset,1); // Offset to Handle which should ALWAYS be 29
            pak.AddUint32(0,1); // Unknown UInt32 Zero
            pak.AddUint16(firstNameOffset,1 );
            pak.AddUint16(lastNameOffset,1 );
            pak.AddUint32(300,1); // Character Trait ?
            pak.AddUShort((ushort)charData["alignment"]);
            pak.AddUint16(crewNameOffset,1);
            pak.AddUint16(factionNameOffset,1);
            pak.AddUint32((UInt32)charData["conquest_points"],1);

            pak.AddSizedTerminatedString(charData["handle"].ToString());
            pak.AddSizedTerminatedString(charData["firstname"].ToString());
            pak.AddSizedTerminatedString(charData["lastname"].ToString());
            if (crewNameOffset > 0)
            {
                pak.AddSizedTerminatedString(charData["crew_name"].ToString());
            }
            
            if (factionNameOffset > 0)
            {
                pak.AddSizedTerminatedString(charData["faction_name"].ToString());
            }
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        public void SendPlayerBackground(WorldClient client, Hashtable charData)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_PLAYER_HANDLE_BACKGROUND,0 );
            pak.AddUint16(5,1);
            pak.AddUShort(1);
            if (charData["background"].ToString().Length > 0)
            {
                pak.AddSizedTerminatedString(charData["background"].ToString());
            }
            else
            {
                pak.AddUint16(1,0);
                pak.AddUShort(0);
            }
            
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        // ToDo: Move it to player Packets and make a ?moa command for it
        public void sendChangeChangeMoaRSI(WorldClient client, byte[] rsi)
        {
            // ToDo: proove to remove
            PacketContent pak = new PacketContent();
            pak.AddUint16(2,1);
            pak.AddHexBytes("0281008080808004");
            pak.AddByteArray(rsi);
            pak.AddByte(0x41);
            pak.AddByte(0x00);
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(),false);

        }

        public void sendJackoutEffect(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            double x = 0; double y = 0; double z = 0;
            byte[] Ltvector3d = client.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            int rotation = (int)client.playerInstance.YawInterval.getValue()[0];
            float xPos = (float)x;
            float yPos = (float)y;
            float zPos = (float)z;
            
            pak.AddUint16(2,1); // ToDo: we should change this for other views ?
            pak.AddHexBytes("032802C000740010");
            pak.AddFloatLtVector3f(xPos,yPos,zPos);
            pak.AddHexBytes("E93C991E8080808080801001000000"); // ToDo: analyze it more ?
            client.playerData.isJackoutInProgress = true;
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(),false);
        }
        

        public void sendExitGame(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_JACKOUT_FINISH,0);
            pak.AddHexBytes("000000000000");
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }



    }
}
