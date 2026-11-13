using System.Xml;
using System.Xml.Linq;
using hds;
using HeadlessClient;
using NativeQa.Modes;
namespace hds.Tests;
public class ZionPropagandaCountTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static string PathToMission=>Path.Combine(Root,"data","missions","zion_1_2_2_the_power_of_propaganda.xml");
    private static Mission Mission(){var xml=new XmlDocument();xml.Load(PathToMission);return MissionXml.Parse(xml.DocumentElement!);}
    [Theory][InlineData(0,6)][InlineData(1,4)]
    public void LastRequiredPosterAlonePermitsAreaCompletion(int area,int count)
    {
        var mission=Mission();var progress=new MissionProgress(mission){phaseIndex=area};
        Assert.Equal(count,progress.Phase.objects.Count);Assert.Equal(count,progress.Phase.missionObjectives.Count);
        Assert.Equal(Enumerable.Range(1,count),progress.Phase.objects.Select(o=>(int)o.id));
        Assert.Equal(Enumerable.Range(1,count),progress.Phase.missionObjectives.Select(o=>o.goal));
        foreach(var goal in progress.Phase.missionObjectives.Take(count-1))progress.Complete(goal);
        Assert.False(progress.AllDone);Assert.False(progress.waitingForExit);var final=Assert.Single(progress.OpenObjectives());
        Assert.Equal(count,final.goal);Assert.Equal((ushort)count,final.objectId);progress.Complete(final);
        Assert.True(progress.AllDone);Assert.True(progress.waitingForExit);
    }
    [Fact] public void ExistingDialogueEnemiesRewardsAndBoxPolicyStayExplicit()
    {
        var xml=XElement.Load(PathToMission);var phases=xml.Elements("phase").ToArray();Assert.Equal(new[]{"3","5"},phases.Select(p=>(string)p.Attribute("enemies")!));
        Assert.Equal("137500",(string?)xml.Attribute("exp"));Assert.Equal("91000",(string?)xml.Attribute("info"));
        Assert.Contains("Six of Morpheus",phases[0].Element("enter")!.Value);Assert.Contains("four posters",phases[1].Element("enter")!.Value);
        Assert.Contains("appealing to these Exiles",phases[0].Elements("objective").Single(o=>(int)o.Attribute("goal")! ==2).Value);
        Assert.All(phases.SelectMany(p=>p.Elements("object")),o=>Assert.Equal("Box",(string?)o.Attribute("kind")));
        Assert.All(phases.SelectMany(p=>p.Elements("objective")),o=>{Assert.Equal("Use",(string?)o.Attribute("type"));Assert.Null(o.Attribute("optional"));});
    }
    [Fact] public void PrerequisitesOnlyExposeThisNormalCriticalAndDoNotGrantIt()
    {
        var all=Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(p=>{var d=new XmlDocument();d.Load(p);return MissionXml.Parse(d.DocumentElement!);}).ToList();
        Assert.Equal(21,ZionPropagandaFixture.Prerequisites.Length);Assert.Equal(21,ZionPropagandaFixture.Prerequisites.Distinct().Count());Assert.DoesNotContain(101202,ZionPropagandaFixture.Prerequisites);
        Assert.Equal(ZionCriticalFixture.Prerequisites.Concat(new[]{101101,101102,101103,101201}),ZionPropagandaFixture.Prerequisites);
        var contact=MissionContacts.ForOrg(1,0);Assert.Equal(2000u,contact.id);
        var offered=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),ZionPropagandaFixture.Prerequisites.ToHashSet());
        Assert.Equal(101202,Assert.Single(offered,m=>m.plot).id);
        var mode=new ZionPropagandaMode();Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);Assert.Equal("101101",mode.Plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Equal(ZionPropagandaFixture.SeedPrerequisites(110),mode.Plan.AfterHelpers[0]);Assert.Contains("exp="+PlayerProgression.ExperienceForLevel(50),mode.Plan.AfterHelpers[1]);
        Assert.NotNull(Scenarios.All.Single(s=>s.Name==mode.Name));
    }
}
