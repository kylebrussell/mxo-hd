using System;

namespace hds
{
    /// <summary>Active buff/debuff totals used by the existing script-derived combat formulas.</summary>
    public static class AbilityStats
    {
        public static int Player(WorldClient client, UInt16 stat) => AbilityBuffs.Total(client, stat);
        public static int Npc(Mob mob, UInt16 stat) => TimedEffects.ForMob(mob).Effects.Total(stat);
        public static int Attack(WorldClient client, bool ranged) => Player(client, 163) + Player(client, ranged ? (UInt16)524 : (UInt16)522) +
            (TimedEffects.ForPlayer(client).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0);
        public static int Attack(Mob mob, bool ranged) => Npc(mob, 163) + Npc(mob, ranged ? (UInt16)524 : (UInt16)522) +
            (TimedEffects.ForMob(mob).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0);
        public static int Defense(WorldClient client, bool ranged) => Player(client, 959) + Player(client, ranged ? (UInt16)1010 : (UInt16)1009);
        public static int Defense(Mob mob, bool ranged) => Npc(mob, 959) + Npc(mob, ranged ? (UInt16)1010 : (UInt16)1009);
        public static int Toughness(WorldClient client, bool ranged) => Player(client, 144) + Player(client, ranged ? (UInt16)181 : (UInt16)180);
        public static int Toughness(Mob mob, bool ranged) => Npc(mob, 144) + Npc(mob, ranged ? (UInt16)181 : (UInt16)180);
    }
}
