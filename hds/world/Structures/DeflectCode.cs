using System;
namespace hds
{
    /// <summary>OURS: positive Determination Bonus shortens newly applied known hostile controls.
    /// The retail bonus coefficient is unresolved. Existing raw575 mitigation and total33% floor remain.</summary>
    public static class DeflectCode
    {
        public static bool KnownControl(ushort state) => state is TempMods.MezAbility or TempMods.BlindAbility or
            TempMods.ConfuseAbility or TempMods.EnrageAbility or TempMods.PowerlessAbility or TempMods.RootedAbility or
            TempMods.StunAbility or TempMods.DazeAbility or TempMods.OffBalanceAbility or TempMods.StaggeredAbility or TempMods.DisarmedAbility;

        public static int CappedBonus(int value) => Math.Clamp(value,0,(int)AbilityCatalog.Find(1047).Number("Ability/TotalMaxBuffValue",130));
        public static int PlayerBonus(WorldClient target) => Math.Max(0,AbilityStats.Player(target,1047));

        public static float Duration(float seconds,int casterLevel,int rawDetermination,int bonus,ushort state)
        {
            float mitigated=AbilityControlDurations.Mitigate(seconds,casterLevel,rawDetermination);
            if(seconds<=0 || !KnownControl(state) || bonus<=0)return mitigated;
            return (float)Math.Max(seconds*.33,mitigated*(1-Math.Min(CappedBonus(bonus),100)/100.0));
        }
    }
}
