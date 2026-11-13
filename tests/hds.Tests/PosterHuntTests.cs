using System.Xml;
using System.Xml.Linq;
using hds;
using HeadlessClient;
using NativeQa.Modes;

namespace hds.Tests;

public class PosterHuntTests
{
    private static string Root
    {
        get { var p = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(p, "hds.sln"))) p = Path.GetDirectoryName(p)!; return p; }
    }
    private static string FilePath => Path.Combine(Root, "data", "missions", "machines_1_2_2_poster_hunt.xml");
    private static Mission Mission()
    {
        var xml = new XmlDocument(); xml.Load(FilePath); return MissionXml.Parse(xml.DocumentElement!);
    }

    [Fact]
    public void FourthPosterPreventsPrematureAreaCompletion()
    {
        var mission = Mission();
        Assert.Equal(new[] { 3, 1, 4 }, mission.phases.Select(p => p.objects.Count));
        Assert.Equal(new[] { 3, 1, 4 }, mission.phases.Select(p => p.missionObjectives.Count));
        var progress = new MissionProgress(mission) { phaseIndex = 2 };
        foreach (var goal in progress.Phase.missionObjectives.Take(3)) progress.Complete(goal);
        Assert.False(progress.AllDone);
        Assert.False(progress.waitingForExit);
        var fourth = Assert.Single(progress.OpenObjectives());
        Assert.Equal(4, fourth.goal);
        Assert.Equal((ushort)4, fourth.objectId);
        progress.Complete(fourth);
        Assert.True(progress.AllDone);
        Assert.True(progress.waitingForExit);
    }

    [Fact]
    public void ExistingSecondPosterAlarmAndAuthoredLimitsRemain()
    {
        var xml = XElement.Load(FilePath); var last = xml.Elements("phase").Last();
        Assert.Equal("2", (string?)last.Attribute("enemiesAfter"));
        Assert.All(xml.Elements("npc"), npc => Assert.Equal("2", (string?)npc.Attribute("spawnAfter")));
        Assert.Contains("triggered an alarm", last.Elements("objective").Single(g => (int)g.Attribute("goal")! == 2).Value);
        Assert.All(last.Elements("object"), o => Assert.Equal("Box", (string?)o.Attribute("kind")));
        Assert.All(last.Elements("objective"), g => Assert.Equal("Use", (string?)g.Attribute("type")));
    }

    [Fact]
    public void NormalOfferRequiresOnlyEarlierReceipts()
    {
        var all = Directory.GetFiles(Path.Combine(Root, "data", "missions"), "*.xml")
            .Select(f => { var d = new XmlDocument(); d.Load(f); return MissionXml.Parse(d.DocumentElement!); }).ToList();
        var contact = MissionContacts.ForOrg(2, 0);
        var offers = DataLoader.ContactOfferWithStandards(all.Where(m => MissionContacts.Offers(contact, m)).OrderBy(m => m.order).ToList(), PosterHuntFixture.Prerequisites.ToHashSet());
        Assert.Equal(201202, Assert.Single(offers, m => m.plot).id);
        Assert.DoesNotContain(201202, PosterHuntFixture.Prerequisites);
        var plan = new PosterHuntMode().Plan;
        Assert.False(plan.GmCharacter110); Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(PosterHuntFixture.SeedPrerequisites(110), plan.AfterHelpers[0]);
        Assert.Equal("201202", plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
    }
}
