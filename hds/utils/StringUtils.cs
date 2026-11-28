
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace hds
{
	
	
	public class StringUtils
	{
		private const string HexChars = "0123456789abcdef";
		private static readonly Regex WhitespaceRegex = new Regex(@"\s", RegexOptions.Compiled);

		public StringUtils()
		{}
		
		// If we want just N bytes from the array
		static public string bytesToString(byte[] data,int length){
			if (data == null || length <= 0) return string.Empty;
			int count = length <= data.Length ? length : data.Length;
			return string.Create(count * 3, (data, count), (chars, state) =>
			{
				byte[] src = state.data;
				int n = state.count;
				for (int i = 0; i < n; i++)
				{
					byte b = src[i];
					int o = i * 3;
					chars[o] = HexChars[b >> 4];
					chars[o + 1] = HexChars[b & 0xF];
					chars[o + 2] = ' ';
				}
			});
		}
		
		// If we want all bytes from the array
		static public string bytesToString(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			return bytesToString(data, data.Length);
		}
		
		// If we want all bytes from the array but no spaces
		static public string bytesToString_NS(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			return string.Create(data.Length * 2, data, (chars, src) =>
			{
				for (int i = 0; i < src.Length; i++)
				{
					byte b = src[i];
					int o = i * 2;
					chars[o] = HexChars[b >> 4];
					chars[o + 1] = HexChars[b & 0xF];
				}
			});
		}
		
		
		// If we want an array of characters (from bytes) to be a string
		static public string charBytesToString(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			return string.Create(data.Length, data, (chars, src) =>
			{
				for (int i = 0; i < src.Length; i++)
				{
					chars[i] = (char)src[i];
				}
			});
		}
		
		// If we want an array of characters (from bytes) to be a string
		static public string charBytesToString_NZ(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			int len = Array.IndexOf<byte>(data, 0);
			if (len < 0) len = data.Length;
			if (len == 0) return string.Empty;
			return string.Create(len, (data, len), (chars, state) =>
			{
				byte[] src = state.data;
				int n = state.len;
				for (int i = 0; i < n; i++)
				{
					chars[i] = (char)src[i];
				}
			});
		}
		
		static public byte[] stringToBytes(string data){
			return Encoding.ASCII.GetBytes(data);
		}

        public static byte[] bitStringToBytes(string bitString)
        {
            byte[] output = new byte[bitString.Length / 8];

            for (int i = 0; i < output.Length; i++)
            {
                for (int b = 0; b <= 7; b++)
                {
                    output[i] |= (byte)((bitString[i * 8 + b] == '1' ? 1 : 0) << (7 - b));
                }
            }

            return output;
        }


		
		static public byte[] hexStringToBytes(string hexString){
            if (string.IsNullOrEmpty(hexString)) return Array.Empty<byte>();
            return Convert.FromHexString(hexString);
		}

		private static int DecodeHexNibble(char c)
		{
			if ((uint)(c - '0') <= 9u) return c - '0';
			if ((uint)(c - 'a') <= 5u) return c - 'a' + 10;
			if ((uint)(c - 'A') <= 5u) return c - 'A' + 10;
			return -1;
		}

		internal static bool TryDecodeHex(string hexString, Span<byte> destination)
		{
			int byteCount = hexString.Length >> 1;
			for (int i = 0; i < byteCount; i++)
			{
				int hi = DecodeHexNibble(hexString[i << 1]);
				int lo = DecodeHexNibble(hexString[(i << 1) + 1]);
				if ((hi | lo) < 0) return false;
				destination[i] = (byte)((hi << 4) | lo);
			}
			return true;
		}

		
		
		
		static public string removeWhitespaceChars(string data){
			return WhitespaceRegex.Replace( data, "" );
		}
		
	}
}
