using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>Unclaimed NPC loot belongs to the killer and stays with the particular death of its source.</summary>
    public sealed class NpcLootCache
    {
        public sealed class Corpse
        {
            public readonly Mob Source;
            private readonly int deathGeneration;
            public UInt32 Info;
            public UInt32 MissionItem;
            public readonly List<UInt32> Items;

            public Corpse(Mob source, UInt32 info, UInt32 missionItem, IEnumerable<UInt32> items)
            {
                Source = source;
                deathGeneration = source.DeathGeneration;
                Info = info;
                MissionItem = missionItem;
                Items = new List<UInt32>(items);
            }

            public bool Available => Source.getIsDead() && Source.getIsSpawned() &&
                                     Source.DeathGeneration == deathGeneration;
        }

        private readonly Dictionary<UInt64, Corpse> corpses = new Dictionary<UInt64, Corpse>();
        private Corpse current;

        // Callers hold this cache's lock while changing the selected corpse or granting its contents.
        public Corpse Current => current != null && current.Available ? current : null;

        public Corpse Add(Mob source, UInt32 info, UInt32 missionItem, IEnumerable<UInt32> items)
        {
            foreach (UInt64 entity in new List<UInt64>(corpses.Keys))
            {
                if (!corpses[entity].Available)
                {
                    corpses.Remove(entity);
                }
            }
            current = new Corpse(source, info, missionItem, items);
            corpses[source.getEntityId()] = current;
            return current;
        }

        public Corpse Select(UInt64 entity)
        {
            corpses.TryGetValue(entity, out current);
            return Current;
        }

        public void Forget(Mob source)
        {
            corpses.Remove(source.getEntityId());
            if (current?.Source == source)
            {
                current = null;
            }
        }
    }
}
