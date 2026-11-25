using System.Collections.Generic;
using hds;
using Xunit;

namespace hds.Tests;

public class MissionNpcGoalsTests
{
    private static Mob Npc(ushort id, string name, bool friendly = false, bool dead = false)
    {
        var mob = new Mob { missionNpcId = id, isFriendly = friendly };
        mob.setName(name);
        mob.setHealthM(500);
        mob.setHealthC(dead ? (ushort)0 : (ushort)500);
        mob.setIsDead(dead);
        return mob;
    }

    [Fact]
    public void ListsEachSpawnedNpcWithTheOpenGoalsItServes()
    {
        var mission = new Mission { id = 42, title = "Test Op" };
        mission.npcs.Add(new MissionNpc { id = 1, friendly = true });
        mission.npcs.Add(new MissionNpc { id = 2 });
        mission.npcs.Add(new MissionNpc { id = 3 });
        mission.npcs.Add(new MissionNpc { id = 4, wave = 1 });
        var phase = new MissionPhase();
        phase.missionObjectives.Add(new MissionObjective { goal = 0, type = MissionGoalType.Talk, npcId = 1 });
        phase.missionObjectives.Add(new MissionObjective { goal = 1, type = MissionGoalType.Defeat, npcId = 2 });
        phase.missionObjectives.Add(new MissionObjective { goal = 2, type = MissionGoalType.DefeatAll, waves = { 0 } });
        phase.missionObjectives.Add(new MissionObjective { goal = 3, type = MissionGoalType.Loot, npcId = 3, afterGoal = 1 });
        mission.phases.Add(phase);
        var progress = new MissionProgress(mission);

        var contact = Npc(1, "Contact", friendly: true);
        var target = Npc(2, "Target");
        var guard = Npc(3, "Guard");
        var reinforcement = Npc(4, "Reinforcement");
        var generated = Npc(0, "Generated Hostile");
        progress.hostiles.Add(generated);
        progress.hostileWave[generated] = 0;
        var views = new Dictionary<Mob, ushort> { [contact] = 7, [target] = 8 };

        var lines = MissionNpcGoals.Describe(progress, new[] { contact, target, guard, reinforcement, generated },
            mob => views.TryGetValue(mob, out ushort v) ? v : null);

        Assert.Equal("[MISSION-NPCS] mission 42 'Test Op' phase 1: 5 spawned, open goals 0:Talk,1:Defeat,2:DefeatAll", lines[0]);
        Assert.Equal("[MISSION-NPC] npc 1 'Contact' view 7 hp 500/500 alive friendly goals 0:Talk", lines[1]);
        // The kill target is the only hostile named by a Defeat goal; DefeatAll also counts it.
        Assert.Equal("[MISSION-NPC] npc 2 'Target' view 8 hp 500/500 alive hostile goals 1:Defeat,2:DefeatAll", lines[2]);
        // Loot opens only after the Defeat goal; wave 1 is outside DefeatAll waves=0.
        Assert.Equal("[MISSION-NPC] npc 3 'Guard' view - hp 500/500 alive hostile goals 2:DefeatAll", lines[3]);
        Assert.Equal("[MISSION-NPC] npc 4 'Reinforcement' view - hp 500/500 alive hostile goals none", lines[4]);
        Assert.Equal("[MISSION-NPC] npc - 'Generated Hostile' view - hp 500/500 alive hostile goals 2:DefeatAll", lines[5]);

        progress.Complete(phase.missionObjectives[1]);
        lines = MissionNpcGoals.Describe(progress, new[] { guard }, _ => null);
        Assert.Equal("[MISSION-NPC] npc 3 'Guard' view - hp 500/500 alive hostile goals 2:DefeatAll,3:Loot", lines[1]);
    }

    [Fact]
    public void ReportsNoMissionAndIsDevGated()
    {
        Assert.Equal(new[] { "[MISSION-NPCS] no active mission" }, MissionNpcGoals.Describe(null, new Mob[0], _ => null));
        Assert.True(MissionNpcGoals.Allowed("1"));
        Assert.False(MissionNpcGoals.Allowed(null));
        Assert.False(MissionNpcGoals.Allowed("0"));
    }
}
