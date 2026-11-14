using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
    /// <summary>One selected mission container: grant first, acknowledge exact claims, then run goal callbacks.
    /// MissionPersistence.Mutate owns the checkpoint/output transaction around this operation.</summary>
    internal static class MissionContainerClaim
    {
        internal static ushort FreeSlot(IEnumerable<int> occupied, IEnumerable<ushort> reserved) =>
            hds.databases.MyWorldDbAccess.LowestFreeBagSlot(occupied.Concat(reserved.Select(s => (int)s)).ToHashSet());

        internal static bool Take(MissionObjectState state, IEnumerable<MissionObjective> objectives, ushort instance,
            Func<uint,bool> give, Action<(byte[] Data,uint Item)[],bool> acknowledge, Action opened,
            Action<MissionObjective> complete)
        {
            if (!state.lootOpen) return false;
            var claimed = new List<(byte[] Data,uint Item)>();
            var completed = new List<MissionObjective>();
            bool full = false;
            foreach (var objective in objectives.Where(o => o.type == MissionGoalType.Search && o.objectId == state.def.id).ToArray())
            {
                if (objective.item != 0)
                {
                    if (!give(objective.item)) { full = true; break; }
                    // Exactly the instance data used by MissionRuntime's original 81 19 window.
                    claimed.Add((new byte[] { 0, (byte)instance, (byte)(instance >> 8), 0x82 }, objective.item));
                }
                completed.Add(objective);
            }
            state.lootOpen = full;
            acknowledge(claimed.ToArray(), !full);
            if (!full) opened();
            foreach (var objective in completed) complete(objective);
            return true;
        }
    }
}
