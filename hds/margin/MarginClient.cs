/// <summary>
/// Hardline Dreams Team - 2010
/// Created by Neo
/// Modified by Morpheus
/// </summary>

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using hds.databases.Entities;
using hds.shared;
using hds.accounts;
using hds.auth;

namespace hds{

	public class MarginClient{

        /// <summary>
        /// Known code recipes (character data opcode 0x0a): u32 4, then recipe result goids. This is the list the
        /// margin always sent; every goid in it is a recipe result in the client's cookbook.
        /// Crafting.BuildKnownRecipesBlock adds the Code Bit fragment and health pill recipes.
        /// </summary>
        public static readonly byte[] KnownRecipesBaseBlock = StringUtils.hexStringToBytes("04000000a7010000ac010000cc010000d1010000d60100001302000019020000650200006602000068020000b4020000b5020000b6020000bb020000bc020000c2020000c4020000d5020000d6020000de020000e2020000e3020000e6020000e7020000e90200003f03000086030000a0030000aa030000e3030000fc030000fe030000030400000604000008040000090400000a040000270400004c0400004e040000bb040000c2040000ce040000eb040000ec040000ee040000f5040000f6040000f7040000f9040000540900005f09000082090000be090000bf090000c1090000c4090000c7090000c8090000c9090000040a0000080a0000b0120000fa12000009130000691400008614000095140000e9140000ef140000f11400001c1500002d1500005b150000631500007815000080150000ab15000000160000011600001f1600002416000033160000611600006e160000731600008216000084160000871600009416000000170000241700002517000038170000451700005e1700007317000074170000f4170000f6170000fc1700004f1800005c1800001a1900002d1900003a1900005a1900005b1900006519000066190000121a0000181a0000281a0000341a0000451a0000471a0000621a00006e1a00007b1a00007c1a00007d1a0000e51a0000eb1a0000ec1a0000ed1a0000ee1a0000f01a0000f11a00000c1b00002d1c0000521c0000c31c00003a1d00003f1d0000501d0000521d0000551d0000671d0000d81d0000011e0000041e0000061e0000071e0000091e00000d1e00003b1e0000521e0000751e0000ae1e0000b41e0000c11e0000c21e0000c31e0000c41e0000c51e0000c61e0000c71e0000c81e0000c91e0000ca1e0000cb1e0000cc1e0000cd1e0000ce1e0000cf1e0000d01e0000d11e0000d21e0000d31e0000d41e0000d51e0000d61e0000d71e0000d81e0000da1e0000dd1e0000e61e0000e91e0000f51e0000fc1e0000fd1e0000001f0000031f00007a1f00007c1f0000821f0000a61f0000b91f0000302000003d200000492000005820000082200000832000008420000085200000a5200000be200000c4200000ce200000db200000e7200000eb200000152100007f2100008d210000972100009f210000a7210000b8210000e3210000022200000f2200001222000016220000582200005b2200007022000075220000782200007d2200008922000099220000a4220000ab220000b2220000be220000c9220000d5220000d6220000d72200001b2300001e2300001f23000023230000392300005a230000a7240000c1240000ea240000f924000008250000092500000a25000029250000a9250000cf250000e1250000662600006e260000d2260000b02800005131000070310000f6330000823400004c36000030390000743900009e390000a0390000f63b0000813d0000203e00004b3e0000cd3e0000f73e0000033f00000d3f00006f3f0000164000005b40000069400000f2410000d2420000eb4200002c4300004b430000584300005b4300005e4300009c4300009e4300009f430000b7430000d4430000094400001344000019440000c3440000994d0000804f0000b44f0000b3510000b5510000fb51000005520000b75300009e550000b2550000de5500000156000021560000fc5600006f5700007057000087570000dd570000fc570000105800002658000063580000dd5800000a5900001759000063590000925b0000ea5b0000fe5c000086600000a2640000136b0000306b00004f6f000023700000cc700000ba7100000473000057730000ee7300006e740000a8740000117500009e750000407600006a8800007a8800009388000015890000fc890000a58a0000b38a0000b68a0000b78a0000978b0000ac8b00003b8c0000758c0000a78c0000db8c0000138d0000728d0000548e0000a38e0000e68e0000128f0000f28f0000f88f0000fa8f0000109400005c9600005d960000ee990000ef990000f0990000f1990000f2990000f3990000f4990000f5990000f6990000689a00006a9a00006c9a00006e9a00006f9a00001e9d00004e9d00007b9d00000a9e00007d9e0000c49e0000f49e0000289f00008d9f0000919f0000b29f00001da000003fa0000073a0000097a0000009a100002ea1000045a1000074a10000cea10000d6a10000fba10000fca1000046a2000047a2000049a2000050a20000aca20000bca2000009a3000045a3000054a300005aa30000aba30000b5a3000020a4000021a4000025a4000027a400002ea40000bda500000ea6000011a600002da6000066a60000c1b100007fb20000beb20000cfb20000");
        private static readonly byte[] KnownContactsBlock = StringUtils.hexStringToBytes("0700000001000800000000000a00000003000d00000000001c0000000100");
        internal const int PreAuthReceiveTimeoutMs = 30_000;
        internal const int SendTimeoutMs = 30_000;
        public const int MaxFrameLength = 8192;
		
		private int hashID;
		private TcpClient tcpClient;
		private volatile bool working;
		private bool waitingForWorld;
		private UInt32 userID;
        private UInt32 newCharID;
        private uint reservedCreationID;
        private ushort numCharacterReplies;
        private bool isNewCreatedChar;
        private MarginServer ownerServer;
        private Func<UInt32, int?> accountStatusCheckForTesting;
        private readonly object sendLock = new object();
        public List<MarginAbilityItem> Abilities;
		
		private MarginEncryption marginEncr;
		  
		private NetworkStream clientStream;
        public int marginThreadId;

        public MarginClient(int hashID){
			
			this.hashID = hashID;
			
			working = true;
			waitingForWorld = false;
			
			// One random session key per margin connection (one login), not the old fixed one.
			marginEncr = MarginEncryption.ForNewSession();
			 
		}

        internal void AttachOwner(MarginServer server)
        {
            ownerServer = server;
        }

        internal void SetAccountStatusCheckForTesting(Func<UInt32, int?> check)
        {
            accountStatusCheckForTesting = check;
        }

        internal void SetClaimedCharId(UInt32 charId)
        {
            newCharID = charId;
        }


		public int getID(){
			return hashID;
		}

        public UInt32 getCharID()
        {
            return newCharID;
        }

        /// <summary>True once this connection presented a verified ticket and loaded (or created) one of its own characters.</summary>
        public bool HoldsVerifiedCharacter(UInt32 charId)
        {
            return working && userID != 0 && charId != 0 && newCharID == charId;
        }

        /// <summary>This login's session key (sent to the client under its account key), for the world's UDP session.</summary>
        public byte[] SessionKey => marginEncr.getTFKey();
		
		public bool isWorking(){
			return working;
		}
		
		public void passiveClose(){
			working = false;
		}
		
		public void forceClose(){
			working = false;
			try { tcpClient?.Close(); } catch { }
			try { clientStream?.Close(); } catch { }
		}
		
		public bool isYourClientWaiting(UInt32 waitingID){
			if (userID==waitingID){
				waitingForWorld = false;
				return true;
			}
			return false;
		}
			
		public void HandleClientComm(object client){
            try
            {
                tcpClient = (TcpClient)client;
                if (!working) return;
                if (tcpClient.ReceiveTimeout == 0)
                {
                    try { tcpClient.ReceiveTimeout = PreAuthReceiveTimeoutMs; } catch { }
                }
                if (tcpClient.SendTimeout == 0)
                {
                    try { tcpClient.SendTimeout = SendTimeoutMs; } catch { }
                }
                clientStream = tcpClient.GetStream();

                // Reassemble variable-length TCP frames across arbitrary TCP read boundaries (fragmented or coalesced),
                // and allow frames up to MaxFrameLength (covering 4092-byte encrypted character creation payloads).
                byte[] message = new byte[4096];
                AuthFrameBuffer frameBuffer = new AuthFrameBuffer(MaxFrameLength);
                List<byte[]> frames = new List<byte[]>(2);
                int bytesRead;

                Output.WriteLine("Margin client Connected.");

                // Receive TCP margin packets from the connected client.
                while (working){
                    bytesRead = 0;

                    try{
                        bytesRead = clientStream.Read(message, 0, message.Length);
                    }
                    catch{ break; }

                    if (bytesRead == 0){
                        Output.OptWriteLine("Margin: 0 Bytes received");
                        break;
                    }

                    // Reassemble and dispatch each complete Margin frame in arrival order.
                    try{
                        frames.Clear();
                        frameBuffer.Append(message, 0, bytesRead, frames);
                        for (int i = 0; i < frames.Count && working; i++)
                        {
                            packetHandler(frames[i], clientStream);
                        }
                        frames.Clear();
                    }catch(MarginException marEx){
                        Output.WriteLine(marEx);
                        break;
                    }catch(Exception ex){
                        Output.WriteLine("Margin client dropped (" + ex.GetType().Name + "): " + ex.Message);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine("Margin client setup failed (" + ex.GetType().Name + "): " + ex.Message);
            }
            finally
            {
                working = false;
                (ownerServer ?? Store.margin)?.UnregisterClient(this);
                Output.WriteLine("Margin Client Disconnected");
                forceClose();
            }
		}	
		
		
		public void packetHandler(byte[] packet, NetworkStream client )
        {
            if (packet == null || packet.Length < 3)
            {
                working = false;
                forceClose();
                return;
            }
            bool encrypted = true;
            byte opcode = packet[2];
            byte[] data = { };
			byte pointer = packet[0];
			
            // Only the initial pre-auth CertConnectRequest (0x01, 2-byte length 0x81xx) is unencrypted.
            // Checking userID == 0 prevents a 1-in-256 random-IV collision on 256..511 byte encrypted frames.
            if (userID == 0 && pointer == 0x81 && opcode == 0x01)
            {
                encrypted = false;
                data = packet;
            }

            if (encrypted)
            {
                byte[] encryptedPacket = { };
                if (packet[0] >= 0x80)
                {
                    // try to readjust the packet for one byte less if the lenght is too long
                    // just a crappy way
                    encryptedPacket = new byte[packet.Length-1];
                    ArrayUtils.copy(packet,1,encryptedPacket,0,packet.Length-1);
                }
                else{
                    encryptedPacket = packet;
                }
                // Get the IV from encrypted Packet and set it in encryptor/decryptor
                byte[] decrypted = marginEncr.decryptMargin(encryptedPacket);
	            if (decrypted == null)
	            {
		            // This shouldnt happen - but can happen
		            working = false;
		            forceClose();
                    return;
	            }
                // Creation opts into exact declared payload; do not weaken its body parser or alter other Margin paths.
                if(decrypted.Length>0&&(decrypted[0]==0x0a||decrypted[0]==0x0c))
                {
                    if(!marginEncr.TryDecryptCreationBody(encryptedPacket,out decrypted))
                    {working=false;forceClose();return;}
                }
                // Just 2 zero bytes for opcode handling later (As we use third byte for both state, encrypted or not
                byte[] spacer = { 0x00, 0x00 };

                DynamicArray din = new DynamicArray();
				din.append(spacer);
				din.append(decrypted);
				
                data = din.getBytes(); 

            }else{
                data = packet;
            }
            Output.WritePacketLog(data, "MARGINCLIENT", "0", "0", "0");

	        if (data.Length >= 3)
	        {
				opcode = data[2];
				
				//TODO: check if this needs "packet" or "data"
				switch (opcode)
				{
					case 0x01:
						certConnectRequest(packet, client);
						
					break;
	
					case 0x03:
						certConnectReply(packet, client);
					
					break;
	
					case 0x06:
						connectChallenge(packet, client);
					
					break;
	
					case 0x08:
						ConnectChallengeResponse(packet, client);
					break;
					
					case 0x0a:
						//CharNameRequest
						charNameRequest(data,client);
					break;
					
					case 0x0c:
						//TODO: this is creation. must be done someday
						Output.writeToLogForConsole("CREATECHAR RSI VALUES:"+ StringUtils.bytesToString(data));
						//loadCharacter(data, client);
						createCharacterRSI(data, client);
						// Add the first abilitys
						// AbilityID : 2147485696 (Awakened) Level 2
						// AbilityID : 
						
					break;
	
					case 0x0d:
						// Delete Charname Request
						deleteCharName(data, client);
					break;
					
					case 0x0f:
						loadCharacter(data, client, 0);
					break;
				}
	        }
        }



        private void SendMarginCharData(byte[] data, byte opcode, NetworkStream client, UInt16 shortAfterId, bool isLast, bool emptyDataBlock = false)
        {
            // This Method will be used to send the real Data to the client

            // Format for general LoadCharacterReply Responses:
            // 10 00 00 00 <- everytime the same
            // 00 f9 76 04 <- uint32 Char ID 
            // 00 00 00 09 <- uint32 number of Character Replys (just raise one up)
            // 00 05 <- viewData opcode (what data is inside) uint16 
            // 10 00 <- if it has Data it is 10 00 if not is just 00 00 (and no Data can follow up and packet ends here)
            // e1 00 <- short / uint16 data size
            // DATA
            numCharacterReplies++;
            
            PacketContent pak = new PacketContent();
            pak.AddByte(0x10);
            pak.AddUint32(0,1);
            pak.AddUint32(newCharID, 1);
            pak.AddUint16(shortAfterId,1);
            pak.AddUShort(numCharacterReplies);
            if (isLast)
            {
	            pak.AddUShort(1);
            }
            else
            {
	            pak.AddUShort(0);
            }
            pak.AddUShort(opcode);
            if (data.Length > 0 || emptyDataBlock)
            {
	            pak.AddByteArray(new byte[]{0x10, 0x00});
	            pak.AddUint16((UInt16)data.Length,1);
	            pak.AddByteArray(data);
            }
            else
            {
	            pak.AddByteArray(new byte[]{0x00, 0x00});
            }

            if (opcode == QaCharacterUi.DataType)
                Output.WriteDebugLog($"[MARGIN SERVER RESPONSE] character UI chunk {numCharacterReplies}, {data.Length} bytes");
            else
                Output.WriteDebugLog("[MARGIN SERVER RESPONSE] for OPCODE " + opcode + " : " + StringUtils.bytesToString(pak.ReturnFinalPacket()));
            byte[] encryptedResponse = marginEncr.encrypt(pak.ReturnFinalPacket());
            sendTCPVariableLenPacket(encryptedResponse, client);
        }

		private void sendMarginData(string data,NetworkStream client){
			byte[] response = StringUtils.hexStringToBytes(data);
            byte[] encryptedResponse = marginEncr.encrypt(response);
            sendTCPVariableLenPacket(encryptedResponse, client);
		}

        private void deleteCharName(byte[] packet, NetworkStream client)
        {
            // Response should something like : 0e 44 87 1f 00 00 00 00 00 00 00 00 00
            
            // Get the charId from the packet
            byte[] charIDB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            ArrayUtils.copy(packet, 5, charIDB, 0, 8); //Offset for charID is 5 in request
            
            // Need for SQL Delete
            UInt64 charID = NumericalUtils.ByteArrayToUint32(charIDB, 1);
            if (!OwnsCharacter((uint) charID))
            {
                Output.WriteLine("Margin: refused to delete character " + charID + " for account " + userID);
                working = false; forceClose(); return;
            }
            Store.dbManager.MarginDbHandler.deleteCharacter(charID);

            // Create viewData
            string deleteRequestResponse = "0e" + StringUtils.bytesToString_NS(charIDB) + "0000000000000000";
            sendMarginData(deleteRequestResponse, client);
        }
		

        private void createCharacterRSI(byte[] packet,NetworkStream client)
        {
            // Complete native shape/assets/trait validation before the first creation write.
            byte[] body=packet.Length>=2?packet.AsSpan(2).ToArray():Array.Empty<byte>();
            if(userID==0||reservedCreationID==0||reservedCreationID!=newCharID||
                !CharacterCreation.TryParse(body,out var request)||!CharacterCreation.TryResolve(request,DataLoader.getInstance(),out var state))
            {working=false;forceClose();return;}
            try
            {
                if(!Store.dbManager.MarginDbHandler.CompleteCharacterCreation(reservedCreationID,userID,state))
                {working=false;forceClose();return;}
            }
            catch(Exception e)
            {
                Output.WriteDebugLog("[CREATION] completion rolled back char="+reservedCreationID+": "+e);
                working=false;forceClose();return;
            }
            reservedCreationID=0;isNewCreatedChar=true;
            loadCharacter(packet,client,newCharID);
        }

		private void charNameRequest(byte[] packet,NetworkStream client){
            if(userID==0||packet.Length<8||packet[3]!=3||packet[4]!=0) {working=false;forceClose();return;}
            int nameLength=packet[5]|packet[6]<<8;
            if(nameLength<2||nameLength>32||packet.Length!=7+nameLength||packet[packet.Length-1]!=0||Array.IndexOf(packet,(byte)0,7,nameLength-1)>=0)
            {working=false;forceClose();return;}

			
			// Get the handle text
			byte[] handleSize = new byte[2];
			handleSize[0] =packet[5];
			handleSize[1] =packet[6];
			
			int size = NumericalUtils.ByteArrayToUint16(handleSize,1);
			
			byte[] handleB = new byte[size-1];
			ArrayUtils.copy(packet,7,handleB,0,size-1);
			string handleBStr = StringUtils.bytesToString_NS(handleB); // Handle as "414141"
			string handleStr = StringUtils.charBytesToString(handleB); // Handle as "AAA"
			
			// Process DB to answer the client - we directly create the character
            uint dbResult;
            try{dbResult=Store.dbManager.MarginDbHandler.getNewCharnameID(handleStr,userID);}
            catch(Exception e)
            {
                Output.WriteDebugLog("[CREATION] reservation rolled back account="+userID+": "+e);
                working=false;forceClose();return;
            }

            // Add the new charId so that we can work with it

			// Create the answer
			string nameRequestResponse="";
			
			if(dbResult==0){
				nameRequestResponse += "0b0f00010000110000000000000000";
				nameRequestResponse += StringUtils.bytesToString_NS(handleSize);
				nameRequestResponse += handleBStr+"00";
			}else{
				
				newCharID = dbResult;
                reservedCreationID = dbResult;
				nameRequestResponse += "0b0f0000000000";
				nameRequestResponse += StringUtils.bytesToString_NS(NumericalUtils.uint32ToByteArray(dbResult,1));
				nameRequestResponse += "0000000000";
				nameRequestResponse += StringUtils.bytesToString_NS(handleSize);
				nameRequestResponse += handleBStr+"00";
			
			}

            Output.writeToLogForConsole(nameRequestResponse);
			sendMarginData(nameRequestResponse,client);
			
		}

        private byte[] loadBackgroundInfo(int charID){
	        Character marginCharacter = Store.dbManager.MarginDbHandler.GetCharInfo(charID);


            PacketContent pak = new PacketContent();
            byte[] appearance = isNewCreatedChar ? null : CharacterAppearance.Build(
                Store.dbManager.MarginDbHandler.LoadAppearance(charID), DataLoader.getInstance().newRSIItemsDb);
            pak.AddByteArray(CharacterPersonality.BackgroundPrefix(!isNewCreatedChar, marginCharacter.TraitId, appearance));
            pak.AddStringWithFixedSized(marginCharacter.FirstName,31);
            pak.AddStringWithFixedSized(marginCharacter.LastName,31);
            pak.AddStringWithFixedSized(marginCharacter.Background,1023);


            pak.AddUint32((uint) marginCharacter.Exp,1);
            pak.AddUint32((uint) marginCharacter.Cash,1);
            pak.AddUShort(0x01); // Flag - always 1
            pak.AddUint32(200,1); // CQ Points
            
            pak.AddByteArray(TimeUtils.getCurrentTime());
            //pak.addByteArray(StringUtils.hexStringToBytes("875D714A")); // Last Changed Timestamp - current its static - needs to be dynamic

            // Signed standings in the order read by client.dll (0x6219902b and 0x621992aa).
            pak.AddInt16(unchecked((short)marginCharacter.RepCyph), 1);
            pak.AddInt16(unchecked((short)marginCharacter.RepMachine), 1);
            pak.AddInt16(unchecked((short)marginCharacter.RepMero), 1);
            pak.AddInt16(unchecked((short)marginCharacter.RepNiobe), 1);
            pak.AddInt16(unchecked((short)marginCharacter.RepEpn), 1);
            pak.AddInt16(unchecked((short)marginCharacter.RepZion), 1);
            pak.AddInt16(unchecked((short)marginCharacter.RepGm), 1);
            pak.AddByte(MissionContacts.StoryContactMask);
            
            // 01 = onWorld , 00 = LA . In World you have to take care to not spawn the Character in LA State
            if (marginCharacter.DistrictId != (uint)MxOLocations.LA)
            {
                pak.AddByte(0x01);
            }
            else
            {
                pak.AddByte(0x00);
            }

            pak.AddHexBytes("0301310000b402320000b403380000b4");
            pak.AddByteArray(CharacterAttributes.BuildSpentPoints(LoadAttributes(charID, marginCharacter),marginCharacter.TraitId));
            // TheLast Part

	        #if DEBUG
            if (isNewCreatedChar)
            {
                Output.WriteLine("Load Background Reply for Created Char:\n" + pak.ReturnFinalPacket());
            }
			#endif

            return pak.ReturnFinalPacket();
            
        }

        /// <summary>
        /// The character's attribute rows. A level 1 character from before attributes were saved has nothing invested,
        /// so it gets the starting rows now. Higher characters without rows keep the old fixed values and list.
        /// </summary>
        private Dictionary<byte, UInt16> LoadAttributes(int charID, Character character)
        {
            Dictionary<byte, UInt16> attributes = Store.dbManager.MarginDbHandler.LoadCharacterAttributes(charID);
            CharacterPersonality.Resolve(character.TraitId);
            if(character.TraitId.HasValue&&attributes.Count!=5)throw new InvalidOperationException("Assigned character trait has incomplete attribute rows");
            if (attributes.Count == 0 && character.Level <= 1)
            {
                Store.dbManager.MarginDbHandler.SetStartingAttributes((UInt32)charID);
                attributes = Store.dbManager.MarginDbHandler.LoadCharacterAttributes(charID);
            }
            return attributes;
        }

        public byte[] loadInventory(int charID)
        {
            List<MarginInventoryItem> Inventory = new List<MarginInventoryItem>();
            Inventory = Store.dbManager.MarginDbHandler.LoadInventory(charID);

            DynamicArray din = new DynamicArray();
            foreach (MarginInventoryItem item in Inventory)
            {
                din.append(NumericalUtils.uint16ToByteArrayShort(item.getSlot())); // slot
                if (AbilityProgression.IsAbilityCode(item.getItemID()))
                {
                    // A bought ability that is not uploaded yet: item 46 with the ability as its descriptor, as in the 5e.
                    din.append(NumericalUtils.uint32ToByteArray(AbilityProgression.AbilityCodeItem, 1));
                    din.append(NumericalUtils.uint32ToByteArray(AbilityProgression.AbilityCodeValue(item.getItemID()), 1));
                    continue;
                }
                din.append(NumericalUtils.uint32ToByteArray(item.getItemID(),1));

                din.append(0x00);
                din.append(0x00);
                // Stack size as (count - 1) * 4, the same amount byte as the 5e item add (see ItemStacks).
                din.append(ItemStacks.AmountByte(ItemStacks.CountFromDb(item.getAmount())));
                din.append(0x0c); // ToDo : purity
                
            }
            return din.getBytes();
        }

        
        public void loadAbilities(int charId)
        {
            Abilities = Store.dbManager.MarginDbHandler.LoadAbilities(charId);
        }


        public byte[] loadKnownAbilities()
        {

            DynamicArray din = new DynamicArray();

            foreach (MarginAbilityItem ability in Abilities)
            {   
                Int32 finalAblityID = ability.getAbilityID() + ability.getLevel();
             
                string debugAbility = StringUtils.bytesToString(NumericalUtils.int32ToByteArray(finalAblityID, 1));
                din.append(NumericalUtils.uint16ToByteArray(ability.getSlot(),1)); // slot
                din.append(NumericalUtils.int32ToByteArray(finalAblityID, 1));
            }

            return din.getBytes();
        }

        public byte[] loadEquippedAbilities()
        {
            DynamicArray din = new DynamicArray();

            //AbilityItem abTemp = AbilityDB.Find(delegate(AbilityItem a) { return a.getAbilityID() == id; });
            

            foreach (MarginAbilityItem ability in Abilities)
            {
                if (ability.getLoaded())
                {
                    Int32 finalAblityID = ability.getAbilityID() + ability.getLevel();
                    din.append(NumericalUtils.uint16ToByteArray(ability.getSlot(), 1)); // slot
                    din.append(NumericalUtils.int32ToByteArray(finalAblityID, 1));
                }
                
            }
            return din.getBytes();
        }

		
        private void loadCharacter(byte[] packet, NetworkStream client, UInt32 theCharId)
        {
            if (theCharId == 0)
            {
                if (packet == null || packet.Length < 7)
                {
                    working = false; forceClose(); return;
                }
                byte[] charIDB = new byte[4];
                ArrayUtils.copy(packet, 3, charIDB, 0, 4); //Offset for charID is 3 in request
                uint charId = NumericalUtils.ByteArrayToUint32(charIDB, 1);
                // Only this account's own characters, and only after a verified ticket.
                if (!OwnsCharacter(charId))
                {
                    Output.WriteLine("Margin: refused to load character " + charId + " for account " + userID);
                    working = false; forceClose(); return;
                }
                // Atomically check and claim charId so two simultaneous logins cannot race and dead
                // disconnected Margin sessions cannot lock out a character.
                MarginServer server = ownerServer ?? Store.margin;
                if (server != null)
                {
                    if (!server.TryClaimCharacter(this, charId))
                    {
                        CharacterAlreadyInUseReply(client);
                        return;
                    }
                }
                else
                {
                    newCharID = charId;
                }
            }


            byte[] knownHardlines = HardlineRegistration.KnownCache(newCharID);
            byte[] empty = Array.Empty<byte>();

            // New Margin Method to send Data

            // Pre-Load Abilities so that we just need to filter the result later
            loadAbilities((int)newCharID);

            // Stock 7.6005 dispatches DataType 0x0c to 0x43ffc6. It places each
            // chunk at (reply ordinal - 1) * 1000, so UI chunks lead the sequence.
            byte[] qaUi = QaCharacterUi.Read(userID, newCharID);
            if (qaUi.Length != 0)
            {
                numCharacterReplies = 0;
                for (int offset = 0; offset < qaUi.Length; offset += QaCharacterUi.ChunkSize)
                {
                    int count = Math.Min(QaCharacterUi.ChunkSize, qaUi.Length - offset);
                    SendMarginCharData(qaUi.AsSpan(offset, count).ToArray(), QaCharacterUi.DataType, client, 0, false);
                }
            }
            
            SendMarginCharData(empty, 0x01, client, 0, false);
            SendMarginCharData(loadBackgroundInfo((int)newCharID), 0x02, client,0,false);
            SendMarginCharData(empty, 0x03, client,0, false);                            // BuddyList (but old one)
            SendMarginCharData(empty, 0x04, client,0, false);                            // Unknown - no one has data there so ignore it
            SendMarginCharData(loadInventory((int)newCharID), 0x05, client,0, false);            // Inventory
            //sendMarginCharData(StringUtils.hexStringToBytes(itemData), 0x05, client);            // Inventory CR1
            SendMarginCharData(loadEquippedAbilities(), 0x06, client,0, false);          // Loaded Abilitys
            //sendMarginCharData(empty, 0x06, client);                          // Loaded Abilitys CR1
            SendMarginCharData(loadKnownAbilities(), 0x07, client,0, false);             // Known Abilities
            SendMarginCharData(knownHardlines, 0x08, client,0, false);                   // Hardlines
            SendMarginCharData(empty, 0x09, client,0, false);                            // Access Nodes?
            //sendMarginCharData(codeArchiveTest, 0x0a, client);                // Code Storage
            SendMarginCharData(Crafting.BuildKnownRecipesBlock(KnownRecipesBaseBlock,
                Store.dbManager.WorldDbHandler.GetLearnedRecipes(newCharID)), 0x0a, client,0, false);   // Known code recipes
            SendMarginCharData(KnownContactsBlock, 0x0b, client,0, false);                    // Contacts
            SendMarginCharData(empty, 0x0e, client,0, false);

            // MotD has a special handling: reuse already-loaded config rather than re-reading Config.xml on every login.
            string motd = Store.config?.dbParams?.Motd;
            if (motd == null)
            {
                DbParams _dbParams;
                XmlParser.loadDBParams("Config.xml", out _dbParams);
                motd = _dbParams?.Motd ?? "";
            }

			if (!motd.Equals("")){ // if MOTD not empty
				DynamicArray announcement = new DynamicArray();
				byte[] currentTime = TimeUtils.getUnixTime();
				//Load motd from file
				byte[] text = StringUtils.stringToBytes(motd);
				announcement.append(currentTime);
				announcement.append(text);
				
				SendMarginCharData(announcement.getBytes(), 0x0d, client, 10000, true);
			}
			else
			{
				// Without a MOTD nothing above is flagged as the last reply, and the client waits
				// at "Loading Character" forever. Retail ends the load with an empty 0x0f reply
				// flagged as last (10 00 00 00 00 <charId> 10 27 0d 01 0f 10 00 00 00 in the
				// RajkoPacketsNearTheEnd captures).
				SendMarginCharData(empty, 0x0f, client, 10000, true, emptyDataBlock: true);
			}
			/* End of the MOTD */

        }
		
		public void waitForWorldReply(){
			waitingForWorld = true;
			
			while(waitingForWorld){
				Thread.Sleep(25);
			}
			
		}

        public void EstablishUdpSessionReply(byte[] packet, NetworkStream client){
            Console.WriteLine("Establish UDP Session Reply for CharID:" + newCharID);
            byte[] response = { 0x11, 0x00, 0x00, 0x00, 0x00, 0x01 };
            byte[] encryptedResponse = marginEncr.encrypt(response);
            sendTCPVariableLenPacket(encryptedResponse, client);
        }

        private bool OwnsCharacter(uint charId)
        {
            if (userID == 0) return false;
            try
            {
                return AccountStore.FromConfig().CharacterBelongsTo(charId, userID);
            }
            catch (Exception e)
            {
                Output.WriteLine("Margin: character ownership check failed (" + e.GetType().Name + ")");
                return false;
            }
        }

        public void CharacterAlreadyInUseReply(NetworkStream client)
        {
            byte[] response = {0x10, 0x25, 0x00, 0x00, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
            byte[] encryptedResponse = marginEncr.encrypt(response);
            sendTCPVariableLenPacket(encryptedResponse, client);
        }

        public void EstablishUDPSessionReplyExternal(){
            NetworkStream stream = clientStream;
            if (!working || stream == null) return;
            Console.WriteLine("Establish UDP Session Reply for CharID:" + newCharID);
            byte[] response = { 0x11, 0x00, 0x00, 0x00, 0x00, 0x01 };
            try
            {
                byte[] encryptedResponse = marginEncr.encrypt(response);
                sendTCPVariableLenPacket(encryptedResponse, stream);
            }
            catch (Exception ex)
            {
                Output.WriteDebugLog("Margin: EstablishUDPSessionReplyExternal failed for charId " + newCharID + ": " + ex.Message);
            }
        }

        public void ConnectChallengeResponse(byte[] packet, NetworkStream client)
        {
            // just accept the packet everytime
            // it seems that it has a 16 byte blob which could be encrypted but dunno if we will ever need it again
            byte[] response = HardlineRegistration.LoginReply();
            byte[] encryptedResponse = marginEncr.encrypt(response);
            sendTCPVariableLenPacket(encryptedResponse, client);
        }

        public void connectChallenge(byte[] packet, NetworkStream client)
        {

            byte[] response = { 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x01, 0x00 };
            byte[] encryptedResponse = marginEncr.encrypt(response);
            sendTCPVariableLenPacket(encryptedResponse, client);
        }

        public void certConnectReply(byte[] packet, NetworkStream client)
        {
            // Just send the viewData
            byte[] response = { 0x04, 0x00, 0x00, 0x00, 0x00 };
            byte[] encryptedResponse = marginEncr.encrypt(response);
            sendTCPVariableLenPacket(encryptedResponse, client);

        }

        public void certConnectRequest(byte[] packet, NetworkStream client)
        {
            if (packet == null || packet.Length < 7)
            {
                Output.WriteLine("Margin: refused a short ticket frame");
                working = false; forceClose(); return;
            }
            int firstnum = BitConverter.ToInt16(packet, 3);
            int authstart = BitConverter.ToInt16(packet, 5);
            if (firstnum != 3)
            {
                //showMarginDebug("FirstNum is not 3, it is" + firstnum);
            }

            if (authstart != 310)
            {
                //showMarginDebug("AuthStart is not 0x3601");

            }

            // `<frame u16> 01 03 00 <ticket size u16> <signature 128> <signed data 182>`. Only a ticket the auth server
            // signed is accepted: the user id and account key come from it, so an unchecked one let anyone pick an
            // account. Expiry has a grace period (AuthTicketCodec.GraceSeconds) because clients reuse the ticket.
            int ticketSize = authstart;
            if (ticketSize != AuthTicketCodec.SignatureSize + AuthTicketCodec.SignedDataSize || packet.Length < 7 + ticketSize)
            {
                Output.WriteLine("Margin: refused a malformed ticket");
                working = false; forceClose(); return;
            }
            byte[] signature = new byte[AuthTicketCodec.SignatureSize];
			ArrayUtils.copy(packet, 7, signature, 0, signature.Length);
            byte[] signedData = new byte[AuthTicketCodec.SignedDataSize];
            ArrayUtils.copy(packet, 7 + signature.Length, signedData, 0, signedData.Length);

            if (!AuthTicketCodec.TryVerify(signature, signedData, TimeUtils.getUnixTimeUint32(), out VerifiedTicket ticket))
            {
                Output.WriteLine("Margin: refused a ticket the auth server didn't issue (bad signature or expired)");
                working = false; forceClose(); return;
            }
            int? accountStatus;
            try
            {
                accountStatus = accountStatusCheckForTesting != null ? accountStatusCheckForTesting(ticket.UserId) : AccountStore.FromConfig().GetStatus(ticket.UserId);
            }
            catch (Exception e)
            {
                Output.WriteLine("Margin: account check failed (" + e.GetType().Name + ")");
                working = false; forceClose(); return;
            }
            if (accountStatus != AccountLogin.StatusActive)
            {
                Output.WriteLine("Margin: refused account " + ticket.UserId + " (missing, banned or disabled)");
                working = false; forceClose(); return;
            }
			userID = ticket.UserId;
            // Authenticated retail clients keep Margin TCP open quietly while playing in World;
            // clear the pre-auth handshake timeout now that the ticket is verified.
            try { if (tcpClient != null) tcpClient.ReceiveTimeout = 0; } catch { }

            // Stripout Exponent and Modulus

            byte[] exponent = {0x00, 0x00, 0x00, 0x11};
            byte[] modulus = ticket.PublicModulus;
            
            // Init our encryptor with users modulus and exponent
            marginEncr.setUserPubKey(exponent, modulus);
            // build the final packet, it consists of 1 byte 0x00 + twofish key for world and margin + encryptIV

            byte[] encryptMeResponse = new byte[33];
            encryptMeResponse[0] = 0x00;
			
			ArrayUtils.copy(marginEncr.getTFKey(),0,encryptMeResponse,1,16);
			ArrayUtils.copy(marginEncr.getIV(),0,encryptMeResponse,17,16);
            
            byte[] encryptedShit = { };
            encryptedShit = marginEncr.encryptUsersPublic(encryptMeResponse);

            byte[] blobSize = NumericalUtils.uint16ToByteArray((UInt16)encryptedShit.Length,1);

            byte[] header = { 0x02, 0x03, 0x00 };
            // Write final packet
			
			DynamicArray din = new DynamicArray();
            din.append(header);
			din.append(blobSize);
			din.append(encryptedShit);
            
			sendTCPVariableLenPacket(din.getBytes(), client);
			
        }

        public void sendTCPVariableLenPacket(byte[] packet, NetworkStream client)
        {
            int len = packet.Length;
            byte[] finalPacket;
            if (len > 127)
            {
                finalPacket = new byte[2 + len];
                ushort header = (ushort)(len + 0x8000);
                finalPacket[0] = (byte)(header >> 8);
                finalPacket[1] = (byte)(header & 0xFF);
                Buffer.BlockCopy(packet, 0, finalPacket, 2, len);
            }
            else
            {
                finalPacket = new byte[1 + len];
                finalPacket[0] = (byte)len;
                Buffer.BlockCopy(packet, 0, finalPacket, 1, len);
            }

            lock (sendLock)
            {
                try
                {
                    client.Write(finalPacket, 0, finalPacket.Length);
                    client.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR ex: " + ex);
                    throw;
                }
            }
        }

		
	}
	
}
