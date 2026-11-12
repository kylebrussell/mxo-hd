using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class ZionChainTests
{
    private static Mission Load(int id)
    {
        string root=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(root,"data","missions")))root=Path.GetDirectoryName(root)!;
        foreach(string file in Directory.GetFiles(Path.Combine(root,"data","missions"),"zion_arc_*.xml"))
        {var doc=new XmlDocument();doc.Load(file);var mission=MissionXml.Parse(doc.DocumentElement!);if(mission.id==id)return mission;}
        throw new Exception("Missing chain mission "+id);
    }
    [Fact]
    public void SevenTyndallMissionsHaveAllTwentyOnePhasesAndOnlyPriorMissionsSeeded()
    {
        var chain=ZionChainFixture.Missions.Select(Load).ToArray();
        Assert.Equal(21,chain.Sum(m=>m.phases.Count));Assert.Equal(Enumerable.Range(14,7),chain.Select(m=>m.order));
        Assert.All(chain,m=>{Assert.Equal("Tyndall",m.contact);Assert.Equal((byte)1,m.org);Assert.All(m.phases,p=>Assert.NotEmpty(p.missionObjectives));});
        var plan=new ZionChainMode().Plan;Assert.Equal(ZionChainFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.Empty(ZionChainFixture.Missions.Intersect(ZionChainFixture.Prerequisites));
        Assert.True(plan.DevCommands);Assert.False(plan.FixedCallbackRolls);Assert.False(plan.GmCharacter110);Assert.Equal("230410",Assert.Single(plan.EnvironmentOverrides).Value);
    }
    [Fact]
    public void ReconDataWaitsForTraitorAndFinalUploadWaitsForSurvivorsBuildingExit()
    {
        var m=Load(2308);Assert.Equal(1,m.phases[1].missionObjectives.Single(o=>o.type==MissionGoalType.Search).afterGoal);
        Assert.Equal(1,m.phases[2].missionObjectives.Single(o=>o.type==MissionGoalType.Upload).afterGoal);
    }
    [Fact]
    public void ArtifactClosingSequenceCannotExposeFinalKillBeforeHandoff()
    {
        var m=Load(2310);var goals=m.phases[4].missionObjectives;
        Assert.Equal(2,goals.Single(o=>o.type==MissionGoalType.Give).afterGoal);
        Assert.Equal(3,goals.Single(o=>o.type==MissionGoalType.Defeat).afterGoal);
        var opponent=m.npcs.Single(n=>n.id==4);Assert.Equal(3,opponent.spawnAfter);Assert.False(opponent.attacksOnSight);
    }
}
