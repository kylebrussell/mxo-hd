using hds;

namespace HeadlessClient;

/// <summary>An authored escort exit can advance without a physical elevator teleport.</summary>
public static class ZionAfter1212EscortProof
{
    private static MissionObjective? Objective(MissionProgress.SavedState before, Mission mission, int goal)
    {
        if (mission.id != before.Mission || string.IsNullOrWhiteSpace(before.Identity) ||
            before.Phase < 0 || before.Phase >= mission.phases.Count || before.Done.Contains(goal)) return null;
        var found = mission.phases[before.Phase].missionObjectives.Where(o => o.goal == goal).ToArray();
        if (found.Length != 1) return null;
        var objective = found[0];
        return objective.type == MissionGoalType.Escort && objective.escortTo == "exit" &&
            (objective.afterGoal < 0 || before.Done.Contains(objective.afterGoal)) ? objective : null;
    }

    public static bool CanApproach(MissionProgress.SavedState state, Mission mission, int goal)
    {
        var objective = Objective(state, mission, goal);
        return objective != null && state.Flags.TryGetValue("entered", out bool entered) && entered &&
            (!state.Flags.TryGetValue("failed", out bool failed) || !failed) && objective.Npcs.Count > 0 &&
            objective.Npcs.All(id => id != 0 && state.Following.Contains(id) &&
                state.Mobs.Count(m => m.Npc == id && !m.Dead && m.Health > 0) == 1);
    }

    // This is only an outcome to await after an authorized approach, never permission to send a door request.
    public static bool CompletedPendingAdvance(MissionProgress.SavedState before, MissionProgress.SavedState? current,
        Mission mission, int goal)
    {
        var objective = Objective(before, mission, goal);
        return objective != null && current != null && current.Mission == before.Mission && current.Phase == before.Phase &&
            current.Identity == before.Identity && current.Started == before.Started && current.Instance == before.Instance &&
            current.Done.Contains(goal) && current.EverDone.Contains(goal) &&
            current.Flags.GetValueOrDefault("entered") && !current.Flags.GetValueOrDefault("failed") &&
            current.Flags.GetValueOrDefault("waitingForExit") && current.Flags.GetValueOrDefault("goalsCompleteSent") &&
            current.Flags.GetValueOrDefault("leaveNotNeeded") &&
            mission.phases[before.Phase].missionObjectives.Where(o => !o.optional).All(o => current.Done.Contains(o.goal)) &&
            objective.Npcs.Count > 0 && objective.Npcs.All(id => id != 0 && current.Following.Contains(id) &&
                current.Mobs.Count(m => m.Npc == id && !m.Dead && m.Health > 0) == 1);
    }

    public static bool Advanced(MissionProgress.SavedState before, MissionProgress.SavedState? current,
        Mission mission, int goal, IReadOnlyList<byte[]> records, int cursor)
    {
        return Objective(before, mission, goal) != null && cursor >= 0 && cursor <= records.Count &&
            goal is > 0 and <= 255 && records.Skip(cursor).Any(r => r.Length >= 6 &&
                r[0] == 0x80 && r[1] == 0xa0 && r[2] == goal && r[3] == 6 && r[4] == 0 && r[5] == 1) &&
            current != null && current.Mission == before.Mission && current.Identity == before.Identity &&
            current.Started == before.Started && current.Phase == before.Phase + 1 &&
            current.Phase < mission.phases.Count && current.EverDone.Contains(goal);
    }
}
