using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>
/// Waves of hostiles with their own arrival and kill-all scope, arrivals on the area, a place or a timer, hostiles that
/// appear beside the player or an object, NPCs that change side, and timed goals (Survive, limit).
/// </summary>
public class MissionWaveTests
{
    private const string Waves = @"<mission id=""9"" title=""T"" description=""D"">
        <npc id=""1"" handle=""Dora"" type=""FRIENDLY"" at=""1"" rsi=""1"" hostileAfter=""2"" />
        <npc id=""2"" handle=""Guard"" type=""HOSTILE"" at=""1"" rsi=""2"" wave=""1"" spawnAfter=""enter"" spawnDelay=""20"" spawnAt=""object:1"" />
        <npc id=""3"" handle=""Turncoat"" type=""HOSTILE"" at=""2"" rsi=""3"" friendlyAfter=""1"" attacksOnSight=""0"" />
        <npc id=""4"" handle=""Watcher"" type=""HOSTILE"" at=""1"" rsi=""4"" wave=""1"" spawnNear=""object:1"" spawnRadius=""500"" />
        <phase site=""Normal"" enemy=""Zion"" enemies=""2"">
          <object id=""1"" kind=""Computer"" />
          <wave enemy=""Machines"" count=""3"" after=""1"" delay=""30"" spawnAt=""player"">
            <say>More hostiles coming in!</say>
          </wave>
          <wave id=""5"" count=""2"" after=""3"" near=""object:1"" radius=""800"" />
          <objective goal=""1"" text=""Use the computer"" type=""Use"" object=""1"" />
          <objective goal=""2"" text=""Kill the Zionites"" type=""DefeatAll"" wave=""0"" />
          <objective goal=""3"" text=""Kill the reinforcements"" type=""DefeatAll"" wave=""1, 5"" after=""1"" />
          <objective goal=""4"" text=""Hold out"" type=""Survive"" seconds=""180"" after=""1"" />
          <objective goal=""6"" text=""Be quick"" type=""Use"" object=""1"" limit=""90"" after=""4"" />
        </phase>
        <phase enemy=""Zion"" enemies=""2"" enemiesAfter=""1"" enemiesAt=""player"">
          <objective goal=""1"" text=""Talk to the Turncoat"" type=""Talk"" npc=""3"" />
          <objective goal=""2"" text=""Kill them"" type=""DefeatAll"" after=""1"" />
        </phase>
      </mission>";

    private static Mission Parse(string xml = Waves)
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xml);
        return MissionXml.Parse(doc.DocumentElement!)!;
    }

    private static MissionObjective Goal(MissionProgress progress, int goal) =>
        progress.Phase!.missionObjectives.Single(o => o.goal == goal);

    private static (MissionProgress progress, Func<double> time, Action<double> wait) Start(string xml = Waves)
    {
        double now = 0;
        MissionProgress progress = new MissionProgress(Parse(xml)) { clock = () => now };
        return (progress, () => now, seconds => now += seconds);
    }

    [Fact]
    public void ReadsWavesArrivalsSidesAndTimers()
    {
        Mission mission = Parse();
        MissionPhase phase = mission.phases[0];

        Assert.Equal(new[] { 0, 1, 5 }, phase.Waves().Select(w => w.id));
        MissionWave first = phase.waves[0];
        Assert.Equal("Machines", first.enemy);
        Assert.Equal(3, first.count);
        Assert.Equal(1, first.arrival.afterGoal);
        Assert.Equal(30, first.arrival.delay);
        Assert.True(first.arrival.AtPlayer);
        Assert.Equal("More hostiles coming in!", Assert.Single(first.lines).text);
        MissionWave second = phase.waves[1];
        Assert.Equal("Zion", second.enemy);   // the phase's enemy unless it says
        Assert.Equal("object:1", second.arrival.near);
        Assert.Equal(800, second.arrival.radius);

        MissionNpc guard = mission.npcs[1];
        Assert.True(guard.arrival.onEnter);
        Assert.Equal(-1, guard.spawnAfter);
        Assert.Equal(20, guard.arrival.delay);
        Assert.Equal((ushort)1, guard.arrival.AtObject);
        Assert.False(guard.spawnAtPlayer);
        Assert.Equal(1, guard.wave);
        Assert.Equal(2, mission.npcs[0].hostileAfter);
        Assert.Equal(1, mission.npcs[2].friendlyAfter);
        Assert.Equal(500, mission.npcs[3].arrival.radius);

        Assert.Equal(new[] { 0 }, phase.missionObjectives[1].waves);
        Assert.Equal(new[] { 1, 5 }, phase.missionObjectives[2].waves);
        Assert.Equal(MissionGoalType.Survive, phase.missionObjectives[3].type);
        Assert.Equal(180, phase.missionObjectives[3].seconds);
        Assert.Equal(90, phase.missionObjectives[4].limit);

        MissionWave own = Assert.Single(mission.phases[1].Waves());
        Assert.Equal(0, own.id);
        Assert.Equal(1, own.arrival.afterGoal);
        Assert.True(own.arrival.AtPlayer);
    }

    [Fact]
    public void OldPhasesHaveOnlyTheirOwnWave()
    {
        Mission mission = Parse(@"<mission id=""1"" title=""T""><phase site=""Normal"" enemy=""Zion"" enemies=""3"" enemiesAfter=""1"">
            <objective goal=""1"" text=""Talk"" type=""Use"" object=""1"" /></phase>
            <phase><objective goal=""1"" text=""Talk"" type=""Use"" object=""1"" /></phase></mission>");

        MissionWave own = Assert.Single(mission.phases[0].Waves());
        Assert.Equal(3, own.count);
        Assert.Null(own.arrival.at);
        Assert.False(own.arrival.onEnter);
        Assert.Empty(mission.phases[1].Waves());
        Assert.Empty(mission.phases[0].missionObjectives[0].waves);
        Assert.Equal(0, mission.phases[0].missionObjectives[0].limit);
    }

    [Fact]
    public void AWaveComesOnItsOwnGoalAndAfterItsDelay()
    {
        (MissionProgress progress, _, Action<double> wait) = Start();

        progress.UpdateArrivals();
        Assert.Empty(progress.wavesJustArrived);   // wave 0 stands there from the start; waves 1 and 5 wait

        progress.Complete(Goal(progress, 1));
        Assert.Empty(progress.wavesJustArrived);   // wave 1 is on its way: 30 s
        wait(29);
        progress.UpdateArrivals();
        Assert.Empty(progress.wavesJustArrived);
        wait(1);
        progress.UpdateArrivals();

        Assert.Equal(1, Assert.Single(progress.wavesJustArrived).id);
        Assert.Contains(1, progress.wavesArrived);
        Assert.False(progress.enemiesArrived);     // the phase's own aren't late
    }

    [Fact]
    public void AWaveCanWaitForThePlayerToComeNearAPlace()
    {
        (MissionProgress progress, _, _) = Start();
        progress.Complete(Goal(progress, 1));
        progress.Complete(Goal(progress, 3));      // wave 5's goal (as if; it waits on the place too)
        List<(string place, double radius)> asked = new List<(string, double)>();

        progress.UpdateArrivals((place, radius) => { asked.Add((place, radius)); return false; });
        Assert.DoesNotContain(progress.wavesJustArrived, w => w.id == 5);
        Assert.Contains(("object:1", 800.0), asked);

        progress.UpdateArrivals((place, radius) => place == "object:1");
        Assert.Contains(progress.wavesJustArrived, w => w.id == 5);
    }

    [Fact]
    public void AnNpcCanArriveSecondsAfterThePlayerReachesTheArea()
    {
        (MissionProgress progress, _, Action<double> wait) = Start();

        wait(100);
        progress.UpdateArrivals();
        Assert.False(progress.HasArrived(2));      // a generated phase: the area is its floor

        progress.entered = true;
        progress.UpdateArrivals();
        wait(19);
        progress.UpdateArrivals();
        Assert.False(progress.HasArrived(2));
        wait(1);
        progress.UpdateArrivals();

        Assert.True(progress.HasArrived(2));
        Assert.Equal("Guard", Assert.Single(progress.justArrived).handle);
    }

    [Fact]
    public void WithoutASiteTheAreaIsThereFromTheStart()
    {
        (MissionProgress progress, _, Action<double> wait) = Start(@"<mission id=""1"" title=""T"">
            <npc id=""1"" handle=""Commando"" type=""FRIENDLY"" x=""1"" y=""2"" z=""3"" rsi=""1"" spawnAfter=""enter"" spawnDelay=""27"" />
            <phase><objective goal=""1"" text=""Talk"" type=""Talk"" npc=""1"" /></phase></mission>");

        progress.UpdateArrivals();
        wait(27);
        progress.UpdateArrivals();

        Assert.True(progress.HasArrived(1));
    }

    [Fact]
    public void AnNpcCanArriveOnComingNearAPlace()
    {
        (MissionProgress progress, _, _) = Start();

        progress.UpdateArrivals((place, radius) => false);
        Assert.False(progress.HasArrived(4));
        progress.UpdateArrivals((place, radius) => place == "object:1" && radius == 500);

        Assert.True(progress.HasArrived(4));
    }

    [Fact]
    public void AScopedKillAllWaitsOnlyForItsOwnWaves()
    {
        (MissionProgress progress, _, Action<double> wait) = Start();
        MissionObjective zionites = Goal(progress, 2), reinforcements = Goal(progress, 3);

        // Goal 2 counts the phase's own, who are there; nobody of theirs is to come
        Assert.False(progress.HostilesStillToArrive(zionites));
        // Goal 3 counts wave 1 (after goal 1, then 30 s), wave 5, the Guard (20 s after the area) and the Watcher (near the computer)
        Assert.True(progress.HostilesStillToArrive(reinforcements));

        progress.Complete(Goal(progress, 1));
        Assert.True(progress.HostilesStillToArrive(reinforcements)); // on their way: the delay is running
        progress.entered = true;
        progress.UpdateArrivals((place, radius) => true);
        wait(31);
        progress.UpdateArrivals((place, radius) => true);

        // Wave 5 waits on goal 3 itself, so goal 3 can't wait for it
        Assert.False(progress.HostilesStillToArrive(reinforcements));
        Assert.True(progress.HostilesStillToArrive()); // an unscoped look still sees wave 5 coming
    }

    [Fact]
    public void AKillAllDoesNotWaitForAWaveThatComesAfterIt()
    {
        (MissionProgress progress, _, _) = Start(@"<mission id=""1"" title=""T"">
            <npc id=""1"" handle=""Effectuator"" type=""FRIENDLY"" at=""1"" rsi=""1"" />
            <phase site=""Normal"" enemy=""Machines"" enemies=""2"" enemiesAfter=""1"">
              <object id=""1"" kind=""Mainframe"" />
              <wave count=""2"" after=""3"" />
              <objective goal=""1"" text=""Access the mainframe"" type=""Use"" object=""1"" />
              <objective goal=""2"" text=""Kill the Captains"" type=""DefeatAll"" after=""1"" />
              <objective goal=""3"" text=""Talk to the Effectuator"" type=""Talk"" npc=""1"" after=""2"" />
              <objective goal=""4"" text=""Get the data"" type=""Search"" object=""1"" item=""6787"" after=""3"" />
            </phase></mission>");
        MissionObjective captains = Goal(progress, 2);

        Assert.True(progress.HostilesStillToArrive(captains)); // the Captains come after goal 1
        progress.Complete(Goal(progress, 1));

        Assert.False(progress.HostilesStillToArrive(captains)); // the wave after goal 3 opens only once they're dead
        Assert.True(progress.WaitsOn(4, 2));
        Assert.False(progress.WaitsOn(1, 2));
    }

    [Fact]
    public void AGoalTurnsAnNpcHostileOrFriendly()
    {
        (MissionProgress progress, _, _) = Start();
        MissionNpc dora = progress.mission.npcs[0], turncoat = progress.mission.npcs[2];

        Assert.True(progress.IsFriendly(dora));
        progress.Complete(Goal(progress, 1));
        Assert.True(progress.IsFriendly(dora));    // goal 1 is the Turncoat's in phase 2, not this phase's
        Assert.Empty(progress.justSwitched);

        progress.Complete(Goal(progress, 2));
        Assert.False(progress.IsFriendly(dora));
        Assert.Equal(new ushort[] { 1 }, progress.justSwitched);

        progress.NextPhase();
        Assert.Empty(progress.justSwitched);
        Assert.False(progress.IsFriendly(turncoat));
        progress.Complete(Goal(progress, 1));
        Assert.True(progress.IsFriendly(turncoat));
        Assert.False(progress.IsFriendly(dora));   // sides are kept for the mission
    }

    [Fact]
    public void AnNpcTurnedHostileIsCountedByAKillAllStillWaitingForIt()
    {
        (MissionProgress progress, _, _) = Start(@"<mission id=""1"" title=""T"">
            <npc id=""1"" handle=""Hector"" type=""FRIENDLY"" x=""1"" y=""2"" z=""3"" rsi=""1"" hostileAfter=""1"" spawnAfter=""2"" />
            <phase>
              <objective goal=""1"" text=""Read the e-mail"" type=""Talk"" npc=""2"" />
              <objective goal=""2"" text=""Wait"" type=""Talk"" npc=""2"" />
              <objective goal=""3"" text=""Deal with Hector"" type=""DefeatAll"" />
            </phase></mission>");

        Assert.False(progress.HostilesStillToArrive());   // friendly: nothing to wait for
        progress.Complete(Goal(progress, 1));
        Assert.True(progress.HostilesStillToArrive());    // hostile now, and still to come
    }

    [Fact]
    public void AHoldOutEndsWhenItsTimeIsUp()
    {
        (MissionProgress progress, _, Action<double> wait) = Start();
        progress.entered = true;
        progress.Complete(Goal(progress, 1));

        Assert.Empty(progress.DueSurvivals());     // starts its clock
        wait(179);
        Assert.Empty(progress.DueSurvivals());
        wait(1);

        Assert.Equal(4, Assert.Single(progress.DueSurvivals()).goal);
    }

    [Fact]
    public void AHoldOutOpenFromTheStartCountsFromReachingTheArea()
    {
        (MissionProgress progress, _, Action<double> wait) = Start(@"<mission id=""1"" title=""T"">
            <phase site=""Normal""><objective goal=""1"" text=""Hold out"" type=""Survive"" seconds=""60"" /></phase></mission>");

        wait(500);
        Assert.Empty(progress.DueSurvivals());     // still on the way there
        progress.entered = true;
        Assert.Empty(progress.DueSurvivals());
        wait(60);

        Assert.Single(progress.DueSurvivals());
    }

    [Fact]
    public void ALimitRunsOutIfTheGoalIsntDoneInTime()
    {
        (MissionProgress progress, _, Action<double> wait) = Start();
        progress.entered = true;
        progress.Complete(Goal(progress, 1));
        progress.DueSurvivals();
        wait(180);
        progress.Complete(Assert.Single(progress.DueSurvivals()));

        progress.DueSurvivals();                   // goal 6 opened: its clock starts
        wait(89);
        Assert.Null(progress.Overdue());
        wait(1);
        Assert.Equal(6, progress.Overdue()!.goal);

        progress.Complete(Goal(progress, 6));
        Assert.Null(progress.Overdue());
    }

    [Fact]
    public void TimedGoalsOpenedByAnActionStartBeforeTheNextAreaTick()
    {
        (MissionProgress progress, _, Action<double> wait) = Start(@"<mission id=""1"" title=""T"">
            <phase site=""Normal"">
              <objective goal=""1"" text=""Start the encounter"" type=""Use"" object=""1"" />
              <objective goal=""2"" text=""Hold out"" type=""Survive"" seconds=""5"" after=""1"" />
              <objective goal=""3"" text=""Finish in time"" type=""Use"" object=""1"" limit=""3"" after=""1"" />
            </phase></mission>");

        wait(500); // Travel before the action does not spend either timer.
        Assert.Empty(progress.DueSurvivals());
        progress.Complete(Goal(progress, 1));
        wait(3);
        Assert.Equal(3, progress.Overdue()?.goal);
        Assert.Empty(progress.DueSurvivals());
        wait(2);
        Assert.Equal(2, Assert.Single(progress.DueSurvivals()).goal);
    }

    [Fact]
    public void WavesAndClocksAreForgottenWithTheirPhase()
    {
        (MissionProgress progress, _, Action<double> wait) = Start();
        progress.entered = true;
        progress.SpotsFor(1).Add(new double[] { 1, 2, 3 });
        progress.Complete(Goal(progress, 1));
        progress.DueSurvivals();
        wait(40);
        progress.UpdateArrivals();
        Assert.NotEmpty(progress.wavesJustArrived);

        progress.NextPhase();

        Assert.Empty(progress.wavesJustArrived);
        Assert.Empty(progress.wavesArrived);
        Assert.Empty(progress.reservedWaveSpots);
        Assert.Empty(progress.DueSurvivals());
    }

    [Fact]
    public void ArrivalsBesideAnObjectStandApartAroundIt()
    {
        double[] first = MissionRuntime.Beside(500, 40, -200, 0);
        double[] second = MissionRuntime.Beside(500, 40, -200, 1);

        Assert.Equal(300, Math.Sqrt((first[0] - 500) * (first[0] - 500) + (first[2] + 200) * (first[2] + 200)), 3);
        Assert.Equal(40, first[1]);
        Assert.True(Math.Abs(first[0] - second[0]) + Math.Abs(first[2] - second[2]) > 200);
    }
}
