using System;
using System.Buffers.Binary;
using System.Text;

namespace hds
{
	public class Attribute
	{
		
		byte[] innerValue;
		int size;
		string name;
		bool active;
				
		
		public Attribute (int _size,string _name)
		{
			size = _size;
			innerValue = new byte[_size];
			name = _name;
			active = false;
		}
		
		public string getName(){
			return name;
		}
		
		public byte[] getValue(){
			return innerValue;
		}
		
		public bool isActive(){
			return active;
		}
		
		public void enable(){
			active = true;
		}
		
		public void disable(){
			active = false;
		}
		
		public int getSize(){
			return size;
		}
		
		public void setValue(byte[] newValue){
			int copyLen = newValue.Length < size ? newValue.Length : size;
			if (copyLen > 0)
			{
				Buffer.BlockCopy(newValue, 0, innerValue, 0, copyLen);
			}
			if (copyLen < size)
			{
				Array.Clear(innerValue, copyLen, size - copyLen);
			}

			enable();
		}
		
				
		#region interfaces for values other-than-byte
		public void setValue(bool newValue){
			if (size > 0)
			{
				innerValue[0] = newValue ? (byte)0x01 : (byte)0x00;
				if (size > 1)
				{
					Array.Clear(innerValue, 1, size - 1);
				}
			}
			enable();
		}
		
		public void setValue(byte newValue){
			if (size > 0)
			{
				innerValue[0] = newValue;
				if (size > 1)
				{
					Array.Clear(innerValue, 1, size - 1);
				}
			}
			enable();
		}
		
		public void setValue(UInt16 newValue){
			if (size >= 2)
			{
				BinaryPrimitives.WriteUInt16LittleEndian(innerValue, newValue);
				if (size > 2)
				{
					Array.Clear(innerValue, 2, size - 2);
				}
			}
			else if (size == 1)
			{
				innerValue[0] = (byte)newValue;
			}
			enable();
		}
		
		public void setValue(UInt32 newValue){
			if (size >= 4)
			{
				BinaryPrimitives.WriteUInt32LittleEndian(innerValue, newValue);
				if (size > 4)
				{
					Array.Clear(innerValue, 4, size - 4);
				}
			}
			else if (size > 0)
			{
				Span<byte> tmp = stackalloc byte[4];
				BinaryPrimitives.WriteUInt32LittleEndian(tmp, newValue);
				tmp.Slice(0, size).CopyTo(innerValue);
			}
			enable();
		}
		
		public void setValue(float newValue){
			if (size >= 4)
			{
				BinaryPrimitives.WriteSingleLittleEndian(innerValue, newValue);
				if (size > 4)
				{
					Array.Clear(innerValue, 4, size - 4);
				}
			}
			else if (size > 0)
			{
				Span<byte> tmp = stackalloc byte[4];
				BinaryPrimitives.WriteSingleLittleEndian(tmp, newValue);
				tmp.Slice(0, size).CopyTo(innerValue);
			}
			enable();
		}
		
		public void setValue(double newValue){
			if (size >= 8)
			{
				BinaryPrimitives.WriteDoubleLittleEndian(innerValue, newValue);
				if (size > 8)
				{
					Array.Clear(innerValue, 8, size - 8);
				}
			}
			else if (size > 0)
			{
				Span<byte> tmp = stackalloc byte[8];
				BinaryPrimitives.WriteDoubleLittleEndian(tmp, newValue);
				tmp.Slice(0, size).CopyTo(innerValue);
			}
			enable();
		}
		
		public void setValue(string newValue){
			if (newValue == null) throw new ArgumentNullException(nameof(newValue));
			if (size > 0)
			{
				int copyChars = newValue.Length < size ? newValue.Length : size;
				int written = copyChars > 0
					? Encoding.ASCII.GetBytes(newValue.AsSpan(0, copyChars), innerValue)
					: 0;
				if (written < size)
				{
					Array.Clear(innerValue, written, size - written);
				}
			}
			enable();
		}

		internal bool EqualsVector3d(double x, double y, double z)
		{
			if (size != 24)
			{
				return false;
			}

			ReadOnlySpan<byte> v = innerValue;
			return BinaryPrimitives.ReadInt64LittleEndian(v.Slice(0, 8)) == BitConverter.DoubleToInt64Bits(x)
				&& BinaryPrimitives.ReadInt64LittleEndian(v.Slice(8, 8)) == BitConverter.DoubleToInt64Bits(y)
				&& BinaryPrimitives.ReadInt64LittleEndian(v.Slice(16, 8)) == BitConverter.DoubleToInt64Bits(z);
		}

		internal void SetVector3d(double x, double y, double z)
		{
			if (size >= 24)
			{
				Span<byte> v = innerValue;
				BinaryPrimitives.WriteDoubleLittleEndian(v.Slice(0, 8), x);
				BinaryPrimitives.WriteDoubleLittleEndian(v.Slice(8, 8), y);
				BinaryPrimitives.WriteDoubleLittleEndian(v.Slice(16, 8), z);
				if (size > 24)
				{
					Array.Clear(innerValue, 24, size - 24);
				}
				enable();
				return;
			}

			setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
		}
		
		#endregion
		
	}
}

