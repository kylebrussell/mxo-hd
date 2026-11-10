using System;
using System.Collections;
using hds.shared;

namespace hds{


	public class PacketsUtils{
		
		public static byte[]getRSIBytes(int[] rsiValues){
			
			Hashtable rsiTable = new Hashtable();
			
			string[] keys = {"sex","body","hat","face","shirt","coat","pants","shoes","gloves","glasses","hair","facialdetail","shirtcolor","pantscolor","coatcolor","shoecolor","glassescolor","haircolor","skintone","tattoo","facialdetailcolor","leggins"};
			
			for (int i = 0;i<keys.Length;i++){
				rsiTable[keys[i]] = rsiValues[i];	
			}
			
			/* ORDER:
			 * sex body	hat face shirt coat	pants shoes	gloves glasses 
			 * hair	facialdetail shirtcolor pantscolor coatcolor shoecolor 
			 * glassescolor haircolor skintone tatto facialdetailcolor leggins */
			
			string unknown1 = "0";
			string unknown2 = "00";
			
			string binaryRSI = "";
			
			// Common part
			
			binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["sex"],1);
			binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["body"],2);
			binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["hat"],6);
			binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["face"],5);
			binaryRSI +=unknown1;
			binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shirt"],5);
			
			
			if (rsiValues[0]==0){// Male part
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["coat"],6);	
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["pants"],5);	
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shoes"],6);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["gloves"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["glasses"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["hair"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["facialdetail"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shirtcolor"],6);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["pantscolor"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["coatcolor"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shoecolor"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["glassescolor"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["haircolor"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["skintone"],5);
				binaryRSI +=unknown2;
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["tattoo"],3);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["facialdetailcolor"],3);
				binaryRSI +="000000";
			}
			else{// Female part
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["coat"],5);	
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["pants"],5);	
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shoes"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["gloves"],6);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["glasses"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["hair"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["leggins"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["facialdetail"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shirtcolor"],6);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["pantscolor"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["coatcolor"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["shoecolor"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["glassescolor"],4);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["haircolor"],5);
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["skintone"],5);
				binaryRSI +=unknown2;
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["tattoo"],3); 
				binaryRSI +=NumericalUtils.intToBitsString((int)rsiTable["facialdetailcolor"],3);
				binaryRSI +="000";
			}
			
			
			return NumericalUtils.bigBinaryStringToBytes(binaryRSI);
		}



        public static byte[] createRpcPacket(ArrayList contents, bool timedRPC, WorldClient client)
        {
			
			RPCPacket rpcp = new RPCPacket(client);
			
			for (int i = 0;i<contents.Count;i++){
				rpcp.appendMsgBlock((byte[])contents[i]);
			}
					
			return rpcp.getBytesWithHeader(timedRPC);
		}
		
		public static byte[] createBigRpcPacket(ArrayList[] contents,bool timedRPC,WorldClient client){
			RPCPacket rpcp = new RPCPacket(client);
			
			// Max size is 4
			
			for (int i = 0; i<contents.Length;i++){
				RPCPacket temp = new RPCPacket(client);
				for(int j = 0;j<contents[i].Count;j++){
					temp.appendMsgBlock((byte[])contents[i][j]);
				}
				rpcp.appendRpc(temp);
			}
			
			return rpcp.getBytesWithHeader(timedRPC);
			
		}

        public static byte[] createVendorPacket(int id, WorldClient client)
        {
			//ToDo: Near check as it should contain the size if i am right
			
			ArrayList content = new ArrayList();
			content.Add(StringUtils.hexStringToBytes("4a810d7cadd94300000000a0b7f6c000000000003c9440000000000051c8c020000a0000140080002000800018008000040080002c008000f41180009806800090078000c4068000640680"));
            byte[] packet = createRpcPacket(content, false, client);
					
			return packet;
		}
		
		public static byte[] createTeleportPacket(int x, int y, int z){
			// Adjust coords between human view and mxo format
			return createTeleportPacket(x*100.0f, y*100.0f, z*100.0f);
		}

		/// <summary>No nav polygon: the client then searches about 100 units around the position itself.</summary>
		public const UInt16 NoPlace = 0xffff;

		/// <summary>
		/// Own-view teleport, as retail's record (about 1,700 of them in the captures): view 2, parts 01, flags 0x1c, then
		/// <c>&lt;teleport id u8&gt; &lt;sector u16&gt; &lt;region u16&gt; &lt;polygon u16&gt;</c> (flag 0x10), <c>&lt;yaw u8&gt;</c>
		/// (0x04, the client's own yaw echoed), <c>&lt;x y z f32&gt; &lt;server sim time ms u32&gt;</c> (0x08). The id counts up
		/// per teleport and re-sends repeat it. The client compares the place with the elevator panel's 81 3f (it closes the
		/// panel with 81 41 when they differ) and keeps the player on its nav mesh from it. The message queue ends the
		/// view list with 00 00.
		/// </summary>
		public static byte[] createTeleportPacket(float x, float y, float z, UInt16 sector, UInt16 region, UInt16 polygon, byte teleportId, byte yaw)
		{
			DynamicArray din = new DynamicArray();
			din.append(StringUtils.hexStringToBytes("0200011c"));
			din.append(teleportId);
			din.append(BitConverter.GetBytes(sector));
			din.append(BitConverter.GetBytes(region));
			din.append(BitConverter.GetBytes(polygon));
			din.append(yaw);
			din.append(NumericalUtils.floatToByteArray(x, 1));
			din.append(NumericalUtils.floatToByteArray(y, 1));
			din.append(NumericalUtils.floatToByteArray(z, 1));
			din.append(BitConverter.GetBytes((UInt32)(TimeUtils.GetSimTimeSeconds() * 1000)));
			return din.getBytes();
		}

		/// <summary>
		/// Where a teleport lands on the client's nav mesh: the polygon at the spot, else the nearest one within 4 m (a
		/// spot in a gap between prefabs, such as beside an elevator door). False when there's none.
		/// </summary>
		public static bool PlaceNear(UInt16 metrId, double x, double y, double z, out NavLocation at)
		{
			if (NavMesh.Locate(metrId, x, y, z, out at))
			{
				return true;
			}

			for (int radius = 50; radius <= 400; radius += 50)
			{
				for (int step = 0; step < 16; step++)
				{
					double angle = step * Math.PI / 8;
					if (NavMesh.Locate(metrId, x + radius * Math.Cos(angle), y, z + radius * Math.Sin(angle), out at))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>The next teleport id for this client: counts 1 to 255, never 0.</summary>
		private static byte NextTeleportId(WorldClient client)
		{
			if (client?.playerData == null)
			{
				return 1;
			}

			client.playerData.teleportCounter = (byte)(client.playerData.teleportCounter == 255 ? 1 : client.playerData.teleportCounter + 1);
			return client.playerData.teleportCounter;
		}

		/// <summary>
		/// Own-view teleport for this client, naming the nav mesh place it lands in (NavMesh). Off the mesh, the nearest
		/// place within 4 m; with none, no place (0xffff), never another sector's.
		/// </summary>
		public static byte[] createTeleportPacket(WorldClient client, float x, float y, float z)
		{
			NavLocation at = new NavLocation { sector = NoPlace, region = NoPlace, polygon = NoPlace };
			bool found = client?.playerData != null && PlaceNear((UInt16)client.playerData.getDistrictId(), x, y, z, out at);
			if (!found)
			{
				at = new NavLocation { sector = NoPlace, region = NoPlace, polygon = NoPlace };
				Output.WriteDebugLog("[TELEPORT] No nav polygon near " + (int)x + "," + (int)y + "," + (int)z);
			}

			byte yaw = client?.playerInstance != null ? client.playerInstance.YawInterval.getValue()[0] : (byte)0;
			return createTeleportPacket(x, y, z, at.sector, at.region, at.polygon, NextTeleportId(client), yaw);
		}

		public static byte[] createTeleportPacket(float x, float y, float z){
			return createTeleportPacket(Store.currentClient, x, y, z);
		}

		/// <summary>Retail's elevator ding after a ride: 81 42 with the destination's region and sector as user data.</summary>
		public static byte[] BuildElevatorDing(UInt16 region, UInt16 sector)
		{
			byte[] ding = { 0x81, 0x42, 0x2a, 0x06, 0x00, 0x22, 0, 0, 0, 0, 0, 0, 0, 0 };
			BitConverter.GetBytes(region).CopyTo(ding, 10);
			BitConverter.GetBytes(sector).CopyTo(ding, 12);
			return ding;
		}
		

       

        public static byte[] createSystemMessageWithoutRPC(string message)
        {
            byte[] messageBytes = StringUtils.stringToBytes(message + "\x00");

            
            UInt16 messageSize = (UInt16)(message.Length + 1);

            
            PacketContent packet = new PacketContent();
            packet.AddUShort((UInt16)RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
            packet.AddHexBytes("0700000000000000000024000000000000000000000000000000000000000000000000");
            packet.AddUint16(messageSize, 1);
            packet.AddByteArray(messageBytes);

            return packet.ReturnFinalPacket();
        }

		public static byte[] createSystemMessage(string message, WorldClient client){

			
			byte[] messageBytes = StringUtils.stringToBytes(message+"\x00");
					
			DynamicArray din = new DynamicArray();
					
			UInt16 messageSize = (UInt16) (message.Length+1);			
			byte[] messageSizeHex = NumericalUtils.uint16ToByteArray(messageSize,1);

            byte[] hexContents = StringUtils.hexStringToBytes("0700000000000000000024000000000000000000000000000000000000000000000000");
            
            din.append((byte)RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
			din.append(hexContents);
			din.append(messageSizeHex);
			din.append(messageBytes);
				
			
			return din.getBytes();
		}


        public static byte[] createMessage(string message, string type, WorldClient client){
            
            byte typeByte;
            switch (type){

                case "TEAM":
                    typeByte = 0x05;
                    break;
                case "CREW":
                    typeByte = 0x02;
                    break;

                case "FACTION":
                    typeByte = 0x03;
                    break;
                case "SYSTEM":
                    typeByte = 0x07;
                    break;
                case "MODAL":
                    typeByte = 0x17;
                    break;

                case "FRAMEMODAL":
                    typeByte = 0xd7;
                    break;

                case "BROADCAST":
                    typeByte = 0xc7;
                    break;
                case "AREA":
                    typeByte = 0x10;
                    break;
                default:
                    typeByte = 0x07;
                    break;
            }

            byte[] messageBytes = StringUtils.stringToBytes(message + "\x00");

            DynamicArray din = new DynamicArray();

            UInt16 messageSize = (UInt16)(message.Length + 1);

            byte[] messageSizeHex = NumericalUtils.uint16ToByteArray(messageSize, 1);
            byte[] hexContents = StringUtils.hexStringToBytes("00000000000000000024000000000000000000000000000000000000000000000000");



            din.append((byte)RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE); // Response header
            din.append(typeByte);
            din.append(hexContents);
            din.append(messageSizeHex);
            din.append(messageBytes);


            return din.getBytes();
        }
		
	}
}

