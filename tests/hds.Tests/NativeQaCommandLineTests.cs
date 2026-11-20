using System.Diagnostics;
using System.Net;
using System.Text.Json;
using NativeQa;
namespace hds.Tests;
public class NativeQaCommandLineTests
{
    [Theory][InlineData("--plan")][InlineData("--list")][InlineData("--fixture-plann")]
    public async Task UnknownLeadingOptionFailsBeforeAnyFixtureSetup(string option)
    {
        var result=await Run(option);
        Assert.Equal(2,result.Exit);Assert.Empty(result.Output);
        Assert.Contains("Unknown NativeQa option: "+option,result.Error);
    }
    [Theory][InlineData("--help")][InlineData("-h")]
    public async Task HelpPrintsSupportedCommandsWithoutSetup(string option)
    {
        var result=await Run(option);
        Assert.Equal(0,result.Exit);Assert.Empty(result.Error);
        Assert.Contains("--fixture-plan",result.Output);
        Assert.Contains("--bootstrap-checks",result.Output);
    }
    [Fact]
    public async Task InvalidRepositoryFailsBeforeCreatingTheRequestedFixtureDirectory()
    {
        var result=await Run("missing-repository","must-not-be-created","local");
        Assert.Equal(2,result.Exit);Assert.Empty(result.Output);
        Assert.Contains("repository must contain SQL/reality_hd.sql",result.Error);
    }

    [Fact]
    public async Task SecondaryPlanUsesTheStockPortsOnItsOwnLoopbackAddress()
    {
        var result = await Run("--fixture-plan", "actions", "--bind-address", "127.0.0.2");
        Assert.Equal(0, result.Exit);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var environment = json.RootElement.GetProperty("Environment");
        Assert.Equal("127.0.0.2", environment.GetProperty("MXO_BIND_ADDRESS").GetString());
        Assert.Equal("11000", environment.GetProperty("MXO_AUTH_PORT").GetString());
        Assert.Equal("10000", environment.GetProperty("MXO_MARGIN_PORT").GetString());
        Assert.Equal("10000", environment.GetProperty("MXO_WORLD_PORT").GetString());
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("127.0.2")]
    [InlineData("192.168.0.2")]
    public async Task RejectsAddressesOutsideTheCanonicalIpv4LoopbackRange(string address)
    {
        var result = await Run("--fixture-plan", "--bind-address", address);
        Assert.Equal(2, result.Exit);
        Assert.Contains("IPv4 loopback", result.Error);
    }

    [Fact]
    public void AListenerOnThePrimaryAddressDoesNotReserveTheSecondaryAddress()
    {
        var primary = NativeQaEndpoint.Parse("127.0.0.1");
        var secondary = NativeQaEndpoint.Parse("127.0.0.2");
        var listeners = new[] { new IPEndPoint(primary.Address, 11000), new IPEndPoint(secondary.Address, 10000) };
        Assert.False(NativeQaEndpoint.Conflicts(secondary.Address, 11000, listeners));
        Assert.True(NativeQaEndpoint.Conflicts(primary.Address, 11000, listeners));
        Assert.True(NativeQaEndpoint.Conflicts(secondary.Address, 10000, listeners));
        Assert.True(NativeQaEndpoint.Conflicts(secondary.Address, 11000, [new(IPAddress.Any, 11000)]));
        Assert.Equal("mxo-client.lock", primary.ClientLock("mxo-client.lock"));
        Assert.Equal("mxo-client.127.0.0.2.lock", secondary.ClientLock("mxo-client.lock"));
    }

    static async Task<(int Exit,string Output,string Error)> Run(params string[] args)
    {
        string empty=Path.Combine(Path.GetTempPath(),"mxo-nativeqa-cli-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var start=new ProcessStartInfo("dotnet"){WorkingDirectory=empty,RedirectStandardOutput=true,
                RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"hds.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"hds.Tests.deps.json"),typeof(NativeQaFixturePlan).Assembly.Location}.Concat(args))start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
            Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
            return(process.ExitCode,await output,await error);
        }
        finally{if(!Directory.EnumerateFileSystemEntries(empty).Any())Directory.Delete(empty);}
    }
}
