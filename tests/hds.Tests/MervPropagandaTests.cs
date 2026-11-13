using System.Xml;
using System.Xml.Linq;
using hds;
using HeadlessClient;
using NativeQa.Modes;

namespace hds.Tests;

public class MervPropagandaTests
{
    private static string Root
    {
        get { var p = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(p, "hds.sln"))) p = Path.GetDirectoryName(p)!; return p; }
    }
    private static string FilePath => Path.Combine(Root, "data", "missions", "merovingian_1_2_2_propaganda.xml");
    private static Mission Mission()
    {
        var xml = new XmlDocument(); xml.Load(FilePath); return MissionXml.Parse(xml.DocumentElement!);
    }

    [Fact]
    public void FifthPosterPreventsPrematureAreaCompletion()
    {
        var mission = Mission();
        Assert.Equal(new[] { 3, 5 }, mission.phases.Select(p => p.objects.Count));
        Assert.Equal(new[] { 3, 5 }, mission.phases.Select(p => p.missionObjectives.Count));
        var progress = new MissionProgress(mission) { phaseIndex = 1 };
        foreach (var goal in progress.Phase.missionObjectives.Take(3)) progress.Complete(goal);
        Assert.False(progress.AllDone);
        Assert.False(progress.waitingForExit);
        Assert.Equal(2, progress.OpenObjectives().Count());
        var fourth = progress.OpenObjectives().First();
        Assert.Equal(4, fourth.goal);
        Assert.Equal((ushort)4, fourth.objectId);
        progress.Complete(fourth);
        Assert.False(progress.AllDone);
        Assert.False(progress.waitingForExit);
        var fifth=Assert.Single(progress.OpenObjectives());
        Assert.Equal(5,fifth.goal); Assert.Equal((ushort)5,fifth.objectId);
        progress.Complete(fifth);
        Assert.True(progress.AllDone);
        Assert.True(progress.waitingForExit);
    }

    [Fact]
    public void ExistingCameraEnemiesAndAuthoredLimitsRemain()
    {
        var xml=XElement.Load(FilePath);var phases=xml.Elements("phase").ToArray();
        Assert.Equal(new[]{"3","5"},phases.Select(p=>(string?)p.Attribute("enemies")));
        Assert.Contains("spotted by the camera",phases[0].Elements("objective").First().Value);
        Assert.Contains("at least three",phases[0].Element("enter")!.Value);
        Assert.Contains("five more posters",phases[1].Element("enter")!.Value);
        Assert.Equal("68750",(string?)xml.Attribute("exp"));Assert.Equal("91000",(string?)xml.Attribute("info"));
        foreach(var phase in phases){Assert.All(phase.Elements("object"),o=>Assert.Equal("Box",(string?)o.Attribute("kind")));Assert.All(phase.Elements("objective"),o=>Assert.Equal("Use",(string?)o.Attribute("type")));}
    }

    [Fact]
    public void NormalOfferRequiresOnlyEarlierReceipts()
    {
        var all = Directory.GetFiles(Path.Combine(Root, "data", "missions"), "*.xml")
            .Select(f => { var d = new XmlDocument(); d.Load(f); return MissionXml.Parse(d.DocumentElement!); }).ToList();
        MissionContacts.Load(Path.Combine(Root,"data","mission_contacts.csv"));
        var contact = MissionContacts.ForOrg(3, 0);
        var offers = DataLoader.ContactOfferWithStandards(all.Where(m => MissionContacts.Offers(contact, m)).OrderBy(m => m.order).ToList(), MervPropagandaFixture.Prerequisites.ToHashSet());
        Assert.Equal(301202, Assert.Single(offers, m => m.plot).id);
        Assert.Equal(21,MervPropagandaFixture.Prerequisites.Length);
        Assert.Equal(21,MervPropagandaFixture.Prerequisites.Distinct().Count());
        Assert.DoesNotContain(301202, MervPropagandaFixture.Prerequisites);
        var plan = new MervPropagandaMode().Plan;
        Assert.False(plan.GmCharacter110); Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(MervPropagandaFixture.SeedPrerequisites(110), plan.AfterHelpers[0]);
        Assert.Equal("301101", plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
    }
}
