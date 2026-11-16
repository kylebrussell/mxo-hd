using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public sealed class MachinesAfter201206FurnitureIntegrationTests
{
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static string Root=>NativeBoundFurnitureFixture.FindRepository();
 static JsonObject Payload()=>JsonNode.Parse(NativeBoundFurnitureFixture.Prepare(Root).Payload)!.AsObject();
 static JsonNode Witness(JsonObject p)=>p["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==(int)p["Phase"]!)!;
 static MissionProgress.SavedState State(JsonObject p)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(p.ToJsonString(),Fields)!;
 [Fact]public void BothSharedStreetReadersDispatchExactV3WithoutChangingSavedStateVersion()
 {
  var state=State(Payload());var a=MervFollowupStreetEntry.ForSite(state);var b=ZionAfter1212StreetEntry.ForSite(state);Assert.NotNull(a);Assert.NotNull(b);Assert.Equal(a.Raw.GetRawText(),b.Raw.GetRawText());Assert.Equal(2,state.Version);
  var plan=MissionBoundFurnitureRouteProof.Read(Root,state,1)!;Assert.Equal(plan.Floor.Standing,a.Floor.Standing);Assert.Equal(plan.Lobby.Standing,b.Lobby.Standing);
  MissionBoundFurnitureRouteProof.RequireArrival(plan,plan.Floor.Standing!,985,99,1000);
 }
 [Theory][InlineData("kind")][InlineData("profile")][InlineData("missingrow")][InlineData("wrongtype")][InlineData("oldmetadata")]
 public void SharedStreetReadersCannotTreatMalformedV3AsOld326OrRawFallback(string fault)
 {
  var p=Payload();var w=Witness(p);var e=w["FurnitureRoutes"]!;switch(fault){case"kind":e["Kind"]="Bound326Routes";break;case"profile":e["FurnitureManifest"]="bad";break;case"missingrow":e["Routes"]!.AsArray().Clear();break;case"wrongtype":e["Routes"]![0]!["Goid"]=326;break;case"oldmetadata":w["Version"]=1;break;}
  var state=State(p);Assert.Throws<InvalidDataException>(()=>MervFollowupStreetEntry.ForSite(state));Assert.Throws<InvalidDataException>(()=>ZionAfter1212StreetEntry.ForSite(state));
 }
 [Theory][InlineData("x")][InlineData("floor")][InlineData("sector")][InlineData("region")]
 public void V3CannotBorrowAnotherActualRideRoot(string fault)
 {
  var plan=MissionBoundFurnitureRouteProof.Read(Root,State(Payload()),1)!;var root=plan.Floor.Standing!.ToArray();ushort sector=985,region=99;double floor=1000;
  switch(fault){case"x":root[0]++;break;case"floor":floor++;break;case"sector":sector++;break;case"region":region++;break;}
  Assert.Throws<InvalidDataException>(()=>MissionBoundFurnitureRouteProof.RequireArrival(plan,root,sector,region,floor));
 }
 static byte[] Encode(GameObject obj,ushort view,byte spawn)=>new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
 static OwnViewLifeLog BodyLife(MissionProgress.SavedState state,MissionBoundFurnitureRouteProof.Plan plan,bool rawPose=false)
 {
  var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();var body=new AttributeClass46("Dresser",1450);body.SetRelatedStaticObjId(state.Objects.Single().Spot.mxoStaticId);body.DisableAllAttributes();
  body.Position.enable();body.Position.setValue((rawPose?new[]{state.Objects.Single().Spot.pos_x,state.Objects.Single().Spot.pos_y,state.Objects.Single().Spot.pos_z}:plan.Centre).SelectMany(BitConverter.GetBytes).ToArray());
  body.Orientation.enable();body.Orientation.setValue(Convert.FromHexString(plan.Quaternion));body.MissionKey.enable();body.MissionKey.setValue(new[]{(byte)0xff,(byte)state.Instance,(byte)(state.Instance>>8),(byte)2});
  var log=new OwnViewLifeLog();var sections=new[]{Encode(own,2,1),Encode(body,9,5)};for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log;
 }
 [Fact]public void CurrentBodyUsesIndependentV3NativePoseRatherThanAnUnboundServerFacade()
 {
  var state=State(Payload());var plan=MissionBoundFurnitureRouteProof.Read(Root,state,1)!;var scope=ZionCurrentBoundBody.Capture(state,1);var life=BodyLife(state,plan);
  var body=ZionCurrentBoundBody.Require(life.Snapshot(),scope,state,plan);Assert.Equal((ushort)1450,body.View.Goid);
  Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(life.Snapshot(),scope,state));Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(BodyLife(state,plan,true).Snapshot(),scope,state,plan));
  var snapshot=life.Snapshot();life.EndLife();Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(snapshot,scope,state,plan));
 }
 [Fact]public void ActualRegisteredChainKeepsItsSeedBudgetAndMandatoryPin()
 {
  var scenario=Assert.Single(Scenarios.All,s=>s.Name=="machines-critical-after-201206");Assert.Equal("201207",scenario.Environment!["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal(TimeSpan.FromMinutes(20),scenario.Timeout);
  Assert.Equal("MXO_MACHINES_AFTER201206_SOURCE_SHA256",MachinesAfter201206Fixture.SourcePin);Assert.Equal(26,MachinesAfter201206Fixture.Prerequisites.Length);
 }
}
