using System;
using System.Linq;
using System.Security.Cryptography;
using hds.auth;
using HDS.Network;

namespace hds.Tests;

// Locks AS_GetPublicKeyReply to the layout and checks decompiled from launcher.exe
// (FUN_00444e20 logger, FUN_00447f50 handler, FUN_00447780 / FUN_00468f80 verify).
// See docs/CLIENT-SETUP-PLAN.md. The tests read the reply the way the launcher does and
// only use public key material for verification.
public class AuthPublicKeyReplyTests
{
    private const uint Time = 0x6543_2100;

    private static ushort U16(byte[] b, int o) => BitConverter.ToUInt16(b, o);
    private static uint U32(byte[] b, int o) => BitConverter.ToUInt32(b, o);

    // Strip the auth TCP length prefix and return the body starting at the opcode.
    private static byte[] Body(byte[] framed)
    {
        Assert.True(framed[0] >= 0x80, "long replies use the two-byte 0x8000 length prefix");
        int length = ((framed[0] << 8) | framed[1]) & 0x7FFF;
        Assert.Equal(framed.Length - 2, length);
        return framed.Skip(2).ToArray();
    }

    private static byte[] ArrayAt(byte[] body, int offset)
    {
        int length = U16(body, offset);
        Assert.True(offset + 2 + length <= body.Length, "array must fit inside the message");
        return body.Skip(offset + 2).Take(length).ToArray();
    }

    [Fact]
    public void FixedFieldsMatchDecompiledLayout()
    {
        byte[] body = Body(AuthHandler.BuildPublicKeyReply(Time));

        Assert.Equal((byte)AuthOpcode.AS_GetPublicKeyReply, body[0x00]);
        Assert.Equal(0u, U32(body, 0x01));                            // Status (<= 0 accepted)
        Assert.Equal(Time, U32(body, 0x05));                          // CurrentTime
        Assert.Equal(AuthHandler.ServerPublicKeyId, U32(body, 0x09)); // PublicKeyId
        Assert.Equal(AuthHandler.PublicKeyReplyFixedSize, U16(body, 0x0d)); // N right after fixed part
        Assert.Equal((byte)0x11, body[0x0f]);                         // E
        Assert.Equal(0x12 + 2 + 128, U16(body, 0x10));                // Signature after N
        Assert.Equal(0x12 + 2 + 128 + 2 + 256, body.Length);
    }

    [Fact]
    public void ServerKeyIsTheOneTheLauncherAccepts()
    {
        byte[] body = Body(AuthHandler.BuildPublicKeyReply(Time));
        byte[] n = ArrayAt(body, U16(body, 0x0d));

        // FUN_00447780 rejects anything but a 128-byte modulus.
        Assert.Equal(128, n.Length);
        Assert.True((n[0] & 0x80) != 0, "top bit set, so Integer::Encode(…, 128) is lossless");
        Assert.Equal(MxoRSA.ServerPublicModulus(), n);
    }

    [Fact]
    public void SignatureVerifiesWithRootPublicKeyOverModulusAndExponent()
    {
        byte[] body = Body(AuthHandler.BuildPublicKeyReply(Time));
        byte[] n = ArrayAt(body, U16(body, 0x0d));
        byte e = body[0x0f];
        byte[] signature = ArrayAt(body, U16(body, 0x10));

        Assert.Equal(256, signature.Length);

        // The launcher builds its verifier from the embedded 256-byte modulus and e = 0x11.
        byte[] rootModulus = MxoRSA.RootPublicModulus();
        Assert.Equal(256, rootModulus.Length);
        Assert.Equal((byte)0x11, MxoRSA.RootPublicExponent());

        using RSA root = RSA.Create();
        root.ImportParameters(new RSAParameters { Modulus = rootModulus, Exponent = new byte[] { 0x11 } });

        byte[] message = n.Concat(new[] { e }).ToArray();
        Assert.True(root.VerifyData(message, signature, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1));

        // Tampering with E or N must break it (guards against signing the wrong bytes).
        message[^1] ^= 0x01;
        Assert.False(root.VerifyData(message, signature, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void AdvertisedKeyDecryptsWhatTheLauncherEncrypts()
    {
        byte[] body = Body(AuthHandler.BuildPublicKeyReply(Time));
        byte[] n = ArrayAt(body, U16(body, 0x0d));

        // The launcher OAEP-encrypts the login blob to (N, E); hds must be able to decrypt it.
        using RSA advertised = RSA.Create();
        advertised.ImportParameters(new RSAParameters { Modulus = n, Exponent = new[] { body[0x0f] } });
        byte[] blob = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();
        byte[] cipher = advertised.Encrypt(blob, RSAEncryptionPadding.OaepSHA1);

        Assert.Equal(blob, new MxoRSA().decryptWithPrivkey(cipher));
    }

    [Fact]
    public void ShortMessagesKeepTheOneByteLengthPrefix()
    {
        byte[] framed = AuthHandler.FrameAuthMessage(new byte[0x12]);
        Assert.Equal(0x12, framed[0]);
        Assert.Equal(0x13, framed.Length);

        byte[] longFramed = AuthHandler.FrameAuthMessage(new byte[0x80]);
        Assert.Equal(new byte[] { 0x80, 0x80 }, longFramed.Take(2).ToArray());
    }
}
