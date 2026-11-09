using System.Xml;
using hds;

namespace hds.Tests;

public class MissionProgressTests
{
    private const string TwoPhases = @"<mission id=""7"" title=""T"" description=""D"" plot=""1"" order=""2"" exp=""50"" info=""60"">
        <npc id=""1"" handle=""Crow"" type=""FRIENDLY"" x=""100"" y=""5"" z=""-25"" rsi=""b50000b4"" level=""3"" maxHP=""250"">
          <say>Before</say>
          <say after=""0"" display=""5"">After</say>
        </npc>
        <npc id=""2"" handle=""Watchman"" type=""HOSTILE"" x=""1"" y=""2"" z=""3"" rsi=""3b01004e"" attacksOnSight=""0"" dead=""1"" phase=""1"" weapon=""700"" />
        <phase>
          <briefing header=""30000100950105000000010000003c00"" a=""1,2,3"" b=""4,5,6"">Go, {player}.</briefing>
          <waypoint x=""1000"" y=""50"" z=""0"" door=""30015019"" />
          <objective goal=""1"" text=""Talk to Crow"" type=""Talk"" npc=""1"">
            <talk><say npc=""1"">Hi</say></talk>
            <done><say>Well done</say></done>
          </objective>
          <objective goal=""0"" text=""Talk again"" type=""Talk"" npc=""1"" />
          <objective goal=""2"" text=""Kill"" type=""Defeat"" npc=""2"" after=""1"" />
          <near x=""1"" y=""2"" z=""3"" radius=""600""><say>You're there</say></near>
        </phase>
        <phase>
          <waypoint x=""9000"" y=""0"" z=""0"" />
          <objective goal=""0"" text=""Give"" type=""Give"" npc=""1"" item=""1266"" />
        </phase>
        <finish>Thanks, {player}.</finish>
      </mission>";

    private static Mission Parse()
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(TwoPhases);
        return MissionXml.Parse(doc.DocumentElement);
    }

    [Fact]
    public void ReadsTheMissionFile()
    {
        Mission mission = Parse();

        Assert.Equal(7, mission.id);
        Assert.True(mission.plot);
        Assert.Equal(50, mission.exp);
        Assert.Equal(60, mission.info);
        Assert.Equal("Thanks, {player}.", mission.finishLine);
        Assert.Equal(2, mission.phases.Count);

        MissionPhase phase = mission.phases[0];
        Assert.Equal("Go, {player}.", phase.briefing);
        Assert.Equal(new[] { 4f, 5f, 6f }, phase.areaB);
        Assert.Equal("30015019", phase.waypoints[0].doorHex);
        Assert.Single(phase.triggers);

        MissionObjective first = phase.missionObjectives[0];
        Assert.Equal(1, first.goal);
        Assert.Equal(MissionGoalType.Talk, first.type);
        Assert.Equal(MissionSpeaker.Npc, first.talkLines[0].speaker);
        Assert.Equal(MissionSpeaker.Operator, first.doneLines[0].speaker);
        Assert.Equal(1, phase.missionObjectives[2].afterGoal);

        Assert.Equal(1266u, mission.phases[1].missionObjectives[0].item);
    }

    [Fact]
    public void ReadsMissionNpcs()
    {
        Mission mission = Parse();

        MissionNpc crow = mission.npcs[0];
        Assert.True(crow.friendly);
        Assert.Equal(100, crow.x); // world units, as the captures carry them
        Assert.Equal(-25, crow.z);
        Assert.Equal(250, crow.maxHealth);
        Assert.Equal(5, crow.lines[1].display);
        Assert.Equal(0, crow.lines[1].afterGoal);

        MissionNpc watchman = mission.npcs[1];
        Assert.False(watchman.friendly);
        Assert.False(watchman.attacksOnSight);
        Assert.True(watchman.dead);
        Assert.Equal(1, watchman.phase);
        Assert.Equal(700u, watchman.weapon);
    }

    [Fact]
    public void ObjectivesOpenTogetherUntilOneWaitsForAnother()
    {
        MissionProgress progress = new MissionProgress(Parse());

        Assert.Equal(new[] { 1, 0 }, progress.OpenObjectives().Select(o => o.goal));

        // Either can go first, and goal 2 opens only after goal 1.
        MissionObjective again = progress.FindOpen(MissionGoalType.Talk, 1);
        Assert.Equal(1, again.goal);
        List<MissionObjective> opened = progress.Complete(again);
        Assert.Equal(2, Assert.Single(opened).goal);
        Assert.Equal(new[] { 0, 2 }, progress.OpenObjectives().Select(o => o.goal));
        Assert.False(progress.waitingForExit);
    }

    [Fact]
    public void APhaseWaitsForThePlayerToLeaveThenMovesOn()
    {
        MissionProgress progress = new MissionProgress(Parse());
        foreach (MissionObjective objective in progress.mission.phases[0].missionObjectives)
        {
            progress.Complete(objective);
        }

        Assert.True(progress.waitingForExit);
        Assert.False(progress.IsExiting(1000, 2000, 0)); // same spot on another floor isn't the door
        Assert.False(progress.IsExiting(2000, 50, 0));
        Assert.True(progress.IsExiting(1400, 50, 0));    // 4 m from the door

        Assert.True(progress.NextPhase());
        Assert.False(progress.waitingForExit);
        Assert.False(progress.IsDone(0));                // goal ids repeat between phases
        Assert.True(progress.WasDoneInAnyPhase(0));
        Assert.Equal(new[] { 0 }, progress.OpenObjectives().Select(o => o.goal));

        progress.Complete(progress.mission.phases[1].missionObjectives[0]);
        Assert.False(progress.NextPhase());              // that was the last: the mission is over
    }

    [Theory]
    [InlineData(1000, 50, 0, true)]     // at the door
    [InlineData(1500, 50, 0, true)]     // 5 m past it, where retail's Welcome to the Matrix ended
    [InlineData(1700, 50, 0, false)]    // still inside
    [InlineData(1000, 3000, 0, false)]  // a floor away
    [InlineData(8000, 50, 0, true)]     // gone another way, such as a hardline
    public void LeavingMeansReachingADoorOrGoingFarAway(double x, double y, double z, bool left)
    {
        Assert.Equal(left, MissionExit.HasLeft(Parse().phases[0], x, y, z));
    }

    [Fact]
    public void TriggersFireOncePerPhase()
    {
        MissionProgress progress = new MissionProgress(Parse());
        MissionTrigger trigger = progress.mission.phases[0].triggers[0];

        Assert.True(progress.Fire(trigger));
        Assert.False(progress.Fire(trigger));
    }

    [Fact]
    public void AContactOffersTheNextStoryMissionThenAllOfThem()
    {
        List<Mission> missions = new List<Mission>
        {
            new Mission { id = 1201, title = "Welcome" },
            new Mission { id = 1204, title = "Exile" }
        };

        Assert.Equal("Welcome", Assert.Single(DataLoader.ContactOffer(missions, new HashSet<int>())).title);
        Assert.Equal("Exile", Assert.Single(DataLoader.ContactOffer(missions, new HashSet<int> { 1201 })).title);
        Assert.Equal(2, DataLoader.ContactOffer(missions, new HashSet<int> { 1201, 1204 }).Count);
    }

    [Theory]
    [InlineData("zion_01_welcome_to_the_matrix.xml", 1201, "Talk to Abalakin", 4)]
    [InlineData("zion_02_exile_extermination.xml", 1204, "Kill Angol", 1)]
    [InlineData("zion_03_the_internuncio.xml", 1203, "Get the Statue from Campbell", 1)]
    public void TheRealMissionFilesLoad(string file, int id, string firstObjective, int objectives)
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "data", "missions")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        XmlDocument doc = new XmlDocument();
        doc.Load(Path.Combine(dir, "data", "missions", file));
        Mission mission = MissionXml.Parse(doc.DocumentElement!);

        Assert.Equal(id, mission.id);
        Assert.Equal(firstObjective, mission.phases[0].missionObjectives[0].description);
        Assert.Equal(objectives, mission.phases[0].missionObjectives.Count);
        Assert.False(string.IsNullOrEmpty(mission.finishLine));
        foreach (MissionPhase phase in mission.phases)
        {
            Assert.NotEmpty(phase.waypoints);
            foreach (MissionObjective objective in phase.missionObjectives)
            {
                Assert.Contains(mission.npcs, n => n.id == objective.npcId);
            }
        }
    }
}
