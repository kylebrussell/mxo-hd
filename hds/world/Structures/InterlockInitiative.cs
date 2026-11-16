using System;
using hds.shared;
namespace hds
{
    /// <summary>OURS: known Initiative totals add attack-base points in interlock, not free fire or free casts.
    /// The native tactic-total helper can add1044; its authoritative retail caller remains unknown.</summary>
    public static class InterlockInitiative
    {
        public static int Bonus(WorldClient actor) => Math.Max(0, AbilityStats.Player(actor,1044));
        public static int Add(int baseline,int bonus) => (int)Math.Clamp((long)baseline+Math.Max(0,bonus),int.MinValue,int.MaxValue);
        public static int Attack(WorldClient actor,AbilityItem ability)
        {
            int baseline=AbilityStats.SpecialAttack(actor,ability);
            // Escapes/withdrawal retain their existing contested-withdraw policy.
            if(ability!=null&&AbilityCatalog.Find(ability.getAbilityID())?.Number("Ability/TestFunction",-1)==6)return baseline;
            int bonus=Bonus(actor),total=Add(baseline,bonus);
            if(bonus!=0) Output.WriteDebugLog("[INITIATIVE OURS] "+TimedEffects.PlayerName(actor)+" base="+baseline+" bonus="+bonus+" total="+total);
            return total;
        }
        // Read-only paired draws. Does not replace a live fight's RNG or change health/cooldowns.
        public static string Diagnostic(WorldClient actor)
        {
            if(actor==null||!ReferenceEquals(actor,Store.currentClient)||!actor.Alive||actor.playerData.worldChangePending||
                actor.playerData.DistrictIsLA()||actor.playerData.duelDeath||
                !TypedAttackDiagnostic.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                    actor.playerData.getCharID(),actor.playerData.getOnWorld(),actor.playerData.isDead,TimedEffects.PlayerName(actor)))
                return "Initiative diagnostic requires a live non-TheNeo character in a dev mxotest_ fixture.";
            int baseline=AbilityStats.SpecialAttack(actor,null),bonus=Bonus(actor),total=Add(baseline,bonus),hits=0;
            var rng=new Random(731);
            for(int i=0;i<1000;i++)
                if(CombatInterlock.RollHit(rng,CombatTactic.Speed,50,CombatTactic.Speed,50,out _,out _,CombatTacticType.Normal,total,0))hits++;
            return "Initiative OURS base="+baseline+" bonus="+bonus+" total="+total+" seeded="+hits+"/1000; read-only, no live RNG/HP changed.";
        }
    }
}
