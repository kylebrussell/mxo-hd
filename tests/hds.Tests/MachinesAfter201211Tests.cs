using System.Xml.Linq;
using System.Text.RegularExpressions;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class MachinesAfter201211Tests
{
 static string Root
 {get{var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!Directory.Exists(Path.Combine(d.FullName,"data","missions")))d=d.Parent;return d!.FullName;}}
 static XElement Mission(int id)=>Directory.GetFiles(Path.Combine(Root,"data","missions"),"machines_1_2_*.xml").Select(XElement.Load).Single(m=>(int)m.Attribute("id")! ==id);
 [Fact] public void SeedHasExactly31EarlierStoryReceiptsPlusOnlyStandard986()
 {
  var story=MachinesAfter201211Fixture.EarlierStoryReceipts;Assert.Equal(31,story.Length);Assert.Equal(31,story.Distinct().Count());
  Assert.Equal(32,MachinesAfter201211Fixture.Prerequisites.Length);Assert.Equal(story.Append(986),MachinesAfter201211Fixture.Prerequisites);
  Assert.Empty(MachinesAfter201211Fixture.Prerequisites.Intersect(MachinesAfter201211Fixture.Missions.Append(201217)));
  Assert.Equal(32,Regex.Matches(MachinesAfter201211Fixture.SeedPrerequisites(110),@"\(110,\d+\)").Count);
  Assert.DoesNotContain("char_active_missions",MachinesAfter201211Fixture.SeedPrerequisites(110));Assert.DoesNotContain("inventory",MachinesAfter201211Fixture.SeedPrerequisites(110));
 }
 [Theory][InlineData(201212,4,14,88000,59500)][InlineData(201213,2,6,88000,59500)][InlineData(201214,2,7,93500,63000)][InlineData(201215,2,5,61625,63000)][InlineData(201216,2,4,93500,63000)]
 public void AuthoredMissionCountsAndIndependentCurrentPolicyLiteralsRemainUnchanged(int id,int phases,int goals,uint xp,uint information)
 {
  var xml=Mission(id);Assert.Equal(phases,xml.Elements("phase").Count());Assert.Equal(goals,xml.Elements("phase").Elements("objective").Count());
  Assert.Equal((xp,information),MachinesAfter201211Fixture.ExpectedReward(id));Assert.Equal(xp,(uint)xml.Attribute("exp")!);Assert.Equal(information,(uint)xml.Attribute("info")!);Assert.Null(xml.Attribute("rewardLevel"));
 }
 [Fact] public void NewCaseRetainsExactSeedBudgetAndMandatoryPin()
 {
  var x=Assert.Single(Scenarios.All,s=>s.Name=="machines-critical-after-201211");Assert.Equal(TimeSpan.FromMinutes(20),x.Timeout);Assert.Equal("201212",x.Environment!["MXO_MISSION_SITE_QA_SEED"]);
  Assert.Equal("1",x.Environment["MXO_PACKET_LOG"]);Assert.Equal("test",x.Environment["MXO_MOBS"]);Assert.Equal(60,MachinesAfter201211Fixture.PhaseTransitionSeconds);
  Assert.Throws<InvalidDataException>(()=>MachinesAfter201211Fixture.RequirePin(null,new string('a',64)));Assert.Throws<InvalidDataException>(()=>MachinesAfter201211Fixture.RequirePin(new string('a',64),new string('b',64)));MachinesAfter201211Fixture.RequirePin(new string('a',64),new string('A',64));
 }
 [Fact] public void ActualModeUsesExactStoryAndStandardSeedWithoutActiveOrItemGrants()
 {
  var mode=NativeQaModes.Find("machines-critical-after-201211");var plan=mode.Plan;Assert.Contains(mode.Name,NativeQaModes.Names);
  Assert.Equal(new ushort[]{2,17,1006,77},plan.Abilities);Assert.Equal("201212",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
  Assert.Equal(31,plan.ExtraQaMetadata["EarlierStoryReceiptsOnly"].GetInt32());Assert.Equal(986,plan.ExtraQaMetadata["StandardTrainingReceipt"].GetInt32());Assert.Equal(32,plan.ExtraQaMetadata["SeededReceiptCount"].GetInt32());
  Assert.Contains(MachinesAfter201211Fixture.SeedPrerequisites(110),plan.AfterHelpers);Assert.DoesNotContain(plan.BeforeLoadout.Concat(plan.AfterLoadout).Concat(plan.AfterHelpers),s=>s.Contains("char_active_missions")||s.Contains("INSERT INTO inventory"));
 }
 [Fact] public void RepeatedUploadsCarryAndMultiNpcGoalsComeFromActualXml()
 {
  var weapons=Mission(201212);Assert.Equal(new[]{6783,6792,6835},weapons.Elements("phase").Elements("objective").Where(x=>(string?)x.Attribute("type")=="Upload").Select(x=>(int)x.Attribute("item")!));
  var statue=weapons.Elements("phase").ElementAt(1).Elements("objective").Single(x=>(int)x.Attribute("goal")! ==5);Assert.Equal("Search",(string)statue.Attribute("type")!);Assert.Equal(6815,(int)statue.Attribute("item")!);
  Assert.True(MachinesAfter201211Fixture.CarryCheckpoint(201212,1,5));Assert.False(MachinesAfter201211Fixture.CarryCheckpoint(201209,1,1));Assert.Equal(6815u,MachinesAfter201211Fixture.CarryItem);
  var police=Mission(201216).Elements("phase").First().Elements("objective").Single(x=>(string?)x.Attribute("type")=="Talk");Assert.Equal(new[]{"1","2"},MachinesAfter201211Fixture.NpcReferences(police));
 }
 [Fact] public void OptionalPoliceCombatAndProtectAreNotMandatoryActionClicks()
 {
  var missions=MachinesAfter201211Fixture.Missions.Select(Mission).ToArray();Assert.Equal(12,missions.Sum(m=>m.Elements("phase").Count()));Assert.Equal(36,missions.Sum(m=>m.Elements("phase").Elements("objective").Count()));Assert.Equal(32,missions.Sum(m=>m.Elements("phase").Sum(p=>MachinesAfter201211Fixture.RequiredActions(p).Count())));
  Assert.DoesNotContain(MachinesAfter201211Fixture.RequiredActions(Mission(201216).Elements("phase").First()),g=>(string?)g.Attribute("type")=="DefeatAll");
  Assert.Equal(new[]{1},MachinesAfter201211Fixture.ProtectedIds(Mission(201213),0));Assert.Equal(new[]{1},MachinesAfter201211Fixture.ProtectedIds(Mission(201213),1));Assert.Equal(new[]{1},MachinesAfter201211Fixture.ProtectedIds(Mission(201215),1));
 }
 [Theory][InlineData("1,1")][InlineData("0")][InlineData("")][InlineData("other")]
 public void UnknownOrDuplicateNpcReferenceCannotBecomeAnotherTarget(string value)
 {Assert.Throws<InvalidDataException>(()=>MachinesAfter201211Fixture.NpcReferences(new XElement("objective",new XAttribute("type","Talk"),new XAttribute("npc",value))));}
 [Fact] public void NextExpectedOfferExistsInCurrentAuthoredOrder()
 {var next=Mission(201217);Assert.Equal("Bombs and Betrayal",(string)next.Attribute("title")!);Assert.Equal(201217,MachinesAfter201211Fixture.NextMission);}
}
