using hds.shared;

namespace hds
{
    /// <summary>Only UI Lag97: shipped selector4 says enemies within the radius of self.
    /// Completion-time centre and existing floor/view/PvP predicates remain server policy.
    /// No emitter: each legal recipient receives the existing timed effect once.</summary>
    internal static class UiLagArea
    {
        internal const ushort Ability = 97;
        internal static void Land(WorldClient caster, AbilityItem ability)
        {
            if (ability?.getAbilityID() != Ability || caster == null || !caster.Alive ||
                !caster.playerData.getOnWorld() || caster.playerData.worldChangePending ||
                caster.playerData.DistrictIsLA() || caster.playerData.isDead || caster.playerData.duelDeath ||
                !ControlActions.CanUseAbilities(caster)) return;
            double x=0,y=0,z=0;
            NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(),ref x,ref y,ref z);
            int world=caster.playerData.worldGeneration,life=caster.playerData.lifeGeneration;
            bool Current()=>caster.playerData.worldGeneration==world && caster.playerData.lifeGeneration==life &&
                caster.playerData.getOnWorld() && !caster.playerData.worldChangePending && !caster.playerData.isDead && !caster.playerData.duelDeath && ControlActions.CanUseAbilities(caster);
            double radius=AbilityRadius.TargetMetres(caster,Ability);
            if(radius<=0)return;
            // Selection is intentionally absent: a selected distant enemy cannot move the centre.
            var mobs=AbilityHostile.MobsInArea(caster,x,y,z,radius);
            var players=AbilityHostile.PlayersInArea(caster,x,y,z,radius);
            foreach(var (mob,view) in mobs)
            {
                if(!Current())return;
                if(mob.pet==null)AbilityTargets.LandOne(caster,ability,view,origin:AbilityCastOrigin.NormalFreeCast);
            }
            foreach(var target in players)
            {
                if(!Current())return;
                PvpCombat.LandAbilityOn(caster,ability,target,origin:AbilityCastOrigin.NormalFreeCast);
            }
        }
    }
}
