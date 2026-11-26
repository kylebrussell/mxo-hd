
using System;
using System.Security.Cryptography;

namespace hds
{
	public class MxoRSA
	{
		const string privKeyXML="<RSAKeyValue><Modulus>qMIfEkrXWpRr44ecWMzJHV7Hjg9bnru2PZv3NydzOZ6uab52wET+RoHhIzv+zJb3zBhmETAtsrmNnBXiW7tfqPK0xf6lb9RbvupfnfYSHO5WaEcWEi0JjQRBevg9d8qlETo9Hrfy9PEfpeK1T2WF+xxx73chvBTB12Paa7yT+Ik=</Modulus><Exponent>EQ==</Exponent><P>y1biPGcMKGk2VRKuHJZBvge0A/8YLTM5+gvdiZn2+2sC09iz/zjWa26ARP9ENMA687B6vo9asGtHovQCizWNgw==</P><Q>1HaPHEY726MwhSXUOl9l3Roon/hxOdfEE4ooMSe0z8WYW3+IDXTAbYCpJHMZNIvrTLSEuReuDdNL8W91Kq5wAw==</Q><DP>v2DU7Y4pj3IVBMZJhFEu0Pgw9LPahOTrRbDQgYHZZRlsEq3WldskKOB4uWi4qh5Vmg+ClTugpgqdxotNsDJnEQ==</DP><DQ>V3wcz2g2w9nIr0vP28zttWUfyWZMvXb2YmYQjLX/KGBr6XC/jRH04cuQ8OQZb/1g41lj07502IQuVFsSIKIuHw==</DQ><InverseQ>CnkbOBzROWVuJMlB8YIwswZSkBmhckoz+EjEKqIUSE8cYPnhOr2KNfUnTVFTFLlOzWc4jrrOQPi94rrDae8yPw==</InverseQ><D>RX0b2lsNYYhoqPuauycloq6OZ6v4jKelZKmiB6bVF7nPWLfWi2ez/uovhvqWGAHtkEZIJTH0swEcMTYwB6eBvV5fQPkL1CiZJELi7UGEME9v/UMqsMn/aAs3iqDYn0sR1kC4mZHRRNzIq7l16yufV6XnNaVuwlDMo3OLVWwqWE0=</D></RSAKeyValue>";
		const string privKeyXML2="<RSAKeyValue><Modulus>paFUaHOzouk8tl++bnudYwPn41Jx6wjp+s8RZmf4MCwCvryqBYcmU/ZQBgYPYjTDjNzMaW35zfSwRYhgppQExTTyxEX2HNXQWGd6zPzoAu3Q2JIslrSRgXuHXoAqercvjdjqpQcOyLkXDiWuLzRo18oSYrPUORmpJOsSrPfjKsRyTiB5XPp/Azy1nesfeXfnBi1nCLvaBiQ1xiXGIXSoW4VD9GIFvsRjZcZyU/dqQrMWt8ykyDM08qxt1AzKDCuksS7XJx3SBJ4aggqmh5DMDay+lKYgTAKrpOXIjXKQ+Ib4VNXQVijCzVnQUhW0bmdClNVz+5MTFtMN9mXU2U7opQ==</Modulus><Exponent>EQ==</Exponent><P>uUcMF/eZJf4KAxbGkmOb72rsIRpX1U6YI126SPfZ5vo1LGtlfikSlJktj4iXyRJn7IeSQDHWagth3J+4UzTzhclQqufUzKe1Y0PqLmsoTnh/2e1G1eX4BL0xuQNiZudre47vi7ThbByw3Y9xVnvG1II4CdKrjg058qrXs7BIXUU=</P><Q>5NpiShLNf2X+Q0ASlqjMW/z/JSilMul2d9piQI0DXNPccBdOMGkcvCP0dHuey+0ARgRmkTXWLvBirOUD7O5vyNDYsxAZsFa71Q9/tC3MhDtLUFfkZNLsJ1gGM4KIhWtctlvf3AWQHzVrfhkONLTV/Yac8QyD3d5EVhqglXUqo+E=</Q><DP>Nn5O2d9pR2jVxKxYhWiXRmq9655WEY+WKIT6je6LYg1a7vJpJRsjlR3+V2Ro4Mktn+ujfErktcccyGs2NpcacqSfQVM+lout0ebMaAFmNTJ/9M1REbwbxSiWJ1tZLVMfnM+v3cvJ1IDouaKo3TN2tvkfimsjZgPj3fYDNNl+sgU=</DP><DQ>rwFaOKT3f4o66CHwGNtvGSrhOoh+VBvxLnnSqdU+zoPkzi/wf19wU6MGOvUfFGnxJnvV9pKUunuWwHLk4lv7IRgtPaLmd8na7jkHTYxvN/EbeayfmGUO8Oj1rutZVve/XkZBxl6bYyjZusfdr9WUlLI7x2PsXl6sulCY6sMCfVE=</DQ><InverseQ>f24bB2UyT8Suowp1HbgzA4Bg5BGMWwoPEt8p8EFfLN0haV4XLQY4hXShCBGHf+D7hv+3hNLsgcmnXWUxSv8mZYjNcpMP69JffM73QrCadptczxyfwMjuJ3i8Z98BbOlHPaIiWPbF2tREKhSddaHGW3Jx8S8I83ixel8X+PB2qiM=</InverseQ><D>HTqWbMkfs1ZWAhDlXsqFL5dHCf99g9Rlhp0DEhJY+XEtqTBaPTX3tHbC0+Lzp+sxkVQkEprv2Q0QDEU+O4OIXwlYBITRFCW7WuUVq7Qo8XVC+Qq8kxDOYiTbp0PLQtUIZFN0s7XzjNVea+iIJnKpFwWKxh+8ChOWUc8hadFkUtcWYj2LpSkmZ2ObKqjRIk4MZzjYm13oygPP+vKSOcBdCec7qqZLnnSo8JV84OXLSBxGYhZSenIbheyv0cLrKRRLbXlvvQhaTGJGCDp87d9KHuuSpJ03km7LN3kItgjg0fR9XgEhhOQY2YNQ0qIHRyO5gh4zfXr/Yf/mkCnrehe/cQ==</D></RSAKeyValue>";

		private static readonly Lazy<RSAParameters> ServerPrivateParameters =
			new Lazy<RSAParameters>(() => ParseParameters(privKeyXML, true));
		private static readonly Lazy<RSAParameters> ServerPublicParameters =
			new Lazy<RSAParameters>(() => ParseParameters(privKeyXML, false));
		private static readonly Lazy<RSAParameters> RootPrivateParameters =
			new Lazy<RSAParameters>(() => ParseParameters(privKeyXML2, true));
		private static readonly Lazy<RSAParameters> RootPublicParameters =
			new Lazy<RSAParameters>(() => ParseParameters(privKeyXML2, false));

		public MxoRSA(){}

		public byte[] decryptWithPrivkey(byte[] block)
		{
			using (RSA rsa = RSA.Create())
			{
				rsa.ImportParameters(ServerPrivateParameters.Value);
				/* OAEP SHA-1 matches RSACryptoServiceProvider.Decrypt(block, true). */
				return rsa.Decrypt(block, RSAEncryptionPadding.OaepSHA1);
			}
		}

		public byte[] signWithMD5(byte[] md5fromStructure)
		{
			using (RSA rsa = RSA.Create())
			{
				rsa.ImportParameters(ServerPrivateParameters.Value);
				return rsa.SignData(md5fromStructure, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
			}
		}

		/// <summary>
		/// Checks a signature made by <see cref="signWithMD5"/> over the same MD5 digest: margin uses this to accept only
		/// tickets the auth server issued.
		/// </summary>
		public static bool VerifyWithServerKey(byte[] md5fromStructure, byte[] signature)
		{
			if (md5fromStructure == null || signature == null) return false;
			using (RSA rsa = RSA.Create())
			{
				rsa.ImportParameters(ServerPublicParameters.Value);
				try
				{
					return rsa.VerifyData(md5fromStructure, signature, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
				}
				catch (CryptographicException)
				{
					return false;
				}
			}
		}

		// Key roles, as decompiled from launcher.exe (see docs/CLIENT-SETUP-PLAN.md):
		//  - privKeyXML  (1024-bit, e=17) is the auth *server* key: the launcher encrypts the
		//    login blob to it (OAEP) and verifies auth replies with it.
		//  - privKeyXML2 (2048-bit, e=17) is the *root* key: the launcher embeds its modulus
		//    and uses it to verify the server key sent in AS_GetPublicKeyReply.

		/// <summary>Big-endian modulus of the auth server key (128 bytes).</summary>
		public static byte[] ServerPublicModulus() => (byte[])ServerPublicParameters.Value.Modulus.Clone();

		/// <summary>Public exponent of the auth server key as a single byte (17).</summary>
		public static byte ServerPublicExponent() => SingleByteExponent(ServerPublicParameters.Value);

		/// <summary>Big-endian modulus of the root key (256 bytes); this is what a patched launcher embeds.</summary>
		public static byte[] RootPublicModulus() => (byte[])RootPublicParameters.Value.Modulus.Clone();

		/// <summary>Public exponent of the root key as a single byte (17); the launcher hardcodes 0x11.</summary>
		public static byte RootPublicExponent() => SingleByteExponent(RootPublicParameters.Value);

		/// <summary>
		/// Signs the server key the way launcher.exe verifies it: RSASSA-PKCS1-v1.5 with MD5 by the
		/// root key over (modulus as 128 big-endian bytes || exponent as one byte).
		/// </summary>
		public static byte[] SignServerKeyWithRoot(byte[] serverModulus, byte serverExponent)
		{
			byte[] message = new byte[serverModulus.Length + 1];
			Buffer.BlockCopy(serverModulus, 0, message, 0, serverModulus.Length);
			message[serverModulus.Length] = serverExponent;

			using (RSA root = RSA.Create())
			{
				root.ImportParameters(RootPrivateParameters.Value);
				return root.SignData(message, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
			}
		}

		private static RSAParameters ParseParameters(string xml, bool includePrivate)
		{
			using (RSA key = RSA.Create())
			{
				key.FromXmlString(xml);
				return key.ExportParameters(includePrivate);
			}
		}

		private static byte SingleByteExponent(RSAParameters parameters)
		{
			byte[] exponent = parameters.Exponent;
			if (exponent.Length != 1)
				throw new InvalidOperationException("Launcher protocol carries the public exponent as one byte.");
			return exponent[0];
		}
	}
}
