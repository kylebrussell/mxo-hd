using System;
namespace hds
{
	public class Protocol04Manager
	{
		private byte[] buffer;
		private int offset;
		private RpcManager rpcManager;
		private readonly RpcReceiveWindow receiveWindow = new RpcReceiveWindow();

		/// <summary>The client restarted its communication (a world reload): its RPC numbering starts over.</summary>
		public void ResetReceiveWindow(){
			receiveWindow.Reset();
		}
		
		
		public Protocol04Manager (){
			rpcManager = new RpcManager();
		}
		
		
		public int parse(int _offset, ref byte[] packetData){
			
			buffer = packetData;
			offset = _offset;
			// See what we are dealing with:
			int rpcBlocks = (int)  BufferHandler.readByte(ref buffer,ref offset);
			
			for (int i = 0;i<rpcBlocks;i++){
				parseRpcBlock();	
			}
			
			
			return offset;
		}

        private void parseRpcBlock(){

            // The block's first RPC's sequence number; the rest count up from it
            byte[] counter = BufferHandler.readBytes(ref buffer, ref offset, 2);
            UInt16 sequence = (UInt16)((counter[0] << 8) | counter[1]);
            int rpcInside = (int)BufferHandler.readByte(ref buffer, ref offset);
            for (int i = 0; i < rpcInside; i++){
                parseRpc((UInt16)(sequence + i));
            }
        }

        private void parseRpc(UInt16 sequence){
            int length = BufferHandler.readByte(ref buffer, ref offset);
            if (length >= 0x80){ //2 bytes as size
                length = (length - 0x80) << 8;
                length = length + (int)BufferHandler.readByte(ref buffer, ref offset);
            }

            int header = BufferHandler.readByte(ref buffer, ref offset);
            length--; //discount 1 byte for the header
            if (header >= 0x80){ //2 bytes as size
                header = (header - 0x80) << 8;
                header = header + (int)BufferHandler.readByte(ref buffer, ref offset);
                length--; // discount another byte for the header (2nd byte)
            }
            byte[] rpcValues = BufferHandler.readBytes(ref buffer, ref offset, length);
	        #if DEBUG
            Output.WriteLine("RPC (Header " + header + " | Content:" + StringUtils.bytesToString(rpcValues));
			#endif
            if (receiveWindow.IsResend(sequence, header, rpcValues)){
                Output.WriteDebugLog("[RPC] Resend of " + sequence + " (header " + header.ToString("x") + ") ignored");
                return;
            }
            rpcManager.HandleRpc(header, ref rpcValues);


        }

	}
}

