using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class NativeBoundFurnitureTests
{
 static string Root=>NativeBoundFurnitureFixture.FindRepository();
 static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
 static StaticWorldObject Source()=>new(){metrId=1,sectorID=985,staticId=1208,mxoStaticId=1032848568,type=BitConverter.GetBytes((ushort)1450),pos_x=42300,pos_y=1145,pos_z=-27045,rot=90,quat="0000000000000000000000000000803f"};
 static JsonObject Profile()
 {using var f=File.OpenRead(Path.Combine(Root,"data/mission_bound_furniture_poses/1/985.json.gz"));using var gz=new GZipStream(f,CompressionMode.Decompress);var p=JsonNode.Parse(gz)!.AsObject();p["Rows"]=new JsonArray(p["Rows"]!.AsArray().Single(r=>(uint)r!["NativeKey"]! ==1032848568)!.DeepClone());return p;}
 static MissionBoundFurniturePoses Read(JsonObject p){using var d=JsonDocument.Parse(p.ToJsonString());return MissionBoundFurniturePoses.Read(d.RootElement,985);}
 [Fact]public void Exact1450NativePosePreservesRawSourceAnd326Policy()
 {
  var raw=Source();string before=JsonSerializer.Serialize(raw,Options);Assert.True(Read(Profile()).TryResolve(raw,out var pose,out int region,out _));
  Assert.Equal((41445d,1049.98193359375,-27900d),(pose.X,pose.Y,pose.Z));Assert.Equal(114,region);Assert.Equal("00000000F304353F00000000F304353F",Convert.ToHexString(pose.Orientation));
  Assert.NotEqual(raw.pos_x,pose.X);Assert.NotEqual(raw.pos_y,pose.Y);Assert.NotEqual(raw.pos_z,pose.Z);Assert.Equal(before,JsonSerializer.Serialize(raw,Options));var q=pose.Orientation;q[0]=33;Assert.Equal(0,pose.Orientation[0]);
  var effective=MissionObjectPoses.For(new(){spot=raw,goid=1450});Assert.Equal(raw.pos_y,effective.Y);
  Assert.Equal("95657513146af308f60d6e532624e143a572b649a1cad98c4601b9eb8ac1c234",MissionBoundObjectPoses.AuditedManifestSha256);
 }
 [Theory][InlineData("world")][InlineData("goid")][InlineData("key")][InlineData("position")][InlineData("quaternion")][InlineData("rotation")][InlineData("exterior")]
 public void RawIdentityCannotBorrowProfile(string fault)
 {
  var raw=Source();switch(fault){case"world":raw.metrId=2;break;case"goid":raw.type=BitConverter.GetBytes((ushort)1451);break;case"key":raw.mxoStaticId++;break;case"position":raw.pos_x++;break;case"quaternion":raw.quat="000000000000803f0000000000000000";break;case"rotation":raw.rot++;break;case"exterior":raw.exterior=true;break;}
  Assert.False(Read(Profile()).TryResolve(raw,out _,out _,out _));
 }
 [Theory][InlineData("world")][InlineData("format")][InlineData("kind")][InlineData("326")][InlineData("centre")][InlineData("bounds")][InlineData("region")][InlineData("duplicate")][InlineData("navhash")]
 public void TypedProfileCorruptionRefuses(string fault)
 {
  var p=Profile();var r=p["Rows"]![0]!;switch(fault){case"world":p["World"]=2;break;case"format":p["Format"]=2;break;case"kind":p["Kind"]="Bound326";break;case"326":r["Goid"]=326;break;case"centre":r["Centre"]![1]=1050;break;case"bounds":r["Minimum"]![0]=r["Maximum"]![0]!.GetValue<double>();break;case"region":r["Region"]=113;break;case"duplicate":p["Rows"]!.AsArray().Add(r.DeepClone());break;case"navhash":r["NavSha256"]="bad";break;}
  Assert.Throws<InvalidDataException>(()=>Read(p));
 }
 [Fact]public void Native100IsOriginalModelObbNotAnIncreasedCentreRadius()
 {
  Assert.True(MissionBoundFurnitureRange.TryBounds(new(){spot=Source(),goid=1450},out var b,out _));Assert.Equal(new[]{84f,49.981971740722656f,29.19049835205078f},b.HalfExtents);
  var standing=new[]{41545d,1095,-27900};Assert.True(MissionBoundFurnitureRange.Valid(standing,b));Assert.Equal(5013.984375f,MissionBoundFurnitureRange.NativeDistanceSquared(standing,b));
  Assert.False(MissionBoundFurnitureRange.Valid(MissionBoundFurnitureRange.Centre(b),b));Assert.False(MissionBoundFurnitureRange.Valid(new[]{42000d,1095,-27900},b));
 }
 [Fact]public void Exact20TypeOriginalModelSetAndEveryCompressedProfileValidate()
 {
  ushort[] expected={573,1431,1432,1433,1434,1435,1438,1439,1443,1444,1445,1446,1447,1448,1449,1450,1451,6987,37913,38484};
  string directory=Path.Combine(Root,"data/mission_bound_furniture_poses/1");using var m=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"manifest.json")));
  Assert.Equal(expected,m.RootElement.GetProperty("Goids").EnumerateArray().Select(v=>v.GetUInt16()));foreach(ushort g in new ushort[]{3,326,1401,357,552})Assert.False(MissionBoundFurniturePoses.SupportsGoid(g));
  Assert.Equal("3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103",m.RootElement.GetProperty("ModelContracts").GetProperty("SourceGobSha256").GetString());
  Assert.All(m.RootElement.GetProperty("ModelContracts").GetProperty("Models").EnumerateArray(),v=>Assert.Equal(100,v.GetProperty("OpenMaxDistance").GetInt32()));
  int total=0;foreach(var f in m.RootElement.GetProperty("Files").EnumerateArray())total+=MissionBoundFurniturePoses.LoadAudited(directory,f.GetProperty("Sector").GetUInt16()).Count;Assert.Equal(40725,total);
 }
 [Fact]public void DefaultV1EncodingAndComputerPhoneBindingsAcquireNoNewAuthority()
 {
  var s=new MissionStreetSelection(1,1,"owner",1,0,null,"Objects",null,null);using var d=JsonDocument.Parse(JsonSerializer.Serialize(s,MissionProgress.SaveJson));Assert.False(d.RootElement.TryGetProperty("FurnitureRoutes",out _));Assert.False(d.RootElement.TryGetProperty("BoundRoutes",out _));
  Assert.False(MissionBoundFurnitureRange.Supports(3));Assert.False(MissionBoundFurnitureRange.Supports(1401));
 }
 static StaticWorldObject Row(ushort goid,uint key)=>new(){metrId=1,sectorID=985,mxoStaticId=key,staticId=key&0xfffff,type=BitConverter.GetBytes(goid),pos_x=key,pos_y=1145,pos_z=-27000,quat="0000000000000000000000000000803f"};
 static MissionPhase Phase(params string[] kinds){var p=new MissionPhase{site="Normal"};ushort id=0;foreach(var kind in kinds)p.objects.Add(new(){id=++id,kind=kind});return p;}
 [Fact]public void Bad1451CannotBeRandomlyChosenWhenOtherFurnitureProvidesCapacity()
 {
  var phase=Phase("Furniture","Furniture");var bad=Row(1451,1);var good=Row(1450,2);var cabinet=Row(326,3);var phone=Row(1401,4);var site=new MissionSite{metrId=1};site.containers.AddRange(new[]{bad,good,cabinet,phone});
  Assert.True(MissionBoundFurnitureAdmission.BuildPools(phase,site,r=>!ReferenceEquals(r,bad),out var pool,out _));var rng=new Random(7);var selected=MissionStaging.SelectObjectsFromPools(phase,site,rng,pool).ToArray();
  Assert.DoesNotContain(selected,c=>ReferenceEquals(c.Spot,bad));Assert.Equal(2,selected.Length);Assert.All(selected,c=>Assert.True(ReferenceEquals(c.Spot,good)||ReferenceEquals(c.Spot,cabinet)));
  var original=new Random(7);for(int i=0;i<site.containers.Count;i++)original.Next();Assert.Equal(original.Next(),rng.Next());
 }
 [Fact]public void SourceCapacityFailureIsPerSiteNotAWholeMissionRejection()
 {
  var phase=Phase("Furniture","Furniture");var site=new MissionSite{metrId=1};site.containers.Add(Row(1451,1));site.containers.Add(Row(1450,2));Assert.False(MissionBoundFurnitureAdmission.BuildPools(phase,site,r=>MissionSites.Goid(r)!=1451,out _,out _));
  site.containers.Add(Row(1433,3));Assert.True(MissionBoundFurnitureAdmission.BuildPools(phase,site,r=>MissionSites.Goid(r)!=1451,out _,out _));
 }
 [Fact]public void PhonePriorityAndMixedComputerNativePriorityStayOriginal()
 {
  var phone=Row(1401,4);var good=Row(1450,2);var site=new MissionSite{metrId=1};site.containers.AddRange(new[]{phone,good});var phase=Phase("Phone","Furniture");
  Assert.True(MissionBoundFurnitureAdmission.BuildPools(phase,site,_=>true,out var pool,out _));var chosen=MissionStaging.SelectObjectsFromPools(phase,site,new Random(1),pool).ToArray();Assert.Same(phone,chosen[0].Spot);Assert.Same(good,chosen[1].Spot);
  var computer=Row(3,9);site.realObjects.Add(computer);phase=Phase("Computer","Furniture");Assert.False(MissionBoundFurnitureAdmission.Applies(phase,site));chosen=MissionStaging.SelectObjects(phase,site,new Random(7)).ToArray();Assert.Same(computer,chosen[0].Spot);
 }
 [Fact]public void InactiveLegacyApiIsPreservedButDeniedEnrollmentNeverFallsBack()
 {
  var field=typeof(MissionStreetCoverage).GetField("current",BindingFlags.Static|BindingFlags.NonPublic)!;var before=field.GetValue(null);var phase=Phase("Furniture");var site=new MissionSite{metrId=1};site.containers.Add(Row(1450,2));
  try{field.SetValue(null,null);Assert.True(MissionStaging.TrySourcePools(phase,site,0,out var pools,out _));Assert.Null(pools);
   var state=typeof(MissionStreetCoverage).GetNestedType("State",BindingFlags.NonPublic)!;field.SetValue(null,Activator.CreateInstance(state,BindingFlags.Instance|BindingFlags.NonPublic,null,new object?[]{null,"DeniedFixtureEnrollment"},null));Assert.False(MissionStaging.TrySourcePools(phase,site,0,out _,out string why));Assert.Equal("DeniedFixtureEnrollment",why);}
  finally{field.SetValue(null,before);}
 }
 [Fact]public void CapturedFixtureUsesFreshBuilderWithoutChangingOriginalRawGoalsItems()
 {
  var old=NativeBoundFurnitureFixture.VerifyCapture(NativeBoundFurnitureFixture.Capture);var prepared=NativeBoundFurnitureFixture.Prepare(Root);var route=NativeBoundFurnitureProof.Read(Root,prepared.State);
  Assert.Equal(old.Done,prepared.State.Done);Assert.Equal(old.Identity,prepared.State.Identity);Assert.Equal(JsonSerializer.Serialize(old.Objects,Options),JsonSerializer.Serialize(prepared.State.Objects,Options));
  Assert.Equal(3,route.Selection.GetProperty("Version").GetInt32());Assert.Equal("BoundFurnitureRoutes",route.Selection.GetProperty("FurnitureRoutes").GetProperty("Kind").GetString());
  var steps=NativeBoundFurnitureProof.Steps(Root,route);Assert.Contains(steps,s=>s.Doors.Contains(1032848039u));Assert.Equal(114,(int)route.Region);Assert.Equal((ushort)6958,Assert.Single(route.Doors).Goid);Assert.DoesNotContain(NativeBoundFurnitureFixture.EarlierReceipts,id=>id==301210);
 }
 [Theory][InlineData("version")][InlineData("kind")][InlineData("profile")][InlineData("goid")][InlineData("owner")][InlineData("source")][InlineData("standing")][InlineData("door")]
 public void RecognizedRouteCorruptionRefusesInsteadOfUpgrading(string fault)
 {
  var prepared=NativeBoundFurnitureFixture.Prepare(Root);var node=JsonNode.Parse(prepared.Payload)!;var w=node["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==1)!;var envelope=w["FurnitureRoutes"]!;var row=envelope["Routes"]![0]!;
  switch(fault){case"version":w["Version"]=2;break;case"kind":envelope["Kind"]="Bound326Routes";break;case"profile":row["ProfileManifestSha256"]="bad";break;case"goid":row["Goid"]=326;break;case"owner":w["Identity"]="foreign";break;case"source":row["RawSourceJson"]="{}";break;case"standing":row["Standing"]![1]=2000;break;case"door":row["Doors"]![0]!["Goid"]=6959;break;}
  var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(node.ToJsonString(),Options)!;Assert.Throws<InvalidDataException>(()=>NativeBoundFurnitureProof.Read(Root,saved));
 }
 [Theory][InlineData("unchanged")][InlineData("owner")][InlineData("phase")][InlineData("source")][InlineData("replacement")][InlineData("selection")][InlineData("goid")][InlineData("old-version")]
 public void ValidatedCurrentPresentationIsBoundAndLegacyCannotAcquireIt(string change)
 {
  var prepared=NativeBoundFurnitureFixture.Prepare(Root);var saved=prepared.State;
  var rows=(IEnumerable<StaticWorldObject>)typeof(NativeBoundFurnitureFixture).GetMethod("Rows",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{Root})!;
  var all=rows.ToArray();var sites=MissionSites.Build(all,1);var selected=JsonSerializer.Deserialize<MissionStreetSelection>(JsonNode.Parse(prepared.Payload)!["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==1)!.ToJsonString(),Options)!;
  var site=sites.Single(s=>s.elevatorDoor?.mxoStaticId==selected.Site.DoorKey&&MissionOpenFloorSite.CatalogFingerprint(s)==selected.Site.Fingerprint);
  var xml=new System.Xml.XmlDocument();xml.Load(Path.Combine(Root,"data/missions/merovingian_1_2_10_into_the_hive.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
  var progress=new MissionProgress(mission){persistenceId=saved.Identity,startedAt=saved.Started,phaseIndex=saved.Phase,site=site};progress.sites.Add(site);
  var raw=saved.Objects.Single();var obj=new MissionObjectState{def=mission.phases[1].objects.Single(d=>d.id==raw.Id),spot=raw.Spot,goid=raw.Goid};progress.objects.Add((ushort)raw.Id,obj);
  progress.streetWitnesses.Add(1,selected);var lookup=all.GroupBy(r=>r.mxoStaticId).ToDictionary(g=>g.Key,g=>g.First());
  MissionStreetFurnitureRoutes.Validate(progress,selected,key=>lookup.GetValueOrDefault(key)!);MissionStreetFurnitureRoutes.RequireCurrentSources(progress,selected);
  if(change=="old-version")
  {
   progress.streetWitnesses[1]=new MissionStreetSelection(1,selected.Mission,selected.Identity,selected.Started,selected.Phase,selected.Site,selected.Kind,selected.Context,null);
   MissionStreetFurnitureRoutes.BindValidatedCurrent(progress,obj);Assert.Null(obj.FurniturePresentation);Assert.Equal(raw.Spot.pos_y,MissionObjectPoses.For(obj).Y);return;
  }
  MissionStreetFurnitureRoutes.BindValidatedCurrent(progress,obj);Assert.Equal(1049.98193359375,MissionObjectPoses.For(obj).Y);
  switch(change){case"owner":progress.persistenceId="foreign";break;case"phase":progress.phaseIndex++;break;case"source":obj.spot.pos_x++;break;
   case"replacement":progress.objects[(ushort)raw.Id]=new MissionObjectState();break;case"selection":progress.streetWitnesses.Remove(1);break;case"goid":obj.goid=1451;break;}
  if(change=="unchanged")Assert.Equal(1049.98193359375,MissionObjectPoses.For(obj).Y);
  else Assert.Throws<InvalidDataException>(()=>MissionObjectPoses.For(obj));
 }

}
