using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class MervChainTests
{
    private static Mission Load(int id)
    {
        string root=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(root,"data","missions")))root=Path.GetDirectoryName(root)!;
        foreach(string file in Directory.GetFiles(Path.Combine(root,"data","missions"),"exile_*.xml"))
        {var doc=new XmlDocument();doc.Load(file);var mission=MissionXml.Parse(doc.DocumentElement!);if(mission.id==id)return mission;}
        throw new Exception("Missing chain mission "+id);
    }
    [Fact]
    public void TenFloodMissionsHaveAllTwentyFivePhasesAndOnlyEarlierPrerequisites()
    {
        var chain=MervChainFixture.Missions.Select(Load).ToArray();
        Assert.Equal(25,chain.Sum(m=>m.phases.Count));Assert.Equal(49,chain.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Equal(Enumerable.Range(11,10),chain.Select(m=>m.order));
        Assert.All(chain,m=>{Assert.Equal("Flood",m.contact);Assert.Equal((byte)3,m.org);Assert.All(m.phases,p=>Assert.NotEmpty(p.missionObjectives));});
        Assert.Empty(MervChainFixture.Missions.Intersect(MervChainFixture.Prerequisites));
        var plan=new MervChainMode().Plan;Assert.Equal(MervChainFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("210110",Assert.Single(plan.EnvironmentOverrides).Value);
    }
    [Fact]
    public void SourceOrderedGuardsCannotBeSkippedByTransportOrPlanting()
    {
        Assert.Equal(2,Load(2102).phases[0].missionObjectives.Single(o=>o.type==MissionGoalType.Escort).afterGoal);
        Assert.Equal(1,Load(2103).phases[1].missionObjectives.Single(o=>o.type==MissionGoalType.Put).afterGoal);
        var goals=Load(2108).phases[1].missionObjectives;
        Assert.Equal(1,goals.Single(o=>o.type==MissionGoalType.Put).afterGoal);
    }
}
