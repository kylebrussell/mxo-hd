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
			
			if (packetData == null || _offset < 0 || _offset >= packetData.Length){
				return packetData != null ? packetData.Length : 0;
			}

			buffer = packetData;
			offset = _offset;
			// See what we are dealing with:
			int rpcBlocks = buffer[offset++];
			
			for (int i = 0; i < rpcBlocks && offset < buffer.Length; i++){
				if (!parseRpcBlock()){
					break;
				}
			}
			
			
			return offset;
		}

        private bool parseRpcBlock(){
            if (offset + 3 > buffer.Length){
                offset = buffer.Length;
                return false;
            }

            // The block's first RPC's sequence number; the rest count up from it
            UInt16 sequence = (UInt16)((buffer[offset] << 8) | buffer[offset + 1]);
            offset += 2;
            int rpcInside = buffer[offset++];
            for (int i = 0; i < rpcInside; i++){
                if (!parseRpc((UInt16)(sequence + i))){
                    return false;
                }
            }
            return true;
        }

        private bool parseRpc(UInt16 sequence){
            if (offset >= buffer.Length){
                return false;
            }

            int length = buffer[offset++];
            if (length >= 0x80){ //2 bytes as size
                if (offset >= buffer.Length){
                    return false;
                }
                length = ((length - 0x80) << 8) + buffer[offset++];
            }

            if (offset >= buffer.Length){
                return false;
            }
            int header = buffer[offset++];
            length--; //discount 1 byte for the header
            if (header >= 0x80){ //2 bytes as size
                if (offset >= buffer.Length){
                    return false;
                }
                header = ((header - 0x80) << 8) + buffer[offset++];
                length--; // discount another byte for the header (2nd byte)
            }

            if (length < 0 || offset + length > buffer.Length){
                offset = buffer.Length;
                return false;
            }

            byte[] rpcValues = length == 0 ? Array.Empty<byte>() : new byte[length];
            if (length > 0){
                Buffer.BlockCopy(buffer, offset, rpcValues, 0, length);
                offset += length;
            }
	        #if DEBUG
            Output.WriteLine("RPC (Header " + header + " | Content:" + StringUtils.bytesToString(rpcValues));
			#endif
            if (receiveWindow.IsResend(sequence, header, rpcValues)){
                Output.WriteDebugLog("[RPC] Resend of " + sequence + " (header " + header.ToString("x") + ") ignored");
                return true;
            }
            rpcManager.HandleRpc(header, ref rpcValues);
            return true;
        }

	}
}

