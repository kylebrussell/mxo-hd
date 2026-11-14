using System.Text.Json;
using hds;
using G=hds.MissionOpenFloorResolver;
using P=hds.MissionOpenFloorPhasePlanner;
namespace hds.Tests;
public class MissionOpenFloorSingleTests
{
    private const string Q="0000000000000000000000000000803f";
    private static G.Request Input(bool rootAtAnchor=false)
    {
        var id=new G.Id(127,1,1);G.Point[] rectangle={new(-500,-500),new(500,-500),new(500,500),new(-500,500)};
        return new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(0,145,0),Q,1,1415),new(new(0,0,0),Q,1,1415),"literal source",
            new OpenFloorModelBounds(3,new(-10,-10,-10),new(10,10,10),"literal bounds"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal400"),
            new[]{new G.Surface(id,rectangle,0,0,0)},new(id,new(rootAtAnchor?0:-300,95,0),"literal root"),new(-500,500,-500,500),new(1,true,new(-500,500,-500,500),-100,1000,Array.Empty<G.StaticObstacle>(),"literal complete scene"));
    }
    private static string Fixed(G.Result r)=>JsonSerializer.Serialize(new{r.InputFingerprint,r.Surface.Sector,r.Surface.Region,r.Surface.Polygon,Centre=new[]{r.Centre.X,r.Centre.Y,r.Centre.Z},Standing=new[]{r.Standing.X,r.Standing.Y,r.Standing.Z},
        Route=r.Route.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),RouteIds=r.RoutePolygons.Select(i=>new[]{i.Sector,i.Region,i.Polygon}).ToArray(),Base=r.Base.Select(p=>new[]{p.X,p.Z}).ToArray(),r.NumericalGuard,r.Range.DistanceSquared,r.Range.LowerBound,r.Range.LimitSquared});
    [Fact] public void PositiveSingleShortcutPreservesEveryFixedResultByteAndAvoidsExpandedPreparation()
    {
        var r=Input();var direct=G.Plan(r);Assert.True(direct.HasPlan,direct.Refusal);
        Assert.True(P.TryPlan(new[]{new P.Input(1,r)},out var plan,out string why,out var work,P.Limits.Default),why);
        Assert.True(plan.DirectSingle);Assert.Equal(Fixed(direct),Fixed(plan.Results[1]));Assert.Equal(1,work.BodyNodes);Assert.Equal(1,work.RouteGraphs);Assert.Equal(0,work.StaticRootGraphs);
        Assert.True(P.TryPlan(new[]{new P.Input(1,r)},out var expanded,out why,out _,P.Limits.Default,false),why);
        Assert.Equal(Fixed(expanded.Results[1]),Fixed(plan.Results[1]));
    }
    [Fact] public void OrdinaryDirectNoPlanRetainsExpandedAvailabilityAndConsumedWork()
    {
        var r=Input(true);Assert.False(G.Plan(r).HasPlan); // nearest body covers the actual root
        Assert.True(P.TryPlan(new[]{new P.Input(1,r)},out var plan,out string why,out var work,P.Limits.Default),why);
        Assert.False(plan.DirectSingle);Assert.InRange(work.BodyNodes,2,P.Limits.Default.BodyNodes);Assert.Equal(2,work.RouteGraphs);Assert.Equal(1,work.StaticRootGraphs);
        Assert.True(G.ValidateSet(plan.Results.Values.ToArray(),out why),why);
        Assert.True(P.TryPlan(new[]{new P.Input(1,r)},out var prior,out why,out _,P.Limits.Default,false),why);
        Assert.Equal(Fixed(prior.Results[1]),Fixed(plan.Results[1]));
    }
    [Fact] public void DirectAndFallbackCannotEachSpendANewGraphBudget()
    {
        Assert.False(P.TryPlan(new[]{new P.Input(1,Input(true))},out _,out string why,out var work,new P.Limits(4096,1)));
        Assert.Equal("SearchExhausted:RouteGraphs",why);Assert.Equal(1,work.RouteGraphs);Assert.Equal(0,work.StaticRootGraphs);
    }
    [Fact] public void DirectCandidateWorkAlsoHonorsTheBodyNodeBudget()
    {
        Assert.False(P.TryPlan(new[]{new P.Input(1,Input(true))},out _,out string why,out var work,new P.Limits(1,32)));
        Assert.Equal("SearchExhausted:BodyNodes",why);Assert.Equal(1,work.BodyNodes);Assert.Equal(1,work.RouteGraphs);
    }
    [Fact] public void DirectAndExpandedPoliciesBothKeepTheBodyOnTheReachableSideOfAPartition()
    {
        var r=Input();G.Point[] wall={new(-20,-500),new(20,-500),new(20,500),new(-20,500)};
        var scene=new G.StaticScene(1,true,r.Scene.Coverage,-100,1000,new[]{new G.StaticObstacle("literal-partition",null,null,0,200,wall,"literal closed wall")},"literal complete scene");
        var across=new G.Request(r.World,r.SourceModel,r.SourceModelEvidence,r.RawSource,new(new(100,0,0),Q,1,1415),r.AnchorEvidence,r.Model,r.Usable,r.Surfaces,r.Entry,r.SiteBounds,scene);
        Assert.True(P.TryPlan(new[]{new P.Input(1,across)},out var direct,out string why),why);Assert.True(direct.Results[1].Centre.X< -20);
        Assert.True(P.TryPlan(new[]{new P.Input(1,across)},out var expanded,out why,out _,P.Limits.Default,false),why);Assert.True(expanded.Results[1].Centre.X<0);
        Assert.True(G.ValidateSet(direct.Results.Values.ToArray(),out why),why);Assert.True(G.ValidateSet(expanded.Results.Values.ToArray(),out why),why);
        Assert.All(direct.Results[1].Route,p=>Assert.True(p.X< -20));
        // Being in range through the wall cannot qualify the nearer body on the other side.
    }
}
