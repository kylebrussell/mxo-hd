using System.Xml;
using System.Xml.Linq;
using HeadlessClient;
using hds;
using NativeQa;
namespace hds.Tests;
public class MachinesAfter201206Tests
{
 static string Repo{get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
 static Mission[] Missions()=>Directory.GetFiles(Path.Combine(Repo,"data/missions"),"machines_1_2_*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).Where(m=>MachinesAfter201206Fixture.Missions.Contains(m.id)).OrderBy(m=>m.order).ToArray();
 [Fact]public void ExactNextFiveKeepAuthoredOrderCountsAndUnchangedCurrentPolicyRewards()
 {
  var m=Missions();Assert.Equal(new[]{201207,201208,201209,201210,201211},m.Select(x=>x.id));Assert.Equal(new[]{2,3,2,2,1},m.Select(x=>x.phases.Count));Assert.Equal(26,m.Sum(x=>x.phases.Sum(p=>p.missionObjectives.Count)));
  foreach(var x in m){var q=MachinesAfter201206Fixture.ExpectedReward(x.id);Assert.Equal((uint)x.exp,q.Xp);Assert.Equal((uint)x.info,q.Info);Assert.Equal(0,x.rewardLevel);var paid=MissionRewards.Calculate(x,50);Assert.Equal(q.Xp,paid.Experience);Assert.Equal(q.Info,paid.Info);}
  Assert.Equal(new[]{201207,201208,201209,201210,201211},MachinesAfter201206Fixture.Missions);Assert.Equal(201212,MachinesAfter201206Fixture.NextMission);
 }
 [Fact]public void TwentySixOnlyEarlierReceiptsAreDisjointFromSelectedAndNext()
 {
  var p=MachinesAfter201206Fixture.Prerequisites;Assert.Equal(26,p.Length);Assert.Equal(p.Length,p.Distinct().Count());Assert.Empty(p.Intersect(MachinesAfter201206Fixture.Missions.Append(201212)));Assert.Contains(201206,p);
  Assert.Equal(201207,MachinesAfter201206Fixture.Seed);Assert.DoesNotContain("char_active_missions",MachinesAfter201206Fixture.SeedPrerequisites(110));
 }
 [Fact]public void ActualRegisteredScenarioAndModeUseComputedEnvironmentAndNoSelectedProgressGrants()
 {
  var s=Assert.Single(Scenarios.All,s=>s.Name=="machines-critical-after-201206");Assert.NotNull(s.Environment);Assert.Equal("201207",s.Environment!["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal("1",s.Environment["MXO_PACKET_LOG"]);Assert.Equal(TimeSpan.FromMinutes(20),s.Timeout);
  var mode=NativeQaModes.Find(s.Name);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);Assert.Equal(new ushort[]{2,17,1006,77},mode.Plan.Abilities);Assert.Equal("201207",mode.Plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
  Assert.Equal(MachinesAfter201206Fixture.SeedPrerequisites(110),mode.Plan.AfterHelpers[0]);Assert.DoesNotContain("char_active_missions",string.Join("\n",mode.Plan.AfterHelpers));Assert.DoesNotContain("inventory",string.Join("\n",mode.Plan.AfterHelpers));
  Assert.False(mode.Plan.ExtraQaMetadata["StakeoutTimedWavesImplemented"].GetBoolean());Assert.False(mode.Plan.ExtraQaMetadata["CorpseTracerGiveImplemented"].GetBoolean());
 }
 [Fact]public void ProtectedWitnessIdsAreDerivedFromActualCurrentPhaseNotOldMissionNpcOneAssumption()
 {
  var x=XElement.Load(Path.Combine(Repo,"data/missions/machines_1_2_10_witness_protection.xml"));Assert.Equal(new[]{1},MachinesAfter201206Fixture.ProtectedIds(x,0));Assert.Equal(new[]{2},MachinesAfter201206Fixture.ProtectedIds(x,1));
 }
 [Fact]public void ExistingStakeoutRemainsExplicitlyUntimedAndRecruiterEscortKeepsBothIds()
 {
  var m=Missions();var stake=m.Single(m=>m.id==201211);Assert.Empty(stake.phases[0].waves);Assert.Equal(2,stake.phases[0].missionObjectives.Count);
  var red=m.Single(m=>m.id==201208);Assert.All(red.phases.SelectMany(p=>p.missionObjectives).Where(g=>g.type==MissionGoalType.Escort),g=>Assert.Equal(new ushort[]{2,1},g.Npcs));
 }
 [Theory][InlineData(null)][InlineData("")][InlineData("abcd")]
 public void ExactSourcePinIsMandatory(string? supplied)=>Assert.Throws<InvalidDataException>(()=>MachinesAfter201206Fixture.RequirePin(supplied,new string('a',64)));
}
