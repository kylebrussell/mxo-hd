using System;
using System.Collections;
using System.Drawing.Printing;
using System.IO;
using System.Security.Cryptography;
using ManyMonkeys.Cryptography;

namespace hds
{
    public class WorldEncryption : IWorldEncryption
    {
        public byte[] TF_Key = { 0x6C, 0xAB, 0x8E, 0xCC, 0xE7, 0x3C, 0x22, 0x47, 0xDB, 0xEB, 0xDE, 0x1A, 0xA8, 0xE7, 0x5F, 0xB8 };
        public byte[] IV = { 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30 };
        
        private Crc32 crc32;
        private MxoTwofish tf;

        // A client's packets are decrypted on the socket callback and its queue is encrypted on the flush thread, at the
        // same time. This used to keep the working buffer in a field and share the cipher's IV unguarded: a decrypt could
        // replace the buffer mid-encrypt, sending an all-zero 2065-byte packet whose RPCs were then lost, which the client
        // only notices as a gap in the RPC counter (found by tools/HeadlessClient).
        private readonly object cipherLock = new object();

        public WorldEncryption()
        {
            InitCrypto();
        }

        public void InitCrypto()
        {
            crc32 = new Crc32();
            tf = new MxoTwofish();
            tf.SetIV(IV);
            tf.SetKey(TF_Key);
        }
        
        public byte[] encrypt(byte[] plainData, int length, ushort pss, ushort cseq, ushort sseq)
        {
            UInt32 seqValue = (uint)pss << 24 | (uint)cseq << 12 | (uint)sseq;
      
            // Create the packet that needs to be CRC32 checksummed
            PacketContent packet = new PacketContent();
            packet.AddUint16((UInt16)(plainData.Length + 4),1);
            packet.AddByteArray(TimeUtils.getCurrentTime());
            packet.AddUint32(seqValue,0);
            packet.AddByteArray(plainData);

            byte[] paddingBytes = getPaddedBytes(packet.ReturnFinalPacket());
            
            // Final Packet which needs to be encrypted
            PacketContent packetWithCrc = new PacketContent();
            packetWithCrc.AddByteArray(crc32.checksumB(packet.ReturnFinalPacket(),1));
            packetWithCrc.AddByteArray(packet.ReturnFinalPacket());
            packetWithCrc.AddByteArray(paddingBytes);

            byte[] encrypted = new byte[packetWithCrc.ReturnFinalPacket().Length];
            PacketContent encryptedPacket = new PacketContent();
            lock (cipherLock)
            {
                // The IV sent has to be the one the data is encrypted with
                tf.SetIV(IV);
                tf.Encrypt(packetWithCrc.ReturnFinalPacket(), encrypted);
                encryptedPacket.AddByte(0x01);
                encryptedPacket.AddByteArray(IV);
            }
            encryptedPacket.AddByteArray(encrypted);
            return encryptedPacket.ReturnFinalPacket();
        }

        public ArrayList decrypt(byte[] encryptedData, int length)
        {
            ArrayList response = new ArrayList();

            PacketReader readerEncrypted = new PacketReader(encryptedData);
            readerEncrypted.IncrementOffsetByValue(1); // skip 0x01
            byte[] packetIv = readerEncrypted.ReadBytes(16);

            // Read the whole Data
            byte[] decryptedPacket = new byte[encryptedData.Length - 17];
            byte[] decryptBuffer = readerEncrypted.ReadBytes(encryptedData.Length - 17);
            lock (cipherLock)
            {
                IV = packetIv;
                tf.SetIV(packetIv);
                tf.Decrypt(decryptBuffer, decryptedPacket);
            }

            PacketReader reader = new PacketReader(decryptedPacket);
            UInt32 crc32fromPacket = reader.ReadUInt32(1);
            UInt16 decryptionLen = (ushort) ((reader.ReadUInt16(1)) - 4 + 10);
            reader.SetOffsetOverrideValue(4);

            byte[] crc32FromPacketBytes = NumericalUtils.uint32ToByteArray(crc32fromPacket, 1); // ToDo: Remove when working

            // A corrupt or foreign datagram can declare a length past the end of the buffer; reading it used to
            // throw inside the receive callback and take the whole server down.
            if (decryptionLen > decryptedPacket.Length - 4)
            {
                Output.WriteLine("Dropping packet: declared length " + decryptionLen + " exceeds " + (decryptedPacket.Length - 4) + " bytes");
                return null;
            }

            // Verify CRC32 Data part
            byte[] packetCheckData = reader.ReadBytes(decryptionLen);
            UInt32 crc32plainDataToCompare = NumericalUtils.ByteArrayToUint32(crc32.checksumB(packetCheckData,1),1);

            if (crc32plainDataToCompare != crc32fromPacket)
            {
                Output.WriteLine("Dropping packet: CRC didn't match (from packet: " + StringUtils.bytesToString(crc32FromPacketBytes) +" generated: " + StringUtils.bytesToString(crc32.checksumB(packetCheckData,1)) + ") wire " + encryptedData.Length +
                                 " bytes, decrypted head " + StringUtils.bytesToString(decryptedPacket.Length > 32 ? ArraySegmentCopy(decryptedPacket, 32) : decryptedPacket) +
                                 " wire head " + StringUtils.bytesToString(ArraySegmentCopy(encryptedData, Math.Min(24, encryptedData.Length))));
                return null;
            }
            
            // as we read the whole packet for verifiy we reset the offset to read the other things
            reader.SetOffsetOverrideValue(4);
            UInt16 packetSize = reader.ReadUInt16(1);
//            UInt32 timeStamp = reader.readUInt32(1); // Well we didnt need it - but just read it as uint32
            reader.SetOffsetOverrideValue(10);
            UInt32 seqValues = reader.ReadUInt32(0);

            UInt16 pss=0;
            UInt16 cseq=0;
            UInt16 sseq=0;
            
            cseq = (UInt16)(seqValues&0xFFF);
            sseq = (UInt16) (seqValues >> 12&0xFFF);
            pss = (ushort) (seqValues >> 24&0xFF);

            // Finally add our responses and read the rest of the packet
            response.Add(reader.ReadBytes(packetSize-4));
            response.Add(pss);
            response.Add(cseq);
            response.Add(sseq);
			
            return response;
        }

        private static byte[] ArraySegmentCopy(byte[] source, int count)
        {
            byte[] result = new byte[Math.Min(count, source.Length)];
            Array.Copy(source, result, result.Length);
            return result;
        }

        public byte[] getPaddedBytes(byte[] packet)
        {
            int totalLength = packet.Length + 4;
            int padding = 16 - (totalLength % 16);
            
            int i = 0;
            byte[] padded = new byte[padding];
           
            for (i = 0; i < padding; i++)
            {
                padded[i] += (byte)padding;
            }
            return padded;
        }
    }
}