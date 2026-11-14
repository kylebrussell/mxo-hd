using NativeQa;
using HeadlessClient;

namespace hds.Tests;

public class NativeQaBootstrapChecksTests
{
    [Fact]
    public void IsolatedChecksKeepModeControlsButLetTheHarnessAllocateAllPorts()
    {
        foreach (string mode in NativeQaModes.Names)
        {
            var actual = NativeQaBootstrapChecks.IsolatedEnvironment(mode);
            var native = NativeQaFixturePlan.EnvironmentFor(mode);
            foreach (var pair in native)
                if (pair.Key is "MXO_AUTH_PORT" or "MXO_MARGIN_PORT" or "MXO_WORLD_PORT" or "MXO_CONSOLE_PORT")
                    Assert.DoesNotContain(pair.Key, actual.Keys);
                else Assert.Equal(pair.Value, actual[pair.Key]);
        }
    }

    [Fact]
    public void BootstrapContextRetainsNativeDefaultsAndUsesExplicitIsolatedAddress()
    {
        var helpers = new Dictionary<string,HeadlessPlayer>();
        var native = new NativeQaBootstrapContext("proof", Array.Empty<string>(), helpers);
        Assert.Equal(new ServerAddress("127.0.0.1",11000,10000,10000), native.Address);
        var isolated = new ServerAddress("127.0.0.1",21000,22000,23000);
        Assert.Same(isolated, (native with { Server = isolated }).Address);
    }
}
