using System.Text;
using hds;
using HdsAttribute = hds.Attribute;

namespace HeadlessClient;

/// <summary>One server RPC record: the bytes after the length, starting at the opcode.</summary>
public sealed record RpcRecord(ushort ListCounter, int Index, byte[] Bytes)
{
    /// <summary>The opcode as the server's headers number them: 0x28 for `28`, 0x187 for `81 87`.</summary>
    public ushort? DatagramSequence { get; init; }

    public int Opcode => WireFormat.OpcodeOf(Bytes);

    public string Hex => WireFormat.ToHex(Bytes);

    public override string ToString() => $"#{ListCounter}.{Index}: {Hex}";
}

/// <summary>What a decrypted world packet held: flags, sim time, the raw 03 list and the RPC lists.</summary>
public sealed class WorldPayload
{
    public byte Flags;
    public float? SimTime;
    public byte[]? ViewSection;
    public List<RpcRecord> Rpcs = new();

    /// <summary>The list's first RPC counter for each RPC list, with its size, in packet order.</summary>
    public List<(ushort Counter, int Count)> RpcLists = new();

    /// <summary>The server asks the client to reset its RPC counters (0x40, see WorldPacket.getFinalData).</summary>
    public bool ResetRequested => (Flags & 0x40) != 0;

    /// <summary>A bare ack (a flags byte and maybe the time). Acking one of these would ping-pong forever.</summary>
    public bool IsAckOnly => ViewSection == null && RpcLists.Count == 0;
}

/// <summary>A creation record found in a 03 list: `01 00 &lt;0c|08&gt; goid [static] counter cd ab attributes view 00`.</summary>
public sealed record ViewCreation(ushort ViewId, ushort Goid, byte SpawnId, IReadOnlyDictionary<string, byte[]> Attributes)
{
    /// <summary>The name attribute, when the record carries one.</summary>
    public string? Name => Attributes.TryGetValue("CharacterName", out byte[]? name) ? WireFormat.ZeroTerminated(name) : null;
}

/// <summary>
/// The client's side of the world packet layout. Everything here is written from the protocol description (the server's
/// parser and retail captures), not by calling the server's parser, so a parser bug can't hide behind itself. The
/// encryption is the server's own class.
/// </summary>
public static class WireFormat
{
    public const byte RpcSection = 0x04;
    public const byte ViewSection = 0x03;

    public static int OpcodeOf(byte[] record)
    {
        if (record.Length == 0)
        {
            return -1;
        }
        return record[0] >= 0x80 && record.Length > 1 ? ((record[0] - 0x80) << 8) + record[1] : record[0];
    }

    public static string ToHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    public static byte[] FromHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    public static string ZeroTerminated(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        return Encoding.Latin1.GetString(bytes, 0, end < 0 ? bytes.Length : end);
    }

    /// <summary>An RPC record's length prefix: one byte up to 0x7f, else big-endian u16 with the 0x8000 flag.</summary>
    public static byte[] RpcLength(int length)
    {
        return length > 0x7f
            ? new[] { (byte)(0x80 | (length >> 8)), (byte)length }
            : new[] { (byte)length };
    }

    /// <summary>`02 04 01 &lt;counter u16 BE&gt; &lt;count&gt;` then `&lt;length&gt; &lt;record&gt;` for each record.</summary>
    public static byte[] BuildRpcPacket(ushort counter, IReadOnlyCollection<byte[]> records, byte flags = 0x02)
    {
        List<byte> packet = new() { flags, RpcSection, 0x01, (byte)(counter >> 8), (byte)counter, (byte)records.Count };
        foreach (byte[] record in records)
        {
            packet.AddRange(RpcLength(record.Length));
            packet.AddRange(record);
        }
        return packet.ToArray();
    }

    /// <summary>`02 03 &lt;view list&gt; 00 00`: a client protocol 03 section.</summary>
    public static byte[] BuildViewPacket(byte[] viewList)
    {
        List<byte> packet = new() { 0x02, ViewSection };
        packet.AddRange(viewList);
        packet.Add(0);
        packet.Add(0);
        return packet.ToArray();
    }

    /// <summary>
    /// Reads a decrypted server packet. The server closes a packet's 03 list before starting RPCs
    /// (WorldClient.FlushQueue), so a list is the rest of the packet and ends in `00 00`; its view updates have no
    /// lengths, so it stays raw. An error in the RPC part throws: a real client would misread it too.
    /// </summary>
    public static WorldPayload ParsePayload(byte[] plain)
    {
        WorldPayload payload = new();
        if (plain.Length == 0)
        {
            return payload;
        }
        int offset = 0;
        payload.Flags = plain[offset++];
        if ((payload.Flags & 0x80) != 0)
        {
            payload.SimTime = BitConverter.ToSingle(plain, offset);
            offset += 4;
        }
        if (offset < plain.Length && plain[offset] == ViewSection)
        {
            offset++;
            if (plain.Length - offset < 2 || plain[^1] != 0 || plain[^2] != 0)
            {
                throw new FormatException("03 list doesn't end in view id 0: " + ToHex(plain));
            }
            payload.ViewSection = plain[offset..^2];
            return payload;
        }
        if (offset < plain.Length && plain[offset] == RpcSection)
        {
            offset++;
            int lists = plain[offset++];
            for (int list = 0; list < lists; list++)
            {
                ushort counter = (ushort)((plain[offset] << 8) | plain[offset + 1]);
                int count = plain[offset + 2];
                offset += 3;
                payload.RpcLists.Add((counter, count));
                for (int i = 0; i < count; i++)
                {
                    int length = plain[offset++];
                    if (length >= 0x80)
                    {
                        length = ((length - 0x80) << 8) + plain[offset++];
                    }
                    if (offset + length > plain.Length)
                    {
                        throw new FormatException($"RPC {i} of {count} runs past the packet: {ToHex(plain)}");
                    }
                    payload.Rpcs.Add(new RpcRecord(counter, i, plain[offset..(offset + length)]));
                    offset += length;
                }
            }
            if (offset != plain.Length)
            {
                throw new FormatException($"{plain.Length - offset} bytes after the RPC lists: {ToHex(plain)}");
            }
            return payload;
        }
        if (offset != plain.Length)
        {
            throw new FormatException("Unknown section after the flags: " + ToHex(plain));
        }
        return payload;
    }

    /// <summary>
    /// Finds the creation records in a 03 list. View updates between them have no lengths, so this looks for the
    /// record's start and its `cd ab` mark, then reads the attributes by the object's definition. Records of objects we
    /// have no definition for are skipped.
    /// </summary>
    public static List<ViewCreation> FindCreations(byte[] section)
    {
        List<ViewCreation> found = new();
        for (int i = 0; i + 8 < section.Length; i++)
        {
            if (section[i] != 0x01 || section[i + 1] != 0x00 || (section[i + 2] != 0x0c && section[i + 2] != 0x08))
            {
                continue;
            }
            int offset = i + 3;
            ushort goid = (ushort)(section[offset] | (section[offset + 1] << 8));
            offset += 2;
            if (section[i + 2] == 0x08)
            {
                offset += 4; // related static object id
            }
            if (offset + 3 > section.Length || section[offset + 1] != 0xcd || section[offset + 2] != 0xab)
            {
                continue;
            }
            byte spawnId = section[offset];
            offset += 3;
            if (TryReadCreationAttributes(section, goid, ref offset, out Dictionary<string, byte[]> attributes) &&
                offset + 3 <= section.Length && section[offset + 2] == 0x00)
            {
                ushort viewId = (ushort)(section[offset] | (section[offset + 1] << 8));
                found.Add(new ViewCreation(viewId, goid, spawnId, attributes));
                i = offset + 2;
            }
        }
        return found;
    }

    /// <summary>Inverse of GameObject.GetCreationAttributes: count, then per group a flag byte and the group's values.</summary>
    public static bool TryReadCreationAttributes(byte[] section, ushort goid, ref int offset, out Dictionary<string, byte[]> attributes)
    {
        int cursor = offset;
        if (!TryReadAttributes(section, goid, ref cursor, out attributes))
        {
            attributes.Clear();
            return false;
        }
        offset = cursor;
        return true;
    }

    private static bool TryReadAttributes(byte[] section, ushort goid, ref int offset, out Dictionary<string, byte[]> attributes)
    {
        attributes = new Dictionary<string, byte[]>();
        IReadOnlyList<HdsAttribute>? definition = ObjectDefinitions.CreationAttributes(goid);
        if (definition == null || offset < 0 || offset >= section.Length)
        {
            return false;
        }
        int remaining = section[offset++];
        for (int group = 0; remaining > 0; group++)
        {
            if (offset >= section.Length || group * 7 >= definition.Count)
            {
                return false;
            }
            byte header = section[offset++];
            for (int bit = 0; bit < 7; bit++)
            {
                if ((header & (1 << bit)) == 0)
                {
                    continue;
                }
                int index = group * 7 + bit;
                if (index >= definition.Count || remaining == 0)
                {
                    return false;
                }
                int size = definition[index].getSize();
                if (offset + size > section.Length)
                {
                    return false;
                }
                attributes[definition[index].getName()] = section[offset..(offset + size)];
                offset += size;
                remaining--;
            }
            if (((header & 0x80) != 0) != (remaining > 0))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>The server's object definitions, by goid. Building them is slow, so it happens once.</summary>
public static class ObjectDefinitions
{
    private const ushort PlayerGoid = 12;
    private static readonly Lazy<GameObjectDefinitions> Definitions = new(() => new GameObjectDefinitions());
    private static readonly Lazy<IReadOnlyList<HdsAttribute>> Player = new(() => new PlayerCharacter().GetCreationAttributeList());

    public static IReadOnlyList<HdsAttribute>? CreationAttributes(ushort goid)
    {
        if (goid == PlayerGoid)
        {
            return Player.Value;
        }
        var field = typeof(GameObjectDefinitions).GetField("Object" + goid);
        return field?.GetValue(Definitions.Value) is GameObject definition ? definition.GetCreationAttributeList() : null;
    }
}
