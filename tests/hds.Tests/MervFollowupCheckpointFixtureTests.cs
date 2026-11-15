using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;

public class MervFollowupCheckpointFixtureTests
{
    [Fact] public void HexPreservesNestedElevatorSourcesAndQuotedRawJson()
    {
        var options=new JsonSerializerOptions{IncludeFields=true};
        var spot=new StaticWorldObject{metrId=1,mxoStaticId=0x0d100166,pos_x=29300,pos_y=1426,pos_z=13860,quat="000000000000803f000000002ebd3bb3",type=new byte[]{1,2}};
        string source=JsonSerializer.Serialize(spot,options);
        var witness=new MissionElevatorWitness(1,"site",47,new[]{28500d,1345,14400},new[]{28500d,545,14400},"floor","lobby","evidence",new[]{source});
        var state=new MissionProgress.SavedState{Mission=301208,Phase=0,ElevatorWitnesses=new[]{witness}};
        string raw=JsonSerializer.Serialize(state,options);
        Assert.Contains("\\u0022",raw);
        string hex=Convert.ToHexString(Encoding.UTF8.GetBytes(raw));
        Assert.Equal(raw,MervFollowupCheckpointFixture.DecodeHex(hex));
        var restored=MervFollowupCheckpointFixture.ParseHex(hex)!;
        string recovered=Assert.Single(Assert.Single(restored.ElevatorWitnesses).Sources);
        Assert.Equal(source,recovered);
        Assert.Equal(0x0d100166u,JsonSerializer.Deserialize<StaticWorldObject>(recovered,options)!.mxoStaticId);
        // mysql -B doubles backslashes: parsing its raw text is insufficient for nested JSON strings.
        string batch=raw.Replace("\\","\\\\");
        string corrupt=JsonSerializer.Deserialize<MissionProgress.SavedState>(batch,options)!.ElevatorWitnesses[0].Sources[0];
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<StaticWorldObject>(corrupt,options));
    }
    [Theory][InlineData(null)][InlineData("")][InlineData(" \r\n")]
    public void AbsentOwnedPayloadRemainsAbsent(string? value)=>Assert.Null(MervFollowupCheckpointFixture.ParseHex(value));
    [Theory][InlineData("0")][InlineData("GG")][InlineData("NULL")][InlineData("7B 7D")]
    public void MalformedHexRefuses(string value)=>Assert.Throws<FormatException>(()=>MervFollowupCheckpointFixture.ParseHex(value));
    [Theory][InlineData("FF")][InlineData("C328")][InlineData("E282")]
    public void InvalidUtf8RefusesInsteadOfReplacingBytes(string value)=>Assert.Throws<DecoderFallbackException>(()=>MervFollowupCheckpointFixture.ParseHex(value));
    [Fact] public void MalformedJsonStillRefuses()=>Assert.Throws<JsonException>(()=>MervFollowupCheckpointFixture.ParseHex("7B"));
    [Fact] public void SharedAndStrict213CurrentReadsUseHexBeforeJson()
    {
        string root=FindRoot();
        foreach(string file in new[]{"ScenariosMervFollowupApproach.cs","ScenariosPrimaChain.cs"})
        {
            string text=File.ReadAllText(Path.Combine(root,"tools","HeadlessClient",file));
            Assert.Contains("SELECT IFNULL(HEX(payload),'')",text);
            Assert.DoesNotContain("SELECT IFNULL(payload,'')",text);
            Assert.Contains("MervFollowupCheckpointFixture.",text);
        }
    }
    static string FindRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory!=null&&!Directory.Exists(Path.Combine(directory.FullName,"tools","HeadlessClient")))directory=directory.Parent;
        return directory?.FullName??throw new DirectoryNotFoundException("Repository root");
    }
}
