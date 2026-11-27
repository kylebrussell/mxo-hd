using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Security.Cryptography;

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

        // Per-thread CSPRNG IV pool: refills 256 x 16-byte fresh IVs per OS RandomNumberGenerator.Fill syscall
        // so outbound datagrams keep fresh CSPRNG IVs (never reused, IV[4] != 0) without per-packet kernel transitions.
        private const int IvPoolSize = 4096;
        [ThreadStatic] private static byte[] threadIvPool;
        [ThreadStatic] private static int threadIvOffset;

        private static void FillSecureIv(Span<byte> destination)
        {
            byte[] pool = threadIvPool;
            int offset = threadIvOffset;
            if (pool == null)
            {
                pool = new byte[IvPoolSize];
                threadIvPool = pool;
                offset = IvPoolSize;
            }
            if (offset + 16 > IvPoolSize)
            {
                RandomNumberGenerator.Fill(pool);
                offset = 0;
            }
            pool.AsSpan(offset, 16).CopyTo(destination);
            threadIvOffset = offset + 16;

            // Stock client.dll's CThreadedUDPDriver::GetPacket (0x623C096F..0x623C098A) checks raw_packet[5] == 0
            // without checking (raw_packet[0] & 1) == 0, calls HandleDriverPacket (which returns false for 0x01
            // encrypted datagrams), and then unconditionally drops the packet. On an encrypted packet (0x01 +
            // 16-byte IV + ciphertext), raw_packet[5] is IV[4].
            if (destination[4] == 0)
            {
                destination[4] = (byte)(1 + RandomNumberGenerator.GetInt32(255));
            }
        }

        public WorldEncryption()
        {
            InitCrypto();
        }

        /// <summary>A session under its own key: the one margin generated for this login (MarginEncryption.ForNewSession).</summary>
        public WorldEncryption(byte[] sessionKey)
        {
            if (sessionKey == null || sessionKey.Length != 16) throw new ArgumentException("A session key is 16 bytes.", nameof(sessionKey));
            TF_Key = (byte[])sessionKey.Clone();
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
            int payloadLen = plainData.Length;
            int bodyLen = 10 + payloadLen;
            int totalWithoutPad = 4 + bodyLen;
            int padding = 16 - (totalWithoutPad % 16);
            int paddedLen = totalWithoutPad + padding;

            byte[] workBuf = ArrayPool<byte>.Shared.Rent(paddedLen);
            try
            {
                Buffer.BlockCopy(plainData, 0, workBuf, 14, payloadLen);
                byte[] wire = new byte[1 + 16 + paddedLen];
                EncryptPreparedWorkBuffer(workBuf, payloadLen, pss, cseq, sseq, wire);
                return wire;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(workBuf);
            }
        }

        /// <summary>
        /// Encrypts a plaintext payload already serialized into <paramref name="workBuf"/> starting at offset 14
        /// (with at least 14 + payloadLen + 16 bytes capacity) into a rented ArrayPool wire buffer. Caller must
        /// return <paramref name="wireBuf"/> to <see cref="ArrayPool{T}.Shared"/> after synchronous send.
        /// </summary>
        internal void EncryptFromWorkBuffer(byte[] workBuf, int payloadLen, ushort pss, ushort cseq, ushort sseq,
            out byte[] wireBuf, out int wireLength)
        {
            int totalWithoutPad = 14 + payloadLen;
            int padding = 16 - (totalWithoutPad & 15);
            int paddedLen = totalWithoutPad + padding;
            wireLength = 1 + 16 + paddedLen;
            wireBuf = ArrayPool<byte>.Shared.Rent(wireLength);
            EncryptPreparedWorkBuffer(workBuf, payloadLen, pss, cseq, sseq, wireBuf);
        }

        private void EncryptPreparedWorkBuffer(byte[] workBuf, int payloadLen, ushort pss, ushort cseq, ushort sseq, byte[] wire)
        {
            int bodyLen = 10 + payloadLen;
            int totalWithoutPad = 4 + bodyLen;
            int padding = 16 - (totalWithoutPad & 15);
            int paddedLen = totalWithoutPad + padding;

            UInt32 seqValue = ((uint)pss << 24) | ((uint)cseq << 12) | sseq;
            UInt32 unixTime = (UInt32)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            BinaryPrimitives.WriteUInt16LittleEndian(workBuf.AsSpan(4, 2), (UInt16)(payloadLen + 4));
            BinaryPrimitives.WriteUInt32LittleEndian(workBuf.AsSpan(6, 4), unixTime);
            BinaryPrimitives.WriteUInt32BigEndian(workBuf.AsSpan(10, 4), seqValue);

            UInt32 crc = crc32.checksum(workBuf, 4, bodyLen);
            BinaryPrimitives.WriteUInt32LittleEndian(workBuf.AsSpan(0, 4), crc);
            workBuf.AsSpan(totalWithoutPad, padding).Fill((byte)padding);

            wire[0] = 0x01;
            FillSecureIv(wire.AsSpan(1, 16));

            lock (cipherLock)
            {
                tf.Encrypt(wire, 1, workBuf, 0, wire, 17, paddedLen);
            }
        }

        public ArrayList decrypt(byte[] encryptedData, int length)
        {
            if (!TryDecrypt(encryptedData, length, out byte[] payload, out ushort pss, out ushort cseq, out ushort sseq, stripTimedHeader: false))
            {
                return null;
            }

            ArrayList response = new ArrayList(4);
            response.Add(payload);
            response.Add(pss);
            response.Add(cseq);
            response.Add(sseq);
            return response;
        }

        internal bool TryDecrypt(byte[] encryptedData, int length, out byte[] payload, out ushort pss, out ushort cseq, out ushort sseq, bool stripTimedHeader = false)
        {
            payload = null;
            pss = 0;
            cseq = 0;
            sseq = 0;
            if (encryptedData == null || length < 33 || length > encryptedData.Length || ((length - 17) & 15) != 0)
            {
                return false;
            }

            int cipherLen = length - 17;
            byte[] workBuf = ArrayPool<byte>.Shared.Rent(cipherLen);
            try
            {
                lock (cipherLock)
                {
                    tf.Decrypt(encryptedData, 1, encryptedData, 17, workBuf, 0, cipherLen);
                }

                UInt32 crc32fromPacket = BinaryPrimitives.ReadUInt32LittleEndian(workBuf.AsSpan(0, 4));
                UInt16 packetSize = BinaryPrimitives.ReadUInt16LittleEndian(workBuf.AsSpan(4, 2));
                int decryptionLen = packetSize - 4 + 10;

                // A corrupt or foreign datagram can declare a length past the end of the buffer; reading it used to
                // throw inside the receive callback and take the whole server down.
                if (packetSize < 4 || decryptionLen > cipherLen - 4)
                {
                    Output.WriteLine("Dropping packet: declared length " + decryptionLen + " exceeds " + (cipherLen - 4) + " bytes");
                    return false;
                }

                UInt32 crc32plainDataToCompare = crc32.checksum(workBuf, 4, decryptionLen);
                if (crc32plainDataToCompare != crc32fromPacket)
                {
                    byte[] crcFromBytes = NumericalUtils.uint32ToByteArray(crc32fromPacket, 1);
                    byte[] crcGenBytes = NumericalUtils.uint32ToByteArray(crc32plainDataToCompare, 1);
                    Output.WriteLine("Dropping packet: CRC didn't match (from packet: " + StringUtils.bytesToString(crcFromBytes) + " generated: " + StringUtils.bytesToString(crcGenBytes) + ") wire " + length +
                                     " bytes, decrypted head " + StringUtils.bytesToString(ArraySegmentCopy(workBuf, Math.Min(32, cipherLen))) +
                                     " wire head " + StringUtils.bytesToString(ArraySegmentCopy(encryptedData, Math.Min(24, length))));
                    return false;
                }

                UInt32 seqValues = BinaryPrimitives.ReadUInt32BigEndian(workBuf.AsSpan(10, 4));
                cseq = (UInt16)(seqValues & 0xFFF);
                sseq = (UInt16)((seqValues >> 12) & 0xFFF);
                pss = (ushort)((seqValues >> 24) & 0xFF);

                int payloadLength = packetSize - 4;
                if (stripTimedHeader && payloadLength >= 5 && workBuf[14] == 0x82)
                {
                    // Strip the 4-byte timed header here to avoid a second allocation in WorldClient.DecryptReceivedPacket.
                    payload = new byte[payloadLength - 4];
                    payload[0] = 0x02;
                    Buffer.BlockCopy(workBuf, 19, payload, 1, payloadLength - 5);
                }
                else
                {
                    payload = new byte[payloadLength];
                    Buffer.BlockCopy(workBuf, 14, payload, 0, payloadLength);
                }
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(workBuf);
            }
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