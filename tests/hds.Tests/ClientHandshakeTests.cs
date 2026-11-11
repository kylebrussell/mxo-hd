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
