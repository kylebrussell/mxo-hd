using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using hds;

namespace HeadlessClient;

public sealed record CharacterEntry(string Handle, uint CharId);

/// <summary>What the auth server hands a logged-in client for the margin: a signed ticket and the account's key.</summary>
public sealed class AuthTicket
{
    public required byte[] Signature;
    public required byte[] SignedData;
    public required byte[] PrivateExponent;
    public required List<CharacterEntry> Characters;

    public uint UserId => BitConverter.ToUInt32(SignedData, 1);
    public byte[] PublicModulus => SignedData[82..178];
}

/// <summary>
/// The launcher's login, on the auth TCP port: get the server key, send our Twofish key and name under it, answer the
/// challenge with MD5 and the password, and read back the character list and margin ticket.
/// </summary>
public static class AuthLogin
{
    private const byte GetPublicKeyRequest = 0x06;
    private const byte GetPublicKeyReply = 0x07;
    private const byte AuthRequest = 0x08;
    private const byte AuthChallenge = 0x09;
    private const byte AuthChallengeResponse = 0x0a;
    private const byte AuthReply = 0x0b;

    public static async Task<AuthTicket> LoginAsync(string host, int port, string username, string password, CancellationToken cancel)
    {
        using TcpClient tcp = new();
        await tcp.ConnectAsync(host, port, cancel);
        NetworkStream stream = tcp.GetStream();
        FramedReader reader = new(stream);

        // Public key: the launcher asks once, then caches it
        await stream.WriteAsync(FramedReader.Frame(new byte[] { GetPublicKeyRequest, 0, 0 }), cancel);
        byte[] keyReply = await reader.ReadFrameAsync(cancel);
        if (keyReply[0] != GetPublicKeyReply)
        {
            throw new InvalidDataException("Expected a public key reply, got " + WireFormat.ToHex(keyReply));
        }
        int modulusOffset = BitConverter.ToUInt16(keyReply, 13);
        byte exponent = keyReply[15];
        int modulusLength = BitConverter.ToUInt16(keyReply, modulusOffset);
        byte[] modulus = keyReply[(modulusOffset + 2)..(modulusOffset + 2 + modulusLength)];

        // Auth request: blob under the server key is 7 unknown bytes, our 16-byte Twofish key, 6 more, the name, NUL
        byte[] twofishKey = RandomNumberGenerator.GetBytes(16);
        byte[] blob = new byte[29 + username.Length + 1];
        twofishKey.CopyTo(blob, 7);
        Encoding.ASCII.GetBytes(username).CopyTo(blob, 29);
        using RSA serverKey = RSA.Create(new RSAParameters { Modulus = modulus, Exponent = new[] { exponent } });
        byte[] encryptedBlob = serverKey.Encrypt(blob, RSAEncryptionPadding.OaepSHA1);

        // Opcode, u32 key version (the server's PublicKeyId), then the blob at 42. The server counts offsets with the
        // two-byte frame prefix on the front, so it reads these at 3 and 44.
        byte[] request = new byte[42 + encryptedBlob.Length];
        request[0] = AuthRequest;
        BitConverter.GetBytes(4u).CopyTo(request, 1);
        encryptedBlob.CopyTo(request, 42);
        await stream.WriteAsync(FramedReader.Frame(request), cancel);

        // Challenge: 16 bytes under our key with a zero IV. The answer is its MD5, and it is the IV for the private key
        byte[] challengeReply = await reader.ReadFrameAsync(cancel);
        if (challengeReply[0] != AuthChallenge)
        {
            throw new InvalidDataException("Expected a challenge, got " + WireFormat.ToHex(challengeReply));
        }
        byte[] encryptedChallenge = challengeReply[1..17];
        MxoTwofish twofish = new();
        twofish.SetKey(twofishKey);
        twofish.SetIV(new byte[16]);
        byte[] challenge = new byte[16];
        twofish.Decrypt(encryptedChallenge, challenge);
        byte[] challengeMd5 = new Md5().digest(challenge);

        // The answer: 1 byte, the MD5, 6 bytes, u16 password size (with NUL), password
        byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
        int plainSize = (25 + passwordBytes.Length + 1 + 15) / 16 * 16;
        byte[] answer = new byte[plainSize];
        challengeMd5.CopyTo(answer, 1);
        BitConverter.GetBytes((ushort)(passwordBytes.Length + 1)).CopyTo(answer, 23);
        passwordBytes.CopyTo(answer, 25);
        twofish.SetIV(new byte[16]);
        byte[] encryptedAnswer = new byte[plainSize];
        twofish.Encrypt(answer, encryptedAnswer);

        byte[] response = new byte[5 + plainSize];
        response[0] = AuthChallengeResponse;
        response[3] = (byte)plainSize;
        encryptedAnswer.CopyTo(response, 5);
        await stream.WriteAsync(FramedReader.Frame(response), cancel);

        // World and character list, or an error
        byte[] reply = await reader.ReadFrameAsync(cancel);
        if (reply[0] != AuthReply)
        {
            throw new InvalidDataException("Expected an auth reply, got " + WireFormat.ToHex(reply));
        }
        uint status = BitConverter.ToUInt32(reply, 1);
        if (status != 0 || reply.Length < 33)
        {
            throw new InvalidOperationException($"Login of {username} refused: status {status:x8}");
        }
        return ParseReply(reply, twofish, twofishKey, encryptedChallenge);
    }

    private static AuthTicket ParseReply(byte[] reply, MxoTwofish twofish, byte[] twofishKey, byte[] encryptedChallenge)
    {
        int authData = BitConverter.ToUInt16(reply, 11);
        int encryptedData = BitConverter.ToUInt16(reply, 13);
        int charData = BitConverter.ToUInt16(reply, 19);

        int ticketSize = BitConverter.ToUInt16(reply, authData);
        byte[] signature = reply[(authData + 2)..(authData + 2 + 128)];
        byte[] signedData = reply[(authData + 2 + 128)..(authData + 2 + ticketSize)];

        int exponentSize = BitConverter.ToUInt16(reply, encryptedData);
        byte[] encryptedExponent = reply[(encryptedData + 2)..(encryptedData + 2 + exponentSize)];
        byte[] privateExponent = new byte[exponentSize];
        twofish.SetKey(twofishKey);
        twofish.SetIV(encryptedChallenge);
        twofish.Decrypt(encryptedExponent, privateExponent);

        // Characters: u16 count, 14 bytes each (id at 3), then the names as u16 size (with NUL) and bytes
        int count = BitConverter.ToUInt16(reply, charData);
        int cursor = charData + 2 + count * 14;
        List<CharacterEntry> characters = new();
        for (int i = 0; i < count; i++)
        {
            uint charId = BitConverter.ToUInt32(reply, charData + 2 + i * 14 + 3);
            int size = BitConverter.ToUInt16(reply, cursor);
            characters.Add(new CharacterEntry(Encoding.ASCII.GetString(reply, cursor + 2, size - 1), charId));
            cursor += 2 + size;
        }

        return new AuthTicket
        {
            Signature = signature,
            SignedData = signedData,
            PrivateExponent = privateExponent,
            Characters = characters,
        };
    }

    /// <summary>RSA with the account's raw key and OAEP-SHA1. The key is only a modulus and exponent, so .NET's RSA can't import it.</summary>
    public static byte[] DecryptWithAccountKey(byte[] cipher, byte[] modulusBigEndian, byte[] privateExponentBigEndian)
    {
        BigInteger n = new(modulusBigEndian, isUnsigned: true, isBigEndian: true);
        BigInteger d = new(privateExponentBigEndian, isUnsigned: true, isBigEndian: true);
        BigInteger c = new(cipher, isUnsigned: true, isBigEndian: true);
        byte[] raw = BigInteger.ModPow(c, d, n).ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] em = new byte[modulusBigEndian.Length];
        raw.CopyTo(em, em.Length - raw.Length);
        return OaepDecode(em);
    }

    private static byte[] OaepDecode(byte[] em)
    {
        const int hashLength = 20;
        if (em[0] != 0)
        {
            throw new CryptographicException("OAEP: first byte is not zero");
        }
        byte[] maskedSeed = em[1..(1 + hashLength)];
        byte[] maskedDb = em[(1 + hashLength)..];
        byte[] seed = Xor(maskedSeed, Mgf1(maskedDb, hashLength));
        byte[] db = Xor(maskedDb, Mgf1(seed, maskedDb.Length));
        byte[] emptyLabelHash = SHA1.HashData(Array.Empty<byte>());
        if (!db[..hashLength].SequenceEqual(emptyLabelHash))
        {
            throw new CryptographicException("OAEP: label hash doesn't match");
        }
        int separator = Array.IndexOf(db, (byte)1, hashLength);
        return separator < 0 ? throw new CryptographicException("OAEP: no separator") : db[(separator + 1)..];
    }

    private static byte[] Mgf1(byte[] seed, int length)
    {
        List<byte> mask = new();
        for (uint counter = 0; mask.Count < length; counter++)
        {
            byte[] block = new byte[seed.Length + 4];
            seed.CopyTo(block, 0);
            block[^4] = (byte)(counter >> 24);
            block[^3] = (byte)(counter >> 16);
            block[^2] = (byte)(counter >> 8);
            block[^1] = (byte)counter;
            mask.AddRange(SHA1.HashData(block));
        }
        return mask.Take(length).ToArray();
    }

    private static byte[] Xor(byte[] a, byte[] b) => a.Select((value, i) => (byte)(value ^ b[i])).ToArray();
}

/// <summary>The TCP length prefix auth and margin share: one byte under 0x80, else big-endian u16 with the 0x8000 flag.</summary>
public sealed class FramedReader
{
    private readonly NetworkStream stream;

    public FramedReader(NetworkStream stream)
    {
        this.stream = stream;
    }

    public static byte[] Frame(byte[] body)
    {
        byte[] prefix = body.Length < 0x80
            ? new[] { (byte)body.Length }
            : new[] { (byte)(0x80 | (body.Length >> 8)), (byte)body.Length };
        return prefix.Concat(body).ToArray();
    }

    public async Task<byte[]> ReadFrameAsync(CancellationToken cancel)
    {
        int length;
        do
        {
            // The auth server ends its reply with `00 00`: two empty frames
            byte first = await ReadByteAsync(cancel);
            length = first < 0x80 ? first : ((first - 0x80) << 8) + await ReadByteAsync(cancel);
        } while (length == 0);

        byte[] body = new byte[length];
        await stream.ReadExactlyAsync(body, cancel);
        return body;
    }

    private async Task<byte> ReadByteAsync(CancellationToken cancel)
    {
        byte[] one = new byte[1];
        await stream.ReadExactlyAsync(one, cancel);
        return one[0];
    }
}
