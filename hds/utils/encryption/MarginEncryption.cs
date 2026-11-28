
using System;
using System.Buffers.Binary;
using System.Security.Cryptography;


namespace hds
{
	
	
	public class MarginEncryption{
		
		/// <summary>The old fixed session key, every login's until per-login keys. Only tests and old tooling still use it.</summary>
		public static readonly byte[] LegacyKey = { 0x6C, 0xAB, 0x8E, 0xCC, 0xE7, 0x3C, 0x22, 0x47, 0xDB, 0xEB, 0xDE, 0x1A, 0xA8, 0xE7, 0x5F, 0xB8 };
		private byte[] TF_Key = (byte[])LegacyKey.Clone();
        private byte[] IV = { 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30 };
		private readonly object cipherLock = new object();

		private const string xmlKeys = "<RSAKeyValue><Modulus>qMIfEkrXWpRr44ecWMzJHV7Hjg9bnru2PZv3NydzOZ6uab52wET+RoHhIzv+zJb3zBhmETAtsrmNnBXiW7tfqPK0xf6lb9RbvupfnfYSHO5WaEcWEi0JjQRBevg9d8qlETo9Hrfy9PEfpeK1T2WF+xxx73chvBTB12Paa7yT+Ik=</Modulus><Exponent>EQ==</Exponent><P>y1biPGcMKGk2VRKuHJZBvge0A/8YLTM5+gvdiZn2+2sC09iz/zjWa26ARP9ENMA687B6vo9asGtHovQCizWNgw==</P><Q>1HaPHEY726MwhSXUOl9l3Roon/hxOdfEE4ooMSe0z8WYW3+IDXTAbYCpJHMZNIvrTLSEuReuDdNL8W91Kq5wAw==</Q><DP>v2DU7Y4pj3IVBMZJhFEu0Pgw9LPahOTrRbDQgYHZZRlsEq3WldskKOB4uWi4qh5Vmg+ClTugpgqdxotNsDJnEQ==</DP><DQ>V3wcz2g2w9nIr0vP28zttWUfyWZMvXb2YmYQjLX/KGBr6XC/jRH04cuQ8OQZb/1g41lj07502IQuVFsSIKIuHw==</DQ><InverseQ>CnkbOBzROWVuJMlB8YIwswZSkBmhckoz+EjEKqIUSE8cYPnhOr2KNfUnTVFTFLlOzWc4jrrOQPi94rrDae8yPw==</InverseQ><D>RX0b2lsNYYhoqPuauycloq6OZ6v4jKelZKmiB6bVF7nPWLfWi2ez/uovhvqWGAHtkEZIJTH0swEcMTYwB6eBvV5fQPkL1CiZJELi7UGEME9v/UMqsMn/aAs3iqDYn0sR1kC4mZHRRNzIq7l16yufV6XnNaVuwlDMo3OLVWwqWE0=</D></RSAKeyValue>";
        private byte[] userModulus = { };
        private byte[] userExponent = { };
        private RSAParameters rsaparam;
		
		private MxoTwofish tf;
		private Crc32 crc32;
		
		public MarginEncryption(){
			tf = new MxoTwofish();
			crc32 = new Crc32();
			tf.SetKey(TF_Key);
		}

		/// <summary>
		/// Margin's side of one login: a fresh random Twofish key. Margin sends it to the client under the account's RSA key
		/// (CertConnectReply) and the world uses it for this character's UDP session (docs/ACCOUNTS.md).
		/// </summary>
		public static MarginEncryption ForNewSession()
		{
			var session = new MarginEncryption();
			session.SetSessionKey(RandomNumberGenerator.GetBytes(16));
			return session;
		}

		/// <summary>The client's side: use the key margin sent.</summary>
		public void SetSessionKey(byte[] key)
		{
			if (key == null || key.Length != 16) throw new ArgumentException("A session key is 16 bytes.", nameof(key));
			lock (cipherLock)
			{
				TF_Key = (byte[])key.Clone();
				tf.SetKey(TF_Key);
			}
		}
		
		public byte[] getTFKey(){
			lock (cipherLock)
			{
				return (byte[])TF_Key.Clone();
			}
		}
		
		public byte[] getIV(){
			lock (cipherLock)
			{
				return (byte[])IV.Clone();
			}
		}
		
		
		public byte[] encryptUsersPublic(byte[] plain)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(rsaparam);
                return rsa.Encrypt(plain, true);
            }
        }
		
		
		public void setUserPubKey(byte[] exponent, byte[] modulus)
        {
            rsaparam = new RSAParameters();
            
            rsaparam.Modulus = modulus;

            rsaparam.Exponent = exponent;
        }
		
		public byte[] encrypt(byte[] packet){
			int psize = packet.Length;
			int bodyLen = 6 + psize;
			int totalLength = 4 + bodyLen;
			int padding = 16 - (totalLength & 15);
			int paddedLen = totalLength + padding;

			byte[] plainBuf = new byte[paddedLen];
			BinaryPrimitives.WriteUInt16LittleEndian(plainBuf.AsSpan(4, 2), (UInt16)psize);
			BinaryPrimitives.WriteUInt32LittleEndian(plainBuf.AsSpan(6, 4), TimeUtils.getUnixTimeUint32());
			Buffer.BlockCopy(packet, 0, plainBuf, 10, psize);

			uint crc = crc32.checksum(plainBuf, 4, bodyLen);
			BinaryPrimitives.WriteUInt32LittleEndian(plainBuf.AsSpan(0, 4), crc);
			plainBuf.AsSpan(totalLength, padding).Fill((byte)padding);

			byte[] response = new byte[16 + paddedLen];
			lock (cipherLock)
			{
				Buffer.BlockCopy(IV, 0, response, 0, 16);
				tf.Encrypt(response, 0, plainBuf, 0, response, 16, paddedLen);
			}
			return response;
		}
		
		
        // Opt-in ONLY for0a/0c: legacy consumers historically receive payload plus PKCS suffix.
        internal bool TryDecryptCreationBody(byte[] packet,out byte[] body)
        {
            body=null;
            if(packet==null||packet.Length<33||(packet.Length-17)%16!=0)return false;
            int cipherLen = packet.Length - 17;
            var plain=new byte[cipherLen];
            lock (cipherLock)
            {
                tf.Decrypt(packet, 1, packet, 17, plain, 0, cipherLen);
            }
            if(plain.Length<16)return false;
            int size=plain[4]|plain[5]<<8;int end=10+size,padding=plain.Length-end;
            if(size<1||end>plain.Length||padding<1||padding>16)return false;
            for(int i=end;i<plain.Length;i++)if(plain[i]!=padding)return false;
            uint expectedCrc = crc32.checksum(plain, 4, 6 + size);
            uint packetCrc = BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(0, 4));
            if(packetCrc != expectedCrc)return false;
            if(plain[10]!=0x0a&&plain[10]!=0x0c)return false;
            body=new byte[size];Array.Copy(plain,10,body,0,size);return true;
        }

		public byte[] decryptMargin(byte[] packet)
        {
	        if (packet == null || packet.Length < 33 || ((packet.Length - 17) & 15) != 0)
	        {
		        // Short or non-block-aligned ciphertext would throw inside Twofish or slicing.
		        return null; 
	        }
            int cipherLen = packet.Length - 17;
            byte[] decryptedBytes = new byte[cipherLen];
            lock (cipherLock)
            {
                Buffer.BlockCopy(packet, 1, IV, 0, 16);
                tf.Decrypt(packet, 1, packet, 17, decryptedBytes, 0, cipherLen);
            }

            byte[] decryptedPacket = new byte[decryptedBytes.Length - 10];
			Buffer.BlockCopy(decryptedBytes, 10, decryptedPacket, 0, decryptedPacket.Length);
			
			return decryptedPacket;
        }
		
		
	}
}
