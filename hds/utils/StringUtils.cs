
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
			char[] chars = new char[count * 3];
			for (int i = 0; i < count; i++)
			{
				byte b = data[i];
				int o = i * 3;
				chars[o] = HexChars[b >> 4];
				chars[o + 1] = HexChars[b & 0xF];
				chars[o + 2] = ' ';
			}
			return new string(chars);
		}
		
		// If we want all bytes from the array
		static public string bytesToString(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			return bytesToString(data, data.Length);
		}
		
		// If we want all bytes from the array but no spaces
		static public string bytesToString_NS(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			int len = data.Length;
			char[] chars = new char[len * 2];
			for (int i = 0; i < len; i++)
			{
				byte b = data[i];
				int o = i * 2;
				chars[o] = HexChars[b >> 4];
				chars[o + 1] = HexChars[b & 0xF];
			}
			return new string(chars);
		}
		
		
		// If we want an array of characters (from bytes) to be a string
		static public string charBytesToString(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			int len = data.Length;
			char[] chars = new char[len];
			for (int i = 0; i < len; i++)
			{
				chars[i] = (char)data[i];
			}
			return new string(chars);
		}
		
		// If we want an array of characters (from bytes) to be a string
		static public string charBytesToString_NZ(byte[] data){
			if (data == null || data.Length == 0) return string.Empty;
			int len = Array.IndexOf<byte>(data, 0);
			if (len < 0) len = data.Length;
			if (len == 0) return string.Empty;
			char[] chars = new char[len];
			for (int i = 0; i < len; i++)
			{
				chars[i] = (char)data[i];
			}
			return new string(chars);
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

		
		
		
		static public string removeWhitespaceChars(string data){
			return WhitespaceRegex.Replace( data, "" );
		}
		
	}
}
