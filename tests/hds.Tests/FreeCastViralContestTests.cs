using hds;
using System.Text.Json;
namespace hds.Tests;
public class FreeCastViralContestTests
{
    [Theory]
    [InlineData(0,114,90)] [InlineData(1,90,100)] [InlineData(2,105,130)] [InlineData(3,0,150)]
    [InlineData(null,100,100)] [InlineData(99,100,100)]
    public void ActualTacticCodesAndExplicitNpcNeutralPolicy(int? tactic,int attack,int defense)
    {
        Assert.Equal(attack,FreeCastViralContest.Tactic(100,tactic,false));
        Assert.Equal(defense,FreeCastViralContest.Tactic(100,tactic,true));
    }
    [Fact] public void ModeledInputsAndConsistencyAreConsumedBeforeStrictContest()
    {
        var a=FreeCastViralContest.Evaluate(0,0,100,100,null,null,0,32766);
        Assert.Equal(50,a.Attack);Assert.Equal(34,a.Defense);Assert.True(a.Hit);
        var b=FreeCastViralContest.Evaluate(100,200,100,100,0,3,0,0);
        Assert.Equal(114,b.AttackInput);Assert.Equal(300,b.DefenseInput);Assert.False(b.Hit);
        Assert.False(new FreeCastViralContest.Result{Attack=50,Defense=50}.Hit);
        Assert.Equal(-1,FreeCastViralContest.Tactic(-2,1,false));
    }
    [Theory]
    [InlineData(33,true)] [InlineData(359,true)] [InlineData(97,true)] [InlineData(261,false)]
    [InlineData(134,false)] [InlineData(35,false)] [InlineData(697,false)]
    public void OnlySupportedPrimaryFreeCastOriginsEnter(ushort id,bool expected)
    {
        Assert.Equal(expected,FreeCastViralContest.Applies(id,AbilityCastOrigin.NormalFreeCast));
        Assert.False(FreeCastViralContest.Applies(id,AbilityCastOrigin.AlreadyResolved));
    }
    [Theory]
    [InlineData("1","mxotest_fixture",110u,true,false,"1","0","32767",true)]
    [InlineData("0","mxotest_fixture",110u,true,false,"1","0","1",false)]
    [InlineData("1","reality_hd",110u,true,false,"1","0","1",false)]
    [InlineData("1","mxotest_",110u,true,false,"1","0","1",false)]
    [InlineData("1","mxotest_x;bad",110u,true,false,"1","0","1",false)]
    [InlineData("1","mxotest_fixture",0u,true,false,"1","0","1",false)]
    [InlineData("1","mxotest_fixture",2u,true,false,"1","0","1",false)]
    [InlineData("1","mxotest_fixture",110u,false,false,"1","0","1",false)]
    [InlineData("1","mxotest_fixture",110u,true,true,"1","0","1",false)]
    [InlineData("1","mxotest_fixture",110u,true,false,"0","0","1",false)]
    [InlineData("1","mxotest_fixture",110u,true,false,"1","-1","1",false)]
    [InlineData("1","mxotest_fixture",110u,true,false,"1","32768","1",false)]
    [InlineData("1","mxotest_fixture",110u,true,false,"1"," 1","1",false)]
    [InlineData("1","mxotest_fixture",110u,true,false,"1","1","NaN",false)]
    public void ContestOnlyQaRequiresStrictDisposableIdentityAndRandDomain(string dev,string db,uint id,bool live,bool dead,string enabled,string a,string d,bool expected)=>
        Assert.Equal(expected,FreeCastViralContest.QaAllowed(dev,db,id,live,dead,enabled,a,d,out _,out _));
    [Fact] public void HitAndDeflectFourFieldPacketsMatchCapturedLiteralBytes()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"research/protocol/freecast-viral-events.json")));
        foreach(var r in doc.RootElement.GetProperty("records").EnumerateArray())
        {
            var values=r.GetProperty("extras").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var names=r.GetProperty("names").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            var actual=FreeCastViralContest.Message(r.GetProperty("ability_id").GetUInt16(),names[0],names[1],new(){Attack=values[0],Defense=values[2]});
            Assert.Equal(Convert.FromHexString(r.GetProperty("rpc_hex").GetString()!.Replace(" ","")),actual);
        }
    }
}
