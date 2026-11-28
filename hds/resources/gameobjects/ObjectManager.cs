using System;
using System.Buffers.Binary;
using System.Collections;

namespace hds.resources.gameobjects
{
	public class ObjectManager
	{
		private static readonly byte[] CreationSeparator = { 0xcd, 0xab };
		private readonly Hashtable _currentObjects;
		
		public ObjectManager (){
			_currentObjects = new Hashtable();
			Output.WriteLine("[SHEEVA] Object Manager Operative. Welcome back");
		}
		
		
		public void PushClient(string key){
            // Create a new gameobject for the unique key
            lock (_currentObjects)
            {
                if (!_currentObjects.ContainsKey(key))
                {
                    _currentObjects.Add(key, new PlayerCharacter());
                }
            }
            
		}
		
		public PlayerCharacter GetAssignedObject(string key){
			// Return gameobject structure
			lock (_currentObjects)
			{
				return _currentObjects[key] as PlayerCharacter;
			}
		}
		
		public void PopClient(string key){
			// Delete key for unused player
			lock (_currentObjects)
			{
				_currentObjects.Remove(key);
			}
		}
		
		public DynamicArray GenerateCreationPacket(IGameObject go,UInt16 viewID, byte idCounter){
            
            byte fullyDynamicFlag=0x08;
			byte[] goid = go.GetGoid();
            UInt32 relatedStaticObjId = go.GetRelatedStaticObjId();
            bool fullyDynamic = (relatedStaticObjId==0xFFFFFFFF);
			if (fullyDynamic){
				fullyDynamicFlag=(byte)0x0c;
			}

			byte[] creationAttributes = go.GetCreationAttributes();
			DynamicArray din = new DynamicArray(creationAttributes.Length + (fullyDynamic ? 6 : 10));

			din.append(fullyDynamicFlag);
			din.append(goid);
			if (!fullyDynamic){
				BinaryPrimitives.WriteUInt32LittleEndian(din.GetAppendSpan(4), relatedStaticObjId);
			}
						
			din.append(idCounter);
			din.append(CreationSeparator);
			
			din.append(creationAttributes);
			
			if(goid[0] == 0x0c && goid[1] == 0x00){
				viewID = 0x0002;
			}

			return din;
		}
	}
}

