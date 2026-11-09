using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Who attacks on sight. The NPC data has no hostility field and the client has no retail logic for it (Monolith defers
    /// to native CharMvt.isEnemy, and IsEnemy is on nearly every NPC, even civilians), so this comes from what NPCs did in the
    /// retail logs. Of 124 melee fights, 65 started with no client request: zombies 35, Special Agent 9, Corrupted 6. Listed
    /// gang names also fired unprompted (10 of 99 sightings near a peer; other names 0 of 58). Choppers: 73 sightings, none.
    /// Everything not listed stays passive until attacked, the safe default where the logs say nothing. The level limits are
    /// our choice: 64 of 82 logged attacks fit them. The logged Agents' lowest victim was level 50.
    /// </summary>
    public enum MobAggroKind
    {
        /// <summary>Only the level gap: the test list (MXO_MOBS=test), whose NPCs are placed to attack.</summary>
        Unfiltered,
        /// <summary>Attacks players it is within the level limits of.</summary>
        WithinLevelLimits,
        /// <summary>Agents: only players at or above <see cref="MobBehaviour.AgentMinPlayerLevel"/>.</summary>
        HighLevelPlayersOnly,
        Passive
    }

    public static class MobBehaviour
    {
        public const int AgentMinPlayerLevel = 50;

        /// <summary>An NPC more than this far above a player doesn't start the fight.</summary>
        public const int MaxLevelsAbovePlayer = 5;

        private static readonly HashSet<string> GangsThatFire = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Blackwood Goof", "Bell Janet", "Bell Madonna", "Assassin Blade", "Assassin Hand", "Zero One Soldier"
        };

        /// <summary>NPCs the retail logs show as talkable or as standing guard; none started a fight.</summary>
        private static readonly string[] NeverAttack =
        {
            "Club Guard", "Club Tough", "Club Sphinx", "Succubus", "Merovingian", "Lupine Lieutenant", "Ammit", "Scarlett",
            "Watchman", "Alex", "Blood Noble"
        };

        /// <summary>Classifies an NPC from mob_parsed.csv: its name and its column 21 (StealthAwareness; blank on civilians).</summary>
        public static MobAggroKind Classify(string name, string stealthAwareness)
        {
            name = name?.Trim() ?? "";
            if (name.StartsWith("Zombie ", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Corrupted", StringComparison.OrdinalIgnoreCase))
            {
                return MobAggroKind.WithinLevelLimits;
            }

            if (name.StartsWith("Agent", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Special Agent", StringComparison.OrdinalIgnoreCase))
            {
                return MobAggroKind.HighLevelPlayersOnly;
            }

            foreach (string never in NeverAttack)
            {
                if (name.Equals(never, StringComparison.OrdinalIgnoreCase))
                {
                    return MobAggroKind.Passive;
                }
            }

            if (string.IsNullOrWhiteSpace(stealthAwareness))
            {
                return MobAggroKind.Passive;
            }

            return GangsThatFire.Contains(name) ? MobAggroKind.WithinLevelLimits : MobAggroKind.Passive;
        }

        /// <summary>The kind's own rule, on top of range and the player being within LevelGap above the NPC.</summary>
        public static bool WillingAtLevels(MobAggroKind kind, int mobLevel, int playerLevel)
        {
            switch (kind)
            {
                case MobAggroKind.Unfiltered:
                    return true;
                case MobAggroKind.WithinLevelLimits:
                    return mobLevel - playerLevel <= MaxLevelsAbovePlayer;
                case MobAggroKind.HighLevelPlayersOnly:
                    return playerLevel >= AgentMinPlayerLevel;
                default:
                    return false;
            }
        }
    }
}
