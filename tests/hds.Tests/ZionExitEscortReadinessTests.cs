using hds;
using HeadlessClient;
using System.Xml.Linq;
namespace hds.Tests;
public class ZionExitEscortReadinessTests
{
    // Retained402 raw final-session03/0e records; source schema20d623e957. No live state writes.
    const string Own="01000C0C0001CDAB15884C61737400000000000000000000000000000000000000000000000000000000904669727374000000000000000000000000000000000000000000000000000000809888130184D007C600005A696F6E4E6578744669766500000000000000000000000000000000000000008813E902000000E9030000160003000000000004000000000000D007BD0000000040B5E94000000000009EA6400000000000AACEC0000032220000C000100000880014010000020000020002802003000002";
    const string Maynard="01000C570202CDAB16AE4D61796E617264204265726D616E000000000000000000000000000000000000001000002200001000D4003500002401A10001C9350000240100000000C0A8E94000000000009EA6400000000000AACEC088003100028201A8326608B83500002400000000000000000000006608060800008801030111040000";
    const string Far="0400020E0156149C4E4700F03445488D78C6";
    const string Near="0400020E015629F24F4700F0344591CA7BC6";
    static byte[] B(string hex)=>Convert.FromHexString(hex);
    static OwnViewLifeLog Log(params string[] sections)
    {var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++){var b=B(sections[i]);log.Observe(false,b,i,WireFormat.FindCreations(b));}return log;}
    static MissionProgress.SavedState State(double x=52550,double z=-15700)=>new(){Version=1,Identity=new string('a',32),Mission=101210,Phase=0,Instance=49,Started=100,
        Done=new[]{1},Following=new ushort[]{1},Mobs=new(){new(){Npc=1,Identity=new string('b',32),Name="Maynard Berman",Phase=0,Friendly=true,Health=2150,Level=50,X=x,Y=2895,Z=z}}};
    static IReadOnlyList<ZionExitEscortReadiness.Observation> Ready(OwnViewLifeLog log,MissionProgress.SavedState before,MissionProgress.SavedState current,long? expected=null,string handle="ZionNextFive")
    {var stream=log.Snapshot();return ZionExitEscortReadiness.Require(stream,expected??stream.Life,handle,before,current,new ushort[]{1},new[]{54200d,2895,-16700});}
    [Fact] public void CapturedEarlyRequestIsNotReadyUntilActualCurrentMovementReaches1500()
    {
        var log=Log(Own,Maynard,Far);var before=State();
        Assert.Throws<InvalidDataException>(()=>Ready(log,before,State(52892.078125,-15907.3203125)));
        var b=B(Near);log.Observe(false,b,3,WireFormat.FindCreations(b));
        var observation=Assert.Single(Ready(log,before,State(53234.16015625,-16114.6416015625)));
        Assert.Equal(4,observation.View);Assert.Equal(3,observation.PositionSection);Assert.Equal(new[]{53234.16015625,2895,-16114.6416015625},observation.Position);
        // A convenient earlier position or stale saved position never overrides the latest movement.
        Assert.Throws<InvalidDataException>(()=>Ready(log,before,State(52892.078125,-15907.3203125)));
    }
    [Theory][InlineData("owner")][InlineData("phase")][InlineData("identity")][InlineData("dead")][InlineData("following")][InlineData("side")][InlineData("wirephase")]
    public void OnlyCurrentOwnedLiveFollowingIdentityCanSatisfyReadiness(string change)
    {
        var before=State();var current=State(53234.16015625,-16114.6416015625);
        switch(change){case "owner":current.Identity=new string('c',32);break;case "phase":current.Phase++;break;case "identity":current.Mobs[0].Identity=new string('c',32);break;
            case "dead":current.Mobs[0].Dead=true;break;case "following":current.Following=Array.Empty<ushort>();break;case "side":current.Mobs[0].Friendly=false;break;case "wirephase":current.Mobs[0].Phase=1;break;}
        Assert.Throws<InvalidDataException>(()=>Ready(Log(Own,Maynard,Far,Near),before,current));
    }
    [Theory][InlineData("delete")][InlineData("reuse")][InlineData("unknown")][InlineData("truncated")][InlineData("duplicate")][InlineData("name")]
    public void TypedCurrentCursorRejectsDeletedReusedUnknownTruncatedOrAmbiguousBodies(string change)
    {
        string birth=Maynard;if(change=="name")birth=birth.Replace(Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes("Maynard")),Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes("Mallory")));
        var sections=new List<string>{Own,birth,Far,Near};
        if(change=="delete"||change=="reuse")sections.Add("01000101000400");if(change=="reuse")sections.Add(Maynard);
        if(change=="unknown")sections.Add("63AA");if(change=="truncated")sections.Add(Near[..^2]);
        if(change=="duplicate"){var b=B(Maynard);b[5]=3;b[^3]=9;sections.Add(Convert.ToHexString(b));}
        Assert.Throws<InvalidDataException>(()=>Ready(Log(sections.ToArray()),State(),State(53234.16015625,-16114.6416015625)));
    }
    [Fact] public void OwnSessionLifeAndHandleMustRemainCurrent()
    {
        var log=Log(Own,Maynard,Far,Near);var stream=log.Snapshot();var current=State(53234.16015625,-16114.6416015625);
        Assert.Throws<InvalidDataException>(()=>Ready(log,State(),current,stream.Life+1));Assert.Throws<InvalidDataException>(()=>Ready(log,State(),current,handle:"Other"));
        log.EndLife();Assert.Throws<InvalidDataException>(()=>ZionExitEscortReadiness.Require(stream,stream.Life,"ZionNextFive",State(),current,new ushort[]{1},new[]{54200d,2895,-16700}));
    }
    [Fact] public void OnlyActualOpenExitObjectivesRequireTheWait()
    {
        var xml=XElement.Parse("<mission id='101210' title='Escort'><phase><objective type='Escort' goal='2' npc='1' to='exit' after='1'/><objective type='Escort' goal='3' npc='2' to='npc:3'/></phase></mission>");
        var state=State();Assert.Equal(new ushort[]{1},ZionExitEscortReadiness.OpenFollowers(xml,state));
        state.Done=Array.Empty<int>();Assert.Empty(ZionExitEscortReadiness.OpenFollowers(xml,state));state.Done=new[]{1,2};Assert.Empty(ZionExitEscortReadiness.OpenFollowers(xml,state));
    }
}
