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

    /// <summary>
    /// This login's session key: margin generates one per connection and sends it under the account's RSA key. Margin
    /// traffic after the certificate exchange and the character's world UDP session are encrypted with it.
    /// </summary>
    public byte[]? SessionKey { get; private set; }
    public byte[] LoginReply { get; private set; } = Array.Empty<byte>();

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
        if (SessionKey.SequenceEqual(MarginEncryption.LegacyKey))
        {
            // Every login used to get this one fixed key, so anyone could read and forge anyone's traffic.
            throw new InvalidDataException("Margin sent the old fixed session key");
        }
        encryption.SetSessionKey(SessionKey);

        await ExchangeAsync(new byte[] { CertConnectAck }, 0x04, cancel);
        await ExchangeAsync(new byte[] { ConnectChallenge }, ConnectChallengeReply, cancel);
        await ExchangeAsync(new byte[17].Select((_, i) => i == 0 ? ConnectChallengeResponse : (byte)0).ToArray(), ConnectResponse, cancel);
    }

    public async Task LoadCharacterAsync(uint charId, CancellationToken cancel, bool stock = false)
    {
        byte[] request = stock ? StockCharacterLoadRequest(charId) : new byte[7];
        if (!stock)
        {
            request[0] = LoadCharacter;
            BitConverter.GetBytes(charId).CopyTo(request, 1);
        }
        RememberSessionKey(charId);
        await SendAsync(request, cancel);

        await ReadCharacterDataAsync(charId, cancel);
    }

    public static byte[] StockCharacterLoadRequest(uint charId)
    {
        // Native PR266 margin-rows line 516, without encrypted framing or padding.
        // Preserve the captured empty-cache record and substitute only GlobalCharacterId.
        byte[] request = new byte[190];
        request[0] = LoadCharacter;
        BitConverter.GetBytes(charId).CopyTo(request, 1);
        byte[] emptyDigest = Convert.FromHexString("d41d8cd98f00b204e9800998ecf8427e");
        for (int offset = 41; offset < 185; offset += emptyDigest.Length)
            emptyDigest.CopyTo(request, offset);
        request[185] = 0xbb;
        request[187] = 1;
        return request;
    }

    private async Task ReadCharacterDataAsync(uint charId, CancellationToken cancel)
    {
        while (true)
        {
            byte[] reply = await ReadEncryptedAsync(cancel);
            if(CreationTranscript.Count>0) CreationTranscript.Add(("S2C",reply.ToArray()));
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
                byte[] data = reply[18..(18 + size)];
                if (opcode == hds.QaCharacterUi.DataType)
                {
                    byte[] previous = CharacterData.GetValueOrDefault(opcode) ?? Array.Empty<byte>();
                    if (previous.Length != (reply[11] - 1) * hds.QaCharacterUi.ChunkSize ||
                        size is <= 0 or > hds.QaCharacterUi.ChunkSize ||
                        previous.Length + size > hds.QaCharacterUi.MaxLength)
                        throw new InvalidDataException("Invalid stock character UI reply chunk.");
                    data = previous.Concat(data).ToArray();
                }
                CharacterData[opcode] = data;
            }
            if (reply[12] == 1)
            {
                return;
            }
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, byte[]> sessionKeys = new();

    /// <summary>
    /// The session key of the margin connection that last loaded or reserved this character, for a WorldSession made
    /// without one (WorldSession.SendSessionRequest looks it up). Callers that hold the MarginSession can pass
    /// SessionKey to the WorldSession directly instead.
    /// </summary>
    public static byte[]? SessionKeyFor(uint charId) => sessionKeys.TryGetValue(charId, out byte[]? key) ? key.ToArray() : null;

    private void RememberSessionKey(uint charId)
    {
        if (SessionKey != null) sessionKeys[charId] = SessionKey.ToArray();
    }

    /// <summary>Only creation/name bodies, never certificates, tickets or account private keys.</summary>
    public List<(string Direction, byte[] Body)> CreationTranscript { get; } = new();

    public async Task<uint> ReserveCharacterAsync(string handle, CancellationToken cancel)
    {
        byte[] body = CharacterCreationWire.Reserve(handle);
        CreationTranscript.Add(("C2S",body.ToArray()));
        await SendAsync(body,cancel);
        byte[] reply = await ReadEncryptedAsync(cancel);
        CreationTranscript.Add(("S2C",reply.ToArray()));
        if(reply.Length<11 || reply[0]!=0x0b || reply[3]!=0 || reply[6]!=0)
            throw new InvalidDataException("Character reservation refused: "+Convert.ToHexString(reply));
        uint id=BitConverter.ToUInt32(reply,7);
        if(id==0)throw new InvalidDataException("Zero character reservation");
        RememberSessionKey(id);
        return id;
    }

    public async Task CreateCharacterAsync(uint reservedId, byte[] body, CancellationToken cancel)
    {
        if(body.Length<77 || body[0]!=0x0c)throw new ArgumentException("Expected full creation body");
        CreationTranscript.Add(("C2S",body.ToArray()));
        await SendAsync(body,cancel);
        await ReadCharacterDataAsync(reservedId,cancel);
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
        if (expectedReply == ConnectResponse) LoginReply = reply.ToArray();
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
