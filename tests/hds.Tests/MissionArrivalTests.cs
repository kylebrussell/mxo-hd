using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>
/// NPCs and hostiles that arrive when a goal completes (spawnAfter, enemiesAfter), kill goals that wait, how a phase
/// ends (leave), and escorts to an object.
/// </summary>
public class MissionArrivalTests
{
    private const string Arrivals = @"<mission id=""9"" title=""T"" description=""D"">
        <npc id=""1"" handle=""Tarka"" type=""FRIENDLY"" at=""1"" rsi=""1"" />
        <npc id=""2"" handle=""Security"" type=""HOSTILE"" at=""1"" rsi=""2"" spawnAfter=""3"" spawnAt=""player"" />
        <npc id=""3"" handle=""Agent"" type=""HOSTILE"" at=""2"" rsi=""3"" spawnAfter=""1"" />
        <phase site=""Normal"" enemy=""Machines"" enemies=""2"" enemiesAfter=""3"">
          <object id=""1"" kind=""Mainframe"" />
          <objective goal=""3"" text=""Talk to Tarka"" type=""Talk"" npc=""1"" />
          <objective goal=""4"" text=""Kill Security"" type=""Defeat"" npc=""2"" after=""3"" />
          <objective goal=""5"" text=""Kill them all"" type=""DefeatAll"" />
          <objective goal=""6"" text=""Lead Tarka to the mainframe"" type=""Escort"" npc=""1"" to=""object:1"" after=""3"" />
        </phase>
        <phase site=""Normal"" leave=""0"">
          <objective goal=""1"" text=""Talk to Tarka"" type=""Talk"" npc=""1"" />
          <objective goal=""2"" text=""Kill the Agent"" type=""Defeat"" npc=""3"" after=""1"" />
        </phase>
        <phase site=""Normal"" leave=""1"">
          <objective goal=""1"" text=""Upload it"" type=""Upload"" item=""6787"" />
        </phase>
      </mission>";

    private static Mission Parse(string xml = Arrivals)
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xml);
        return MissionXml.Parse(doc.DocumentElement!)!;
    }

    private static MissionObjective Goal(MissionProgress progress, int goal) =>
        progress.Phase!.missionObjectives.Single(o => o.goal == goal);

    [Fact]
    public void ReadsTheNewAttributes()
    {
        Mission mission = Parse();

        Assert.Equal(-1, mission.npcs[0].spawnAfter);
        Assert.False(mission.npcs[0].spawnAtPlayer);
        Assert.Equal(3, mission.npcs[1].spawnAfter);
        Assert.True(mission.npcs[1].spawnAtPlayer);
        Assert.False(mission.npcs[2].spawnAtPlayer);

        Assert.Equal(3, mission.phases[0].enemiesAfter);
        Assert.Null(mission.phases[0].leave);
        Assert.False(mission.phases[1].leave);
        Assert.True(mission.phases[2].leave);
        Assert.Equal(-1, mission.phases[1].enemiesAfter);
        Assert.Equal("object:1", mission.phases[0].missionObjectives[3].escortTo);
    }

    [Fact]
    public void OldFilesReadAsBefore()
    {
        Mission mission = Parse(@"<mission id=""1"" title=""T""><npc id=""1"" handle=""A"" rsi=""1"" />
            <phase><objective goal=""1"" text=""Kill"" type=""Defeat"" npc=""1"" /></phase></mission>");

        Assert.Equal(-1, mission.npcs[0].spawnAfter);
        Assert.Equal(-1, mission.phases[0].enemiesAfter);
        Assert.Null(mission.phases[0].leave);
        Assert.True(new MissionProgress(mission).HasArrived(1));
    }

    [Fact]
    public void AnNpcArrivesWhenItsGoalCompletes()
    {
        MissionProgress progress = new MissionProgress(Parse());

        Assert.True(progress.HasArrived(1));
        Assert.False(progress.HasArrived(2));
        Assert.Empty(progress.justArrived);

        progress.Complete(Goal(progress, 3));

        Assert.True(progress.HasArrived(2));
        Assert.Equal("Security", Assert.Single(progress.justArrived).handle);
        Assert.True(progress.enemiesArrived);
        Assert.True(progress.enemiesJustArrived);
    }

    [Fact]
    public void APlacedNpcArrivesOnlyOnItsOwnPhasesGoal()
    {
        MissionProgress progress = new MissionProgress(Parse());

        // The Agent (at="2", spawnAfter="1") doesn't come on some other phase's goal 1
        progress.Complete(Goal(progress, 3));
        Assert.False(progress.HasArrived(3));

        progress.NextPhase();
        progress.Complete(Goal(progress, 1));
        Assert.True(progress.HasArrived(3));
        Assert.Contains(progress.justArrived, n => n.handle == "Agent");
    }

    [Fact]
    public void AMissionNpcWithoutAPhaseArrivesTheFirstTimeItsGoalCompletes()
    {
        Mission mission = Parse(@"<mission id=""1"" title=""T"">
            <npc id=""1"" handle=""A"" type=""FRIENDLY"" x=""1"" y=""2"" z=""3"" rsi=""1"" />
            <npc id=""2"" handle=""Reinforcement"" type=""HOSTILE"" x=""1"" y=""2"" z=""3"" rsi=""1"" spawnAfter=""1"" />
            <phase><objective goal=""2"" text=""Talk"" type=""Talk"" npc=""1"" /></phase>
            <phase><objective goal=""1"" text=""Talk"" type=""Talk"" npc=""1"" />
                   <objective goal=""3"" text=""Kill"" type=""Defeat"" npc=""2"" after=""1"" /></phase></mission>");
        MissionProgress progress = new MissionProgress(mission);

        progress.Complete(Goal(progress, 2));
        Assert.False(progress.HasArrived(2));
        progress.NextPhase();
        progress.Complete(Goal(progress, 1));

        Assert.True(progress.HasArrived(2));
        Assert.Single(progress.justArrived);
    }

    [Fact]
    public void ADefeatAllWaitsForHostilesStillToArrive()
    {
        MissionProgress progress = new MissionProgress(Parse());

        Assert.True(progress.HostilesStillToArrive()); // Security and the two Machines come after goal 3

        progress.Complete(Goal(progress, 3));

        Assert.False(progress.HostilesStillToArrive());
    }

    [Fact]
    public void HostilesWaitingOnAnOptionalGoalDontHoldUpADefeatAll()
    {
        Mission mission = Parse(@"<mission id=""1"" title=""T"">
            <npc id=""1"" handle=""Maybe"" type=""HOSTILE"" x=""1"" y=""2"" z=""3"" rsi=""1"" spawnAfter=""2"" />
            <phase><objective goal=""1"" text=""Kill them"" type=""DefeatAll"" />
                   <objective goal=""2"" text=""Ring the bell"" type=""Talk"" npc=""1"" optional=""1"" /></phase></mission>");

        Assert.False(new MissionProgress(mission).HostilesStillToArrive());
    }

    [Fact]
    public void ArrivalsThatNeverCameAreForgottenWithTheirPhase()
    {
        MissionProgress progress = new MissionProgress(Parse());
        progress.reservedSpots[2] = new double[] { 1, 2, 3 };
        progress.reservedEnemySpots.Add(new double[] { 4, 5, 6 });
        progress.Complete(Goal(progress, 3));

        progress.NextPhase();

        Assert.Empty(progress.justArrived);
        Assert.Empty(progress.reservedSpots);
        Assert.Empty(progress.reservedEnemySpots);
        Assert.False(progress.enemiesArrived);
        Assert.False(progress.enemiesJustArrived);
    }

    [Fact]
    public void KillsAreRememberedAcrossTheMission()
    {
        MissionProgress progress = new MissionProgress(Parse());
        progress.killed.Add(2);
        progress.NextPhase();

        Assert.Contains((ushort)2, progress.killed);
    }

    [Theory]
    [InlineData(0, false)]  // a finished phase with leave="0" moves on at once
    [InlineData(1, true)]   // leave="1": the player leaves, even after an upload
    public void APhaseCanSayWhetherItsEndNeedsLeaving(int skip, bool leave)
    {
        MissionProgress progress = new MissionProgress(Parse());
        progress.NextPhase();
        for (int i = 0; i < skip; i++)
        {
            progress.NextPhase();
        }

        progress.waypoints = new List<MissionWaypoint> { new MissionWaypoint { x = 0, y = 0, z = 0 } };
        Assert.Equal(leave, MissionRuntime.NeedsToLeave(progress, progress.Phase!.missionObjectives[0], 100, 0, 0));
    }

    [Fact]
    public void WithoutLeaveThePlayerLeavesUnlessTheyAreOutAlready()
    {
        Mission mission = Parse(@"<mission id=""1"" title=""T"">
            <npc id=""1"" handle=""A"" type=""FRIENDLY"" x=""1"" y=""2"" z=""3"" rsi=""1"" />
            <phase>
              <waypoint x=""1000"" y=""50"" z=""0"" door=""30015019"" />
              <objective goal=""1"" text=""Talk"" type=""Talk"" npc=""1"" />
              <objective goal=""2"" text=""Upload"" type=""Upload"" item=""6787"" />
              <objective goal=""3"" text=""Lead out"" type=""Escort"" npc=""1"" to=""exit"" />
              <objective goal=""4"" text=""Lead to a hardline"" type=""Escort"" npc=""1"" to=""hardline"" />
              <objective goal=""5"" text=""Lead to B"" type=""Escort"" npc=""1"" to=""npc:2"" />
            </phase></mission>");
        MissionProgress progress = new MissionProgress(mission);
        MissionObjective Of(int goal) => Goal(progress, goal);

        Assert.True(MissionRuntime.NeedsToLeave(progress, Of(1), 1700, 50, 0));    // inside: "Find the nearest exit"
        Assert.True(MissionRuntime.NeedsToLeave(progress, Of(5), 1700, 50, 0));
        Assert.False(MissionRuntime.NeedsToLeave(progress, Of(1), 9000, 50, 0));   // 80 m away: out already
        Assert.False(MissionRuntime.NeedsToLeave(progress, Of(2), 1700, 50, 0));   // uploaded at a hardline
        Assert.False(MissionRuntime.NeedsToLeave(progress, Of(3), 1500, 50, 0));   // led out of the area
        Assert.False(MissionRuntime.NeedsToLeave(progress, Of(4), 1700, 50, 0));
    }

    [Fact]
    public void APhaseThatNeedNotBeLeftMovesOnAtOnce()
    {
        MissionProgress progress = new MissionProgress(Parse());
        progress.waypoints = new List<MissionWaypoint> { new MissionWaypoint { x = 0, y = 0, z = 0 } };
        foreach (MissionObjective objective in progress.Phase!.missionObjectives)
        {
            progress.Complete(objective);
        }

        Assert.True(progress.waitingForExit);
        Assert.False(progress.IsExiting(1700, 0, 0)); // inside, 17 m from the door

        progress.leaveNotNeeded = true;               // MissionRuntime sent "Goals for this mission phase are complete!"
        Assert.True(progress.IsExiting(1700, 0, 0));

        progress.NextPhase();
        Assert.False(progress.leaveNotNeeded);
        Assert.False(progress.goalsCompleteSent);
    }

    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(5999, 0, 0, false)]
    [InlineData(6000, 0, 0, true)]
    public void OutsideMeansFarFromEveryWayOut(double x, double y, double z, bool outside)
    {
        List<MissionWaypoint> doors = new List<MissionWaypoint> { new MissionWaypoint { x = 0, y = 0, z = 0 } };

        Assert.Equal(outside, MissionExit.IsOutside(doors, new List<double[]>(), x, y, z));
        Assert.Equal(outside, MissionExit.IsOutside(new List<MissionWaypoint>(), new List<double[]> { new double[] { 0, 0, 0 } }, x, y, z));
        Assert.True(MissionExit.IsOutside(new List<MissionWaypoint>(), new List<double[]>(), x, y, z)); // no area at all
    }

    [Fact]
    public void ArrivalsBesideThePlayerStandApart()
    {
        double[] first = MissionRuntime.BesidePlayer(1000, 50, 2000, 0);
        double[] second = MissionRuntime.BesidePlayer(1000, 50, 2000, 1);

        Assert.Equal(300, Math.Sqrt((first[0] - 1000) * (first[0] - 1000) + (first[2] - 2000) * (first[2] - 2000)), 3);
        Assert.Equal(50, first[1]);
        Assert.True(Math.Abs(first[0] - second[0]) + Math.Abs(first[2] - second[2]) > 200);
    }
}
