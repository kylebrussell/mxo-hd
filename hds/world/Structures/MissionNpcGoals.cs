using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Dev-only listing of the active mission's spawned NPCs and the open goals each one serves. Hostile mission NPCs carry
    /// no client-visible goal marker, so a tool driving the client (the browser client, NativeQa) cannot otherwise tell the
    /// kill target from ordinary hostiles. Membership follows the runtime exactly: named Talk/Defeat/Loot/Get/Give/Escort/
    /// Protect goals by MissionNpc id, and DefeatAll by <see cref="MissionRuntime.CountsForDefeatAll"/>.
    /// </summary>
    internal static class MissionNpcGoals
    {
        internal const string Command = "?missionnpcs";

        internal static bool Allowed(string devCommands) => devCommands == "1";

        /// <summary>One line per spawned mission NPC, in spawn order, and a header naming the mission and phase.</summary>
        internal static List<string> Describe(MissionProgress progress, IReadOnlyList<Mob> mobs, Func<Mob, ushort?> view)
        {
            var lines = new List<string>();
            if (progress == null)
            {
                lines.Add("[MISSION-NPCS] no active mission");
                return lines;
            }
            var open = progress.OpenObjectives().ToList();
            lines.Add($"[MISSION-NPCS] mission {progress.mission.id} '{progress.mission.title}' phase {progress.phaseIndex + 1}: " +
                $"{mobs.Count} spawned, open goals {(open.Count == 0 ? "none" : string.Join(",", open.Select(o => $"{o.goal}:{o.type}")))}");
            foreach (Mob mob in mobs)
            {
                var goals = open.Where(o => o.type == MissionGoalType.DefeatAll
                        ? !mob.isFriendly && MissionRuntime.CountsForDefeatAll(progress, mob, o)
                        : o.Has(mob.missionNpcId))
                    .Select(o => $"{o.goal}:{o.type}").ToList();
                ushort? v = view(mob);
                lines.Add($"[MISSION-NPC] npc {(mob.missionNpcId == 0 ? "-" : mob.missionNpcId.ToString())} '{mob.getName()}' " +
                    $"view {(v.HasValue ? v.Value.ToString() : "-")} hp {mob.getHealthC()}/{mob.getHealthM()} " +
                    $"{(mob.getIsDead() ? "dead" : "alive")} {(mob.isFriendly ? "friendly" : "hostile")} " +
                    $"goals {(goals.Count == 0 ? "none" : string.Join(",", goals))}");
            }
            return lines;
        }

        internal static List<string> Describe(WorldClient client)
        {
            List<Mob> mobs;
            lock (client.playerData.missionMobs)
                mobs = new List<Mob>(client.playerData.missionMobs);
            return Describe(client.playerData.missionProgress, mobs,
                mob => (ushort?)client.viewMan.FindViewForEntity(mob.getEntityId())?.ViewID);
        }
    }
}
