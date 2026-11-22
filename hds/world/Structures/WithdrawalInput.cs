using System;
namespace hds
{
    /// <summary>Ordinary Withdraw input from direct 1005 and its linked 1045 percentage.</summary>
    public static class WithdrawalInput
    {
        public static bool Ordinary(bool requested,bool opening,AbilityItem queued,bool ordinaryRpc) => requested&&ordinaryRpc&&!opening&&
            (queued==null||AbilityCatalog.Find(queued.getAbilityID())?.Number("Ability/TestFunction",-1)!=6);
        public static int Attack(WorldClient actor,AbilityItem queued,bool ordinary)
        {
            if(!ordinary)return InterlockInitiative.Attack(actor,queued);
            int direct = AbilityStats.Player(actor, 1005);
            int percent = AbilityStats.Player(actor, 1045);
            // The catalog links 1045 to 1005 and stock Status displays 1045 as a percent.
            // Scale the linked stat before adding the character level or applying the tactic.
            int input = direct * Math.Max(0, 100 + percent) / 100;
            Output.WriteDebugLog($"[WITHDRAW INPUT OURS] {TimedEffects.PlayerName(actor)} direct={direct} input={input} bonus1045={percent}");
            return input;
        }
        // combat_calculations.GetTacticWithdrawTacticsMod; DEFENSE is this server's Grab button.
        public static double TacticFactor(CombatTactic tactic) => tactic==CombatTactic.Power?1.2:tactic==CombatTactic.Grab?0.9:1.0;
        public static int AttackBase(int level,int input,CombatTactic tactic) => (int)(Math.Max(0,level+input)*TacticFactor(tactic));
    }
}
