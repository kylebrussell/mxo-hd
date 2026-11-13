using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
namespace hds.Tests;

public class DataMineNativeTraceTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact]
    public void ExactNativeFailureBatchesRetainTheirHashesAndIndependentWireContracts()
    {
        using var trace=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/protocol/datamine-native-failure.json")));
        Assert.Equal("77716469f91d67e22655b624ad2b8cfd5e80b0a532da8cc3e05f596afcfaa521",trace.RootElement.GetProperty("sourceSha256").GetString());
        var packets=trace.RootElement.GetProperty("packets").EnumerateArray().ToDictionary(p=>p.GetProperty("name").GetString()!);
        byte[] Bytes(string name){var p=packets[name];var b=Convert.FromHexString(p.GetProperty("hex").GetString()!);Assert.Equal(p.GetProperty("sha256").GetString(),Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant());return b;}
        var requests=WireFormat.ParsePayload(Bytes("FirstMissingRequestBatch"));
        Assert.Equal(3,requests.Rpcs.Count);Assert.All(requests.Rpcs,r=>Assert.Equal(0xc9,r.Opcode));
        Assert.Equal(0x01b00004u,BitConverter.ToUInt32(requests.Rpcs[0].Bytes,2));
        Assert.Equal(13000f,BitConverter.ToSingle(requests.Rpcs[0].Bytes,6));
        Assert.Equal(4800f,BitConverter.ToSingle(requests.Rpcs[0].Bytes,10));
        Assert.Equal(-5000f,BitConverter.ToSingle(requests.Rpcs[0].Bytes,14));
        Assert.Equal(new byte[]{0x42},Bytes("ClientResetDone"));
        var setup=WireFormat.ParsePayload(Bytes("FirstPostResetSetup"));
        Assert.Null(setup.ViewSection);Assert.Equal(21,setup.Rpcs.Count);
        Assert.Equal(new[]{0x1a9,6},setup.Rpcs.Take(2).Select(r=>r.Opcode));
        Assert.Contains("slums_barrens_full.metr",Encoding.Latin1.GetString(setup.Rpcs[1].Bytes));
        Assert.Contains(setup.Rpcs,r=>r.Opcode==0x3a);Assert.Contains(setup.Rpcs,r=>r.Opcode==0xbc);
    }
}
