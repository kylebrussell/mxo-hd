using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MervBound326ActualStreetRootTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static (MissionProgress.SavedState State,OwnViewLifeLog Log,MervBound326Approach.Arrival Arrival) Case()
    {
        using var d=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Repo,"tests/hds.Tests/Fixtures/MervBound326-6ab301203.json")));var f=d.RootElement;
        Assert.Equal("0F145E9DA4A35E0BFEC75D60B8A4A99372EA29ABBB1D4902308755DB9AF08354",f.GetProperty("RawProofSha256").GetString());
        var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(f.GetProperty("State").GetRawText(),Fields)!;
        double[] root=f.GetProperty("ActualRoot").EnumerateArray().Select(v=>v.GetDouble()).ToArray();Assert.Equal(new[]{-3682.384301035021,495,-6317.92706453424},root);
        SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));Assert.True(NavMesh.Locate(1,root[0],root[1],root[2],out var nav));
        var log=new OwnViewLifeLog();byte[] own=f.GetProperty("OwnBirthBase64").GetBytesFromBase64();log.Observe(false,own,0,WireFormat.FindCreations(own));
        return(s,log,MervBound326Approach.Capture(s,root,nav,log.Snapshot(),true));
    }
    [Fact] public void CapturedStreetRideRootIsGroundedDespiteFailingNpcSpawnInsetAndTargetRemainsInNativeRange()
    {
        var c=Case();var root=c.Arrival.Root;string before=JsonSerializer.Serialize(c.State,Fields);var street=MervFollowupStreetEntry.ForSite(c.State)!;
        Assert.Equal(root,street.Floor.Standing);Assert.Equal((ushort)1129,c.Arrival.Sector);Assert.Equal((ushort)16,c.Arrival.Region);Assert.Equal(400,c.Arrival.FloorY);
        Assert.True(NavMesh.Locate(1,root[0],root[1],root[2],out var nav));Assert.Equal((ushort)291,nav.polygon);
        Assert.False(NavMesh.Project(1,root[0],root[1],root[2],out _,0,root,false));
        var source=Assert.Single(c.State.Objects);Assert.Equal(1183842791u,source.Spot.mxoStaticId);Assert.Equal(new[]{-4850d,547,-5545},new[]{source.Spot.pos_x,source.Spot.pos_y,source.Spot.pos_z});
        var b=MervBound326Approach.Load(Repo,source);Assert.Equal(16,b.Region);Assert.Equal(new[]{-3655d,482,-4350},b.Centre);
        var result=MervBound326Approach.Choose(Repo,c.State,1,c.Arrival,c.Log.Snapshot());
        Assert.Equal(new[]{-3655d,495,-4230},result.Standing);Assert.Equal((ushort)1129,result.Nav.Sector);Assert.Equal((ushort)16,result.Nav.Region);Assert.Equal((ushort)161,result.Nav.Polygon);Assert.Equal(400,result.Nav.FloorY);
        Assert.Equal(8100f,result.NativeDistanceSquared);Assert.True(ContainerActions.InReach(result.Standing.SelectMany(BitConverter.GetBytes).ToArray(),b.Centre[0],b.Centre[1],b.Centre[2]));
        Assert.Equal(before,JsonSerializer.Serialize(c.State,Fields));Assert.Equal(root,c.Arrival.Root);Assert.False(result.DirectRouteProven);Assert.False(result.CollisionProven);Assert.False(result.NativePickingProven);
    }
    [Theory] [InlineData("world")] [InlineData("floor")] [InlineData("region")] [InlineData("root-reference")]
    public void ActualRootCannotBorrowAnotherWorldLayerOrNearestReference(string fault)
    {
        var c=Case();var a=c.Arrival;
        if(fault=="world"){c.State.Objects[0].Spot.metrId=2;Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Choose(Repo,c.State,1,a,c.Log.Snapshot()));return;}
        if(fault=="floor")a=a with{FloorY=800};if(fault=="region")a=a with{Region=17};
        var b=MervBound326Approach.Load(Repo,c.State.Objects[0]);int targetCalls=0;
        MervBound326Approach.Sample? Locate(double[] p)=>NavMesh.Locate(1,p[0],p[1],p[2],out var n)?new(fault=="root-reference"?new[]{0d,495,0}:p.ToArray(),n.sector,n.region,n.polygon,n.floorY):null;
        var provider=new MervBound326Approach.Nav(Locate,(p,r,from)=>{targetCalls++;return null;});
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Select(b,a,provider));Assert.Equal(0,targetCalls);
    }
}
