using System;
namespace hds
{
    /// <summary>Sourced snapshot presentation only: no recovered combat initiative consumer.</summary>
    public static class AbilityDesperation
    {
        // Explicit disposable-fixture control: stable HP for exact-half snapshot QA, never a retail mechanic.
        public static bool HoldQaHealth => Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS")=="1" &&
            Environment.GetEnvironmentVariable("MXO_DESPERATION_QA_HOLD_HEALTH")=="1" &&
            (Environment.GetEnvironmentVariable("MXO_DB_NAME")??"").StartsWith("mxotest_",StringComparison.Ordinal);
        public static void LoadoutChanged(WorldClient client)
        {
            if(AbilityPassives.Level(client,1149)==0) AbilityBuffs.Remove(client,1149);
        }
        public static int Snapshot(int health,int maximum,int initiativeBonus)
        {
            if(maximum<=0) return 0;
            double influence=1.0+initiativeBonus/100.0;
            return (int)(health>maximum/2 ? influence*10 : influence*(1.0-health/(maximum*1.0))*100.0);
        }
        // Catalog recognition uses full health/zero bonus; Resolve supplies the live application snapshot.
        public static bool TryGet(ushort id,int health,int maximum,int initiativeBonus,out AbilityBuffs.Buff buff)
        {
            buff=null;
            if(id!=1149) return false;
            buff=new AbilityBuffs.Buff { Seconds=20, Modifiers=new[] {
                new AbilityBuffs.Modifier(AbilityBuffs.KindStat,1044,Snapshot(health,maximum,initiativeBonus)) } };
            return true;
        }
    }
}
