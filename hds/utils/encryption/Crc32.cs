
using System;
using System.Buffers.Binary;

namespace hds
{
	public class Crc32
	{
		// Slicing-by-8 IEEE 802.3 CRC32 tables (8 x 256 uint = 8 KB L1-resident):
		// processes 8 payload bytes per iteration instead of 1, bit-for-bit identical to byte-by-byte.
		private static readonly uint[][] Tables = BuildTables();
		private static readonly uint[] T0 = Tables[0];
		private static readonly uint[] T1 = Tables[1];
		private static readonly uint[] T2 = Tables[2];
		private static readonly uint[] T3 = Tables[3];
		private static readonly uint[] T4 = Tables[4];
		private static readonly uint[] T5 = Tables[5];
		private static readonly uint[] T6 = Tables[6];
		private static readonly uint[] T7 = Tables[7];

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
			int i = offset;
			int end = offset + length;
			uint[] t0 = T0, t1 = T1, t2 = T2, t3 = T3, t4 = T4, t5 = T5, t6 = T6, t7 = T7;
			ReadOnlySpan<byte> span = block;

			while (i + 8 <= end){
				uint lo = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i, 4)) ^ crc;
				uint hi = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i + 4, 4));
				crc = t7[lo & 0xFF] ^
				      t6[(lo >> 8) & 0xFF] ^
				      t5[(lo >> 16) & 0xFF] ^
				      t4[lo >> 24] ^
				      t3[hi & 0xFF] ^
				      t2[(hi >> 8) & 0xFF] ^
				      t1[(hi >> 16) & 0xFF] ^
				      t0[hi >> 24];
				i += 8;
			}

			while (i < end){
				crc = (crc >> 8) ^ t0[(crc ^ block[i]) & 0xFF];
				i++;
			}
			return crc ^ 0xFFFFFFFFu;
		}

		private static uint[][] BuildTables(){
			uint[][] tables = new uint[8][];
			uint[] t0 = new uint[256];
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
				t0[i] = crc;
			}
			tables[0] = t0;

			for (int s = 1; s < 8; s++){
				uint[] prev = tables[s - 1];
				uint[] cur = new uint[256];
				for (int i = 0; i < 256; i++){
					uint c = prev[i];
					cur[i] = (c >> 8) ^ t0[c & 0xFF];
				}
				tables[s] = cur;
			}
			return tables;
		}
		
		public void generateTable(){
		}
		
		
	}
}
