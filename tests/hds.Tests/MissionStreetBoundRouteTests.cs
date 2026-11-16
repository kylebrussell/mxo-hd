using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using E=hds.MissionInteriorEvidence;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MissionStreetBoundRouteTests
{
 static string Repo{get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
 static T Read<T>(string text)=>JsonSerializer.Deserialize<T>(text,MissionProgress.SaveJson)!;
 static string Json<T>(T value)=>JsonSerializer.Serialize(value,MissionProgress.SaveJson);
 sealed class Case:IDisposable
 {
  internal MissionProgress P;internal MissionStreetSelection Selection;internal MissionOpenFloorChoice[] Choices;internal MissionSite Site;internal E Evidence;internal Dictionary<uint,StaticWorldObject> Doors;
  readonly Dictionary<ushort,List<MissionSite>> catalog;readonly List<MissionSite>? old;readonly bool had;
  internal Case()
  {
   var state=Read<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/BoundRoute-592-101216-phase2.json")));
   Selection=Read<MissionStreetEnvelope>(state.StreetAccess.GetRawText()).Witnesses.Single(w=>w.Phase==2);
   var m=new Mission{id=state.Mission,district=1};m.phases.Add(new());m.phases.Add(new());m.phases.Add(new(){site="Normal"});m.phases[2].objects.Add(new(){id=1,kind="Furniture"});
   P=new MissionProgress(m){phaseIndex=2,persistenceId=state.Identity,startedAt=state.Started,streetPolicy=true};P.itemSlots[6783]=40;
   Site=new MissionSite{metrId=1,elevatorDoor=MissionNpcInteriorPlanning.ReadRaw(Selection.Context.Floor.RawDoorJson),lobbyDoor=MissionNpcInteriorPlanning.ReadRaw(Selection.Context.Lobby.RawDoorJson),minX=8000,maxX=14000,minZ=5000,maxZ=8000,floorY=546};Site.shaft.AddRange(new[]{Site.elevatorDoor,Site.lobbyDoor});
   var source=state.Objects.Single().Spot;Site.containers.Add(source);Site.npcSpots.Add(new[]{13380.6171875,495,6369.380859375,0d});
   Selection=new MissionStreetSelection(1,m.id,P.persistenceId,P.startedAt,2,MissionOpenFloorSite.From(Site),"Objects",Selection.Context,null);
   Choices=new[]{new MissionOpenFloorChoice(1,326,Json(source))};
   P.site=Site;P.sites.Add(Site);P.objects[1]=new(){def=m.phases[2].objects[0],spot=source,goid=326};
   Evidence=E.LoadActive(Path.Combine(Repo,"data/mission_interior_evidence_v2/1"),1,238);
   using var f=File.OpenRead(Path.Combine(Repo,"data/mission_interior_evidence_v2/1/238.json.gz"));using var z=new GZipStream(f,CompressionMode.Decompress);var doc=JsonSerializer.Deserialize<E.Document>(z)!;
   Doors=doc.Groups.ToDictionary(g=>g.Key,g=>{var s=g.Legacy[0].Signature;return new StaticWorldObject{metrId=1,sectorID=238,staticId=g.Key&0xfffff,mxoStaticId=g.Key,type=BitConverter.GetBytes(s.Goid),pos_x=s.Position[0],pos_y=s.Position[1],pos_z=s.Position[2],rot=s.Rotation,quat=s.Quaternion};});
   SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
   catalog=(Dictionary<ushort,List<MissionSite>>)typeof(MissionSites).GetField("byDistrict",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;had=catalog.TryGetValue(1,out old);catalog[1]=new(){Site};
  }
  internal StaticWorldObject Door(uint key)=>Doors.TryGetValue(key,out var row)?row:null!;
  internal MissionStreetSelection Build()=>MissionStreetBoundRoutes.Build(P,Selection,Choices,Door);
  public void Dispose(){if(had)catalog[1]=old!;else catalog.Remove(1);}
 }
 [Fact]public void Literal238CrossRegionRouteStoresNativeRangeAndRequiredOwnGoidDoorAndReplaysWithoutChangingSource()
 {
  using var c=new Case();string old=Json(c.Selection);var built=c.Build();Assert.Equal(2,built.Version);Assert.Equal(old,Json(c.Selection));
  var e=MissionStreetBoundRoutes.Read(built.BoundRoutes);var r=Assert.Single(e.Routes);Assert.Equal(new[]{8575d,495,6495},r.Standing);Assert.Equal((ushort)50,r.Region);Assert.Equal((ushort)41,r.Nodes[0].Region);Assert.Equal((ushort)217,r.Target.Polygon);
  var d=Assert.Single(r.Doors);Assert.Equal(249561776u,d.Key);Assert.Equal((ushort)6958,d.Goid);Assert.Equal(new[]{10300d,545,5600},d.Position);Assert.Equal("00000000ffff7f3f00000000f3043533",d.Quaternion);
  var round=Read<MissionStreetSelection>(Json(built));MissionStreetBoundRoutes.Validate(c.P,round,c.Door,c.Choices);
  c.P.streetWitnesses[2]=round;Assert.True(MissionStreetCoverage.OwnsDoor(c.P,c.Door(d.Key)));Assert.Single(MissionStreetBoundRoutes.CurrentDoors(c.P));c.P.phaseIndex=1;Assert.Empty(MissionStreetBoundRoutes.CurrentDoors(c.P));
 }
 [Theory][InlineData("owner")][InlineData("mission")][InlineData("phase")][InlineData("world")][InlineData("source")][InlineData("digest")][InlineData("standing")][InlineData("node")][InlineData("door-key")][InlineData("door-q")][InlineData("door-goid")]
 public void RecognizedFixedRoutesRefuseForeignOrTamperedAuthority(string fault)
 {
  using var c=new Case();var built=c.Build();var json=JsonNode.Parse(Json(built))!;var e=json["BoundRoutes"]!;var r=e["Routes"]![0]!;var d=r["Doors"]![0]!;
  if(fault=="owner")json["Identity"]="foreign";if(fault=="mission")json["Mission"]=101215;if(fault=="phase")json["Phase"]=1;if(fault=="world")json["Site"]!["District"]=2;
  if(fault=="source"){var raw=JsonNode.Parse(r["RawSourceJson"]!.GetValue<string>())!;raw["pos_x"]=0;r["RawSourceJson"]=raw.ToJsonString();}
  if(fault=="digest")e["EvidenceDigest"]=new string('a',64);if(fault=="standing")r["Standing"]![1]=496;if(fault=="node")r["Nodes"]![1]!["Polygon"]=65535;
  if(fault=="door-key")d["Key"]=249561777u;if(fault=="door-q")d["Quaternion"]="0000000000000000000000000000803f";if(fault=="door-goid")d["Goid"]=580;
  var bad=Read<MissionStreetSelection>(json.ToJsonString());var error=Record.Exception(()=>MissionStreetBoundRoutes.Validate(c.P,bad,c.Door,c.Choices));Assert.True(error is InvalidDataException or MissionPlacementRestoreException,error?.ToString()??"Expected strict refusal");
 }
 [Theory][InlineData("null")][InlineData("alias-id")][InlineData("duplicate-door")][InlineData("version")]
 public void MalformedRecognizedMetadataHasTypedQuarantine(string fault)
 {
  using var c=new Case();var built=c.Build();var node=JsonNode.Parse(built.BoundRoutes.GetRawText())!;
  if(fault=="null")node=null;if(fault=="alias-id")node!["Routes"]![0]!["ObjectId"]=65537;
  if(fault=="duplicate-door"){var a=node!["Routes"]![0]!["Doors"]!.AsArray();a.Add(a[0]!.DeepClone());}if(fault=="version")node!["Version"]=3;
  using var doc=JsonDocument.Parse(node?.ToJsonString()??"null");Assert.Throws<MissionPlacementRestoreException>(()=>MissionStreetBoundRoutes.Shape(c.P,doc.RootElement));Assert.Equal((ushort)40,c.P.itemSlots[6783]);
 }
 [Fact]public void OldVersionOneJsonShapeAndPendingRefusalRemainUnchanged()
 {
  using var c=new Case();string old=Json(c.Selection);Assert.DoesNotContain("BoundRoutes",old);var clone=Read<MissionStreetSelection>(old);Assert.Equal(old,Json(clone));
  var refused=new MissionStreetSelection(1,clone.Mission,clone.Identity,clone.Started,clone.Phase,clone.Site,clone.Kind,clone.Context,"NoNativeRangeStanding");Assert.DoesNotContain("BoundRoutes",Json(refused));
  Assert.Equal(old,Json(c.Selection));Assert.Equal((ushort)40,c.P.itemSlots[6783]);
 }
 [Fact]public void RouteMetadataRoundTripsThroughCheckpointAndInvalidRecognizedDataPreservesOwnedItems()
 {
  using var c=new Case();var built=c.Build();c.P.streetWitnesses[2]=built;
  var saved=c.P.CaptureWithNpcCatalog(50,Array.Empty<Mob>(),new[]{c.Site});string exact=Json(saved);
  var decoded=Read<MissionProgress.SavedState>(exact);var envelope=MissionStreetCheckpoint.PrepareRestore(c.P,decoded,new[]{c.Site},null);
  Assert.Equal(2,Assert.Single(envelope.Witnesses).Version);Assert.Equal(built.BoundRoutes.GetRawText(),envelope.Witnesses[0].BoundRoutes.GetRawText());
  var altered=JsonNode.Parse(exact)!;altered["StreetAccess"]!["Witnesses"]![0]!["BoundRoutes"]!["Routes"]![0]!["ObjectId"]=65537;
  var bad=Read<MissionProgress.SavedState>(altered.ToJsonString());Assert.Throws<MissionPlacementRestoreException>(()=>MissionStreetCheckpoint.PrepareRestore(c.P,bad,new[]{c.Site},null));
  Assert.Equal(exact,Json(saved));Assert.Equal((ushort)40,c.P.itemSlots[6783]);
 }
 [Fact]public void FailedFreshRoutePreparationCannotMutateCheckpointItemsOrExistingSourceSelection()
 {
  using var c=new Case();c.P.streetWitnesses[2]=c.Selection;string before=Json(c.P.CaptureWithNpcCatalog(50,Array.Empty<Mob>(),new[]{c.Site}));string raw=Json(c.Selection);
  Assert.Throws<InvalidDataException>(()=>MissionStreetBoundRoutes.Build(c.P,c.Selection,c.Choices,_=>null!));
  Assert.Equal(raw,Json(c.Selection));Assert.Equal(before,Json(c.P.CaptureWithNpcCatalog(50,Array.Empty<Mob>(),new[]{c.Site})));Assert.Equal((ushort)40,c.P.itemSlots[6783]);
 }

}
