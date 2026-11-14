using hds;
using G=hds.MissionOpenFloorResolver;
using P=hds.MissionOpenFloorPhasePlanner;
namespace hds.Tests;
public class MissionOpenFloorSearchWorkTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper output;
    public MissionOpenFloorSearchWorkTests(Xunit.Abstractions.ITestOutputHelper output){this.output=output;}
    private const string Q="0000000000000000000000000000803f";
    private static G.Request Input(ushort key,bool unreachableRange=false)
    {
        var id=new G.Id(127,1,1);G.Point[] polygon={new(-100,-100),new(100,-100),new(100,100),new(-100,100)};
        var model=unreachableRange?new OpenFloorModelBounds(3,new(-10,-10000,-10),new(10,1,10),"literal tall model cannot be used from floor95"):
            new OpenFloorModelBounds(3,new(-70,-10,-70),new(70,10,70),"literal body wider than half the available room");
        return new G.Request(1,G.SourceModelPresence.Model6Absent,"literal positive absence",new(new(0,145,0),Q,key,1415),new(new(0,0,0),Q,key,1415),"literal unchanged source",
            model,MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal initial400"),new[]{new G.Surface(id,polygon,0,0,0)},new(id,new(-99,95,-99),"literal normal root"),new(-100,100,-100,100),
            new(1,true,new(-100,100,-100,100),-100,20000,Array.Empty<G.StaticObstacle>(),"literal complete domains"));
    }
    [Theory] [InlineData(4)] [InlineData(6)]
    public void UnplaceableWholePhaseHasBoundedDeterministicWorkAndNoFeasibilityClaimOnExhaustion(int count)
    {
        var inputs=Enumerable.Range(1,count).Select(i=>new P.Input((ushort)i,Input((ushort)i))).ToArray();
        Assert.All(inputs,i=>Assert.NotEmpty(G.BodyCandidates(i.Request,out _)));
        var limits=new P.Limits(32,2);
        Assert.False(P.TryPlan(inputs,out var plan,out string why,out var work,limits));Assert.Null(plan);
        Assert.Equal("SearchExhausted:BodyNodes",why);Assert.Equal(32,work.BodyNodes);Assert.Equal(0,work.RouteGraphs);
        Assert.False(P.TryPlan(inputs,out _,out string repeat,out var again,limits));Assert.Equal(why,repeat);Assert.Equal(work.BodyNodes,again.BodyNodes);
        var watch=System.Diagnostics.Stopwatch.StartNew();Assert.False(P.TryPlan(inputs,out _,out string productionReason,out var production,P.Limits.Default));watch.Stop();
        output.WriteLine($"Unplaceable {count}: {watch.ElapsedMilliseconds}ms, bodyNodes={production.BodyNodes}, routeGraphs={production.RouteGraphs}, reason={productionReason}");
        Assert.InRange(production.BodyNodes,1,P.Limits.Default.BodyNodes);Assert.InRange(production.RouteGraphs,0,P.Limits.Default.RouteGraphs);
    }
    [Fact] public void ExpensiveCompleteGraphAttemptsHaveAnIndependentWorkLimit()
    {
        var input=Input(1,true);Assert.NotEmpty(G.BodyCandidates(input,out _));
        Assert.False(P.TryPlan(new[]{new P.Input(1,input)},out _,out string why,out var work,new P.Limits(4096,1)));
        Assert.Equal("SearchExhausted:RouteGraphs",why);Assert.Equal(1,work.RouteGraphs);Assert.InRange(work.BodyNodes,2,4096);
    }

    [Fact] public void EmptyGeometryHasStableNonemptyFailureThatCanBeMemoized()
    {
        var r=Input(1);var impossible=new G.Request(r.World,r.SourceModel,r.SourceModelEvidence,r.RawSource,r.Anchor,r.AnchorEvidence,
            new OpenFloorModelBounds(3,new(-300,-10,-300),new(300,10,300),"literal footprint larger than site"),r.Usable,r.Surfaces,r.Entry,r.SiteBounds,r.Scene);
        var pending=new MissionOpenFloorPendingAdmission(1,901,"no-body",1,100,null,Array.Empty<MissionOpenFloorChoice>(),null);int executions=0;
        string Run(){if(pending.TryFailedGeometry(impossible.Fingerprint,out string cached))return cached;executions++;Assert.False(P.TryPlan(new[]{new P.Input(1,impossible)},out _,out string reason));pending.RememberFailedGeometry(impossible.Fingerprint,reason);return reason;}
        Assert.Equal("NoGeometricBodyCandidates",Run());Assert.Equal("NoGeometricBodyCandidates",Run());Assert.Equal(1,executions);
    }
}
