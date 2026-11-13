using hds;
using System.Text.Json;
using System.Security.Cryptography;
namespace hds.Tests;
public class FreeCastViralRollTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact] public void AllRand15InputsMatchIndependentPrimaryTableFractionOracle()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/protocol/freecast-viral-roll.json")));
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int b=c.GetProperty("bonus").GetInt32(),n=c.GetProperty("consistency").GetInt32(),r=c.GetProperty("range").GetInt32();var bytes=new byte[65536];
            for(int draw=0;draw<32768;draw++)BitConverter.TryWriteBytes(bytes.AsSpan(draw*2,2),FreeCastViralRoll.Roll(b,n,r,draw));
            Assert.Equal(c.GetProperty("all32768Sha256").GetString(),Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            foreach(var v in c.GetProperty("literals").EnumerateObject())Assert.Equal(v.Value.GetInt32(),FreeCastViralRoll.Roll(b,n,r,int.Parse(v.Name)));
        }
    }
    [Fact] public void NativeUnsignedLeftTailAndFinalDrawAreNotReplacedBySymmetricClamping()
    {
        Assert.Equal(100,FreeCastViralRoll.Roll(0,0,100,16382));
        Assert.Equal(50,FreeCastViralRoll.Roll(0,0,100,32767));
        Assert.Equal(50,FreeCastViralRoll.Roll(0,100,100,123));
        Assert.Throws<ArgumentOutOfRangeException>(()=>FreeCastViralRoll.Roll(0,0,100,-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>FreeCastViralRoll.Roll(0,0,100,32768));
        Assert.Throws<ArgumentOutOfRangeException>(()=>FreeCastViralRoll.Roll(0,0,99,0));
    }
}
