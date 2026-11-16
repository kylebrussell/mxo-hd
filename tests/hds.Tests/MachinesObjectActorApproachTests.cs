using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MachinesObjectActorApproachTests
{
 static string Root=>NativeBoundFurnitureFixture.FindRepository();
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static JsonObject Fixture()=>JsonNode.Parse(File.ReadAllText(Path.Combine(Root,"tests/hds.Tests/Fixtures/Machines273-objects-unseen-actor.json")))!.AsObject();
 static MissionProgress.SavedState State(JsonObject data)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(data["Payload"]!.ToJsonString(),Fields)!;
 static OwnViewLifeLog Life(JsonObject data){var log=new OwnViewLifeLog();var bytes=Convert.FromHexString((string)data["Own"]!);log.Observe(false,bytes,0,WireFormat.FindCreations(bytes));return log;}
 [Fact] public void ActualStakeoutObjectsActorUsesExistingStreetFloorAndReciprocalOriginalLocalPath()
 {
  var f=Fixture();var s=State(f);var life=Life(f);string before=JsonSerializer.Serialize(s,Fields);
  var p=MachinesObjectActorApproach.Read(Root,s,1,life.Snapshot());
  Assert.Equal(new[]{192,193,66,53,56,52,65,54,217,227},p.Steps.Take(p.Steps.Length-1).Select(x=>x.Polygon));
  Assert.All(p.Steps,step=>{Assert.Equal(47,step.Region);Assert.Equal(1295,step.Standing[1]);Assert.Empty(step.Doors);});
  Assert.Equal(new[]{29740d,1295,13660},p.Steps[^1].Standing);Assert.Equal(before,JsonSerializer.Serialize(s,Fields));
  MachinesObjectActorApproach.RequireCurrent(p,s,life.Snapshot());Assert.Equal("Objects",ZionAfter1212StreetEntry.ForSite(s)!.Raw.GetProperty("Kind").GetString());
 }
 [Theory][InlineData("owner")][InlineData("started")][InlineData("phase")][InlineData("instance")][InlineData("site")][InlineData("kind")][InlineData("digest")][InlineData("actor")][InlineData("side")][InlineData("x")][InlineData("floor")]
 public void CapturedPlanCannotRetargetChangedAuthority(string change)
 {
  var f=Fixture();var s=State(f);var life=Life(f);var p=MachinesObjectActorApproach.Read(Root,s,1,life.Snapshot());var changed=State(f);
  switch(change){case"owner":changed.Identity=new string('f',32);break;case"started":changed.Started++;break;case"phase":changed.Phase++;break;case"instance":changed.Instance++;break;case"site":changed.SiteDoors[0].pos_x++;break;case"actor":changed.Mobs[0].Identity=new string('f',32);break;case"side":changed.Mobs[0].Friendly=true;break;case"x":changed.Mobs[0].X++;break;case"floor":changed.Mobs[0].Y++;break;
   default:var envelope=JsonNode.Parse(changed.StreetAccess.GetRawText())!;var row=envelope["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==changed.Phase)!;if(change=="kind")row["Kind"]="Npcs";else row["Context"]!["Floor"]!["EvidenceDigest"]=new string('0',64);changed.StreetAccess=JsonSerializer.SerializeToElement(envelope);break;}
  Assert.Throws<InvalidDataException>(()=>MachinesObjectActorApproach.RequireCurrent(p,changed,life.Snapshot()));
 }
 [Theory][InlineData("floor")][InlineData("otherregion")][InlineData("dead")][InlineData("missing")]
 public void FreshRouteRefusesUnprovedTargetRatherThanProjecting(string change)
 {
  var f=Fixture();var s=State(f);var life=Life(f);var actor=s.Mobs.Single(m=>m.Npc==1);
  if(change=="floor")actor.Y+=400;if(change=="otherregion"){actor.X=17000;actor.Z=2200;}
  if(change=="dead")actor.Dead=true;if(change=="missing")s.Mobs=Array.Empty<MissionProgress.SavedMob>();
  Assert.Throws<InvalidDataException>(()=>MachinesObjectActorApproach.Read(Root,s,1,life.Snapshot()));
 }
 [Fact] public void RouteCannotBorrowAnotherOrRetiredOwnLife()
 {
  var f=Fixture();var s=State(f);var life=Life(f);var p=MachinesObjectActorApproach.Read(Root,s,1,life.Snapshot());
  Assert.Throws<InvalidDataException>(()=>MachinesObjectActorApproach.RequireCurrent(p,s,Life(f).Snapshot()));life.EndLife();
  Assert.Throws<InvalidDataException>(()=>MachinesObjectActorApproach.RequireCurrent(p,s,life.Snapshot()));
 }
}
