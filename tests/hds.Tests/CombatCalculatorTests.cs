using hds;

namespace hds.Tests;

public class CombatCalculatorTests
{
    [Theory]
    [InlineData(CombatTactic.Speed, 1.0f)]
    [InlineData(CombatTactic.Power, 1.2f)]
    [InlineData(CombatTactic.Grab, 0.85f)]
    [InlineData(CombatTactic.Block, 0f)]
    public void TacticDamageComesFromRetailStats(CombatTactic tactic, float dealt)
    {
        Assert.Equal(dealt, CombatCalculator.TacticDamageMultiplier(tactic), 3);
    }

    [Fact]
    public void DamageReductionFollowsRetailLevelAndArmorRule()
    {
        // combat_calculations.GetDamageReductionValue: (5 - 50) x 0.2 = -9, capped at -0.9
        Assert.Equal(-0.9, CombatCalculator.DamageReduction(50, 5), 3);
        Assert.Equal(0.9, CombatCalculator.DamageReduction(5, 50), 3);
        // Same level, no armor: armor part = 0.66 / (4 + 20) x 20 - 0.33
        Assert.Equal(0.22, CombatCalculator.DamageReduction(1, 1), 3);
    }

    [Fact]
    public void CloseCombatDamageScalesWithLevelAndPower()
    {
        var rng = new Random(5);
        double Average(CombatTactic tactic, int attacker, int defender)
        {
            int sum = 0;
            for (int i = 0; i < 400; i++) sum += CombatCalculator.CloseCombatDamage(rng, attacker, defender, tactic, out _);
            return sum / 400.0;
        }
        Assert.True(Average(CombatTactic.Power, 50, 5) > Average(CombatTactic.Speed, 50, 5));
        Assert.True(Average(CombatTactic.Speed, 50, 5) > Average(CombatTactic.Speed, 5, 50));
        // Level 1 against level 1: 16 raw like retail's tutorial fight, less the 22% absorbed
        Assert.InRange(Average(CombatTactic.Speed, 1, 1), 9.0, 16.0);
    }

    [Fact]
    public void BlockRegainsInnerStrengthAndOtherTacticsCostNothing()
    {
        // combat.GetInnerStrengthUsed: Block regains 10, the other tactics cost nothing
        Assert.Equal(10, CombatCalculator.RoundInnerStrengthChange(CombatTactic.Block));
        Assert.Equal(0, CombatCalculator.RoundInnerStrengthChange(CombatTactic.Speed));
        Assert.Equal(0, CombatCalculator.RoundInnerStrengthChange(CombatTactic.Power));
        Assert.Equal(0, CombatCalculator.RoundInnerStrengthChange(CombatTactic.Grab));
    }

    [Fact]
    public void TacticChangesCostNothing()
    {
        Assert.Equal(0, CombatCalculator.TACTIC_CHANGE_IS_COST);
    }

    [Fact]
    public void InnerStrengthRegenIsFullOutOfAFightAndAFifthInOne()
    {
        double carry = 0;
        Assert.Equal(8, AbilityHandler.RegenAmount(false, ref carry));
        Assert.Equal(0, carry);

        int total = 0;
        for (int tick = 0; tick < 5; tick++) total += AbilityHandler.RegenAmount(true, ref carry);
        Assert.Equal(8, total); // 1.6 a tick, with the fraction carried over
    }

    [Fact]
    public void CalculateISRegenReturnsAtLeastOne()
    {
        Assert.Equal((ushort)1, CombatCalculator.CalculateISRegen(0));
        Assert.Equal((ushort)5, CombatCalculator.CalculateISRegen(100));
    }

    [Fact]
    public void RangedShotHitsExactlyWhenTheAttackRollBeatsTheDefenseRoll()
    {
        var rng = new Random(1234);
        int hits = 0;
        for (int i = 0; i < 2000; i++)
        {
            bool hit = CombatCalculator.RollRangedHit(rng, out ushort attack, out ushort defense);
            Assert.InRange(attack, (ushort)15, (ushort)99);
            Assert.InRange(defense, (ushort)5, (ushort)69);
            Assert.Equal(attack > defense, hit);
            if (hit) hits++;
        }
        // Retail's tutorial fight hit 7 of 10 shots; keep misses possible but rarer than hits.
        Assert.InRange(hits, 1200, 1800);
    }
}
