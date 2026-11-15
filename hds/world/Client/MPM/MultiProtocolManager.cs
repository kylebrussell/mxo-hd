using System;
using System.Collections;

using hds.shared;

namespace hds{


	public class MultiProtocolManager{

		private int offset;
		private byte[] buffer;
		
		private readonly Protocol03Manager man03;
		private readonly Protocol04Manager man04;

        public MultiProtocolManager() {

            offset = 0;
            buffer = null;
            man03 = new Protocol03Manager();
            man04 = new Protocol04Manager();

        }

		
		private void ParsePlainContent(ref byte[] packetData){
			
			Store.currentClient.messageQueue.addRawMessage(packetData);
		}
		
		private void ParseContent(ref byte[] packetData){

			buffer = null;
			buffer = packetData;
			offset = 0; //initialize to initial byte
			
			byte temp;	
			temp = BufferHandler.readByte(ref packetData,ref offset);

            // Convert to Ack Flags
            BitArray AckFlags = NumericalUtils.byteToBitArray(temp);

            /* Flag Field
             * 1 - unknown / nothing
               2 - isAck (should always be )
               3 - unknown currently
               4 - unknown currently
               5 - SERVERFLAGS_RESETRCC 
               6 - CLIENTFLAGS_RESETDONE (so its done)
               7 - SERVERFLAG_SIMTIME (aka 0x82, 0xc2)
            */

            if (AckFlags.Get(1) == false)
            {
                Output.WriteDebugLog("[WARNING] Packet has acked not set and is encrypted");
            }

            if (AckFlags.Get(7) == true)
            {

                byte[] simTime = BufferHandler.readBytes(ref packetData, ref offset, 4);
                Store.currentClient.playerData.setClientSimTime(NumericalUtils.byteArrayToFloat(simTime,1));
            }


            if (AckFlags.Get(6) == true)
            {
	            #if DEBUG
                Output.WriteDebugLog("[CLIENT]CLIENTFLAGS_RESETDONE found - we can init new RCC Comm ");
				#endif
                var resetClient = Store.currentClient;
                long resetEpoch = resetClient.TransportFence.Capture();
                try
                {
                // No transport gate around life/control cleanup: those paths can flush other clients.
                SafeFloorProbe.ClearBeforeEpoch(resetClient, resetEpoch);
                PosterBoxFloorProbe.Clear(resetClient);
                resetClient.playerData.setOnWorld(false);
                if (!resetClient.TransportFence.ApplyReset(resetEpoch, () =>
                {
                // Reset comm
                Store.currentClient.playerData.setCseq(0);
                Store.currentClient.playerData.setSseq(0);
                Store.currentClient.playerData.setPss(0x00);
                Store.currentClient.playerData.worldChangePending = false;
                Store.currentClient.playerData.setRPCShutDown(false);
                Store.currentClient.playerData.setRPCCounter(0);
                // The client's counters started again: what was sent before can't be resent with its old counter
                Store.currentClient.messageQueue.ForgetSentRpcs();
                Store.currentClient.messageQueue.ForgetWorldObjects();
                man04.ResetReceiveWindow();
                Store.currentClient.viewMan.Clear();
                })) return;
                // The client dropped all its views with the old world, and any fight with them.
                CombatManager.GetHandler(Store.currentClient).AbandonCombat();
                TradeHandler.Abort(Store.currentClient);
                SubwayHandler.Forget(Store.currentClient);
                lock (Store.currentClient.playerData.openDoors)
                {
                    Store.currentClient.playerData.openDoors.Clear(); // their views went with the old world
                }
                PlayerHandler handler = new PlayerHandler();
                handler.processAttributes();
                handler.processPlayerSetup();
                }
                finally { resetClient.TransportFence.Resume(resetEpoch); }
            }


			if (packetData.Length > 1 && AckFlags.Get(1) == true)
			{
				//Not just ACK nor Resetting

				if (packetData[offset] == 0x05)
				{
					offset = offset + 4;
					Output.writeToLogForConsole("Its a 05 Packet");
					Store.currentClient.messageQueue.ackOnlyCount = Store.currentClient.messageQueue.ackOnlyCount + 1;
					Store.currentClient.FlushQueue();
					// We dont need a handler for 05 Packets (as there is only one as i see lol)

				}

				if (packetData[offset] == 0x03)
				{
					// Is a 03 packet
					offset++;
					offset = man03.parse(offset, ref buffer);
				}


				if (offset < packetData.Length)
				{
					// There is no more info if we'r out of offset

					if (packetData[offset] == 0x04)
					{
						// Is a 04 packet
						offset++;
						offset = man04.parse(offset, ref buffer);
					}
				}

				if (Store.currentClient.messageQueue.ObjectMessagesQueue.Count == 0 &&
				    Store.currentClient.messageQueue.RPCMessagesQueue.Count == 0)
				{
					// nothing to send ? we should really ack something then
					Store.currentClient.messageQueue.ackOnlyCount = Store.currentClient.messageQueue.ackOnlyCount + 1;
					Store.currentClient.FlushQueue();
				}

			}
			else
			{
				// we have just ackonly but we need to send it 
				Store.currentClient.messageQueue.ackOnlyCount++;
			}
			
		}
	
		
		public void Parse(bool encryptedPacket,byte[] packetData){
					
	        // This should be needed for handle the init unencrypted packet
            // after init packet are send we need to set it to true
            // else it resends the unencrypted packet 
            // 

            // UDP Session is not established and packet is not encrypted - so its the first packet we know
            if (!Store.currentClient.playerData.getUDPSEssionEstablished() && !encryptedPacket){
	            if (packetData.Length >= 15)
	            {
		            var tempIniHelper = new PlayerHandler();
		            tempIniHelper.processInitUDPSession(ref packetData);
		            Output.WriteUnencryptedPacketLog(packetData,"CLIENT");
		            Store.currentClient.playerData.setUDPSessionEstablished(true);
		            Store.currentClient.playerData.setPss(0x00);
		            tempIniHelper.processPlayerSetup();
		            tempIniHelper.processAttributes();
		            return;    
	            }
                
            }

            if (!encryptedPacket){
	            Output.WriteUnencryptedPacketLog(packetData,"CLIENT");
                ParsePlainContent(ref packetData);
                return;
            }

			// Parse encrypted
            ParseContent(ref packetData);

		}
		
		
	}
}

