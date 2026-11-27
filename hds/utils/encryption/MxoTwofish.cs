using System.Security.Cryptography;
using ManyMonkeys.Cryptography;


namespace hds
{
    public class MxoTwofish
    {
        private readonly Twofish tf;
        private TwofishEncryption cachedCipher;
        private byte[] currentKey;
        private byte[] currentIv;
        private bool keyDirty = true;

        public MxoTwofish()
        {
            tf = new Twofish();
            tf.Mode = CipherMode.CBC;
            tf.Padding = PaddingMode.PKCS7;
            tf.KeySize = 128;
            tf.BlockSize = 128;
        }

        public void SetIV(byte[] iv)
        {
            currentIv = iv;
        }

        public void SetKey(byte[] key)
        {
            currentKey = key;
            keyDirty = true;
        }

        private TwofishEncryption EnsureCipher()
        {
            if (keyDirty || cachedCipher == null)
            {
                byte[] key = currentKey ?? tf.Key;
                byte[] iv = currentIv ?? tf.IV;
                int keyBits = key.Length * 8;
                cachedCipher = new TwofishEncryption(keyBits, ref key, ref iv, CipherMode.CBC,
                    TwofishBase.EncryptionDirection.Encrypting);
                keyDirty = false;
            }
            return cachedCipher;
        }

        public void Encrypt(byte[] plainData, byte[] destination)
        {
            TwofishEncryption cipher = EnsureCipher();
            cipher.SetIV(currentIv ?? tf.IV, 0);
            cipher.EncryptBlocks(plainData, 0, destination, 0, plainData.Length);
        }

        internal void Encrypt(byte[] iv, int ivOffset, byte[] plainData, int srcOffset, byte[] destination, int dstOffset, int length)
        {
            TwofishEncryption cipher = EnsureCipher();
            cipher.SetIV(iv, ivOffset);
            cipher.EncryptBlocks(plainData, srcOffset, destination, dstOffset, length);
        }

        public void Decrypt(byte[] criptedData, byte[] destination)
        {
            TwofishEncryption cipher = EnsureCipher();
            cipher.SetIV(currentIv ?? tf.IV, 0);
            cipher.DecryptBlocks(criptedData, 0, destination, 0, criptedData.Length);
        }

        internal void Decrypt(byte[] iv, int ivOffset, byte[] criptedData, int srcOffset, byte[] destination, int dstOffset, int length)
        {
            TwofishEncryption cipher = EnsureCipher();
            cipher.SetIV(iv, ivOffset);
            cipher.DecryptBlocks(criptedData, srcOffset, destination, dstOffset, length);
        }
    }
}