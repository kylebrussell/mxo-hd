using System;
using System.Collections.Generic;
using System.Security.Policy;

namespace hds
{
	public class GameObject : IGameObject
	{
		
		private Attribute[] attributesCreation;
		private Attribute[] attributesUpdate;
		private int creationGroups;
		private int updateGroups;
		private string name;
		private UInt16 goid;
		private UInt32 relatedStaticObjId;

		public GameObject(int _attributeNumCreation,int _attributeNumUpdate,string _name,UInt16 _goid,UInt32 _relatedStaticObjId){
			attributesCreation = new Attribute[_attributeNumCreation];
			
			if (_attributeNumUpdate>0){ 
				attributesUpdate = new Attribute[_attributeNumUpdate];
			}else{
				attributesUpdate=null;
			}

			creationGroups = (int)Math.Ceiling((double) _attributeNumCreation / 7);
			updateGroups = (int)Math.Ceiling((double) _attributeNumUpdate / 7);
			
			name = _name;
			goid = _goid;
			relatedStaticObjId=_relatedStaticObjId;
		}
		
		public string GetName(){
			return name;
		}
		
		public byte[] GetGoid(){
			return NumericalUtils.uint16ToByteArray(goid,1);
		}
		
		public UInt32 GetRelatedStaticObjId(){
			return relatedStaticObjId;
		}
		
		public void SetRelatedStaticObjId(UInt32 _relatedStaticObjId){
			relatedStaticObjId= _relatedStaticObjId;
		}
		
		public void UnsetRelatedStaticObjId(){
			SetRelatedStaticObjId(0xFFFFFFFF);
		}

		public void DisableAllAttributes(){
			for (int i = 0;i<attributesCreation.Length;i++){
				attributesCreation[i].disable();
			}
		}
		
		public void AddAttribute(ref Attribute newAttribute, int indexCreation, int indexUpdate){
			attributesCreation[indexCreation]=newAttribute;
			if (indexUpdate!=-1){ // if present only
				attributesUpdate[indexUpdate]=newAttribute;	
			}
		}
		
		public void SetAttributeValue(byte[] content, int indexCreation){
			attributesCreation[indexCreation].setValue(content);
		}
		
		/// <summary>The creation attributes in order, for reading a creation record back (the headless test client).</summary>
		public IReadOnlyList<Attribute> GetCreationAttributeList(){
			return attributesCreation;
		}

		public byte[] GetCreationAttributes(){
			int lastActiveGroup = -1;
			int attribCounter = 0;
			int payloadBytes = 0;

			for (int i = 0; i < creationGroups; i++){
				int attribOffset = i * 7;
				int groupEnd = Math.Min(attribOffset + 7, attributesCreation.Length);
				bool anyInGroup = false;

				for (int pos = attribOffset; pos < groupEnd; pos++){
					Attribute temp = attributesCreation[pos];
					if (temp.isActive()){
						anyInGroup = true;
						attribCounter++;
						payloadBytes += temp.getValue().Length;
					}
				}

				if (anyInGroup){
					lastActiveGroup = i;
				}
			}

			if (lastActiveGroup < 0){
				return new byte[] { 0x00 };
			}

			byte[] result = new byte[1 + (lastActiveGroup + 1) + payloadBytes];
			result[0] = (byte)attribCounter;
			int writeOffset = 1;

			for (int i = 0; i <= lastActiveGroup; i++){
				int attribOffset = i * 7;
				int groupEnd = Math.Min(attribOffset + 7, attributesCreation.Length);
				int headerPos = writeOffset++;
				byte tempHeader = i < lastActiveGroup ? (byte)0x80 : (byte)0x00;

				for (int j = 0; attribOffset + j < groupEnd; j++){
					Attribute temp = attributesCreation[attribOffset + j];
					if (temp.isActive()){
						tempHeader = (byte)(tempHeader + (1 << j));
						byte[] val = temp.getValue();
						if (val.Length > 0){
							Buffer.BlockCopy(val, 0, result, writeOffset, val.Length);
							writeOffset += val.Length;
						}
					}
				}

				result[headerPos] = tempHeader;
			}

			return result;
		}
		
		public byte[] GetUpdateAttributes(List<Attribute> attributesNeedsUpdate){
			return EncodeSelectedAttributes(attributesUpdate, updateGroups, attributesNeedsUpdate, true);
		}

		internal static byte[] EncodeSelectedAttributes(Attribute[] table, int groups, List<Attribute> attributesNeedsUpdate, bool includeCountPrefix){
			int lastActiveGroup = -1;
			int attribCounter = 0;
			int payloadBytes = 0;
			int requestedCount = attributesNeedsUpdate != null ? attributesNeedsUpdate.Count : 0;

			if (table != null && requestedCount > 0){
				for (int i = 0; i < groups; i++){
					int attribOffset = i * 7;
					int groupEnd = Math.Min(attribOffset + 7, table.Length);
					bool anyInGroup = false;

					for (int pos = attribOffset; pos < groupEnd; pos++){
						Attribute temp = table[pos];
						if (!temp.isActive()){
							continue;
						}

						string tempName = temp.getName();
						for (int k = 0; k < requestedCount; k++){
							if (attributesNeedsUpdate[k].getName().Equals(tempName)){
								anyInGroup = true;
								attribCounter++;
								payloadBytes += temp.getValue().Length;
							}
						}
					}

					if (anyInGroup){
						lastActiveGroup = i;
					}
				}
			}

			if (lastActiveGroup < 0){
				return includeCountPrefix ? new byte[] { 0x00 } : Array.Empty<byte>();
			}

			int prefixLen = includeCountPrefix ? 1 : 0;
			byte[] result = new byte[prefixLen + (lastActiveGroup + 1) + payloadBytes];
			int writeOffset = 0;
			if (includeCountPrefix){
				result[writeOffset++] = (byte)attribCounter;
			}

			for (int i = 0; i <= lastActiveGroup; i++){
				int attribOffset = i * 7;
				int groupEnd = Math.Min(attribOffset + 7, table.Length);
				int headerPos = writeOffset++;
				byte tempHeader = i < lastActiveGroup ? (byte)0x80 : (byte)0x00;

				for (int j = 0; attribOffset + j < groupEnd; j++){
					Attribute temp = table[attribOffset + j];
					if (!temp.isActive()){
						continue;
					}

					string tempName = temp.getName();
					for (int k = 0; k < requestedCount; k++){
						if (attributesNeedsUpdate[k].getName().Equals(tempName)){
							tempHeader = (byte)(tempHeader + (1 << j));
							byte[] val = temp.getValue();
							if (val.Length > 0){
								Buffer.BlockCopy(val, 0, result, writeOffset, val.Length);
								writeOffset += val.Length;
							}
						}
					}
				}

				result[headerPos] = tempHeader;
			}

			return result;
		}
		
		public void Debug(){
			Console.WriteLine("Creation statuses");
			for(int i = 0;i<attributesCreation.Length;i++){
				if (attributesCreation[i].isActive()){
					Console.WriteLine("\t"+i+ " ON");
				}else{
					Console.WriteLine("\t"+i+ " OFF");
				}
			}
			
			
			Console.WriteLine("Update statuses");
			for(int i = 0;i<attributesUpdate.Length;i++){
				if (attributesUpdate[i].isActive()){
					Console.WriteLine("\t"+i+ " ON");
				}else{
					Console.WriteLine("\t"+i+ " OFF");
				}
			}
			
		}

	}

	public interface IGameObject
	{
		UInt32 GetRelatedStaticObjId();
		byte[] GetGoid();
		void DisableAllAttributes();
		byte[] GetCreationAttributes();
		byte[] GetUpdateAttributes(List<Attribute> attributesNeedsUpdate);
	}
}

