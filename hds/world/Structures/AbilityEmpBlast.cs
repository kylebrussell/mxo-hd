using System;
using hds.shared;

namespace hds
{
    /// <summary>general.py EMPBlast_DirectObject. Production NPC grant owners remain unidentified.</summary>
    public static class AbilityEmpBlast
    {
        public const ushort Ability = 1025, Weakness = 1026;
        public static ushort Damage(Mob target) => target != null && target.HasAbility(Weakness) ? (ushort)5000 : (ushort)0;

        // CastDirectObjects4 is the existing caster-centred hostile selector (also Reactive Code942).
        public static void Land(WorldClient caster)
        {
            double x=0,y=0,z=0;
            NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(),ref x,ref y,ref z);
            var template=AbilityCatalog.Find(Ability);
            foreach(var (mob,view) in AbilityHostile.MobsInArea(caster,x,y,z,template.Number("Ability/RadiusOfEffect")))
            {
                ushort damage=Damage(mob); // Neutral fixed script damage: no generic roll/influence/typed resistance.
                if(damage > 0) CombatManager.GetHandler(caster).ApplyAbilityToNpc(mob,view,damage,(uint)template.Number("Ability/ExecutionFX"));
                Output.WriteDebugLog("[EMP] " + view + " damage=" + damage + " hp=" + mob.getHealthC());
            }
            // Player subjects cannot own this NPC-only marker: the script's ordinary branch is zero.
        }

        public static bool FixtureAllowed(string dev,string database) => dev == "1" &&
            database != null && database.StartsWith("mxotest_",StringComparison.Ordinal);
        public static string Fixture(WorldClient caller,bool granted,ushort? health = null)
        {
            if(!FixtureAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config.dbParams.DatabaseName))
                return "EMP fixture requires MXO_DEV_COMMANDS=1 and a disposable mxotest_ database.";
            var mob=AbilityTargets.FindMob(caller,caller.playerData.currentSelectedTargetViewId,out _);
            if(mob==null || mob.getIsDead() || mob.isFriendly) return "Select a live hostile NPC first.";
            mob.SetAbilityGrant(Weakness,granted);
            if(health.HasValue) { mob.setHealthM(health.Value); mob.setHealthC(health.Value); }
            return "EMP fixture weakness=" + (granted?"1":"0") + "; HP=" + mob.getHealthC() + ".";
        }
    }
}
