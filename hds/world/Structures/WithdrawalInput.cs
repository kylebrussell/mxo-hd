using System;
namespace hds
{
    /// <summary>OURS typed ordinary44 withdrawal input. Retail raw1005/bonus1045 relation is primary;
    /// linked coefficient and server caller are unrecovered. TF6 escapes retain their existing partial path.</summary>
    public static class WithdrawalInput
    {
        public static bool Ordinary(bool requested,bool opening,AbilityItem queued,bool ordinaryRpc) => requested&&ordinaryRpc&&!opening&&
            (queued==null||AbilityCatalog.Find(queued.getAbilityID())?.Number("Ability/TestFunction",-1)!=6);
        public static int Combine(int level,int direct,int percent) => direct+AbilityStats.PercentDelta(level,percent);
        public static int Attack(WorldClient actor,AbilityItem queued,bool ordinary)
        {
            if(!ordinary)return InterlockInitiative.Attack(actor,queued);
            int direct=AbilityStats.Player(actor,1005),percent=AbilityStats.Player(actor,1045);
            int delta=AbilityStats.PercentDelta(actor.playerInstance.Level.getValue()[0],percent),total=Combine(actor.playerInstance.Level.getValue()[0],direct,percent);
            Output.WriteDebugLog("[WITHDRAW INPUT OURS] "+TimedEffects.PlayerName(actor)+" direct="+direct+" bonus="+percent+" delta="+delta+" input="+total);
            return total;
        }
        // combat_calculations.GetTacticWithdrawTacticsMod; DEFENSE is this server's Grab button.
        public static double TacticFactor(CombatTactic tactic) => tactic==CombatTactic.Power?1.2:tactic==CombatTactic.Grab?0.9:1.0;
        public static int AttackBase(int level,int input,CombatTactic tactic) => (int)(Math.Max(0,level+input)*TacticFactor(tactic));
    }
}
