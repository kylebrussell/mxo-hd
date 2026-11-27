using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace ManyMonkeys.Cryptography
{
	/// <summary>
	/// Summary description for TwofishEncryption.
	/// </summary>
	internal class TwofishEncryption : TwofishBase, ICryptoTransform
	{
		public TwofishEncryption(int keyLen, ref byte[] key, ref byte[] iv, CipherMode cMode, EncryptionDirection direction)
		{
			// convert our key into an array of ints
			for (int i=0;i<key.Length/4;i++)
			{
				Key[i] = (uint)( key[i*4+3]<<24) | (uint)(key[i*4+2] << 16) | (uint)(key[i*4+1] << 8) | (uint)(key[i*4+0]);
			}

			cipherMode = cMode;

			// we only need to convert our IV if we are using CBC
			if (cipherMode == CipherMode.CBC && iv != null)
			{
				SetIV(iv, 0);
			}

			encryptionDirection = direction;
			reKey(keyLen,ref Key);
		}

		internal void SetIV(byte[] iv, int offset)
		{
			IV[0] = BinaryPrimitives.ReadUInt32LittleEndian(iv.AsSpan(offset, 4));
			IV[1] = BinaryPrimitives.ReadUInt32LittleEndian(iv.AsSpan(offset + 4, 4));
			IV[2] = BinaryPrimitives.ReadUInt32LittleEndian(iv.AsSpan(offset + 8, 4));
			IV[3] = BinaryPrimitives.ReadUInt32LittleEndian(iv.AsSpan(offset + 12, 4));
		}

		internal void EncryptBlocks(byte[] inputBuffer, int inputOffset, byte[] outputBuffer, int outputOffset, int length)
		{
			int blocks = length >> 4;
			for (int i = 0; i < blocks; i++)
			{
				int src = inputOffset + (i << 4);
				int dst = outputOffset + (i << 4);
				uint x0 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src, 4));
				uint x1 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src + 4, 4));
				uint x2 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src + 8, 4));
				uint x3 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src + 12, 4));
				blockEncrypt(ref x0, ref x1, ref x2, ref x3);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst, 4), x0);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst + 4, 4), x1);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst + 8, 4), x2);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst + 12, 4), x3);
			}
		}

		internal void DecryptBlocks(byte[] inputBuffer, int inputOffset, byte[] outputBuffer, int outputOffset, int length)
		{
			int blocks = length >> 4;
			for (int i = 0; i < blocks; i++)
			{
				int src = inputOffset + (i << 4);
				int dst = outputOffset + (i << 4);
				uint x0 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src, 4));
				uint x1 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src + 4, 4));
				uint x2 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src + 8, 4));
				uint x3 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(src + 12, 4));
				blockDecrypt(ref x0, ref x1, ref x2, ref x3);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst, 4), x0);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst + 4, 4), x1);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst + 8, 4), x2);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(dst + 12, 4), x3);
			}
		}

		// need to have this method due to IDisposable - just can't think of a reason to use it for in this class
		public void Dispose()
		{
		}


		/// <summary>
		/// Transform a block depending on whether we are encrypting or decrypting
		/// </summary>
		/// <param name="inputBuffer"></param>
		/// <param name="inputOffset"></param>
		/// <param name="inputCount"></param>
		/// <param name="outputBuffer"></param>
		/// <param name="outputOffset"></param>
		/// <returns></returns>
		public int TransformBlock(
			byte[] inputBuffer,
			int inputOffset,
			int inputCount,
			byte[] outputBuffer,
			int outputOffset
			)
		{
			uint x0 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset, 4));
			uint x1 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset + 4, 4));
			uint x2 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset + 8, 4));
			uint x3 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset + 12, 4));

			if (encryptionDirection == EncryptionDirection.Encrypting)
			{
				blockEncrypt(ref x0, ref x1, ref x2, ref x3);
			}
			else
			{
				blockDecrypt(ref x0, ref x1, ref x2, ref x3);
			}

			BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(outputOffset, 4), x0);
			BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(outputOffset + 4, 4), x1);
			BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(outputOffset + 8, 4), x2);
			BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(outputOffset + 12, 4), x3);

			return inputCount;
		}

		public byte[] TransformFinalBlock(
			byte[] inputBuffer,
			int inputOffset,
			int inputCount
			)
		{
			byte[] outputBuffer;// = new byte[0];
			
			if (inputCount>0)
			{
				outputBuffer = new byte[16]; // blocksize
				uint x0 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset, 4));
				uint x1 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset + 4, 4));
				uint x2 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset + 8, 4));
				uint x3 = BinaryPrimitives.ReadUInt32LittleEndian(inputBuffer.AsSpan(inputOffset + 12, 4));

				if (encryptionDirection == EncryptionDirection.Encrypting)
				{
					blockEncrypt(ref x0, ref x1, ref x2, ref x3);
				}
				else
				{
					blockDecrypt(ref x0, ref x1, ref x2, ref x3);
				}

				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(0, 4), x0);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(4, 4), x1);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(8, 4), x2);
				BinaryPrimitives.WriteUInt32LittleEndian(outputBuffer.AsSpan(12, 4), x3);
			}
			else
			{
				outputBuffer = new byte[0]; // the .NET framework doesn't like it if you return null - this calms it down
			}
			
			return outputBuffer;
		}

		// not worked out this property yet - placing break points here just don't get caught.
		private bool canReuseTransform = true;
		public bool CanReuseTransform
		{
			get
			{
				return canReuseTransform;
			}
		}

		// I normally set this to false when block encrypting so that I can work on one block at a time
		// but for compression and stream type ciphers this can be set to true so that you get all the data
		private bool canTransformMultipleBlocks = false;
		public bool CanTransformMultipleBlocks
		{
			get
			{
				return canTransformMultipleBlocks;
			}
		}

		public int InputBlockSize
		{
			get
			{
				return inputBlockSize;
			}
		}

		public int OutputBlockSize
		{
			get
			{
				return outputBlockSize;
			}
		}

		private EncryptionDirection encryptionDirection;
	}
}
