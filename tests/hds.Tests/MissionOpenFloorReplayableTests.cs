using hds;
using G=hds.MissionOpenFloorResolver;
using P=hds.MissionOpenFloorPhasePlanner;
namespace hds.Tests;
public class MissionOpenFloorReplayableTests
{
    private const string Q="0000000000000000000000000000803f";
    private static G.Point[] Rect(double left,double right,double bottom=-500,double top=500)=>new[]{new G.Point(left,bottom),new G.Point(right,bottom),new G.Point(right,top),new G.Point(left,top)};
    private static G.Request Input(bool rootAtAnchor=false,double half=10,bool wall=false,bool rightSite=false,ushort key=1)
    {
        var id=new G.Id(127,1,1);return new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(0,145,0),Q,key,1415),new(new(rightSite?100:0,0,0),Q,key,1415),"literal source",
            new OpenFloorModelBounds(3,new(-half,-10,-half),new(half,10,half),"literal body"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal400"),
            new[]{new G.Surface(id,Rect(-500,500),0,0,0)},new(id,new(rootAtAnchor?0:-300,95,0),"literal root"),rightSite?new(30,500,-500,500):new(-500,500,-500,500),
            new(1,true,new(-500,500,-500,500),-100,1000,wall?new[]{new G.StaticObstacle("wall",99,1407,0,200,Rect(-20,20),"literal closed wall")}:Array.Empty<G.StaticObstacle>(),"literal complete scene"));
    }
    [Fact] public void InterleavedAndRepeatedTraversalsReplayMemoizedPassAndFailWithoutDrainingUnusedSuffix()
    {
        var r=Input(rootAtAnchor:true);Assert.True(G.TryBodySequence(r,out var bodies,out string why,out bool graph),why);Assert.True(graph);
        int initial=bodies.GeometryEvaluations;using var a=bodies.GetEnumerator();using var b=bodies.GetEnumerator();
        Assert.True(a.MoveNext());int afterFirst=bodies.GeometryEvaluations;Assert.True(b.MoveNext());Assert.Same(a.Current,b.Current);Assert.Equal(afterFirst,bodies.GeometryEvaluations);
        Assert.True(a.MoveNext());int afterSecond=bodies.GeometryEvaluations;Assert.True(b.MoveNext());Assert.Same(a.Current,b.Current);Assert.Equal(afterSecond,bodies.GeometryEvaluations);
        var all=bodies.ToArray();int completed=bodies.GeometryEvaluations;Assert.True(completed>initial);Assert.Equal(all,bodies.ToArray());Assert.Equal(completed,bodies.GeometryEvaluations);
        Assert.True(G.TryBodySequence(r,out var independent,out why,out _),why);Assert.True(independent.GeometryEvaluations<completed); // no cross-context proof cache
    }
    [Fact] public void EmptyRootOnlyTraversalDoesNotDrainAllWaveAndAllWaveStillFindsLegalStandingRange()
    {
        var r=Input(wall:true,rightSite:true);Assert.True(G.TryBodySequence(r,out var bodies,out string why,out _),why);int initial=bodies.GeometryEvaluations;
        Assert.Empty(bodies.Enumerate(true));Assert.Equal(initial,bodies.GeometryEvaluations);Assert.NotEmpty(bodies.Enumerate(false));
        Assert.True(P.TryPlan(new[]{new P.Input(1,r)},out var plan,out why,out var work,P.Limits.Default,false),why);
        Assert.Equal(1,work.BodyNodes);Assert.Equal(1,work.RouteGraphs);Assert.True(G.ValidateSet(plan.Results.Values.ToArray(),out why),why);
    }
    [Fact] public void EmptyAndBlockedRootRefusalsKeepTheirPreparationGraphOrder()
    {
        Assert.False(G.TryBodySequence(Input(half:1000),out _,out string why,out bool graph));Assert.Equal("NoGeometricBodyCandidates",why);Assert.False(graph);
        Assert.False(G.TryBodySequence(Input(rootAtAnchor:true,half:400),out _,out why,out graph));Assert.Equal("NoGeometricBodyCandidates",why);Assert.False(graph); // proposals exist, all fail root clearance
        var r=Input();var obstacle=new G.StaticObstacle("root",99,1407,0,200,Rect(-320,-280,-20,20),"literal root obstruction");
        var blocked=new G.Request(r.World,r.SourceModel,r.SourceModelEvidence,r.RawSource,r.Anchor,r.AnchorEvidence,r.Model,r.Usable,r.Surfaces,r.Entry,r.SiteBounds,new(1,true,r.Scene.Coverage,-100,1000,new[]{obstacle},"literal complete scene"));
        Assert.False(G.TryBodySequence(blocked,out _,out why,out graph));Assert.Equal("BlockedStaticRoot",why);Assert.True(graph);
        Assert.False(P.TryPlan(new[]{new P.Input(1,blocked)},out _,out why,out var work,P.Limits.Default,false));Assert.Equal("BlockedStaticRoot",why);Assert.Equal(1,work.StaticRootGraphs);Assert.Equal(0,work.BodyNodes);Assert.Equal(0,work.RouteGraphs);
    }
    [Fact] public void LaterInvalidInputIsPreflightedBeforeAnyPartialBodyOrRouteSearch()
    {
        Assert.False(P.TryPlan(new[]{new P.Input(1,Input()),new P.Input(2,Input(half:1000,key:2))},out var plan,out string why,out var work,P.Limits.Default));
        Assert.Null(plan);Assert.Equal("NoGeometricBodyCandidates",why);Assert.Equal(0,work.BodyNodes);Assert.Equal(0,work.RouteGraphs);Assert.Equal(1,work.StaticRootGraphs);
    }
    [Fact] public void DirectProbeDiscoversWhetherAnotherValidCandidateExistsBeforeDeclaringExhaustion()
    {
        var r=Input(rootAtAnchor:true);var result=G.Plan(r,1,1,out int nodes,out int graphs);
        Assert.False(result.HasPlan);Assert.Equal("NoClearFootprintAndInRangeReachableStandingPoint",result.Refusal);Assert.Equal(1,nodes);Assert.Equal(1,graphs);
    }
}
