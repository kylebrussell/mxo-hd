using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
using Xunit;
namespace hds.Tests;
public sealed class ZionFurnitureDestinationDoorTests
{
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static string Root=>NativeBoundFurnitureFixture.FindRepository();
 static JsonObject Payload()=>JsonNode.Parse(File.ReadAllText(Path.Combine(Root,"tests/hds.Tests/Fixtures/ZionFurniture-101301-7ced482880.json")))!.AsObject();
 static MissionProgress.SavedState State(JsonObject p)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(p.ToJsonString(),Fields)!;
 static JsonNode Row(JsonObject p)=>p["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==1)!["FurnitureRoutes"]!["Routes"]![0]!;
 static JsonObject Evidence(){using var input=File.OpenRead(Path.Combine(Root,"data/mission_interior_evidence_v2/1/1334.json.gz"));using var zip=new GZipStream(input,CompressionMode.Decompress);return JsonNode.Parse(zip)!.AsObject();}
 static JsonNode Group(JsonObject evidence)=>evidence["Groups"]!.AsArray().Single(g=>(uint)g!["Key"]! ==1398803739u)!;
 static void Own(JsonObject e){using var g=JsonDocument.Parse(Group(e).ToJsonString());using var regions=JsonDocument.Parse(e["Regions"]!.ToJsonString());MissionBound326RouteProof.RequireOwnGoidDoorEvidence(g.RootElement,regions.RootElement,346,new[]{46800d,1745,21500},"00000000f204353f00000000f304353f");}
 [Fact] public void Retained6987RouteUsesExact346OnDestinationSideWithoutBorrowing348()
 {
  var p=Payload();string before=p.ToJsonString();var plan=MissionBoundFurnitureRouteProof.Read(Root,State(p),1)!;Assert.Equal((ushort)6987,plan.Goid);Assert.Equal((ushort)71,plan.Region);
  var steps=MissionBoundFurnitureRouteProof.Steps(Root,plan);Assert.Contains(steps,s=>s.Doors.Contains(1398803739));Assert.Equal((ushort)346,Assert.Single(plan.Doors).Goid);Assert.Equal(before,p.ToJsonString());
  var e=Evidence();var own=Group(e)["Occurrences"]!.AsArray();Assert.Equal(71,(int)own.Single(o=>(int)o!["Goid"]! ==346)!["Region"]!);Assert.Equal(116,(int)own.Single(o=>(int)o!["Goid"]! ==348)!["Region"]!);Own(e);
 }
 [Theory][InlineData("companionOnly")][InlineData("wrongOwnGoid")][InlineData("position")][InlineData("quaternion")][InlineData("globalConflict")][InlineData("unavailable")]
 public void ExactGlobalOwnDoorContractRefuses(string fault)
 {
  var e=Evidence();var rows=Group(e)["Occurrences"]!.AsArray();var own=rows.Single(o=>(int)o!["Goid"]! ==346)!;
  switch(fault){case"companionOnly":rows.Remove(own);break;case"wrongOwnGoid":own["Goid"]=343;break;case"position":own["ModelPosition"]![0]=46801;break;case"quaternion":own["Quaternion"]="0000000000000000000000000000803f";break;case"globalConflict":var conflict=own.DeepClone();conflict["ModelPosition"]![0]=46801;rows.Add(conflict);break;case"unavailable":e["Regions"]!.AsArray().Single(r=>(int)r!["Id"]! ==71)!["Available"]=false;break;}
  Assert.Throws<InvalidDataException>(()=>Own(e));
 }
 [Theory][InlineData("owner")][InlineData("source")][InlineData("portal")][InlineData("doorType")][InlineData("doorPose")][InlineData("doorQuaternion")][InlineData("entryRegion")]
 public void RetainedSourceOwnerPortalAndEntryGuardsStillRefuse(string fault)
 {
  var p=Payload();var row=Row(p);var selection=p["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==1)!;
  switch(fault){case"owner":selection["Identity"]="foreign";break;case"source":row["RawSourceJson"]="{}";break;case"portal":row["Nodes"]![0]!["Region"]=71;break;case"doorType":row["Doors"]![0]!["Goid"]=348;break;case"doorPose":row["Doors"]![0]!["Position"]![0]=46801;break;case"doorQuaternion":row["Doors"]![0]!["Quaternion"]="0000000000000000000000000000803f";break;case"entryRegion":selection["Context"]!["Floor"]!["Node"]!["Region"]=71;break;}
  Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.Read(Root,State(p),1));
 }
 [Fact] public void CurrentOwnerCannotChangeAfterProof(){var p=Payload();var plan=MissionBoundFurnitureRouteProof.Read(Root,State(p),1)!;p["Started"]=1;Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.RequireCurrent(plan,State(p)));}
}
