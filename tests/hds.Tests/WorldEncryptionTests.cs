using System.Collections;
using hds;

namespace hds.Tests;

public class WorldEncryptionTests
{
    [Fact]
    public void RoundTripsAPacket()
    {
        byte[] plain = { 0x02, 0x04, 0x01, 0x00, 0x05, 0x01, 0x02, 0x81, 0x4c };
        byte[] wire = new WorldEncryption().encrypt(plain, plain.Length, 0x7f, 3, 4);

        ArrayList result = new WorldEncryption().decrypt(wire, wire.Length);

        Assert.NotNull(result);
        Assert.Equal(plain, (byte[])result[0]);
        Assert.Equal((ushort)0x7f, (ushort)result[1]);
    }

    [Fact]
    public void DropsACorruptPacketInsteadOfThrowing()
    {
        // A datagram that decrypts to garbage once crashed the server: its declared length ran past the buffer
        // and PacketReader threw inside the socket callback.
        byte[] wire = new byte[49];
        wire[0] = 0x01;
        for (int i = 1; i < wire.Length; i++)
        {
            wire[i] = (byte)(i * 37);
        }

        Assert.Null(new WorldEncryption().decrypt(wire, wire.Length));
    }

    [Fact]
    public void DropsAPacketWithABadChecksum()
    {
        byte[] plain = { 0x02, 0x04, 0x01, 0x00, 0x05, 0x01, 0x02, 0x81, 0x4c };
        byte[] wire = new WorldEncryption().encrypt(plain, plain.Length, 0x7f, 3, 4);
        wire[wire.Length - 1] ^= 0xff; // flip the last encrypted block

        Assert.Null(new WorldEncryption().decrypt(wire, wire.Length));
    }

    /// <summary>
    /// A client's packets are decrypted on the socket callback while its queue is encrypted on the flush thread. Sharing
    /// a working buffer sent all-zero 2065-byte packets now and then (seen by tools/HeadlessClient's login scenario).
    /// </summary>
    [Fact]
    public async Task EncryptingWhileDecryptingOnAnotherThreadAlwaysGivesAValidPacket()
    {
        WorldEncryption shared = new WorldEncryption();
        byte[] incoming = new WorldEncryption().encrypt(new byte[] { 0x02 }, 1, 0, 0, 1);
        byte[] plain = new byte[700];
        plain[0] = 0x02;

        using CancellationTokenSource stop = new CancellationTokenSource();
        Task decrypting = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                Assert.NotNull(shared.decrypt(incoming, incoming.Length));
            }
        });

        WorldEncryption reader = new WorldEncryption();
        for (int i = 0; i < 3000; i++)
        {
            byte[] packet = shared.encrypt(plain, plain.Length, 0, 0, (ushort)(i & 0xfff));
            ArrayList decrypted = reader.decrypt(packet, packet.Length);
            Assert.NotNull(decrypted);
            Assert.Equal(plain, (byte[])decrypted[0]);
        }

        stop.Cancel();
        await decrypting;
    }

    [Fact]
    public void EncryptGeneratesFreshIvAndNeverEmitsZeroWireByteFive()
    {
        WorldEncryption sender = new WorldEncryption();
        WorldEncryption reader = new WorldEncryption();
        byte[] plain = { 0x02 };
        byte[]? previousIv = null;

        for (int i = 0; i < 2000; i++)
        {
            byte[] wire = sender.encrypt(plain, plain.Length, 0x7f, 1, (ushort)((i + 1) & 0xfff));
            Assert.Equal(0x01, wire[0]);
            Assert.NotEqual(0x00, wire[5]); // IV[4]: stock client CThreadedUDPDriver::GetPacket drops wire[5] == 0

            byte[] iv = wire[1..17];
            if (previousIv != null)
            {
                Assert.NotEqual(previousIv, iv);
            }
            previousIv = iv;

            ArrayList decrypted = reader.decrypt(wire, wire.Length);
            Assert.NotNull(decrypted);
            Assert.Equal(plain, (byte[])decrypted[0]);
        }
    }

    [Fact]
    public void DecryptingClientPacketWithZeroIvByteFourDoesNotPoisonSubsequentEncrypts()
    {
        byte[] sessionKey = new byte[16];
        for (int i = 0; i < sessionKey.Length; i++) sessionKey[i] = (byte)(0x21 + i);

        // Build a valid encrypted client->server datagram whose IV[4] == 0x00 (wire[5] == 0x00),
        // matching the 1-in-256 packets stock client.dll sends that used to poison WorldEncryption.IV
        // and cause a 12-second server->client blackout.
        byte[] badClientIv = { 0x11, 0x22, 0x33, 0x44, 0x00, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff };
        byte[] clientPlain = { 0x02 };
        byte[] clientWire = EncryptWithExplicitIv(sessionKey, badClientIv, clientPlain, 0x7f, 136, 256);
        Assert.Equal(0x00, clientWire[5]);

        WorldEncryption server = new WorldEncryption(sessionKey);
        ArrayList inbound = server.decrypt(clientWire, clientWire.Length);
        Assert.NotNull(inbound);
        Assert.Equal(clientPlain, (byte[])inbound[0]);

        WorldEncryption clientPeer = new WorldEncryption(sessionKey);
        for (int i = 0; i < 500; i++)
        {
            byte[] reply = server.encrypt(clientPlain, clientPlain.Length, 0x7f, 136, (ushort)(257 + i));
            Assert.NotEqual(0x00, reply[5]);
            Assert.NotEqual(badClientIv, reply[1..17]);

            ArrayList decrypted = clientPeer.decrypt(reply, reply.Length);
            Assert.NotNull(decrypted);
            Assert.Equal(clientPlain, (byte[])decrypted[0]);
        }
    }

    [Theory]
    [InlineData(128, "9F589F5CF6122C32B6BFEC2F2AE8C35A")]
    [InlineData(192, "EFA71F788965BD4453F860178FC19101")]
    [InlineData(256, "57FF739D4DC92C1BD7FC01700CC8216F")]
    public void TwofishSboxTablesMatchOfficialSchneierEcbKnownAnswerVectors(int keyBits, string expectedHex)
    {
        byte[] key = new byte[keyBits / 8];
        byte[] plain = new byte[16];
        byte[] cipher = new byte[16];
        byte[] decrypted = new byte[16];

        using var tf = new ManyMonkeys.Cryptography.Twofish
        {
            Mode = System.Security.Cryptography.CipherMode.ECB,
            KeySize = keyBits,
            BlockSize = 128
        };

        using var enc = tf.CreateEncryptor(key, new byte[16]);
        enc.TransformBlock(plain, 0, 16, cipher, 0);
        Assert.Equal(expectedHex, Convert.ToHexString(cipher));

        using var dec = tf.CreateDecryptor(key, new byte[16]);
        dec.TransformBlock(cipher, 0, 16, decrypted, 0);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void MxoTwofishReusesKeyAcrossMultipleIvsAndInvalidatesOnSetKey()
    {
        byte[] key1 = Enumerable.Range(0, 16).Select(i => (byte)(0x10 + i)).ToArray();
        byte[] key2 = Enumerable.Range(0, 16).Select(i => (byte)(0x80 + i)).ToArray();
        byte[] plain = Enumerable.Range(0, 1280).Select(i => (byte)(i * 31 + 7)).ToArray();
        byte[] cipher1 = new byte[plain.Length];
        byte[] cipher2 = new byte[plain.Length];
        byte[] roundTrip = new byte[plain.Length];

        var tf = new MxoTwofish();
        tf.SetKey(key1);

        for (int i = 0; i < 16; i++)
        {
            byte[] iv = Enumerable.Range(0, 16).Select(b => (byte)(i * 17 + b + 1)).ToArray();
            tf.SetIV(iv);
            tf.Encrypt(plain, cipher1);

            // Fresh instance must match byte-for-byte with reused key schedule
            var reference = new MxoTwofish();
            reference.SetKey(key1);
            reference.SetIV(iv);
            reference.Encrypt(plain, cipher2);
            Assert.Equal(cipher2, cipher1);

            tf.SetIV(iv);
            tf.Decrypt(cipher1, roundTrip);
            Assert.Equal(plain, roundTrip);
        }

        byte[] sharedIv = Enumerable.Repeat((byte)0x5A, 16).ToArray();
        tf.SetIV(sharedIv);
        tf.Encrypt(plain, cipher1);

        tf.SetKey(key2);
        tf.SetIV(sharedIv);
        tf.Encrypt(plain, cipher2);
        Assert.NotEqual(cipher1, cipher2);

        tf.SetIV(sharedIv);
        tf.Decrypt(cipher2, roundTrip);
        Assert.Equal(plain, roundTrip);
    }

    [Fact]
    public void DecryptDropsShortOrUnalignedBuffersAndCrcSliceMatchesFullBuffer()
    {
        var crypto = new WorldEncryption();
        Assert.Null(crypto.decrypt(null!, 0));
        Assert.Null(crypto.decrypt(Array.Empty<byte>(), 0));
        Assert.Null(crypto.decrypt(new byte[17], 17));
        Assert.Null(crypto.decrypt(new byte[32], 32));
        Assert.Null(crypto.decrypt(new byte[34], 34));

        byte[] data = Enumerable.Range(0, 128).Select(i => (byte)(i * 13 + 3)).ToArray();
        byte[] slice = data[17..91];
        var crc = new Crc32();
        Assert.Equal(crc.checksum(slice), crc.checksum(data, 17, 74));

        var dyn = new DynamicArray();
        dyn.append((byte)0x01);
        dyn.append(new byte[] { 0x02, 0x03 });
        dyn.append((byte)0x04);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, dyn.getBytes());
    }

    [Fact]
    public void Twofish128MatchesSchneier49StepIterativeKnownAnswerVector()
    {
        // Schneier Twofish-128 iterative KAT (ecb_tbl.txt): chains 49 distinct keys and plaintexts,
        // exercising all 256 entries of the precomputed S-box/MDS tables and round subkey schedule.
        byte[] ctPrev2 = new byte[16];
        byte[] ctPrev1 = new byte[16];
        byte[] ct = new byte[16];
        byte[] decOut = new byte[16];

        using var tf = new ManyMonkeys.Cryptography.Twofish
        {
            Mode = System.Security.Cryptography.CipherMode.ECB,
            KeySize = 128,
            BlockSize = 128
        };

        for (int i = 1; i <= 49; i++)
        {
            byte[] key = (byte[])ctPrev2.Clone();
            byte[] pt = (byte[])ctPrev1.Clone();
            using var enc = tf.CreateEncryptor(key, new byte[16]);
            enc.TransformBlock(pt, 0, 16, ct, 0);

            if (i == 1)
            {
                Assert.Equal("9F589F5CF6122C32B6BFEC2F2AE8C35A", Convert.ToHexString(ct));
            }
            else if (i == 49)
            {
                Assert.Equal("5D9D4EEFFA9151575524F115815A12E0", Convert.ToHexString(ct));
            }

            using var dec = tf.CreateDecryptor(key, new byte[16]);
            dec.TransformBlock(ct, 0, 16, decOut, 0);
            Assert.Equal(pt, decOut);

            Array.Copy(ctPrev1, ctPrev2, 16);
            Array.Copy(ct, ctPrev1, 16);
        }
    }

    [Fact]
    public async Task ConcurrentMultiKeyMultiBlockEncryptAndDecryptNeverDropsOrCorrupts()
    {
        var rng = new Random(1236);
        for (int session = 0; session < 24; session++)
        {
            byte[] key = new byte[16];
            rng.NextBytes(key);
            var serverEnc = new WorldEncryption(key);
            var clientEnc = new WorldEncryption(key);

            // Build multi-block client->server packets covering wire sizes 33..177 (1..10 Twofish blocks).
            byte[][] inboundWire = new byte[160][];
            byte[][] inboundPlain = new byte[160][];
            for (int i = 0; i < inboundWire.Length; i++)
            {
                int len = 1 + (i % 144);
                inboundPlain[i] = new byte[len];
                rng.NextBytes(inboundPlain[i]);
                inboundWire[i] = clientEnc.encrypt(inboundPlain[i], len, 0x7f, (ushort)(i & 0xfff), (ushort)((i + 1) & 0xfff));
            }

            using var stop = new CancellationTokenSource();
            int outboundErrors = 0;
            Task flushWriter = Task.Run(() =>
            {
                byte[] mobUpdate = new byte[96];
                mobUpdate[0] = 0x02;
                while (!stop.IsCancellationRequested)
                {
                    byte[] wire = serverEnc.encrypt(mobUpdate, mobUpdate.Length, 0x7f, 1, 1);
                    ArrayList? roundTrip = clientEnc.decrypt(wire, wire.Length);
                    if (roundTrip == null || !((byte[])roundTrip[0]!).AsSpan().SequenceEqual(mobUpdate))
                    {
                        Interlocked.Increment(ref outboundErrors);
                    }
                }
            });

            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 0; i < inboundWire.Length; i++)
                {
                    ArrayList? decrypted = serverEnc.decrypt(inboundWire[i], inboundWire[i].Length);
                    Assert.NotNull(decrypted);
                    Assert.Equal(inboundPlain[i], (byte[])decrypted![0]!);
                }
            }

            stop.Cancel();
            await flushWriter;
            Assert.Equal(0, outboundErrors);
        }
    }

    [Fact]
    public void PooledEncryptFromWorkBufferAndTryDecryptRoundTripWithNonZeroWire5AndStripTimed()
    {
        byte[] sessionKey = Enumerable.Range(1, 16).Select(i => (byte)(i * 11)).ToArray();
        var server = new WorldEncryption(sessionKey);
        var client = new WorldEncryption(sessionKey);

        // Exercise > 256 datagrams so the thread-local 4 KB CSPRNG IV pool refills multiple times.
        var seenIvs = new HashSet<string>();
        for (int i = 0; i < 600; i++)
        {
            byte[] plain = new byte[1 + (i % 180)];
            plain[0] = (i % 3 == 0 && plain.Length >= 6) ? (byte)0x82 : (byte)0x02;
            for (int j = 1; j < plain.Length; j++) plain[j] = (byte)((i + j * 7) & 0xFF);

            byte[] workBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(14 + plain.Length + 16);
            byte[]? wireBuf = null;
            try
            {
                Buffer.BlockCopy(plain, 0, workBuf, 14, plain.Length);
                ushort pss = (ushort)(i & 0x7F);
                ushort cseq = (ushort)(i & 0xFFF);
                ushort sseq = (ushort)((i + 17) & 0xFFF);
                server.EncryptFromWorkBuffer(workBuf, plain.Length, pss, cseq, sseq, out wireBuf, out int wireLen);

                Assert.Equal(0x01, wireBuf[0]);
                Assert.NotEqual(0x00, wireBuf[5]);
                seenIvs.Add(Convert.ToHexString(wireBuf, 1, 16));

                byte[] exactWire = new byte[wireLen];
                Buffer.BlockCopy(wireBuf, 0, exactWire, 0, wireLen);

                Assert.True(client.TryDecrypt(exactWire, exactWire.Length, out byte[] rawOut, out ushort outPss, out ushort outCseq, out ushort outSseq, stripTimedHeader: false));
                Assert.Equal(plain, rawOut);
                Assert.Equal(pss, outPss);
                // encrypt() places cseq in bits 12..23 and sseq in bits 0..11; decrypt() unpacks bits 0..11 as cseq and 12..23 as sseq.
                Assert.Equal(sseq, outCseq);
                Assert.Equal(cseq, outSseq);

                Assert.True(client.TryDecrypt(exactWire, exactWire.Length, out byte[] strippedOut, out _, out _, out _, stripTimedHeader: true));
                if (plain.Length >= 5 && plain[0] == 0x82)
                {
                    Assert.Equal(plain.Length - 4, strippedOut.Length);
                    Assert.Equal(0x02, strippedOut[0]);
                    Assert.Equal(plain[5..], strippedOut[1..]);
                }
                else
                {
                    Assert.Equal(plain, strippedOut);
                }
            }
            finally
            {
                if (wireBuf != null) System.Buffers.ArrayPool<byte>.Shared.Return(wireBuf);
                System.Buffers.ArrayPool<byte>.Shared.Return(workBuf);
            }
        }
        Assert.Equal(600, seenIvs.Count);
    }

    [Fact]
    public void WorldPacketWriteFinalDataMatchesGetFinalDataAndInlineMovementPayload()
    {
        var queue = new MessageQueue();
        byte[] rawMove = { SelfViewUpdate.FlagAction | SelfViewUpdate.FlagYaw | SelfViewUpdate.FlagPosition, 0x3e, 0x40,
            0, 0, 0x80, 0x3f, 0, 0, 0x00, 0x40, 0, 0, 0x40, 0x40 };
        Assert.True(SelfViewUpdate.TryParse(rawMove, 0, out SelfViewUpdate update));
        byte[] forwarded = update.ToForwardedUpdate(rawMove);
        queue.AddOrCoalesceMovement(42, update, forwarded);

        var msg = (SequencedMessage)queue.ObjectMessagesQueue[0]!;
        var dataA = new ClientData();
        dataA.lastSimTimeUpdate = 1;
        dataA.lastSimTimeSeconds = TimeUtils.GetSimTimeSeconds();
        var pktA = new WorldPacket(dataA);
        Assert.True(pktA.addObjectContent(msg));

        byte[] workBuf = new byte[14 + pktA.EstimateMaxPlaintextSize() + 16];
        int writtenLen = pktA.WriteFinalData(dataA, workBuf, 14);
        byte[] directBytes = workBuf[14..(14 + writtenLen)];

        var dataB = new ClientData();
        dataB.lastSimTimeUpdate = 1;
        dataB.lastSimTimeSeconds = dataA.lastSimTimeSeconds;
        var pktB = new WorldPacket(dataB);
        Assert.True(pktB.addObjectContent(msg));
        byte[] legacyBytes = pktB.getFinalData(dataB);

        Assert.Equal(legacyBytes, directBytes);
        Assert.Equal(MessageQueue.BuildViewUpdateFast(42, forwarded), msg.content);
    }

    private static byte[] EncryptWithExplicitIv(byte[] key, byte[] iv, byte[] plainData, ushort pss, ushort cseq, ushort sseq)
    {
        uint seqValue = ((uint)pss << 24) | ((uint)cseq << 12) | sseq;
        PacketContent body = new PacketContent();
        body.AddUint16((ushort)(plainData.Length + 4), 1);
        body.AddByteArray(TimeUtils.getCurrentTime());
        body.AddUint32(seqValue, 0);
        body.AddByteArray(plainData);

        byte[] bodyBytes = body.ReturnFinalPacket();
        int padding = 16 - ((bodyBytes.Length + 4) % 16);
        byte[] padBytes = new byte[padding];
        Array.Fill(padBytes, (byte)padding);

        PacketContent withCrc = new PacketContent();
        withCrc.AddByteArray(new Crc32().checksumB(bodyBytes, 1));
        withCrc.AddByteArray(bodyBytes);
        withCrc.AddByteArray(padBytes);

        byte[] plaintext = withCrc.ReturnFinalPacket();
        byte[] ciphertext = new byte[plaintext.Length];
        MxoTwofish tf = new MxoTwofish();
        tf.SetKey(key);
        tf.SetIV(iv);
        tf.Encrypt(plaintext, ciphertext);

        PacketContent wire = new PacketContent();
        wire.AddByte(0x01);
        wire.AddByteArray(iv);
        wire.AddByteArray(ciphertext);
        return wire.ReturnFinalPacket();
    }
}
