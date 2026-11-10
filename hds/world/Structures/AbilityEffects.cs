using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// research/client-scripts/ability/effects.py: what abilities do to items and other abilities. Corroding an item's
    /// stability, destroying an item (a player's from their equipment, an NPC's weapon), and disabling an ability with
    /// the abilities that depend on it. Also the Crash virus's end (tempmods.py GenericCrashTerm), the only caller of
    /// CorrodeItem with retail's numbers.
    /// </summary>
    public static class AbilityEffects
    {
        public enum Corrosion
        {
            /// <summary>No item given.</summary>
            NoItem,
            /// <summary>Stability 0 means indestructible: nothing happens (effects.py 11-13).</summary>
            Indestructible,
            /// <summary>Stability went down (or up, for a negative amount).</summary>
            Corroded,
            /// <summary>Stability left at or below 0, or above 7: "Item has no stability left" (effects.py 16-17).</summary>
            NoStabilityLeft,
        }

        /// <summary>The highest stability the script treats as valid (effects.py 16: stability &gt; 7 means none left).</summary>
        public const int MaxStability = 7;

        /// <summary>CorrodeItem (effects.py 8-23): stability less the amount; 0 is indestructible.</summary>
        public static Corrosion CorrodeItem(bool hasItem, int stability, int amount, out int newStability)
        {
            newStability = stability;
            if (!hasItem)
            {
                return Corrosion.NoItem;
            }
            if (stability == 0)
            {
                return Corrosion.Indestructible;
            }
            newStability = stability - amount;
            return newStability <= 0 || newStability > MaxStability ? Corrosion.NoStabilityLeft : Corrosion.Corroded;
        }

        public enum Destruction
        {
            /// <summary>The player's item is removed from the slot.</summary>
            PlayerItemRemoved,
            /// <summary>The NPC loses its weapon (slot 109).</summary>
            NpcWeaponRemoved,
            /// <summary>An NPC has nothing in other slots: SUCCESS, nothing removed (effects.py 36-37).</summary>
            NpcSlotEmpty,
            /// <summary>A player's indestructible item (stability 0): DEFLECTED.</summary>
            Indestructible,
        }

        /// <summary>DestroyItem (effects.py 27-39).</summary>
        public static Destruction DestroyItem(bool subjectIsPlayer, int itemStability, int slot)
        {
            if (subjectIsPlayer)
            {
                return itemStability == 0 ? Destruction.Indestructible : Destruction.PlayerItemRemoved;
            }
            return slot == Virii.WeaponSlot ? Destruction.NpcWeaponRemoved : Destruction.NpcSlotEmpty;
        }

        /// <summary>DISABLE_FX = FX_CHARACTER_GENERIC_ABILITY_DOWNGRADE, played on the subject when an ability is disabled.</summary>
        public const UInt32 DisableFx = 0x28000437;

        /// <summary>The tempmod procs a disable puts on (effects.py 52, 60): init GenericDisableInit, end GenericDisableTerm.</summary>
        public static readonly string[] DisableProcs = { "GenericDisableInit", "GenericDisableTerm", "", "", "" };

        /// <summary>
        /// DisableAbility with DisableDependents (effects.py 45-70): every dependent of the ability that the subject has is
        /// disabled for the same time, then the ability itself. Returns the abilities to disable, dependents first.
        /// The dependents are the engine's (abilitylib.getAbilityDependents), not in the client's data.
        /// </summary>
        public static List<UInt16> AbilitiesToDisable(UInt16 ability, IEnumerable<UInt16> dependents, Func<UInt16, bool> subjectHas)
        {
            List<UInt16> list = new List<UInt16>();
            if (dependents != null)
            {
                foreach (UInt16 d in dependents)
                {
                    if (subjectHas(d))
                    {
                        list.Add(d);
                    }
                }
            }
            list.Add(ability);
            return list;
        }

        /// <summary>
        /// GenericCrashTerm (tempmods.py 1094-1120): when the Crash timer runs out, each equipped item corrodes by
        /// VIRUSCRASH_CORRODE_VALUE (2) on a randrange(1, 100) roll at or under VIRUSCRASH_CORRODE_CHANCEVALUE (100, so
        /// always), with VIRUSCRASH_FX, and the player goes to their last hardline. Returns each item's new stability
        /// (indestructible items keep theirs).
        /// </summary>
        public static int[] CrashCorrosion(int[] equippedStability, Random random)
        {
            const int corrodeValue = 2;     // tempmods.py 22 VIRUSCRASH_CORRODE_VALUE
            const int chanceValue = 100;    // tempmods.py 24 VIRUSCRASH_CORRODE_CHANCEVALUE
            int[] result = new int[equippedStability.Length];
            for (int i = 0; i < equippedStability.Length; i++)
            {
                int roll = random.Next(1, 100);
                result[i] = equippedStability[i];
                if (roll > chanceValue)
                {
                    continue;
                }
                CorrodeItem(true, equippedStability[i], corrodeValue, out result[i]);
            }
            return result;
        }
    }
}
