
using System;
using System.Buffers.Binary;
using System.Collections;

namespace hds
{
	
	
	public class NumericalUtils{
		
		
		/* This does reverse the original array */
		static public void reverse4ByteArray(byte[] res){
			byte temp;
			temp = res[0];
			res[0]=res[3];res[3] = temp;
			temp = res[1];
			res[1]=res[2];res[2] = temp;
		}
		
		/* This does reverse the original array */
		static public void reverse2ByteArray(byte[] res){
			byte temp;
			temp = res[0];
			res[0]=res[1];
			res[1]=temp;
		}
		
		
		static public byte[] uint32ToByteArray(UInt32 data, int reversed){
			byte[] res = new byte[4];
			if (reversed == 1)
				BinaryPrimitives.WriteUInt32LittleEndian(res, data);
			else
				BinaryPrimitives.WriteUInt32BigEndian(res, data);
			return res;
		}

        static public byte[] int16ToByteArray(Int16 data, int reversed)
        {
            byte[] res = new byte[2];
            if (reversed == 1)
                BinaryPrimitives.WriteInt16LittleEndian(res, data);
            else
                BinaryPrimitives.WriteInt16BigEndian(res, data);
            return res;
        }

        static public byte[] int32ToByteArray(Int32 data, int reversed){
            byte[] res = new byte[4];
            if (reversed == 1)
                BinaryPrimitives.WriteInt32LittleEndian(res, data);
            else
                BinaryPrimitives.WriteInt32BigEndian(res, data);
            return res;
        }
		
		static public byte[] uint16ToByteArray(UInt16 data,int reversed){
			byte[] res = new byte[2];
			if (reversed == 1)
				BinaryPrimitives.WriteUInt16LittleEndian(res, data);
			else
				BinaryPrimitives.WriteUInt16BigEndian(res, data);
			return res;
		}
		
		//TODO: change this code to uint8 to byte array
		static public byte[] uint16ToByteArrayShort(UInt16 data){
			return new byte[] { (byte)data };
		}
		
		static public UInt16 ByteArrayToUint16(byte[] data, int reversed){
			return reversed == 1
				? BinaryPrimitives.ReadUInt16LittleEndian(data)
				: BinaryPrimitives.ReadUInt16BigEndian(data);
		}
		
		
		static public UInt32 ByteArrayToUint32(byte[] data, int reversed){
			return reversed == 1
				? BinaryPrimitives.ReadUInt32LittleEndian(data)
				: BinaryPrimitives.ReadUInt32BigEndian(data);
		}
		
		static public float byteArrayToFloat(byte[] array, int reversed){
			return reversed == 1
				? BinaryPrimitives.ReadSingleLittleEndian(array)
				: BinaryPrimitives.ReadSingleBigEndian(array);
		}
		
		static public double byteArrayToDouble(byte[] array, int reversed){
			return reversed == 1
				? BinaryPrimitives.ReadDoubleLittleEndian(array)
				: BinaryPrimitives.ReadDoubleBigEndian(array);
		}
		
		
		//It does reversed by default (Bitconverter)
		static public byte[] floatToByteArray(float n,int reversed){
			byte[] res = new byte[4];
			if (reversed == 1)
				BinaryPrimitives.WriteSingleLittleEndian(res, n);
			else
				BinaryPrimitives.WriteSingleBigEndian(res, n);
			return res;
		}
		
		//It does reversed by default (Bitconverter)
		static public byte[] doubleToByteArray(double n,int reversed){
			byte[] res = new byte[8];
			if (reversed == 1)
				BinaryPrimitives.WriteDoubleLittleEndian(res, n);
			else
				BinaryPrimitives.WriteDoubleBigEndian(res, n);
			return res;
		}
		
		
		// Returns N transformed to bits, taking bitsSize bits
		static public string intToBitsString(int n,int bitsSize){
			string temp = Convert.ToString(n,2);
			if(temp.Length==bitsSize)
				return temp;
			else{
				if(temp.Length<bitsSize){
					temp = temp.PadLeft(bitsSize,'0');
					return temp;
				}
				else{
					temp = temp.PadLeft(bitsSize);
					return temp;
				}
				
			}
		
		}
		
		static public int binaryStringToInt(string binaryStr){
			return (int)Convert.ToInt32(binaryStr,2);
		}
		
		static public byte[] bigBinaryStringToBytes(string bigBinaryStr){
			
			byte[] res = new byte[bigBinaryStr.Length/8];
			int pointer = 0;
			
			for(int i = 0;i<bigBinaryStr.Length;i+=8){
				string temp = bigBinaryStr.Substring(i,8);
				res[pointer]=(byte)binaryStringToInt(temp);
				pointer++;
			}
			
			return res;
		}
		
		static public void LtVector3fToFloats(byte[] LtVector3f,ref float x,ref float y, ref float z){
			if(LtVector3f.Length<12) throw new IndexOutOfRangeException();
			ReadOnlySpan<byte> v=LtVector3f;
			x = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(v.Slice(0,4));
			y = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(v.Slice(4,4));
			z = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(v.Slice(8,4));
		}
		
		static public void LtVector3dToDoubles(byte[] LtVector3d,ref double x,ref double y, ref double z){
			// Three little-endian doubles at 0, 8 and 16, read in place: the visibility pass decodes positions for every
			// pair of players each pass, and the old copy into three temporary arrays allocated per call.
			if(LtVector3d.Length<24) throw new IndexOutOfRangeException();
			ReadOnlySpan<byte> v=LtVector3d;
			x = System.Buffers.Binary.BinaryPrimitives.ReadDoubleLittleEndian(v.Slice(0,8));
			y = System.Buffers.Binary.BinaryPrimitives.ReadDoubleLittleEndian(v.Slice(8,8));
			z = System.Buffers.Binary.BinaryPrimitives.ReadDoubleLittleEndian(v.Slice(16,8));
		}
		
		static public byte[] floatsToLtVector3f(float x,float y,float z){
			byte[] res = new byte[12];
			Span<byte> span = res;
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(0, 4), x);
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(4, 4), y);
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(8, 4), z);
			return res;
		}
		
		static public byte[] floatsToQuaternion(float w,float x,float y, float z){
			byte[] res = new byte[16];
			Span<byte> span = res;
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(0, 4), w);
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(4, 4), x);
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(8, 4), y);
			System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(span.Slice(12, 4), z);
			return res;
		}
		
		static public byte[] doublesToLtVector3d(double x,double y,double z){
			byte[] res = new byte[24];
			Span<byte> span = res;
			System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(0, 8), x);
			System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(8, 8), y);
			System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(16, 8), z);
			return res;
		}

        static public BitArray byteToBitArray(byte input)
        {
            BitArray flags = new BitArray(new byte[] { input });
            return flags;
        }
        
        public static uint RotateLeft(uint value, int count)
        {
	        return (value << count) | (value >> (32 - count));
        }

        public static uint RotateRight(uint value, int count)
        {
	        return (value >> count) | (value << (32 - count));
        }

	}
}
