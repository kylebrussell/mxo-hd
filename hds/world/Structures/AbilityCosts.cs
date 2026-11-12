using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Inner Strength cost of each ability, measured in the retail captures under research/packet-dumps: the drop
    /// in the own-view InnerStrengthAvailable update (own-view attribute 25) right after the client's
    /// <c>80 b9 &lt;ability&gt;</c>, taking the value most uses agree on (regeneration ticks of 1-6 a second can
    /// hide a point or two in single samples). E.g. Overclock/Avoidance, abilities_useoverclocked: IS 231 after
    /// the cast; entilsar lastday2 and the abilitychangecoder log: 20 every time. Abilities not in the table cost
    /// <see cref="Default"/>.
    /// </summary>
    public static class AbilityCosts
    {
        /// <summary>Cost of abilities without a retail measurement (the old flat cost).</summary>
        public const UInt16 Default = 20;

        /// <summary>Ability id (abilityIDs.csv) to its retail Inner Strength cost.</summary>
        public static readonly Dictionary<UInt16, UInt16> Retail = new Dictionary<UInt16, UInt16>
        {
            { 6, 20 },     // Consistent Technique
            { 10, 20 },    // Hyper-Dodge
            { 12, 40 },    // Hyper-Speed
            { 72, 40 },    // Resist Combat (one sample pair: 304 before the 6 s cast, 264 after it)
            { 97, 12 },    // UI Lag 1.0
            { 135, 20 },   // Martial Arts
            { 184, 25 },   // Hyper-Sprint
            { 197, 12 },   // Head Butt
            { 198, 20 },   // Cheap Shot
            { 217, 40 },   // Evade Combat
            { 260, 30 },   // Misdirect Punch
            { 261, 30 },   // Machinegun Fist Combo
            { 262, 25 },   // Piston Kicks
            { 267, 30 },   // Sky High Sidekick
            { 314, 35 },   // Upgrade Attacks
            { 327, 20 },   // Overclock (Avoidance)
            { 329, 11 },   // Logic Blast 3.0
            { 330, 13 },   // Logic Barrage 4.0
            { 333, 24 },   // Stun 2.0
            { 336, 14 },   // Logic Bomb 2
            { 337, 18 },   // Logic Bomb 3
            { 344, 20 },   // Combat Hacking
            { 345, 40 },   // Range Booster
            { 357, 20 },   // Logic Bomb 4
            { 359, 25 },   // Code Nuke
            { 396, 25 },   // Crash
            { 428, 35 },   // Suicidal Butterfly
            { 429, 40 },   // Extreme Falling Kick
            { 430, 30 },   // Wooden Dummy Drill
            { 466, 20 },   // Plague Zone 2
            { 861, 50 },   // Balance
            { 868, 20 },   // Purity
            { 870, 40 },   // Spinning Flower
            { 967, 5 },    // Mobius Code
            { 1063, 20 },  // Kung Fu Master buff
            { 1064, 20 },  // Kung Fu Grand Master buff
            { 1067, 20 },  // Aikido Master buff
            { 1084, 20 },  // Ballista buff
            { 1087, 20 },  // Howitzer buff
            { 1088, 40 },  // Destroyer debuff
            { 1089, 20 },  // Team Healer buff
            { 1092, 20 },  // Corpsman buff
            { 1099, 20 },  // Hyper-Block
            { 1100, 20 },  // Hyper-Deflect
            { 1101, 20 },  // Hyper-Sense
            { 1102, 50 },  // Determinate
            { 1237, 15 },  // Zombie Virus 3 Bite
        };

        /// <summary>
        /// The cost after InnerstrengthCostModifier (117, "Inner Strength Cost Reduction", ValueType 1 percent): positive
        /// lowers it ("Efficiency ... decrease the Inner Strength cost"), Inefficiency Field's negative raises it. The
        /// native cost path is not reconstructed; truncating the percent delta like the vital bonuses is this server's choice.
        /// </summary>
        public static UInt16 Effective(WorldClient client, UInt16 abilityId)
        {
            int cost = Cost(abilityId);
            if (client?.playerInstance == null || client.playerData == null) return (UInt16)cost;
            return (UInt16)Math.Clamp(cost - AbilityStats.PercentDelta(cost, AbilityStats.Player(client, 117)), 0, UInt16.MaxValue);
        }

        public static UInt16 Cost(UInt16 abilityId)
        {
            UInt16 canonical = AbilityCatalog.Canonical(abilityId);
            // CompileActivate is a tool/UI session with no stored IS-cost field, not a generic cast.
            if (canonical == CompileSession.Ability) return 0;
            if (Retail.TryGetValue(canonical, out UInt16 cost)) return cost;
            var entry = AbilityCatalog.Find(canonical);
            return entry != null && entry.Has("Ability/AbilityInnerStrengthCost")
                ? (UInt16)Math.Clamp(entry.Number("Ability/AbilityInnerStrengthCost"), 0, UInt16.MaxValue) : Default;
        }
    }
}
