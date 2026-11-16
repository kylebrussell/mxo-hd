using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public sealed class MissionBoundFurnitureRouteProofTests
{
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static string Root=>NativeBoundFurnitureFixture.FindRepository();
 static JsonObject Payload()=>JsonNode.Parse(NativeBoundFurnitureFixture.Prepare(Root).Payload)!.AsObject();
 static JsonNode Current(JsonObject payload)=>payload["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==(int)payload["Phase"]!)!;
 static MissionProgress.SavedState State(JsonObject payload)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(payload.ToJsonString(),Fields)!;
 [Fact] public void Actual1450ReadsIndependentNativeProfileAndFixedOwnDoorPath()
 {
  var prepared=NativeBoundFurnitureFixture.Prepare(Root);string before=JsonSerializer.Serialize(prepared.State,Fields);
  var proof=MissionBoundFurnitureRouteProof.Read(Root,prepared.State,1);Assert.NotNull(proof);Assert.Equal((ushort)1450,proof.Goid);Assert.Equal(new[]{41445d,1049.98193359375,-27900},proof.Centre);Assert.Equal((ushort)114,proof.Region);
  Assert.Equal("00000000f304353f00000000f304353f",proof.Quaternion.ToLowerInvariant());var steps=MissionBoundFurnitureRouteProof.Steps(Root,proof);Assert.Contains(steps,s=>s.Doors.Contains(1032848039u));Assert.Equal((ushort)6958,Assert.Single(proof.Doors).Goid);
  MissionBoundFurnitureRouteProof.RequireCurrent(proof,prepared.State);Assert.Equal(before,JsonSerializer.Serialize(prepared.State,Fields));Assert.Equal(proof.Standing,steps[^1].Standing);
 }
 [Theory][InlineData("owner")][InlineData("mission")][InlineData("started")][InlineData("phase")][InlineData("instance")][InlineData("source")][InlineData("profile")][InlineData("goid")][InlineData("region")][InlineData("standing")][InlineData("node")][InlineData("cycle")][InlineData("door")][InlineData("doorpose")][InlineData("digest")][InlineData("extra")]
 public void RecognizedMalformedOwnerTypeProfileOrSavedPathRefuses(string fault)
 {
  var payload=Payload();var w=Current(payload);var envelope=w["FurnitureRoutes"]!;var row=envelope["Routes"]![0]!;
  switch(fault)
  {
   case"owner":w["Identity"]="foreign";break;case"mission":w["Mission"]=201209;break;case"started":w["Started"]=1;break;case"phase":w["Phase"]=2;break;case"instance":payload["Instance"]=0;break;
   case"source":row["RawSourceJson"]="{}";break;case"profile":row["ProfileManifestSha256"]=MissionBoundFurnitureRouteProof.CabinetManifest;break;case"goid":row["Goid"]=326;break;
   case"region":row["Region"]=99;break;case"standing":row["Standing"]![1]=1096;break;case"node":row["Nodes"]![0]!["Polygon"]=65535;break;
   case"cycle":row["Nodes"]!.AsArray().Insert(1,row["Nodes"]![0]!.DeepClone());break;case"door":row["Doors"]![0]!["Goid"]=6959;break;case"doorpose":row["Doors"]![0]!["Position"]![0]=1;break;
   case"digest":envelope["EvidenceDigest"]=new string('0',64);break;case"extra":row["Unrecognized"]=true;break;
  }
  string before=payload.ToJsonString();Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.Read(Root,State(payload),1));Assert.Equal(before,payload.ToJsonString());
 }
 [Theory][InlineData(1)][InlineData(2)]
 public void OldSelectionDoesNotAcquireFurnitureAuthority(int version)
 {
  var payload=Payload();var w=Current(payload);w["Version"]=version;w.AsObject().Remove("FurnitureRoutes");
  Assert.Null(MissionBoundFurnitureRouteProof.Read(Root,State(payload),1));
 }
 [Theory][InlineData(1)][InlineData(2)]
 public void OldSelectionWithNewMetadataIsRecognizedMalformedNotLegacy(int version)
 {
  var payload=Payload();Current(payload)["Version"]=version;Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.Read(Root,State(payload),1));
 }
 [Fact] public void StreetV3DoesNotInventSavedStateVersion3()
 {
  var payload=Payload();payload["Version"]=3;Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.Read(Root,State(payload),1));
 }
 [Fact] public void CurrentPlanCannotBorrowForeignAcceptancePhaseSourceOrInstance()
 {
  var payload=Payload();var proof=MissionBoundFurnitureRouteProof.Read(Root,State(payload),1)!;
  foreach(string change in new[]{"acceptance","phase","source","instance"})
  {
   var changed=JsonNode.Parse(payload.ToJsonString())!.AsObject();if(change=="acceptance")changed["Identity"]="11111111111111111111111111111111";
   else if(change=="phase")changed["Phase"]=2;else if(change=="instance")changed["Instance"]=50;else changed["Objects"]![0]!["Spot"]!["pos_x"]=1;
   Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.RequireCurrent(proof,State(changed)));
  }
 }
 [Fact] public void NormalContainerStateChangeDoesNotRewriteImmutableSourceAuthority()
 {
  var payload=Payload();var state=State(payload);var proof=MissionBoundFurnitureRouteProof.Read(Root,state,1)!;
  payload["Objects"]![0]!["State"]=0x834;MissionBoundFurnitureRouteProof.RequireCurrent(proof,State(payload));
 }
 [Fact] public void Mixed326RowCannotBorrowNewFurnitureRawSourceOrManifest()
 {
  var payload=Payload();var envelope=Current(payload)["FurnitureRoutes"]!;var obj=payload["Objects"]![0]!.DeepClone();obj["Id"]=2;obj["Goid"]=326;payload["Objects"]!.AsArray().Add(obj);
  var choice=envelope["Choices"]![0]!.DeepClone();choice["ObjectId"]=2;choice["Goid"]=326;envelope["Choices"]!.AsArray().Add(choice);
  var row=envelope["Routes"]![0]!.DeepClone();row["ObjectId"]=2;row["Goid"]=326;row["ProfileManifestSha256"]=MissionBoundFurnitureRouteProof.CabinetManifest;envelope["Routes"]!.AsArray().Add(row);
  Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.Read(Root,State(payload),2));
 }
 [Fact] public void PerTypeRangeUsesCabinetAndDresserModelExtentsSeparately()
 {
  var p=new[]{41545d,1095,-27900};var c=new[]{41445d,1049.98193359375,-27900};const string q="00000000f304353f00000000f304353f";
  float dresser=MissionBoundFurnitureRouteProof.NativeDistance(p,c,q,new[]{84f,49.981971740722656f,29.19049835205078f});
  float cabinet=MissionBoundFurnitureRouteProof.NativeDistance(p,c,q,new[]{25f,80f,30f});Assert.Equal(5013.984375f,dresser);Assert.NotEqual(dresser,cabinet);
 }
 [Fact] public void ActualOriginalTenUnitLocalEdgeIsNotAGeneratedOpenFloorClearanceGate()
 {
  var prepared=NativeBoundFurnitureFixture.Prepare(Root);var plan=MissionBoundFurnitureRouteProof.Read(Root,prepared.State,1)!;
  var nodes=plan.Route.GetProperty("Nodes").EnumerateArray().ToArray();
  Assert.Contains(Enumerable.Range(1,nodes.Length-1),i=>nodes[i-1].GetProperty("Region").GetInt32()==114&&nodes[i-1].GetProperty("Polygon").GetInt32()==157&&nodes[i].GetProperty("Region").GetInt32()==114&&nodes[i].GetProperty("Polygon").GetInt32()==71);
  using var evidence=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(Root,985,plan.Selection.GetProperty("FurnitureRoutes").GetProperty("EvidenceDigest").GetString()!));
  var polygon=evidence.RootElement.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==114).GetProperty("Polygons")[157];var edges=polygon.GetProperty("Edges").EnumerateArray().ToArray();var edge=edges[3];
  Assert.Equal("Local",edge.GetProperty("Reference").GetString());Assert.Equal(71,edge.GetProperty("Neighbor").GetInt32());
  Assert.Equal(new[]{42610d,1000,-27800},edge.GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()));
  Assert.Equal(new[]{42600d,1000,-27800},edges[0].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()));
  Assert.Equal(10d,Math.Abs(edge.GetProperty("Vertex")[0].GetDouble()-edges[0].GetProperty("Vertex")[0].GetDouble()));
  Assert.Equal(30d,NavMesh.MinOpenEdge); // Existing generated/open-floor policy is unchanged, not applied to original local links.
  Assert.Equal(plan.Standing,MissionBoundFurnitureRouteProof.Steps(Root,plan)[^1].Standing);
 }

}
