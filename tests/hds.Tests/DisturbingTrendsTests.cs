using System.Xml;
using System.Xml.Linq;
using hds;
using HeadlessClient;
using NativeQa.Modes;
namespace hds.Tests;
public class DisturbingTrendsTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static string MissionPath=>Path.Combine(Root,"data","missions","machines_1_2_1_disturbing_trends.xml");
    private static Mission Load(){var d=new XmlDocument();d.Load(MissionPath);return MissionXml.Parse(d.DocumentElement!);}
    [Fact] public void ExistingThreePhaseStoryAndSeparateOphiuchiDefinitionsRemain()
    {
        var mission=Load();Assert.Equal(201201,mission.id);Assert.Equal(3,mission.phases.Count);Assert.Equal(3,mission.phases.Sum(p=>p.missionObjectives.Count));
        Assert.Equal(new[]{MissionGoalType.Talk,MissionGoalType.Search,MissionGoalType.Give},mission.phases.Select(p=>Assert.Single(p.missionObjectives).type));
        var xml=XElement.Load(MissionPath);var npcs=xml.Elements("npc").Where(n=>(string?)n.Attribute("handle")=="Ophiuchi").ToArray();Assert.Equal(new[]{1,6},npcs.Select(n=>(int)n.Attribute("id")!));Assert.Equal(new[]{1,3},npcs.Select(n=>(int)n.Attribute("at")!));
        var objectives=xml.Elements("phase").SelectMany(p=>p.Elements("objective")).ToArray();Assert.Equal("1",(string?)objectives[0].Attribute("npc"));Assert.Equal("6",(string?)objectives[2].Attribute("npc"));Assert.Equal("6783",(string?)objectives[1].Attribute("item"));Assert.Equal("6783",(string?)objectives[2].Attribute("item"));
    }
    [Fact] public void OnlyTwentyEarlierReceiptsExposeTheNormalNextCritical()
    {
        Assert.Equal(20,DisturbingTrendsFixture.Prerequisites.Length);Assert.Equal(20,DisturbingTrendsFixture.Prerequisites.Distinct().Count());Assert.DoesNotContain(201201,DisturbingTrendsFixture.Prerequisites);Assert.DoesNotContain(201202,DisturbingTrendsFixture.Prerequisites);
        Assert.Equal(MachinesCriticalFixture.Prerequisites.Concat(MachinesCriticalFixture.Missions),DisturbingTrendsFixture.Prerequisites);
        var all=Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();var contact=MissionContacts.ForOrg(2,0);Assert.Equal(2001u,contact.id);
        var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),DisturbingTrendsFixture.Prerequisites.ToHashSet());Assert.Equal(201201,Assert.Single(offers,m=>m.plot).id);
    }
    [Fact] public void CurrentQuoteDoesNotSilentlyImportPendingCapturedReferencePolicy()
    {
        var mission=Load();Assert.Equal(0,mission.rewardLevel);Assert.Equal(54871,mission.exp);Assert.Equal(45339,mission.info);
        var quote=MissionRewards.Calculate(mission,50);Assert.Equal(DisturbingTrendsFixture.ExpectedXp,quote.Experience);Assert.Equal(DisturbingTrendsFixture.ExpectedInfo,quote.Info);
        var low=MissionRewards.Calculate(mission,17);Assert.Equal(17917u,low.Experience);Assert.Equal(15415u,low.Info);
    }
    [Fact] public void NativeModeKeepsDependencyHoldAndOnlyExistingLayoutControl()
    {
        var plan=new DisturbingTrendsMode().Plan;Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Equal(DisturbingTrendsFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.Single(plan.EnvironmentOverrides);Assert.Equal("201101",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.True(plan.ExtraQaMetadata["NativePickingHold"].GetBoolean());
        Assert.Equal("4dbb2067672e806aa31a04d9e3aba496385342b5",plan.ExtraQaMetadata["RequiredComputerParent"].GetString());Assert.Contains("exp="+PlayerProgression.ExperienceForLevel(50),plan.AfterHelpers[1]);
        Assert.NotNull(Scenarios.All.Single(s=>s.Name=="machines-disturbing-trends"));
    }
}
