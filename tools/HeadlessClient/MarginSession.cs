using System.Net.Sockets;
using hds;

namespace HeadlessClient;

/// <summary>
/// The character server, on the margin TCP port: hand over the auth ticket, get the session key, and load a character.
/// Stays connected for the world session, as the real client does: the server answers the world's UDP setup here.
/// </summary>
public sealed class MarginSession : IDisposable
{
    private const byte CertConnectRequest = 0x01;
    private const byte CertConnectReply = 0x02;
    private const byte CertConnectAck = 0x03;
    private const byte ConnectChallenge = 0x06;
    private const byte ConnectChallengeReply = 0x07;
    private const byte ConnectChallengeResponse = 0x08;
    private const byte ConnectResponse = 0x09;
    private const byte LoadCharacter = 0x0f;
    private const byte CharacterReply = 0x10;
    private const byte UdpSessionReply = 0x11;

    private readonly TcpClient tcp = new();
    private readonly MarginEncryption encryption = new();
    private NetworkStream? stream;
    private FramedReader? reader;

    /// <summary>Character data blocks by opcode (05 inventory, 07 known abilities...), from the last load.</summary>
    public Dictionary<byte, byte[]> CharacterData { get; } = new();

    /// <summary>The world session key the server sent under the account's key (which is what WorldEncryption defaults to).</summary>
    public byte[]? SessionKey { get; private set; }

    public async Task ConnectAsync(string host, int port, AuthTicket ticket, CancellationToken cancel)
    {
        await tcp.ConnectAsync(host, port, cancel);
        stream = tcp.GetStream();
        reader = new FramedReader(stream);

        // The one unencrypted packet: `01 03 00 <ticket size u16> <signature> <signed data>`. Encrypted ones are told
        // apart by the first byte, so this one has to be 0x81: it is, because the body is between 256 and 511 bytes.
        List<byte> body = new() { CertConnectRequest, 0x03, 0x00 };
        body.AddRange(BitConverter.GetBytes((ushort)(ticket.Signature.Length + ticket.SignedData.Length)));
        body.AddRange(ticket.Signature);
        body.AddRange(ticket.SignedData);
        await stream.WriteAsync(FramedReader.Frame(body.ToArray()), cancel);

        byte[] reply = await reader.ReadFrameAsync(cancel);
        if (reply[0] != CertConnectReply)
        {
            throw new InvalidDataException("Expected margin 02, got " + WireFormat.ToHex(reply));
        }
        int blobSize = BitConverter.ToUInt16(reply, 3);
        byte[] plain = AuthLogin.DecryptWithAccountKey(reply[5..(5 + blobSize)], ticket.PublicModulus, ticket.PrivateExponent);
        SessionKey = plain[1..17];
        if (!SessionKey.SequenceEqual(encryption.getTFKey()))
        {
            // MarginEncryption has no way to set the key: the server's is fixed, and this class only works with that one.
            throw new InvalidDataException("Margin sent a session key other than the server's fixed one");
        }

        await ExchangeAsync(new byte[] { CertConnectAck }, 0x04, cancel);
        await ExchangeAsync(new byte[] { ConnectChallenge }, ConnectChallengeReply, cancel);
        await ExchangeAsync(new byte[17].Select((_, i) => i == 0 ? ConnectChallengeResponse : (byte)0).ToArray(), ConnectResponse, cancel);
    }

    public async Task LoadCharacterAsync(uint charId, CancellationToken cancel)
    {
        byte[] request = new byte[7];
        request[0] = LoadCharacter;
        BitConverter.GetBytes(charId).CopyTo(request, 1);
        await SendAsync(request, cancel);

        while (true)
        {
            byte[] reply = await ReadEncryptedAsync(cancel);
            if (reply[0] != CharacterReply)
            {
                throw new InvalidDataException("Expected character data, got " + WireFormat.ToHex(reply));
            }
            // 10 <u32 0> <charId> <u16> <count> <last> <opcode> <10 00|00 00> [<size u16> <data>]
            if (reply.Length < 14)
            {
                throw new InvalidDataException("Short character data: " + WireFormat.ToHex(reply));
            }
            if (reply[1] != 0 && reply[1] != 0x25)
            {
                throw new InvalidDataException("Character load failed: " + WireFormat.ToHex(reply));
            }
            if (reply[1] == 0x25)
            {
                throw new InvalidOperationException($"Character {charId} is already in use");
            }
            byte opcode = reply[13];
            if (reply[14] == 0x10)
            {
                int size = BitConverter.ToUInt16(reply, 16);
                CharacterData[opcode] = reply[18..(18 + size)];
            }
            if (reply[12] == 1)
            {
                return;
            }
        }
    }

    /// <summary>The 0x11 the margin sends once the world has our UDP packet.</summary>
    public async Task WaitForUdpSessionAsync(CancellationToken cancel)
    {
        byte[] reply = await ReadEncryptedAsync(cancel);
        if (reply[0] != UdpSessionReply)
        {
            throw new InvalidDataException("Expected margin 11, got " + WireFormat.ToHex(reply));
        }
    }

    private async Task ExchangeAsync(byte[] request, byte expectedReply, CancellationToken cancel)
    {
        await SendAsync(request, cancel);
        byte[] reply = await ReadEncryptedAsync(cancel);
        if (reply[0] != expectedReply)
        {
            throw new InvalidDataException($"Expected margin {expectedReply:x2}, got " + WireFormat.ToHex(reply));
        }
    }

    private async Task SendAsync(byte[] plain, CancellationToken cancel)
    {
        await stream!.WriteAsync(FramedReader.Frame(encryption.encrypt(plain)), cancel);
    }

    private async Task<byte[]> ReadEncryptedAsync(CancellationToken cancel)
    {
        byte[] frame = await reader!.ReadFrameAsync(cancel);
        // decryptMargin expects the packet with one length byte still on the front
        byte[] withPrefix = new byte[frame.Length + 1];
        frame.CopyTo(withPrefix, 1);
        return encryption.decryptMargin(withPrefix) ?? throw new InvalidDataException("Undecryptable margin frame");
    }

    public void Dispose()
    {
        tcp.Dispose();
    }
}
