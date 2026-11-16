using System.Text.Json;
using hds;
using HeadlessClient;
namespace hds.Tests;
public class MachinesCurrentNpcObservationTests
{
 static string Fixture()
 {
  var dir=new DirectoryInfo(AppContext.BaseDirectory);
  while(dir!=null&&!File.Exists(Path.Combine(dir.FullName,"tests/hds.Tests/Fixtures/Machines273-moving-followers.json")))dir=dir.Parent;
  return File.ReadAllText(Path.Combine(dir!.FullName,"tests/hds.Tests/Fixtures/Machines273-moving-followers.json"));
 }
 static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
 static (MissionProgress.SavedState Before,MissionProgress.SavedState Current,OwnViewLifeLog Log) Capture()
 {
  using var doc=JsonDocument.Parse(Fixture());var d=doc.RootElement;
  var log=new OwnViewLifeLog();int i=0;
  foreach(var section in d.GetProperty("Sections").EnumerateArray()){var b=Convert.FromHexString(section.GetString()!);log.Observe(false,b,i++,WireFormat.FindCreations(b));}
  return (JsonSerializer.Deserialize<MissionProgress.SavedState>(d.GetProperty("Before"),Options)!,JsonSerializer.Deserialize<MissionProgress.SavedState>(d.GetProperty("Current"),Options)!,log);
 }
 [Theory][InlineData(1,34,33)][InlineData(2,36,34)]
 public void ActualMovingFollowerRequiresFreshSavedAndCurrentLivePair(ushort npc,ushort view,byte spawn)
 {
  var (before,current,log)=Capture();var life=log.Snapshot();
  Assert.Null(ZionAfter1212FollowerReadiness.CurrentNpc(life,"MachinesAfter201206",before.Mobs.Single(m=>m.Npc==npc),before.Instance));
  var actual=MachinesCurrentNpcObservation.Read(before,current,npc,life,log.Snapshot(),"MachinesAfter201206");
  Assert.NotNull(actual);Assert.Equal(view,actual.ViewId);Assert.Equal(spawn,actual.SpawnId);
  Assert.Null(MachinesCurrentNpcObservation.Read(before,before,npc,life,log.Snapshot(),"MachinesAfter201206"));
 }
 [Fact] public void LookupLifeAnchorsAfterProvedApproachNotAnEarlierTransportLife()
 {
  var (before,current,old)=Capture();var beforeApproach=old.Snapshot();old.EndLife();
  var arrived=Capture();var lookupLife=arrived.Log.Snapshot();
  Assert.Throws<InvalidDataException>(()=>MachinesCurrentNpcObservation.Read(before,current,1,beforeApproach,lookupLife,"MachinesAfter201206"));
  Assert.NotNull(MachinesCurrentNpcObservation.Read(before,current,1,lookupLife,arrived.Log.Snapshot(),"MachinesAfter201206"));
 }
 [Theory][InlineData("identity")][InlineData("started")][InlineData("mission")][InlineData("phase")][InlineData("instance")][InlineData("uuid")][InlineData("side")][InlineData("npcphase")][InlineData("position")]
 public void FreshPairCannotRetargetAcceptedOrNpcAuthority(string change)
 {
  var (before,current,log)=Capture();var life=log.Snapshot();var npc=current.Mobs.Single(m=>m.Npc==1);
  switch(change){case"identity":current.Identity=new string('f',32);break;case"started":current.Started++;break;case"mission":current.Mission++;break;case"phase":current.Phase++;break;case"instance":current.Instance++;break;case"uuid":npc.Identity=new string('f',32);break;case"side":npc.Friendly=false;break;case"npcphase":npc.Phase++;break;case"position":npc.X+=100;break;}
  if(change=="position")Assert.Null(MachinesCurrentNpcObservation.Read(before,current,1,life,log.Snapshot(),"MachinesAfter201206"));
  else Assert.Throws<InvalidDataException>(()=>MachinesCurrentNpcObservation.Read(before,current,1,life,log.Snapshot(),"MachinesAfter201206"));
 }
 [Theory][InlineData("reset")][InlineData("foreign")][InlineData("ownspawn")][InlineData("delete")][InlineData("reuse")]
 public void PairCannotBorrowPreviousLifeOrDeletedReusedView(string change)
 {
  var (before,current,log)=Capture();var life=log.Snapshot();var stream=life;
  if(change=="reset")log.Observe(true,null,4,Array.Empty<ViewCreation>());
  if(change=="foreign"){var other=Capture();stream=other.Log.Snapshot();}
  if(change=="ownspawn")
  {
   using var doc=JsonDocument.Parse(Fixture());var b=Convert.FromHexString(doc.RootElement.GetProperty("Sections")[0].GetString()!);b[5]++;log.Observe(false,b,4,WireFormat.FindCreations(b));stream=log.Snapshot();
  }
  if(change is "delete" or "reuse")
  {
   var b=Convert.FromHexString("01000101002200");log.Observe(false,b,4,WireFormat.FindCreations(b));
   if(change=="reuse"){using var doc=JsonDocument.Parse(Fixture());var b2=Convert.FromHexString(doc.RootElement.GetProperty("Sections")[0].GetString()!);b2[b2.Length-3]=34;b2[b2.Length-2]=0;b2[5]++;log.Observe(false,b2,5,WireFormat.FindCreations(b2));}
   Assert.Null(MachinesCurrentNpcObservation.Read(before,current,1,life,log.Snapshot(),"MachinesAfter201206"));return;
  }
  Assert.Throws<InvalidDataException>(()=>MachinesCurrentNpcObservation.Read(before,current,1,life,stream,"MachinesAfter201206"));
 }
}
