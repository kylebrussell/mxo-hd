
using System;
using System.Security.Cryptography;

namespace hds
{
	
	public class Md5
	{
		
		private readonly MD5 hasher;
		
		public Md5(){
			hasher = MD5.Create();
		}
		
		public byte[] digest(byte[] data){
			return hasher.ComputeHash(data);
		}
	}
}
