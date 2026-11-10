using hds;

namespace hds.Tests;

/// <summary>
/// CombatRules against retail's RPC 45s (research/packet-dumps). Each case names the capture's tactics, hits and the
/// "won" byte, result and damage fields retail sent.
/// </summary>
public class CombatRulesTests
{
    private static CombatRules.Fighter F(CombatTactic tactic, bool hits, int damage = 20, int absorbed = 0, int health = 1000,
        bool specialMove = false, int attackRoll = 50)
    {
        return new CombatRules.Fighter
        {
            Tactic = tactic, Hits = hits, Damage = damage, Absorbed = absorbed, Health = health,
            SpecialMove = specialMove, AttackRoll = attackRoll
        };
    }

    private static CombatRules.Outcome Round(CombatRules.Fighter player, CombatRules.Fighter npc, bool opening = false,
        bool playerOpened = true, bool coin = true)
    {
        bool first = CombatRules.PlayerAttacksFirst(player.Tactic, npc.Tactic, opening, playerOpened, () => coin);
        return CombatRules.Resolve(player, npc, first, opening);
    }

    [Theory]
    [InlineData(CombatTactic.Power, CombatTactic.Speed, true)]
    [InlineData(CombatTactic.Grab, CombatTactic.Power, true)]
    [InlineData(CombatTactic.Speed, CombatTactic.Grab, true)]
    [InlineData(CombatTactic.Speed, CombatTactic.Block, true)]
    [InlineData(CombatTactic.Speed, CombatTactic.Power, false)]
    [InlineData(CombatTactic.Block, CombatTactic.Grab, false)]
    [InlineData(CombatTactic.Block, CombatTactic.Block, false)]
    public void PreferredTacticIsRetailsCycle(CombatTactic tactic, CombatTactic against, bool preferred)
    {
        Assert.Equal(preferred, CombatRules.PreferredTactic(tactic, against));
    }

    [Fact]
    public void BothHitGoesToThePreferredTactic()
    {
        // Tutorial 13:28:54: player Power, NPC Speed, both hit; won 1, result 8, 4 to the player and 21 (-5) to the NPC
        var o = Round(F(CombatTactic.Power, true, 21, -5), F(CombatTactic.Speed, true, 4, 0));
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.BothHit, o.Flags);
        Assert.Equal((4, 0, 21, -5), (o.DamageToPlayer, o.PlayerAbsorbed, o.DamageToNpc, o.NpcAbsorbed));

        // Player Speed against NPC Power, both hit: retail sent won 0 (136 such rounds, 133 as the rule says)
        o = Round(F(CombatTactic.Speed, true, 181, -5), F(CombatTactic.Power, true, 736, -56));
        Assert.False(o.PlayerWins);
        Assert.Equal(CombatRules.BothHit, o.Flags);
        Assert.Equal((736, 181), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void OneHitGoesToWhoeverLanded()
    {
        // Player preferred but misses, NPC hits: they swap, the NPC wins, result 1 (146 of 146 such rounds)
        var o = Round(F(CombatTactic.Power, false), F(CombatTactic.Speed, true, 30));
        Assert.False(o.PlayerWins);
        Assert.Equal(CombatRules.AttackerHits, o.Flags);
        Assert.Equal((30, 0), (o.DamageToPlayer, o.DamageToNpc));

        o = Round(F(CombatTactic.Speed, true, 25), F(CombatTactic.Power, false));
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.AttackerHits, o.Flags);
        Assert.Equal((0, 25), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void NobodyHittingGoesToThePreferredTactic()
    {
        var o = Round(F(CombatTactic.Grab, false), F(CombatTactic.Power, false));
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.NoHits, o.Flags);
        o = Round(F(CombatTactic.Power, false), F(CombatTactic.Grab, false));
        Assert.False(o.PlayerWins);
    }

    [Fact]
    public void SameTacticIsACoin()
    {
        Assert.True(Round(F(CombatTactic.Speed, false), F(CombatTactic.Speed, false), coin: true).PlayerWins);
        Assert.False(Round(F(CombatTactic.Speed, false), F(CombatTactic.Speed, false), coin: false).PlayerWins);
    }

    [Fact]
    public void BlockAgainstAnAttackGoesToTheAttacker()
    {
        // Retail: player Block, NPC Speed, nobody hit: won 0 (19 rounds); NPC Block, player Speed: won 1 (43 rounds)
        Assert.False(Round(F(CombatTactic.Block, false), F(CombatTactic.Speed, false)).PlayerWins);
        Assert.True(Round(F(CombatTactic.Speed, false), F(CombatTactic.Block, false)).PlayerWins);
        // A blocker who is the true attacker (both on Block) hurts nobody
        var o = Round(F(CombatTactic.Block, true), F(CombatTactic.Block, true));
        Assert.Equal(CombatRules.NoHits, o.Flags);
        Assert.Equal((0, 0), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void OpeningIsTheStartersOpportunityAttack()
    {
        // Tutorial 13:28:32: the first RPC 45, result 0x801, won 1, 21 (-5) to the NPC
        var o = Round(F(CombatTactic.Speed, true, 21, -5), F(CombatTactic.Speed, false, 4), opening: true);
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.AttackerHits | CombatRules.OpportunityAttack, o.Flags);
        Assert.Equal((0, 21, -5), (o.DamageToPlayer, o.DamageToNpc, o.NpcAbsorbed));

        // Both hit: still only the starter's hit lands
        o = Round(F(CombatTactic.Speed, true, 21), F(CombatTactic.Power, true, 9), opening: true);
        Assert.Equal((0, 21), (o.DamageToPlayer, o.DamageToNpc));

        // Even a missed roll deals the starter's damage when the other side misses too (handleOppertunityAttack)
        o = Round(F(CombatTactic.Speed, false, 21), F(CombatTactic.Power, false, 9), opening: true);
        Assert.Equal(CombatRules.AttackerHits | CombatRules.OpportunityAttack, o.Flags);
        Assert.Equal(21, o.DamageToNpc);

        // create_char_dev: the NPC hit and the player missed: result 0x820, won 1, 4 to the player
        o = Round(F(CombatTactic.Speed, false, 21), F(CombatTactic.Speed, true, 4), opening: true);
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.Draw | CombatRules.OpportunityAttack, o.Flags);
        Assert.Equal((4, 0), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void AnNpcThatStartsTheFightMakesTheOpeningAttack()
    {
        var o = Round(F(CombatTactic.Power, false, 21), F(CombatTactic.Speed, true, 9), opening: true, playerOpened: false);
        Assert.False(o.PlayerWins);
        Assert.Equal(CombatRules.AttackerHits | CombatRules.OpportunityAttack, o.Flags);
        Assert.Equal((9, 0), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void KillingHitIsAFinishingMove()
    {
        // Retail: player Speed against NPC Power, only the player hit and killed: result 0x41, won 1, 185 (+7) to the NPC
        var o = Round(F(CombatTactic.Speed, true, 185, 7), F(CombatTactic.Power, false, 50, 0, health: 185));
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.AttackerHits | CombatRules.FinishingMove, o.Flags);
        Assert.Equal(185, o.DamageToNpc);
    }

    [Fact]
    public void BothHitKillSwapsTheWinnerAndTheKillersFields()
    {
        // luizoe Cpy_of_re: player Speed, NPC Grab, both hit, the player's 28 (-12) killed: result 0x48, won 0, and the
        // player's own fields read 0 damage, 1 absorbed (the NPC's hit was 1 with 0 absorbed)
        var o = Round(F(CombatTactic.Speed, true, 28, -12), F(CombatTactic.Grab, true, 1, 0, health: 28));
        Assert.False(o.PlayerWins);
        Assert.Equal(CombatRules.BothHit | CombatRules.FinishingMove, o.Flags);
        Assert.Equal((0, 1, 28, -12), (o.DamageToPlayer, o.PlayerAbsorbed, o.DamageToNpc, o.NpcAbsorbed));

        // luizoe Unknown: the NPC's hit was 13 with -1 absorbed, so the player's fields read -1 and 13
        o = Round(F(CombatTactic.Power, true, 21, -5), F(CombatTactic.Speed, true, 13, -1, health: 21));
        Assert.Equal((-1, 13, 21, -5), (o.DamageToPlayer, o.PlayerAbsorbed, o.DamageToNpc, o.NpcAbsorbed));
    }

    [Fact]
    public void SpecialMoveThatHitsWinsAndNobodyIsHurt()
    {
        var o = Round(F(CombatTactic.Speed, true, specialMove: true), F(CombatTactic.Power, true, 40));
        Assert.True(o.PlayerWins);
        Assert.True(o.SpecialMoveLands);
        Assert.Equal(CombatRules.SpecialMove, o.Flags);
        Assert.Equal((0, 0), (o.DamageToPlayer, o.DamageToNpc));

        // A special move whose roll misses is lost: a plain exchange in which the player deals nothing
        o = Round(F(CombatTactic.Speed, false, specialMove: true), F(CombatTactic.Power, true, 40));
        Assert.False(o.SpecialMoveLands);
        Assert.Equal((40, 0), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void WithdrawalSucceedsWhenTheWithdrawersRollIsHigher()
    {
        // abilitychangecoder 10:38:20: player value 0x53 against the NPC's 0x1f: result 0x200, won 1, nobody hurt
        var player = F(CombatTactic.Power, true, 30, attackRoll: 0x53);
        var o = CombatRules.Resolve(player, F(CombatTactic.Power, true, 40, attackRoll: 0x1f), true, false, playerWithdrawing: true);
        Assert.True(o.Withdrew);
        Assert.True(o.PlayerWins);
        Assert.Equal(CombatRules.Withdraw, o.Flags);
        Assert.Equal((0, 0), (o.DamageToPlayer, o.DamageToNpc));
    }

    [Fact]
    public void FailedWithdrawalIsARoundTheWithdrawerCantHit()
    {
        // Retail's 13 failed rounds: the NPC hit (0x1, won 0) or nobody did (0x4); on Block the player won it
        var o = CombatRules.Resolve(F(CombatTactic.Power, true, 30, attackRoll: 10), F(CombatTactic.Grab, true, 40, attackRoll: 90),
            true, false, playerWithdrawing: true);
        Assert.False(o.Withdrew);
        Assert.False(o.PlayerWins);
        Assert.Equal(CombatRules.AttackerHits, o.Flags);
        Assert.Equal((40, 0), (o.DamageToPlayer, o.DamageToNpc));

        o = CombatRules.Resolve(F(CombatTactic.Power, true, attackRoll: 10), F(CombatTactic.Grab, false, attackRoll: 90), true, false, playerWithdrawing: true);
        Assert.Equal((false, CombatRules.NoHits), (o.PlayerWins, o.Flags));

        o = CombatRules.Resolve(F(CombatTactic.Power, false, attackRoll: 10), F(CombatTactic.Block, false, attackRoll: 90), true, false, playerWithdrawing: true);
        Assert.Equal((true, CombatRules.NoHits), (o.PlayerWins, o.Flags));
    }

    [Fact]
    public void MovesCarryTheWithdrawFlag()
    {
        // abilitychangecoder 10:38:20: 47 00 02 01 01 0e ... (decided and next moves both marked 2)
        var move = new CombatInterlock.Move { Seq = 0x0e, Tactic = CombatTactic.Power, Withdraw = true };
        var next = new CombatInterlock.Move { Seq = 0x0f, Tactic = CombatTactic.Power, Withdraw = true };
        string hex = StringUtils.bytesToString_NS(CombatInterlock.BuildRoundState(CombatInterlock.KindNewRound, move, next)).ToLowerInvariant();
        Assert.StartsWith("4700020101" + "0e", hex);
        Assert.Contains("0100000201010f", hex);
    }

    [Fact]
    public void WithdrawalRoundCarriesTheMargin()
    {
        // Retail's withdrawal rounds put the withdrawer's roll minus the other's in the u32 after the result: 52 on the
        // 0x200 round (0x53 - 0x1f), -23 on a failed one (104 - 127); 46 of 46 such rounds in the captures
        var move = new CombatInterlock.Move { Seq = 0x0e, Tactic = CombatTactic.Power, Withdraw = true, Value = 104 };
        var npc = new CombatInterlock.Move { Seq = 0x09, Tactic = CombatTactic.Power, Value = 127 };
        byte[] built = CombatInterlock.BuildRoundResult(4, false, move, npc, 0x00f00007, CombatRules.AttackerHits, 30, 0, 0, 0, 0, -23);
        Assert.Equal(89, built.Length);
        Assert.Equal(2, built[7]);                                     // the player's move: withdrawing
        Assert.Equal(-23, BitConverter.ToInt32(built, 65));
        Assert.Equal(0, BitConverter.ToInt32(CombatInterlock.BuildRoundResult(4, false, move, npc, 0x00f00007, 1, 30, 0, 0, 0, 0), 65));
    }

    [Theory]
    [InlineData(1, 1, false, 1, 0)]
    [InlineData(2, 2, false, 1, -10)]
    [InlineData(3, 3, true, 1, -5)]
    [InlineData(2, 2, true, 1, 0)]
    public void MultiOnOnePenaltyIsFivePerOpponent(int opponents, int fought, bool negated, int threshold, int expected)
    {
        Assert.Equal(expected, CombatRules.MultiOnOnePenalty(opponents, fought, negated, threshold));
    }

    [Fact]
    public void GunAgainstFistsGetsItsEdgeAtRangeAndLosesItUpClose()
    {
        Assert.Equal(15, CombatRules.GunAgainstFistsModifier(RangedWeapons.ClassRifle, false, false, 1));
        Assert.Equal(-15, CombatRules.GunAgainstFistsModifier(RangedWeapons.ClassRifle, false, true, 1));
        Assert.Equal(-10, CombatRules.GunAgainstFistsModifier(RangedWeapons.ClassPistol, false, false, 2)); // into a melee: -15 + 5
        Assert.Equal(0, CombatRules.GunAgainstFistsModifier(RangedWeapons.ClassPistol, true, false, 1));  // gun against gun
        Assert.Equal(0, CombatRules.GunAgainstFistsModifier(0, false, false, 1));                         // fists
        Assert.Equal(0, CombatRules.GunAgainstFistsModifier(RangedWeapons.ClassSubmachinegun, false, true, 1, shortRangePenaltyNegated: true));
    }
}
