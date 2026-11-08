using System.Net;
using hds;

namespace hds.Tests;

public class NetworkBindTests
{
    [Fact]
    public void DefaultsToAllInterfacesAndAcceptsLoopback()
    {
        Assert.Equal(IPAddress.Any, NetworkBind.Parse(null));
        Assert.Equal(IPAddress.Any, NetworkBind.Parse("not an address"));
        Assert.Equal(IPAddress.Loopback, NetworkBind.Parse("127.0.0.1"));
    }
}
