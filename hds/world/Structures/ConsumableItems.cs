using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Consumable inventory items (pills) and the ability each one applies when used.
    /// The item names come from research/community-data/invitems.txt and the abilities from
    /// data/abilityIDs.csv; the pairing is by name (HealthReplenishPill2 -> PillHealthReplenish2Ability),
    /// as no retail capture of a pill being used is in research/packet-dumps.
    /// </summary>
    public static class ConsumableItems
    {
        public enum Effect
        {
            /// <summary>Restores health: the ability's ValueFrom in data/abilityIDs.csv.</summary>
            Heal,
            /// <summary>Raises maximum health for a while (needs the buff system; not applied yet).</summary>
            MaxHealthBoost,
            /// <summary>Chance to remove negative states (not applied yet).</summary>
            ConditionSweep
        }

        public struct Consumable
        {
            public UInt16 AbilityId;
            public Effect Effect;

            public Consumable(UInt16 abilityId, Effect effect)
            {
                AbilityId = abilityId;
                Effect = effect;
            }
        }

        /// <summary>aaHealPillAbility: the shared timer every pill starts (retail: 10 s).</summary>
        public const UInt16 HealPillGroup = 711;

        /// <summary>Item goid to the consumable's ability.</summary>
        public static readonly Dictionary<UInt32, Consumable> Items = new Dictionary<UInt32, Consumable>
        {
            { 2399, new Consumable(190, Effect.Heal) },            // Health Pill 1.0 -> PillHealthReplenish1Ability
            { 6897, new Consumable(191, Effect.Heal) },            // Health Pill 2.0 -> PillHealthReplenish2Ability
            { 6924, new Consumable(192, Effect.Heal) },            // Health Pill 3.0 -> PillHealthReplenish3Ability
            { 6925, new Consumable(193, Effect.Heal) },            // Health Pill 4.0 -> PillHealthReplenish4Ability
            { 7913, new Consumable(511, Effect.Heal) },            // Health Pill 5.0 -> PillHealthReplenish5Ability
            { 2407, new Consumable(187, Effect.MaxHealthBoost) },  // Health Boost 1.0 -> PillBolsterHealthAbility
            { 39411, new Consumable(672, Effect.MaxHealthBoost) }, // Health Boost 1.0 -> PillBolsterHealth1Ability
            { 39412, new Consumable(188, Effect.MaxHealthBoost) }, // Health Boost 2.0 -> PillBolsterHealth2Ability
            { 39413, new Consumable(189, Effect.MaxHealthBoost) }, // Health Boost 3.0 -> PillBolsterHealth3Ability
            { 39414, new Consumable(673, Effect.MaxHealthBoost) }, // Health Boost 4.0 -> PillBolsterHealth4Ability
            { 38539, new Consumable(659, Effect.ConditionSweep) }, // ConditionSweepGrant -> PillConditionSweep1Ability
        };

        public static bool TryGet(UInt32 goid, out Consumable consumable)
        {
            return Items.TryGetValue(goid, out consumable);
        }
    }
}
