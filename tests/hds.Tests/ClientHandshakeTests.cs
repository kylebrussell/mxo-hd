using hds;

namespace hds.Tests;

/// <summary>The requests the client sends together at world entry (0c, 80 be, 3e, 81 48; docs/CLIENT-ACTIONS.md).</summary>
public class ClientHandshakeTests
{
    [Fact]
    public void FeatureListHasTwoInBoundsEntriesAndTerminatedStrings()
    {
        // client.dll 62120680: stride 7, relative string offset at +5. A previous literal had a zero at +16,
        // making the count 512 and crashing the native client at 621206bb while terminating an out-of-bounds string.
        byte[] packet = ServerPackets.BuildFeatureList();
        Assert.Equal(74, packet.Length);
        int start = BitConverter.ToUInt16(packet, 2);
        Assert.Equal(16, start);
        Assert.Equal(2, BitConverter.ToUInt16(packet, start));
        for (int i = 0; i < 2; i++)
        {
            int row = start + 2 + i * 7;
            Assert.Equal((uint)(200 + i), BitConverter.ToUInt32(packet, row + 1));
            int text = row + BitConverter.ToUInt16(packet, row + 5);
            int size = BitConverter.ToUInt16(packet, text);
            Assert.InRange(text, start + 16, packet.Length - 2);
            Assert.True(text + 2 + size <= packet.Length);
            Assert.Equal(0, packet[text + 1 + size]);
            Assert.Equal("count[0] active[1]", System.Text.Encoding.ASCII.GetString(packet, text + 2, size - 1));
        }
    }
}

public class IgnoreListTests
{
    private static string H(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [Fact]
    public void RecordsAreLaidOutLikeTheFriendList()
    {
        // client.dll 80 e1 / 80 e3: +2 IgnoreName (offset), +4 Status
        Assert.Equal("80e108008400008e0400426f6200", H(IgnoreListPackets.BuildAdded("Bob", IgnoreListPackets.Success)));
        Assert.Equal("80e308005800000e0400426f6200", H(IgnoreListPackets.BuildRemoved("Bob", IgnoreListPackets.NotIgnored)));
    }

    [Fact]
    public void ParsesTheName()
    {
        // 80 e0 04 00 <sized name>, after the two opcode bytes
        Assert.True(IgnoreListPackets.TryParseName(Convert.FromHexString("04000400426f6200"), out string? name));
        Assert.Equal("Bob", name);
        Assert.False(IgnoreListPackets.TryParseName(new byte[] { 4, 0 }, out _));
    }
}

[Collection("ChatAddress")]
public class ChatAddressTests
{
    [Fact]
    public void AddressesUseTheWorldListName()
    {
        // client.dll 0x6224e810 treats SOE+MXO+<its WorldPrefix>+handle as local; WorldPrefix is the world list name
        string before = ChatAddress.WorldName;
        try
        {
            ChatAddress.WorldName = "Recursion";
            Assert.Equal("SOE+MXO+Recursion+MailBot", ChatAddress.Of("MailBot"));
            Assert.Equal("MailBot", MailPackets.HandleOf(ChatAddress.Of("MailBot")));
        }
        finally
        {
            ChatAddress.WorldName = before;
        }
    }
}
