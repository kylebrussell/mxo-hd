using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Consumable inventory items (pills) and the ability each one applies when used.
    /// The item names come from research/community-data/invitems.txt and the abilities from
    /// data/abilityIDs.csv; the pairing is by name (HealthReplenishPill2 -> PillHealthReplenish2Ability).
    /// Antibiotic and condition sweep pairings are verified by raw GOB TriggeredAbility/ReqAbilityID; see
    /// docs/ABILITY-CONSUMABLE-SWEEPS.md. Retail Antibiotic 5.0 (PacketsZombiesPortingEtc line 77036) confirms:
    /// the client asked for Antibiotic5Ability (678) with the item's bag slot.
    /// </summary>
    public static class ConsumableItems
    {
        public enum Effect
        {
            /// <summary>Restores health: the ability's ValueFrom in data/abilityIDs.csv.</summary>
            Heal,
            /// <summary>Applies the antibiotic ability's initial and periodic negative-condition sweeps.</summary>
            Cure,
            /// <summary>Raises maximum health for a while (needs the buff system; not usable yet).</summary>
            MaxHealthBoost,
            /// <summary>Periodic chance to remove negative conditions.</summary>
            ConditionSweep
        }

        public struct Consumable
        {
            public UInt16 AbilityId;
            public Effect Effect;
            /// <summary>The shared timer the item starts (0 = none).</summary>
            public UInt16 Group;
            /// <summary>Length of the item's own timer and of the group timer.</summary>
            public float Seconds;

            public Consumable(UInt16 abilityId, Effect effect, UInt16 group, float seconds)
            {
                AbilityId = abilityId;
                Effect = effect;
                Group = group;
                Seconds = seconds;
            }
        }

        private const UInt16 HealPill = AbilityCooldowns.HealPill;            // 10 s in retail
        private const UInt16 AntibioticPill = AbilityCooldowns.AntibioticPill; // 8 s in retail

        /// <summary>Item goid to the consumable's ability and timers.</summary>
        public static readonly Dictionary<UInt32, Consumable> Items = new Dictionary<UInt32, Consumable>
        {
            { 2399, new Consumable(190, Effect.Heal, HealPill, 10) },             // Health Pill 1.0 -> PillHealthReplenish1Ability
            { 6897, new Consumable(191, Effect.Heal, HealPill, 10) },             // Health Pill 2.0 -> PillHealthReplenish2Ability
            { 6924, new Consumable(192, Effect.Heal, HealPill, 10) },             // Health Pill 3.0 -> PillHealthReplenish3Ability
            { 6925, new Consumable(193, Effect.Heal, HealPill, 10) },             // Health Pill 4.0 -> PillHealthReplenish4Ability
            { 7913, new Consumable(511, Effect.Heal, HealPill, 10) },             // Health Pill 5.0 -> PillHealthReplenish5Ability
            { 39406, new Consumable(674, Effect.Cure, AntibioticPill, 8) },       // Antibiotic 1.0
            { 39407, new Consumable(675, Effect.Cure, AntibioticPill, 8) },       // Antibiotic 2.0
            { 39408, new Consumable(676, Effect.Cure, AntibioticPill, 8) },       // Antibiotic 3.0
            { 39409, new Consumable(677, Effect.Cure, AntibioticPill, 8) },       // Antibiotic 4.0
            { 39410, new Consumable(678, Effect.Cure, AntibioticPill, 8) },       // Antibiotic 5.0 (retail capture)
            { 2407, new Consumable(187, Effect.MaxHealthBoost, HealPill, 10) },   // Health Boost 1.0 -> PillBolsterHealthAbility
            { 39411, new Consumable(672, Effect.MaxHealthBoost, HealPill, 10) },  // Health Boost 1.0 -> PillBolsterHealth1Ability
            { 39412, new Consumable(188, Effect.MaxHealthBoost, HealPill, 10) },  // Health Boost 2.0 -> PillBolsterHealth2Ability
            { 39413, new Consumable(189, Effect.MaxHealthBoost, HealPill, 10) },  // Health Boost 3.0 -> PillBolsterHealth3Ability
            { 39414, new Consumable(673, Effect.MaxHealthBoost, HealPill, 10) },  // Health Boost 4.0 -> PillBolsterHealth4Ability
            { 38539, new Consumable(659, Effect.ConditionSweep, HealPill, 10) },  // ConditionSweepGrant -> PillConditionSweep1Ability
        };

        public static bool TryGet(UInt32 goid, out Consumable consumable)
        {
            return Items.TryGetValue(goid, out consumable);
        }

        /// <summary>Heal pills, antibiotics and the condition sweep grant can be used.</summary>
        public static bool IsUsable(Consumable consumable)
        {
            return consumable.Effect == Effect.Heal || consumable.Effect == Effect.Cure ||
                consumable.Effect == Effect.ConditionSweep;
        }

        // A death/revival or leave/re-entry into the same district invalidates the original cast too.
        internal static bool CastMatches(ClientData data, int world, int life, double castEnd) =>
            data.worldGeneration == world && data.lifeGeneration == life && data.castEndsAt == castEnd;

        /// <summary>Stored CharStatsPrerequisite/ReqCharLevel on the antibiotic item records.</summary>
        public static int RequiredLevel(UInt32 goid) => goid >= 39407 && goid <= 39410 ? (int)(goid - 39406) * 10 : 0;

        /// <summary>
        /// The bag slot a 80 b9 ability request uses an item from, or null. The request body (after the header)
        /// is 41 bytes; byte 36 is 01 when an item is used and byte 35 is its slot. Retail Antibiotic 5.0 from
        /// slot 0x1e: <c>80 b9 a6 02 0b 00 2f 00 .. 08 1e 01 00 00 00 00</c>; plain abilities end in
        /// <c>08 6b 00 00 00 00 00</c>.
        /// </summary>
        public static UInt16? ItemSlotOfAbilityRequest(byte[] body)
        {
            if (body == null || body.Length < 41 || body[36] != 0x01)
            {
                return null;
            }
            return body[35];
        }
    }
}
