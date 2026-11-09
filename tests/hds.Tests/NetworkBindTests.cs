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

    [Fact]
    public void PortsDefaultToWhatTheRealClientExpects()
    {
        Assert.Equal(11000, NetworkBind.DefaultAuthPort);
        Assert.Equal(10000, NetworkBind.DefaultMarginPort);
        Assert.Equal(10000, NetworkBind.DefaultWorldPort);
        Assert.Equal(55557, NetworkBind.DefaultConsolePort);
    }

    [Theory]
    [InlineData(null, 11000)]
    [InlineData("", 11000)]
    [InlineData("not a port", 11000)]
    [InlineData("0", 11000)]
    [InlineData("70000", 11000)]
    [InlineData("21000", 21000)]
    [InlineData(" 21000 ", 21000)]
    public void APortOverrideMustBeAValidPort(string? value, int expected)
    {
        Assert.Equal(expected, NetworkBind.ParsePort(value!, 11000));
    }
}
