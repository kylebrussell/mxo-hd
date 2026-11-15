using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorCoverageSubsetTests
{
    private const string Q="0000000000000000000000000000803f";
    private static G.Request Input(G.Bounds? site=null,G.Bounds? coverage=null,G.Position? root=null)
    {
        var id=new G.Id(127,1,1);G.Point[] polygon={new(-500,-500),new(500,-500),new(500,500),new(-500,500)};
        return new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(300,145,0),Q,1,1415),new(new(300,0,0),Q,1,1415),"literal source",
            new OpenFloorModelBounds(3,new(-10,-10,-10),new(10,10,10),"literal bounds"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal400"),new[]{new G.Surface(id,polygon,0,0,0)},
            new(id,root??new(-150,95,0),"literal root"),site??new(-500,500,-500,500),new(1,true,coverage??new(-200,200,-200,200),-100,1000,Array.Empty<G.StaticObstacle>(),"literal complete scene"));
    }
    private static void InCoverage(G.Request r,IEnumerable<G.Point> points)
    {foreach(var p in points){Assert.InRange(p.X,r.Scene.Coverage.MinX,r.Scene.Coverage.MaxX);Assert.InRange(p.Z,r.Scene.Coverage.MinZ,r.Scene.Coverage.MaxZ);}}
    [Fact] public void PartialSiteCanPlanAndReplayOnlyInsideTheUnchangedQualifiedSubset()
    {
        var r=Input();string before=r.Fingerprint;var result=G.Plan(r);Assert.True(result.HasPlan,result.Refusal);
        Assert.Equal(new G.Bounds(-500,500,-500,500),r.SiteBounds);Assert.Equal(new G.Bounds(-200,200,-200,200),r.Scene.Coverage);
        InCoverage(r,result.Base);InCoverage(r,result.Route.Select(p=>p.Xz));Assert.Equal(before,r.Fingerprint);
        Assert.True(G.ValidateFixed(r,before,result.Surface,result.Centre,result.Route,out string why),why);Assert.True(G.ValidateSet(new[]{result},out why),why);
    }
    [Fact] public void BodyOutsideCoverageRefusesEvenWhenSiteNavAndStandingAreOtherwiseValid()
    {
        var r=Input();var result=G.Plan(r);Assert.True(result.HasPlan);
        Assert.False(G.ValidateFixed(r,r.Fingerprint,result.Surface,new(195,10,0),result.Route,out string why));Assert.Equal("InvalidFixedGeometry",why);
    }
    [Fact] public void NativeCastOverhangCannotBeSmuggledThroughAStoredNonrepresentableCentre()
    {
        var r=Input(coverage:new(-200,199.9999995,-200,200));var result=G.Plan(r);Assert.True(result.HasPlan,result.Refusal);
        const double x=189.999999;Assert.True(x+10<r.Scene.Coverage.MaxX);Assert.True((double)(float)x+10>r.Scene.Coverage.MaxX);
        Assert.False(G.ValidateFixed(r,r.Fingerprint,result.Surface,new(x,10,0),result.Route,out string why));Assert.Equal("InvalidFixedGeometry",why);
    }
    [Fact] public void UncoveredRouteIsRefusedWithoutEnlargingTheSceneOrChangingTheEndpoint()
    {
        var r=Input();var result=G.Plan(r);Assert.True(result.HasPlan);
        G.Position[] route={r.Entry.Standing,new(-250,95,250),result.Standing};
        Assert.False(G.ValidateFixed(r,r.Fingerprint,result.Surface,result.Centre,route,out string why));Assert.Equal("InvalidFixedStandingRouteOrRange",why);
    }
    [Fact] public void RootOutsideOrDisjointIntersectionCannotGainCoverageFromTheSite()
    {
        Assert.Equal("IncompleteStaticEnvelope",G.Plan(Input(root:new(250,95,0))).Refusal);
        Assert.Equal("IncompleteStaticEnvelope",G.Plan(Input(site:new(300,500,-100,100))).Refusal);
        Assert.Equal("IncompleteStaticEnvelope",G.Plan(Input(site:new(200,500,-100,100))).Refusal); // zero-area edge contact
    }
    [Fact] public void FullyContainedRequestRetainsItsOriginalSiteDomainAndFixedCoordinates()
    {
        var r=Input(site:new(-200,200,-200,200),coverage:new(-500,500,-500,500));var result=G.Plan(r);Assert.True(result.HasPlan,result.Refusal);
        Assert.Equal(190,result.Centre.X);Assert.Equal(10,result.Centre.Y);Assert.Equal(0,result.Centre.Z);
        Assert.True(G.ValidateFixed(r,r.Fingerprint,result.Surface,result.Centre,result.Route,out string why),why);
    }
}
