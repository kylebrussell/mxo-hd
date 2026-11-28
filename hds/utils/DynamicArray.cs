
using System;

namespace hds
{
	public class DynamicArray
	{
		private const int DefaultInitialCapacity = 32;
		private byte[] _buffer;
		private int _length;

		public DynamicArray()
		{
			_buffer = Array.Empty<byte>();
			_length = 0;
		}

		public DynamicArray(int capacity)
		{
			_buffer = capacity > 0 ? new byte[capacity] : Array.Empty<byte>();
			_length = 0;
		}

		public byte[] getBytes()
		{
			if (_length == 0)
			{
				return Array.Empty<byte>();
			}

			byte[] result = new byte[_length];
			Buffer.BlockCopy(_buffer, 0, result, 0, _length);
			return result;
		}

		public int getSize()
		{
			return _length;
		}

		public void append(byte data)
		{
			EnsureCapacity(_length + 1);
			_buffer[_length++] = data;
		}

		public void append(byte[] newData)
		{
			if (newData == null || newData.Length == 0)
			{
				return;
			}

			EnsureCapacity(_length + newData.Length);
			Buffer.BlockCopy(newData, 0, _buffer, _length, newData.Length);
			_length += newData.Length;
		}

		internal void Append(ReadOnlySpan<byte> data)
		{
			if (data.IsEmpty)
			{
				return;
			}

			EnsureCapacity(_length + data.Length);
			data.CopyTo(_buffer.AsSpan(_length));
			_length += data.Length;
		}

		internal Span<byte> GetAppendSpan(int count)
		{
			if (count <= 0)
			{
				return Span<byte>.Empty;
			}

			EnsureCapacity(_length + count);
			Span<byte> span = _buffer.AsSpan(_length, count);
			_length += count;
			return span;
		}

		internal void AppendZeros(int count)
		{
			if (count <= 0)
			{
				return;
			}

			EnsureCapacity(_length + count);
			Array.Clear(_buffer, _length, count);
			_length += count;
		}

		internal void Truncate(int newLength)
		{
			if (newLength >= 0 && newLength < _length)
			{
				_length = newLength;
			}
		}

		public void insertBefore(byte data)
		{
			EnsureCapacity(_length + 1);
			if (_length > 0)
			{
				Buffer.BlockCopy(_buffer, 0, _buffer, 1, _length);
			}
			_buffer[0] = data;
			_length++;
		}

		public void insertBefore(byte[] newData)
		{
			if (newData == null || newData.Length == 0)
			{
				return;
			}

			int addLen = newData.Length;
			EnsureCapacity(_length + addLen);
			if (_length > 0)
			{
				Buffer.BlockCopy(_buffer, 0, _buffer, addLen, _length);
			}
			Buffer.BlockCopy(newData, 0, _buffer, 0, addLen);
			_length += addLen;
		}

		private void EnsureCapacity(int required)
		{
			if (required <= _buffer.Length)
			{
				return;
			}

			int nextCapacity = _buffer.Length == 0 ? DefaultInitialCapacity : _buffer.Length * 2;
			if (nextCapacity < required)
			{
				nextCapacity = required;
			}

			byte[] nextBuffer = new byte[nextCapacity];
			if (_length > 0)
			{
				Buffer.BlockCopy(_buffer, 0, nextBuffer, 0, _length);
			}
			_buffer = nextBuffer;
		}
	}
}
	

