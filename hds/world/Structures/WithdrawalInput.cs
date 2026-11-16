using System;
namespace hds
{
    /// <summary>OURS ordinary44 caller for primary direct1005 withdrawal input.
    /// Linked1045 coefficient is unrecovered/unimplemented. TF6 retains its existing partial path.</summary>
    public static class WithdrawalInput
    {
        public static bool Ordinary(bool requested,bool opening,AbilityItem queued,bool ordinaryRpc) => requested&&ordinaryRpc&&!opening&&
            (queued==null||AbilityCatalog.Find(queued.getAbilityID())?.Number("Ability/TestFunction",-1)!=6);
        public static int Attack(WorldClient actor,AbilityItem queued,bool ordinary)
        {
            if(!ordinary)return InterlockInitiative.Attack(actor,queued);
            // Primary direct1005 only. The linked1045 coefficient is unrecovered and is NOT guessed here.
            int direct=AbilityStats.Player(actor,1005);
            Output.WriteDebugLog("[WITHDRAW INPUT OURS] "+TimedEffects.PlayerName(actor)+" direct="+direct+" input="+direct);
            return direct;
        }
        // combat_calculations.GetTacticWithdrawTacticsMod; DEFENSE is this server's Grab button.
        public static double TacticFactor(CombatTactic tactic) => tactic==CombatTactic.Power?1.2:tactic==CombatTactic.Grab?0.9:1.0;
        public static int AttackBase(int level,int input,CombatTactic tactic) => (int)(Math.Max(0,level+input)*TacticFactor(tactic));
    }
}
