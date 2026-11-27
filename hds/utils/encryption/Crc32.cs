
using System;

namespace hds
{
	public class Crc32
	{
		private static readonly uint[] SharedTable = BuildTable();
		private readonly uint[] table = SharedTable;

		public Crc32(){
		}
		
		public byte[] checksumB(byte[]block, int reversed){
			UInt32 temp = checksum(block);
			return NumericalUtils.uint32ToByteArray(temp,reversed);
		}
		
		public UInt32 checksum(byte[] block){
			return checksum(block, 0, block.Length);
		}

		public UInt32 checksum(byte[] block, int offset, int length){
			uint crc = 0xFFFFFFFFu;
			uint[] t = table;
			int end = offset + length;
			for (int i = offset; i < end; i++){
				crc = (crc >> 8) ^ t[(crc ^ block[i]) & 0xFF];
			}
			return crc ^ 0xFFFFFFFFu;
		}

		private static uint[] BuildTable(){
			uint[] t = new uint[256];
			const uint poly = 0xEDB88320u;
			for (int i = 0; i < 256; i++){
				uint crc = (uint)i;
				for (int j = 8; j > 0; j--){
					if ((crc & 1) == 1){
						crc = (crc >> 1) ^ poly;
					}
					else{
						crc >>= 1;
					}
				}
				t[i] = crc;
			}
			return t;
		}
		
		public void generateTable(){
		}
		
		
	}
}
