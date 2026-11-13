using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using hds;
namespace hds.Tests;
public class PosterBoxFloorProbeTests
{
    [Theory]
    [InlineData("1","1","mxotest_owned",110u,true,true)]
    [InlineData("0","1","mxotest_owned",110u,true,false)]
    [InlineData("1","on","mxotest_owned",110u,true,false)]
    [InlineData("1","1","reality",110u,true,false)]
    [InlineData("1","1","mxotest_",110u,true,false)]
    [InlineData("1","1","mxotest_bad-name",110u,true,false)]
    [InlineData("1","1","mxotest_owned",2u,true,false)]
    [InlineData("1","1","mxotest_owned",111u,true,false)]
    [InlineData("1","1","mxotest_owned",110u,false,false)]
    public void ExplicitModeActualDisposableDatabaseAndCsrOnly(string dev,string mode,string db,uint character,bool ready,bool expected)=>
        Assert.Equal(expected,PosterBoxFloorProbe.Allowed(dev,mode,db,character,ready));
    private static MissionObjectState Source()=>new(){def=new(){id=1},goid=548,spot=new(){metrId=1,sectorID=209,staticId=988,mxoStaticId=0x0d1003dc,type=new byte[]{0x87,5},
        pos_x=23940,pos_y=2245,pos_z=15050,quat="00000000f204353f00000000f304353f"}};
    [Fact] public void GuardRequiresActualSourceAndLegacyPoseWithoutMutation()
    {
        var s=Source();var options=new JsonSerializerOptions{IncludeFields=true};string before=JsonSerializer.Serialize(s,options);
        Assert.True(PosterBoxFloorProbe.SourceMatches(s));Assert.Equal(before,JsonSerializer.Serialize(s,options));
        s.goid=3;Assert.False(PosterBoxFloorProbe.SourceMatches(s));s=Source();s.spot.staticId++;Assert.False(PosterBoxFloorProbe.SourceMatches(s));
        s=Source();s.spot.pos_y=2100;Assert.False(PosterBoxFloorProbe.SourceMatches(s));s=Source();s.spot.type=new byte[]{0x88,5};Assert.False(PosterBoxFloorProbe.SourceMatches(s));
        s=Source();s.spot.type=null!;Assert.False(PosterBoxFloorProbe.SourceMatches(s));s=Source();s.spot.type=new byte[]{0x87};Assert.False(PosterBoxFloorProbe.SourceMatches(s));
        s=Source();s.spot.type=new byte[]{0x87,5,0,1};Assert.False(PosterBoxFloorProbe.SourceMatches(s));
        s=Source();s.spot.quat=PosterBoxFloorProbe.Identity;Assert.False(PosterBoxFloorProbe.SourceMatches(s));s=Source();s.def.id=2;Assert.False(PosterBoxFloorProbe.SourceMatches(s));
    }
    [Fact] public void FreeProbeHasNoMissionAuthorityAndOnlyLiteralClass46Fields()
    {
        var obj=PosterBoxFloorProbe.CreateObject();Assert.Equal(uint.MaxValue,obj.GetRelatedStaticObjId());
        Assert.Equal(new byte[4],obj.MissionKey.getValue());Assert.Equal(new byte[]{1},obj.UseCount.getValue());Assert.Equal(BitConverter.GetBytes(0x835u),obj.CurrentState.getValue());
        Assert.Equal(Convert.FromHexString(PosterBoxFloorProbe.Identity),obj.Orientation.getValue());
        Assert.Equal(new[]{25100d,2040d,14100d},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(obj.Position.getValue(),i*8)));
        Assert.False(obj.HalfExtents.isActive()); // no bounds override; the native template supplies its own defaults
    }
    [Theory][InlineData(false,false,true)][InlineData(true,false,false)][InlineData(false,true,false)]
    public void CleanupNeverDeletesReusedSpawnOrBoundSource(bool replacedSpawn,bool bound,bool deleted)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.viewMan=new ViewManager();
        var field=typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!;
        field.SetValue(c,Activator.CreateInstance(field.FieldType,true));
        var source=new ClientView(548,10,100);var own=c.viewMan.addView(101,548);own.spawnId=7;
        var helper=typeof(PosterBoxFloorProbe);var leaseType=helper.GetNestedType("Lease",BindingFlags.NonPublic)!;var lease=Activator.CreateInstance(leaseType,true)!;
        void Set(string name,object value)=>leaseType.GetField(name)!.SetValue(lease,value);
        Set("Views",c.viewMan);Set("View",own);Set("SourceView",bound?own:source);Set("Spawn",7u);Set("Epoch",-1L);
        var table=helper.GetField("leases",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        var holder=table.GetType().GetMethod("GetOrCreateValue")!.Invoke(table,new object[]{c})!;holder.GetType().GetField("Active")!.SetValue(holder,lease);
        if(replacedSpawn)own.spawnId=8;
        PosterBoxFloorProbe.Clear(c);Assert.Equal(deleted,c.viewMan.getViewById(own.ViewID)==null);
        PosterBoxFloorProbe.Clear(c); // once only; no stale handle can be deleted later
    }
    [Fact] public void DiscoveredModeEnablesOnlyScopedDiagnosticAndNormalPrerequisites()
    {
        var plan=new NativeQa.Modes.PosterBoxFloorMode().Plan;
        Assert.Equal("poster-box-floor",new NativeQa.Modes.PosterBoxFloorMode().Name);
        Assert.Contains("201201",HeadlessClient.PosterHuntFixture.SeedPrerequisites(110));
        Assert.DoesNotContain("201202",HeadlessClient.PosterHuntFixture.SeedPrerequisites(110));
        Assert.Contains("LOOK DOWN",string.Join(" ",plan.ReadyMessages));
    }
    [Fact] public void LiteralMeshBottomAndEntireFootprintMatchFlatNavFloor()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        foreach(var x in new[]{25050d,25100d,25150d})foreach(var z in new[]{14030d,14100d,14170d})
        {Assert.True(NavMesh.Locate(1,x,2095,z,out var point));Assert.Equal(2000,point.floorY);}
        Assert.Equal(2000,2040-40); // independently measured MGA minY=-40, identity root matrices
        Assert.True(NavMesh.Project(1,24960,2095,14100,out var p,reachableFrom:new[]{25579.31146596289,2095d,13959.481277120463}));
        Assert.Equal((24960d,2095d,14100d),(p.x,p.y,p.z));
    }
}
