using hds;

namespace hds.Tests;

/// <summary>The requests the client sends together at world entry (0c, 80 be, 3e, 81 48; docs/CLIENT-ACTIONS.md).</summary>
public class ClientHandshakeTests
{
    [Fact]
    public void FeatureListIsRetails()
    {
        // Every Rajko capture answers the 81 48 with these bytes
        Assert.Equal("8149100000000000000000000000000000020000c80000000e0000c90000001c001300636f756e745b305d206163746976655b315d001300636f756e745b305d206163746976655b315d00",
            Convert.ToHexString(ServerPackets.BuildFeatureList()).ToLowerInvariant());
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
