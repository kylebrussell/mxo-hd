using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorGeometryTests
{
    private const string Q="0000000000000000000000000000803f";
    private static G.Point[] Rect(double x1,double z1,double x2,double z2)=>new[]{new G.Point(x1,z1),new G.Point(x2,z1),new G.Point(x2,z2),new G.Point(x1,z2)};
    private static G.Request Input(double origin=0,bool complete=true,G.StaticObstacle[]? obstacles=null,G.Root? root=null)
    {
        var id=new G.Id(127,1,1);var surface=new G.Surface(id,Rect(origin-500,-500,origin+500,500),0,0,0);
        var scene=new G.StaticScene(1,complete,new G.Bounds(origin-500,origin+500,-500,500),-100,1000,
            obstacles??new[]{new G.StaticObstacle("GO:100",100,1407,0,100,Rect(origin-20,-20,origin+20,20),"literal static model bounds")},"complete literal scene; not real-city coverage");
        return new G.Request(1,G.SourceModelPresence.MeasuredModel,"literal Model6 source proof",new G.RawPose(new(origin,155,0),Q,100,1407),new G.RawPose(new(origin,10,0),Q,100,1407),"literal audited native anchor",
            new OpenFloorModelBounds(3,new(-10,-10,-10),new(10,10,10),"literal model min/max"),
            MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"native INITIAL400 profile"),new[]{surface},
            root??new G.Root(id,new(origin-300,95,0),"literal same-floor standing root"),new G.Bounds(origin-400,origin+400,-400,400),scene);
    }
    private static G.Request With(G.Request r,G.RawPose? anchor=null,IEnumerable<G.Surface>? surfaces=null,G.Root? root=null,G.StaticScene? scene=null,OpenFloorModelBounds? model=null)=>
        new(r.World,r.SourceModel,r.SourceModelEvidence,r.RawSource,anchor??r.Anchor,r.AnchorEvidence,model??r.Model,r.Usable,surfaces??r.Surfaces,root??r.Entry,r.SiteBounds,scene??r.Scene);
    [Fact]
    public void LiteralSourceObstacleProducesSeparateOpenFloorProxyAndInRangeStandingPoint()
    {
        var request=Input();var result=G.Plan(request);Assert.True(result.HasPlan,result.Refusal);
        Assert.Equal(-30.00006103515625,result.Centre.X);Assert.Equal(10,result.Centre.Y);Assert.InRange(result.Centre.Z,-1e-7,1e-7);
        // Symmetric clear faces have the same analytic distance; finite projection residuals may choose a different face.
        double approach=Math.Sqrt(Math.Pow(result.Standing.X-result.Centre.X,2)+Math.Pow(result.Standing.Z-result.Centre.Z,2));
        Assert.InRange(approach,10.00006103515625-1e-7,10.00006103515625+1e-7);Assert.Equal(95,result.Standing.Y);
        Assert.True(result.Range.Within);Assert.Equal(400,result.Range.LowerBound);Assert.False(result.NativeWalkingProven);Assert.False(result.NativePickingProven);
        Assert.Equal(0,request.RawSource.Position.X);Assert.Equal(155,request.RawSource.Position.Y);Assert.Equal(Q,request.RawSource.QuaternionHex);
        AssertIndependentClear(result,request.Scene.Obstacles);
    }
    [Theory] [InlineData(32768)] [InlineData(-32768)] [InlineData(16777216)] [InlineData(-16777216)]
    public void FinalPublishedAndFloatCastFootprintsCannotRoundBackOntoClosedStaticBounds(double origin)
    {
        var request=Input(origin);var result=G.Plan(request);Assert.True(result.HasPlan,result.Refusal);
        Assert.True(result.NumericalGuard>=8e-7);Assert.Equal((double)(float)result.Centre.X,result.Centre.X);
        double castX=(float)result.Centre.X;Assert.True(castX+10<origin-20);
        AssertIndependentClear(result,request.Scene.Obstacles);
        var repeat=G.Plan(request);Assert.Equal((result.Centre.X,result.Centre.Y,result.Centre.Z),(repeat.Centre.X,repeat.Centre.Y,repeat.Centre.Z));
    }
    [Fact]
    public void EveryNearbyRelevantStaticParticipatesRatherThanOnlyTheSource()
    {
        var source=new G.StaticObstacle("GO:100",100,1407,0,100,Rect(-20,-20,20,20),"source");
        var other=new G.StaticObstacle("GO:200",200,444,0,100,Rect(-60,-20,-40,20),"nearby measured static");
        var request=Input(obstacles:new[]{source,other});var result=G.Plan(request);Assert.True(result.HasPlan,result.Refusal);
        Assert.Equal(0,result.Centre.X);Assert.Equal(-30.00006103515625,result.Centre.Z);AssertIndependentClear(result,request.Scene.Obstacles);
    }
    [Fact]
    public void MissingSceneCoverageAndUnknownOrDuplicateStaticBoundsFailClosed()
    {
        var input=Input(complete:false);Assert.Equal("MissingStaticCoverage",G.Plan(input).Refusal);
        var original=Input();var row=original.Scene.Obstacles[0];
        var duplicate=new G.StaticScene(1,true,original.Scene.Coverage,-100,1000,new[]{row,row},"literal duplicate");
        Assert.Equal("UnprovenStaticBounds",G.Plan(With(original,scene:duplicate)).Refusal);
        var shortY=new G.StaticScene(1,true,original.Scene.Coverage,0,50,new[]{row},"incomplete vertical coverage");
        Assert.Equal("IncompleteStaticVerticalEnvelope",G.Plan(With(original,scene:shortY)).Refusal);
    }
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void InvalidStandingRootIsNotSilentlyProjected(double value)
    {
        var request=Input(root:new(new(127,1,1),new(-300,value,0),"invalid injected root"));
        Assert.Equal("MissingFiniteRoot",G.Plan(request).Refusal);
    }
    [Fact]
    public void RootInsideOrTouchingClosedStaticCannotInventAnApproach()
    {
        foreach(double x in new[]{0d,20d})
        {var result=G.Plan(Input(root:new(new(127,1,1),new(x,95,0),"inside or touching source")));Assert.False(result.HasPlan);}
    }
    [Fact]
    public void SourceEvidenceAndProxyProfileIdentityAreMandatory()
    {
        var request=Input();var foreign=new G.RawPose(request.Anchor.Position,Q,101,1407);
        Assert.Equal("MismatchedAuditedInputs",G.Plan(With(request,anchor:foreign)).Refusal);
        var wrongModel=new OpenFloorModelBounds(548,new(-10,-10,-10),new(10,10,10),"wrong proxy shape");
        Assert.Equal("MismatchedAuditedInputs",G.Plan(With(request,model:wrongModel)).Refusal);
        var emptyProof=new G.Request(1,request.SourceModel,request.SourceModelEvidence,request.RawSource,request.Anchor,"",request.Model,request.Usable,request.Surfaces,request.Entry,request.SiteBounds,request.Scene);
        Assert.Equal("MissingAuditedInputs",G.Plan(emptyProof).Refusal);
    }
    [Fact]
    public void RampAndRemoteFloorCannotBecomeTheStandingComponent()
    {
        var request=Input();var ramp=new G.Surface(request.Entry.Surface,Rect(-500,-500,500,500),.01,0,0);
        Assert.Equal("InvalidStandingRoot",G.Plan(With(request,surfaces:new[]{ramp})).Refusal);
        var remote=new G.Surface(new(127,1,2),Rect(-500,-500,500,500),0,0,10);
        Assert.Equal("InvalidStandingRoot",G.Plan(With(request,surfaces:new[]{remote})).Refusal);
    }
    [Fact]
    public void ModelMinimumPlacesTheBaseOnTheExactFloorInsteadOfUsingHalfExtents()
    {
        var request=Input();var mesh=new OpenFloorModelBounds(3,new(-10,-17,-10),new(10,20,10),"literal asymmetric bounds");
        var result=G.Plan(With(request,model:mesh));Assert.True(result.HasPlan,result.Refusal);Assert.Equal(17,result.Centre.Y);
        Assert.Equal(0,result.Centre.Y+mesh.Min.Y);
    }
    [Fact]
    public void ImmutableInputCopiesPreventCallerMutationFromChangingAPlan()
    {
        var polygon=Rect(-20,-20,20,20);var obstacle=new G.StaticObstacle("GO:100",100,1407,0,100,polygon,"literal");
        var rows=new[]{obstacle};var input=Input(obstacles:rows);polygon[0]=new(-999,-999);rows[0]=new("GO:999",999,1407,0,100,Rect(-400,-400,400,400),"changed caller array");
        Assert.Equal(100u,input.Scene.Obstacles[0].Key!.Value);Assert.Equal(-20,input.Scene.Obstacles[0].Footprint[0].X);Assert.True(G.Plan(input).HasPlan);
    }
    [Fact]
    public void FixedReplayChecksGeometryAndRangeWithoutReselectingNearestPose()
    {
        var request=Input();var result=G.Plan(request);Assert.True(result.HasPlan,result.Refusal);
        Assert.True(G.ValidateFixed(request,result.InputFingerprint,result.Surface,result.Centre,result.Route,out string why),why);
        Assert.True(result.NativeCastRange.Within);Assert.Equal(64,result.InputFingerprint.Length);
        Assert.False(G.ValidateFixed(request,"changed",result.Surface,result.Centre,result.Route,out _));
        Assert.False(G.ValidateFixed(request,result.InputFingerprint,result.Surface,new(-30,10,0),result.Route,out _)); // exact closed contact
        Assert.False(G.ValidateFixed(request,result.InputFingerprint,result.Surface,new(result.Centre.X,11,result.Centre.Z),result.Route,out _));
        Assert.False(G.ValidateFixed(request,result.InputFingerprint,result.Surface,result.Centre,new[]{request.Entry.Standing,new G.Position(0,95,0),result.Standing},out _));
        // A previously fixed valid result is validated as fixed; nearest-centre selection is not rerun.
        Assert.True(G.ValidateFixed(request,result.InputFingerprint,result.Surface,new(-100,10,0),new[]{request.Entry.Standing,new G.Position(-120,95,0)},out why),why);
    }
    [Fact]
    public void MultiObjectSetRefusesOverlapAndCrossedApproachesWithoutRepacking()
    {
        var a=Input();
        var extraSource=new G.StaticObstacle("GO:300",300,1407,0,100,Rect(200,-20,240,20),"second literal source");
        var scene=new G.StaticScene(1,true,a.Scene.Coverage,-100,1000,a.Scene.Obstacles.Append(extraSource),a.Scene.Evidence);
        var first=G.Plan(With(a,scene:scene));Assert.True(first.HasPlan,first.Refusal);
        var request=new G.Request(1,G.SourceModelPresence.MeasuredModel,"literal Model6 source proof",new(new(220,155,0),Q,300,1407),new(new(220,10,0),Q,300,1407),"second audited source",a.Model,a.Usable,a.Surfaces,a.Entry,a.SiteBounds,scene);
        var second=G.Plan(request);Assert.True(second.HasPlan,second.Refusal);
        Assert.True(G.ValidateSet(new[]{first,second},out string why),why);
        Assert.False(G.ValidateSet(new[]{first,first},out _));
    }
    [Fact]
    public void ObstacleOrderingAndCyclicVertexStartDoNotSelectDifferentPoses()
    {
        var a=new G.StaticObstacle("GO:100",100,1407,0,100,Rect(-20,-20,20,20),"source");
        var polygon=Rect(-60,-20,-40,20);var b=new G.StaticObstacle("GO:200",200,444,0,100,polygon,"other");
        var shifted=new G.StaticObstacle("GO:200",200,444,0,100,polygon.Skip(2).Concat(polygon.Take(2)).Reverse(),"other");
        var first=G.Plan(Input(obstacles:new[]{a,b}));var second=G.Plan(Input(obstacles:new[]{shifted,a}));
        Assert.True(first.HasPlan,first.Refusal);Assert.True(second.HasPlan,second.Refusal);
        Assert.Equal((first.Centre.X,first.Centre.Y,first.Centre.Z),(second.Centre.X,second.Centre.Y,second.Centre.Z));
        Assert.Equal(first.Route.Select(p=>(p.X,p.Y,p.Z)),second.Route.Select(p=>(p.X,p.Y,p.Z)));
    }
    [Fact]
    public void PositiveSourceModelEvidenceCannotBeReplacedByAnAbsentObstacle()
    {
        var request=Input();var missing=new G.StaticScene(1,true,request.Scene.Coverage,-100,1000,Array.Empty<G.StaticObstacle>(),"incorrect claimed completeness");
        Assert.Equal("MissingSourceModelEvidence",G.Plan(With(request,scene:missing)).Refusal);
        var unknown=new G.Request(1,G.SourceModelPresence.Unproven,"unproven",request.RawSource,request.Anchor,request.AnchorEvidence,request.Model,request.Usable,request.Surfaces,request.Entry,request.SiteBounds,request.Scene);
        Assert.Equal("MissingSourceModelEvidence",G.Plan(unknown).Refusal);
    }
    // Independent slab intersection oracle, intentionally not resolver clipping/Touches helpers.
    private static bool SegmentHits(double ax,double az,double bx,double bz,double minX,double maxX,double minZ,double maxZ)
    {
        double lo=0,hi=1;
        foreach(var axis in new[]{(A:ax,B:bx,Min:minX,Max:maxX),(A:az,B:bz,Min:minZ,Max:maxZ)})
        {double d=axis.B-axis.A;if(d==0){if(axis.A<axis.Min||axis.A>axis.Max)return false;}else{double t1=(axis.Min-axis.A)/d,t2=(axis.Max-axis.A)/d;lo=Math.Max(lo,Math.Min(t1,t2));hi=Math.Min(hi,Math.Max(t1,t2));if(lo>hi)return false;}}
        return true;
    }
    private static void AssertIndependentClear(G.Result result,IReadOnlyList<G.StaticObstacle> obstacles)
    {
        foreach(var o in obstacles)
        {
            double minX=o.Footprint.Min(p=>p.X),maxX=o.Footprint.Max(p=>p.X),minZ=o.Footprint.Min(p=>p.Z),maxZ=o.Footprint.Max(p=>p.Z);
            Assert.True(result.Base.Max(p=>p.X)<minX||result.Base.Min(p=>p.X)>maxX||result.Base.Max(p=>p.Z)<minZ||result.Base.Min(p=>p.Z)>maxZ);
            for(int i=0;i<result.Route.Count-1;i++)
            {var a=result.Route[i];var b=result.Route[i+1];Assert.False(SegmentHits(a.X,a.Z,b.X,b.Z,minX,maxX,minZ,maxZ));Assert.False(SegmentHits((float)a.X,(float)a.Z,(float)b.X,(float)b.Z,minX,maxX,minZ,maxZ));}
        }
    }
}
