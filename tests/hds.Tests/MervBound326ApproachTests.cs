using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MervBound326ApproachTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static (MissionProgress.SavedState State,OwnViewLifeLog Log,MervBound326Approach.Arrival Arrival) Case()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/MervBound326-4e46e554.json")));var f=doc.RootElement;
        Assert.Equal("9DFB2646AA02EBA10DFCC218605028C40AEAA417414092D7C7B1C1100C16158B",f.GetProperty("RawProofSha256").GetString());
        var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(f.GetProperty("State").GetRawText(),Fields)!;
        var root=f.GetProperty("ActualRoot").EnumerateArray().Select(v=>v.GetDouble()).ToArray();var own=f.GetProperty("OwnBirthBase64").GetBytesFromBase64();
        var log=new OwnViewLifeLog();log.Observe(false,own,0,WireFormat.FindCreations(own));
        var at=new NavLocation{sector=1118,region=36,polygon=106,floorY=1600};return(state,log,MervBound326Approach.Capture(state,root,at,log.Snapshot(),true));
    }
    static MervBound326Approach.Bounds Bounds()=>new(0x45e0007b,new[]{40160d,1682,12705},new[]{40130d,1602,12680},new[]{40190d,1762,12730},"00000000F30435BF00000000F304353F",36);
    static MervBound326Approach.Nav Fake(MervBound326Approach.Arrival a,double[]? candidate=null,ushort region=36,bool rootMissing=false,bool projectMissing=false)
    {
        var c=candidate??new[]{40030d,1695,12705};MervBound326Approach.Sample S(double[] p,ushort r,ushort poly)=>new(p.ToArray(),1118,r,poly,1600);
        return new(p=>p.SequenceEqual(a.Root)?rootMissing?null:S(p,36,106):S(p,region,315),(p,r,from)=>r==0?rootMissing?null:S(a.Root,36,106):projectMissing?null:S(c,region,315));
    }
    [Fact] public void ExactAuditedCaseHasNativeRangeOutsideBoundsAndOriginalRootComponent()
    {
        var c=Case();SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        var bounds=MervBound326Approach.Load(Repo,c.State.Objects.Single(o=>o.Id==1));Assert.Equal(Bounds().Centre,bounds.Centre);Assert.Equal(Bounds().Minimum,bounds.Minimum);Assert.Equal(Bounds().Maximum,bounds.Maximum);Assert.Equal(Bounds().Quaternion,bounds.Quaternion);
        foreach(var offset in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})Assert.False(NavMesh.Locate(1,bounds.Centre[0]+offset.Item1,bounds.Centre[1],bounds.Centre[2]+offset.Item2,out _));
        var result=MervBound326Approach.Choose(Repo,c.State,1,c.Arrival,c.Log.Snapshot());
        Assert.Equal(new[]{40030d,1695,12705},result.Standing);Assert.Equal((ushort)1118,result.Nav.Sector);Assert.Equal((ushort)36,result.Nav.Region);Assert.Equal((ushort)315,result.Nav.Polygon);
        Assert.InRange(result.Envelope,189.02248,189.02249);Assert.Equal(9999.997f,result.NativeDistanceSquared);Assert.True(result.Standing[0]<result.NativeMinimum[0]);
        Assert.False(result.DirectRouteProven);Assert.False(result.CollisionProven);Assert.False(result.NativePickingProven);
        Assert.False(NavMesh.Project(1,bounds.Centre[0],bounds.Centre[1],bounds.Centre[2],out _,result.Envelope,c.Arrival.Root,true)); // no straight NAV segment
        Assert.True(NavMesh.Project(1,bounds.Centre[0],bounds.Centre[1],bounds.Centre[2],out var p,result.Envelope,c.Arrival.Root,false));Assert.Equal(result.Standing,p.Position);
    }
    [Fact] public void NativeFloatPredicateMatchesReviewedRuntimeOracle()
    {
        var b=Bounds();var q=new OpenFloorOrientation(b.Quaternion);var centre=new OpenFloorPoint(b.Centre[0],b.Centre[1],b.Centre[2]);
        var profile=new MissionOpenFloorUsableRange.Profile(326,3,100,new OpenFloorPoint(25,80,30),"primary original GOB326");
        foreach(var p in new[]{new[]{40030d,1695,12705},new[]{39990d,1695,12705},new[]{40160d,1682,12705}})
        {var oracle=MissionOpenFloorUsableRange.Test(profile,new OpenFloorPoint(p[0],p[1],p[2]),centre,q);Assert.Equal(oracle.DistanceSquared,MervBound326Approach.NativeDistanceSquared(p,b));}
        Assert.True(q.TryNativeWorldRadius(100+Math.Sqrt(25*25+80*80+30*30),out double expected));Assert.Equal(expected,MervBound326Approach.Envelope(b));
    }
    [Theory][InlineData("mission")][InlineData("phase")][InlineData("instance")][InlineData("identity")][InlineData("started")][InlineData("unentered")][InlineData("failed")][InlineData("world")][InlineData("raw-kind")][InlineData("other-kind")][InlineData("reset")][InlineData("other-own-life")]
    public void OwnedArrivalAndExactBoundTypeCannotBeBorrowed(string fault)
    {
        var c=Case();var s=c.State;var life=c.Log.Snapshot();switch(fault){case "mission":s.Mission++;break;case "phase":s.Phase++;break;case "instance":s.Instance++;break;case "identity":s.Identity=new string('b',32);break;case "started":s.Started++;break;
            case "unentered":s.Flags["entered"]=false;break;case "failed":s.Flags["failed"]=true;break;case "world":s.Objects[0].Spot.metrId=2;break;case "raw-kind":s.Objects[0].Spot.type=BitConverter.GetBytes((ushort)1408);break;case "other-kind":s.Objects[0].Goid=1433;break;
            case "reset":c.Log.Observe(true,null,1,Array.Empty<ViewCreation>());break;case "other-own-life":life=Case().Log.Snapshot();break;}
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.RequireScope(s,1,c.Arrival,life));
    }
    [Theory][InlineData("root-missing")][InlineData("project-missing")][InlineData("region")][InlineData("floor")][InlineData("sector")][InlineData("inside")][InlineData("native-far")][InlineData("nonfinite")][InlineData("float-overflow")][InlineData("bad-quaternion")]
    public void CandidateAndRootMustPassEveryIndependentGate(string fault)
    {
        var c=Case();var b=Bounds();var a=c.Arrival;MervBound326Approach.Nav nav;
        switch(fault){case "root-missing":nav=Fake(a,rootMissing:true);break;case "project-missing":nav=Fake(a,projectMissing:true);break;case "region":nav=Fake(a,region:37);break;
            case "floor":a=a with{FloorY=2000};nav=Fake(a);break;case "sector":a=a with{Sector=209};nav=Fake(a);break;
            case "inside":nav=Fake(a,new[]{40160d,1695,12705});break;case "native-far":nav=Fake(a,new[]{39990d,1695,12705});break;
            case "nonfinite":nav=Fake(a,new[]{double.NaN,1695,12705});break;case "float-overflow":nav=Fake(a,new[]{double.MaxValue,1695,12705});break;
            default:b=b with{Quaternion="00000000000000000000000000000000"};nav=Fake(a);break;}
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Select(b,a,nav));
    }
    [Fact] public void MissingRootReferenceCannotFallBackToAnUnrelatedNearestComponent()
    {
        var c=Case();int targetCalls=0;var good=Fake(c.Arrival);
        var nav=new MervBound326Approach.Nav(good.Locate,(p,r,from)=>{if(r==0)return null;targetCalls++;return good.Project(p,r,from);});
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Select(Bounds(),c.Arrival,nav));Assert.Equal(0,targetCalls);
    }
    [Fact] public void CandidateIsCastToFloat32AndRecheckedRatherThanUsingTheDouble()
    {
        var c=Case();var result=MervBound326Approach.Select(Bounds(),c.Arrival,Fake(c.Arrival,new[]{40030.00001,1695,12705}));Assert.Equal(40030d,result.Standing[0]);
        Assert.True(ContainerActions.InReach(new[]{39990d,1695,12705}.SelectMany(BitConverter.GetBytes).ToArray(),40160,1682,12705)); // server600 alone is insufficient
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Select(Bounds(),c.Arrival,Fake(c.Arrival,new[]{39990d,1695,12705})));
    }
    [Theory][InlineData("xyz")][InlineData("key")][InlineData("quaternion")]
    public void OriginalRawSignatureCannotAdoptAnotherBoundProfile(string fault)
    {
        var c=Case();var obj=c.State.Objects[0];switch(fault){case "xyz":obj.Spot.pos_x++;break;case "key":obj.Spot.mxoStaticId++;break;default:obj.Spot.quat="0000000000000000000000000000803f".Replace("803f","003f");break;}
        Assert.Throws<MissionCheckpointException>(()=>MervBound326Approach.Load(Repo,obj));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void QaBoundsReadRequiresThePinnedManifestAndPayload(bool validManifest)
    {
        var c=Case();string temp=Path.Combine(Path.GetTempPath(),"mxo-bound326-qa-"+Guid.NewGuid().ToString("N"));var folder=Path.Combine(temp,"data/mission_bound_poses/1");Directory.CreateDirectory(folder);
        try{File.WriteAllText(Path.Combine(folder,"manifest.json"),validManifest?File.ReadAllText(Path.Combine(Repo,"data/mission_bound_poses/1/manifest.json")):"{}");if(validManifest)File.WriteAllBytes(Path.Combine(folder,"1118.json.gz"),new byte[]{1,2,3});
            Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Load(temp,c.State.Objects[0]));}
        finally{Directory.Delete(temp,true);}
    }
}
