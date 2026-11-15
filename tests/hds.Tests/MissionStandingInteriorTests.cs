using System.Reflection;
using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionStandingInteriorTests
{
    [Fact] public void NativeModeIsDiscoveredAndNormalWireScenarioDoesNotOverrideSources()
    {
        var mode=NativeQa.NativeQaModes.Find("mission-standing-interior");Assert.IsType<NativeQa.Modes.MissionStandingInteriorMode>(mode);
        var plan=mode.Plan;Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Empty(plan.Bootstrap);
        Assert.Equal("201202",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Single(plan.EnvironmentOverrides);
        Assert.Equal(HeadlessClient.PosterHuntFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.DoesNotContain(plan.AfterHelpers,s=>s.Contains("char_active_missions")||s.Contains("0d1003dc"));
        Assert.Single(HeadlessClient.Scenarios.All,s=>s.Name=="mission-standing-interior");
    }
    private const string Q="0000000000000000000000000000803f";
    private static G.Point[] Rect(double x,double z,double a,double b)=>new[]{new G.Point(x,z),new G.Point(a,z),new G.Point(a,b),new G.Point(x,b)};
    [Fact] public void ExactS3SliverHasNoFloat32InteriorButOrdinaryAndDiagonalCellsAreNotInset()
    {
        Assert.True(G.HasNoInteriorFloatCoordinate(Rect(25939.998884831035,14200,25940,14340)));
        Assert.False(G.HasNoInteriorFloatCoordinate(Rect(25600,14000,25689.16049588495,14143.168187920888)));
        // No claim about a diagonal cell's interior; only an empty coordinate interval proves degeneracy.
        Assert.False(G.HasNoInteriorFloatCoordinate(new[]{new G.Point(0,0),new G.Point(10,10),new G.Point(10,10.000001),new G.Point(0,0.000001)}));
        Assert.True(G.HasNoInteriorFloatCoordinate(Rect(-25940,0,-25939.9999,100)));
    }
    private static (bool Found,G.Position[] Route) Standing(bool narrow,bool outside=false,bool bridge=false)
    {
        double z=10000,maxZ=narrow?10000.0001:10100;var id=new G.Id(127,1,1);
        var surface=new G.Surface(id,Rect(0,z,100,maxZ),0,0,0);
        var surfaces=bridge?new[]{surface,new G.Surface(new(127,1,2),Rect(100,z,200,z+0.0001),0,0,0),new G.Surface(new(127,1,3),Rect(200,z,300,maxZ),0,0,0)}:new[]{surface};
        var r=new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(50,145,z),Q,1,1415),new(new(50,0,z),Q,1,1415),"literal anchor",
            new OpenFloorModelBounds(3,new(-1,-1,-1),new(1,1,1),"literal bounds"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal400"),surfaces,
            new(id,new(outside?-10:10,95,z),"literal root"),new(0,bridge?300:100,z,maxZ),new(1,true,new(0,bridge?300:100,z,maxZ),-100,1000,Array.Empty<G.StaticObstacle>(),"literal coverage"));
        object?[] args={r,surfaces,Rect(400,10300,410,10310),new G.Position(bridge?250:50,0,bridge?10050:z),0.00001,null,null,null,null};
        bool found=(bool)typeof(G).GetMethod("Standing",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args)!;
        return(found,found?(G.Position[])args[5]!:Array.Empty<G.Position>());
    }
    [Fact] public void BroadEndpointReachedThroughSliverIsDeprioritizedForWholeRoute()
    {
        var r=Standing(false,bridge:true);Assert.True(r.Found);
        // The closest candidate is in the broad last cell, but its route traverses
        // the degenerate middle cell. Endpoint-only ranking would choose x250.
        Assert.Equal(100,r.Route.Last().X);Assert.Equal(10050,r.Route.Last().Z);
    }
    [Fact] public void NarrowRepresentationalCorridorRetainsTheValidLegacyFallback()
    {
        var r=Standing(true);Assert.True(r.Found);Assert.Equal(50,r.Route.Last().X);Assert.Equal(10000,r.Route.Last().Z);
        Assert.All(r.Route,p=>Assert.Equal(10000,p.Z));
    }
    [Fact] public void OrdinaryInteriorSelectionIsUnchangedAndDisconnectedRootStillRefuses()
    {
        var r=Standing(false);Assert.True(r.Found);Assert.Equal(50,r.Route.Last().X);Assert.Equal(10000,r.Route.Last().Z);
        Assert.False(Standing(true,true).Found);
    }
}
