using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    public class PlayerCharacter : Object12
	{
	    private int parseAttributesPack(ref Attribute[] attList,int maxGroups,ref byte[] data,int offset){
			
			for(int i = 0;i<maxGroups;i++){
				byte groupIndicator = BufferHandler.readByte(ref data,ref offset);
				int basePos = i*7;
				//Output.WriteLine("Parsing group:"+i);
				
				//Output.WriteLine("Group indicator:"+(int)groupIndicator);
				
				for (int j = 0;j<7;j++){
					if ((groupIndicator & (1<<j))==1){ // Attribute is being updated
						//Output.WriteLine("Updating: "+attList[basePos+j].getName());
					}else{
						//Output.WriteLine("Not Updating: "+attList[basePos+j].getName());
					}
				}
				
				if (groupIndicator<0x80){ // More groups flag is OFF
					break;
				}
				if (groupIndicator==0x00){
					break;
				}
			}
			
			return offset;
		}
		
		/// <summary>
		/// The player's own view (2) in a client protocol 03 section; <paramref name="_offset"/> is at the
		/// sub-view byte. Stores the action, yaw, position and names it carries, forwards the state part to
		/// the other players, and returns the offset after the entry. When the entry can't be walked it
		/// returns the packet length, so nothing after it is misread.
		/// </summary>
		public int parseAutoView(ref byte[] buffer, int _offset)
		{
			if (!SelfViewEntry.TryParse(buffer, _offset, out SelfViewEntry entry, out string failure))
			{
				byte[] rest = new byte[Math.Max(0, buffer.Length - _offset)];
				ArrayUtils.copy(buffer, _offset, rest, 0, rest.Length);
				// Not forwarded: without knowing where the update ends, the other players would get the rest of
				// this packet (including the client's RPCs) as view data.
				Output.WriteClientViewRequestLog("[03] Self view not parsed (" + failure + "): " + StringUtils.bytesToString_NS(rest));
				return buffer.Length;
			}

			SelfViewUpdate state = entry.State;
			if (state != null)
			{
				if (state.HasPosition && (!state.PositionInWorldUnits || !ControlActions.CanMove(Store.currentClient)))
				{
					if (!state.PositionInWorldUnits)
						Output.WriteDebugLog($"[03] Corrected invalid self-view position ({state.X:R}, {state.Y:R}, {state.Z:R}) flags={state.Flags:x2}");
					ControlActions.CorrectPosition(Store.currentClient);
					return entry.End;
				}
				#if DEBUG
				Output.WriteLine("[MPM] Parsing selfview Attributes");
				#endif
				if (state.Action.HasValue)
				{
					Action.setValue(state.Action.Value);
				}
				if (state.Yaw.HasValue)
				{
					YawInterval.setValue(state.Yaw.Value);
				}
				if (state.HasPosition)
				{
					WorldClient client = Store.currentClient;
					byte[] nextPosition = NumericalUtils.doublesToLtVector3d(state.X, state.Y, state.Z);
					// Retail closes the Writer on an accepted position change, before the client sends 81 1a.
					if (client.playerData.craftingSession != null && !Position.getValue().AsSpan().SequenceEqual(nextPosition))
						CraftingSession.End(client);
					Position.setValue(nextPosition);
                    MissionProxyIntroduction.AcceptedPosition(Store.currentClient,state.X,state.Y,state.Z);
				}

				// Other players see this player through a view of their own; the update goes there unchanged.
				// This used to copy to the end of the packet, which also forwarded the client's RPCs.
				byte[] forwarded = state.ToForwardedUpdate(buffer);
				if (forwarded != null)
				{
					ForwardStateToOthers(forwarded);
				}
			}

			if (entry.LastName != null)
			{
				Output.WriteLine("[MPM] Parsing 02 View");
				RealLastName.setValue(entry.LastName);
				RealFirstName.setValue(entry.FirstName);
			}

			return entry.End;
		}

		private static void ForwardStateToOthers(byte[] stateData)
		{
			Store.world.SendViewPacketToAllPlayers(stateData, Store.currentClient.playerData.getCharID(), NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.GetGoid(), 1), Store.currentClient.playerData.getEntityId());
		}
		
        internal byte[] GetFullSelfViewAttributeGroups()
        {
            var attributes = attributesSelfView.Select((attribute, index) => (index, ValueOf(attribute))).ToArray();
            return PlayerViewAttributes.Build(attributes)[1..];

            byte[] ValueOf(Attribute attribute)
            {
                if (attribute == RealFirstName || attribute == RealLastName)
                    return EncodeSelfViewName(attribute.getValue());
                // Object12's ordinary RSI selector defaults to 1. Zero is meaningful while disguised.
                if (attribute == UseRSIDescription && !attribute.isActive())
                    return new byte[] { 1 };
                return attribute.getValue();
            }
        }

        private static byte[] EncodeSelfViewName(byte[] value)
        {
            int terminator = Array.IndexOf(value, (byte)0);
            int length = terminator < 0 ? value.Length : terminator;
            byte[] encoded = new byte[length + 1];
            encoded[0] = (byte)length;
            Array.Copy(value, 0, encoded, 1, length);
            return encoded;
        }

		public byte[] GetSelfUpdateAttributes(List<Attribute> attributesNeedsUpdate, bool addAttributeLen){
			
			DynamicArray din = new DynamicArray();
			bool lastGroupEmpty = true;
			int attribCounter = 0;
			
			for (int i = (selfViewGroups-1);i>=0;i--){
				int attribOffset = i*7;
				bool anyAttribEnabled = false;
				byte tempHeader = 0x00;
				
				for (int j = 6;j>=0;j--){
					int position = attribOffset+j;
					// This verifies that the attribute is in a group but groups has not 7 attributes
					if (position<attributesSelfView.Length){ 
						Attribute temp = attributesSelfView[attribOffset+j];

						if (temp.isActive())
						{
							foreach (Attribute attribute in attributesNeedsUpdate)
							{
								if (attribute.getName().Equals(temp.getName()))
								{
									anyAttribEnabled = true;
									attribCounter++;
									tempHeader = (byte) (tempHeader + (1<<j)); // Displacement
									din.insertBefore(temp.getValue());									
								}
							}
						}
					}
				}

				// Updating last attribute group, set it as 0b0XXXXXXX
				if (i == (selfViewGroups-1)){
					if (anyAttribEnabled){
						din.insertBefore(tempHeader);
						lastGroupEmpty = false;
					}
				}
				// Updating other than last attribute group
				else{
					if (!lastGroupEmpty){
						tempHeader = (byte) (tempHeader+0x80);
						din.insertBefore(tempHeader);
					}else{
						if(anyAttribEnabled){
							din.insertBefore(tempHeader);
							lastGroupEmpty = false;
						}
					}				
				}

			}
			
			//add the counter of attributes sent
			if (addAttributeLen)
			{
				din.insertBefore((byte)attribCounter);	
			}
			
			return din.getBytes();
		}
		
	}
}
