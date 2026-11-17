using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MissionBound326ApproachTests
{
 static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
 static (MissionProgress.SavedState State,MissionBound326Approach.Arrival Arrival) Case()
 {
  using var d=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Repo,"tests/hds.Tests/Fixtures/Bound326-6772-101216.json")));
  Assert.Equal("90E305D586550497BBD7FD424CBC12754C70763376721493E58BAF5AC4DF8FF0",d.RootElement.GetProperty("RawProofSha256").GetString());
  var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(d.RootElement.GetProperty("State").GetRawText(),new JsonSerializerOptions{IncludeFields=true})!;
  var root=d.RootElement.GetProperty("ActualRoot").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
  SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
  Assert.True(NavMesh.Locate(1,root[0],root[1],root[2],out var n));return(s,new(root,n.sector,n.region,n.floorY));
 }
 static MissionBound326Approach.Sample? Locate(double[] p)=>NavMesh.Locate(1,p[0],p[1],p[2],out var n)?new(p,n.sector,n.region,n.polygon,n.floorY):null;
 static MissionBound326Approach.Sample? Project(double[] p,double radius,double[] root)=>NavMesh.Project(1,p[0],p[1],p[2],out var n,radius,root,false)?new(n.Position,n.location.sector,n.location.region,n.location.polygon,n.location.floorY):null;
 [Fact] public void ActualRawTargetFailsButAuditedNativeBoundsYieldCurrentComponentNativeRangeStanding()
 {
  var c=Case();var o=Assert.Single(c.State.Objects);Assert.Equal(219152925u,o.Spot.mxoStaticId);
  Assert.Null(Project(new[]{o.Spot.pos_x,o.Spot.pos_y,o.Spot.pos_z},800,c.Arrival.Root));
  Assert.Null(Project(c.Arrival.Root,0,c.Arrival.Root));Assert.Equal((ushort)48,c.Arrival.Region);Assert.Equal(800,c.Arrival.FloorY);
  var b=MissionBound326Approach.Load(Repo,o);Assert.Equal(new[]{28440d,882,11585},b.Centre);
  var r=MissionBound326Approach.Select(b,c.Arrival,new(Locate,Project));
  Assert.Equal(new[]{28480d,895,11480},r.Standing);Assert.Equal((ushort)138,r.Nav.Polygon);Assert.InRange(r.NativeDistanceSquared,6499f,6501f);
  Assert.False(r.CollisionProven);Assert.False(r.DirectRouteProven);Assert.False(r.NativePickingProven);
 }
 [Theory][InlineData("world")][InlineData("source")][InlineData("region")][InlineData("floor")][InlineData("root")]
 public void ForeignContextCannotBorrowAuditedApproach(string fault)
 {
  var c=Case();var o=Assert.Single(c.State.Objects);
  if(fault=="world"||fault=="source"){if(fault=="world")o.Spot.metrId=2;else o.Spot.pos_x++;if(fault=="source")Assert.Throws<MissionCheckpointException>(()=>MissionBound326Approach.Load(Repo,o));else Assert.Throws<InvalidDataException>(()=>MissionBound326Approach.Load(Repo,o));return;}
  var a=c.Arrival;if(fault=="region")a=a with{Region=49};if(fault=="floor")a=a with{FloorY=400};if(fault=="root")a=a with{Root=new[]{0d,895,0}};
  int calls=0;var b=MissionBound326Approach.Load(Repo,o);
  Assert.Throws<InvalidDataException>(()=>MissionBound326Approach.Select(b,a,new(Locate,(p,r,f)=>{calls++;return null;})));Assert.Equal(0,calls);
 }
}
