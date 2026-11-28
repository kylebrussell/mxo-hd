
using System;

namespace hds
{
	
	public class ArrayUtils
	{
		
		public ArrayUtils()	{}
		
		// Reverse it on memory
		// Much faster than Array.reverse method
		public void reverse(byte[] param){
			byte temp;
			int i=0;
			int j=param.Length-1;
			
			while (j>i){
				temp = param[i];
				param[i] = param[j];
				param[j] = temp;
				i++;
				j--;
			}
		}
		
		// Compares two arrays of same length
		public static bool equal(byte[] arrayA,byte[] arrayB){
			return arrayA.AsSpan().SequenceEqual(arrayB);
		}
		
		// Copies N bytes from source offset to destination array offset
		static public void copy(byte[] sourceArray, int offsetB,byte[] destinationArray,int offsetA, int nBytes){
			if (nBytes <= 0 || offsetB >= sourceArray.Length || offsetA >= destinationArray.Length)
			{
				return;
			}

			int count = Math.Min(nBytes, Math.Min(sourceArray.Length - offsetB, destinationArray.Length - offsetA));
			if (count > 0)
			{
				Buffer.BlockCopy(sourceArray, offsetB, destinationArray, offsetA, count);
			}
		}

        // Copies N bytes from source offset to destination array offset
        public static void copyTo(byte[] sourceArray, int offsetB, byte[] destinationArray, int offsetA, int nBytes)
        {
            if (nBytes <= 0 || offsetA >= destinationArray.Length)
            {
                return;
            }

            int count = Math.Min(nBytes, destinationArray.Length - offsetA);
            if (count > 0)
            {
                Buffer.BlockCopy(sourceArray, offsetB, destinationArray, offsetA, count);
            }
        }
		
		
		/* Does a fast copy of array contents */
		public static void fastCopy(byte[] source, byte[]destination, int length){
			if (length <= 0)
			{
				return;
			}

			Buffer.BlockCopy(source, 0, destination, 0, length);
		}
	
		
		
	}
}
