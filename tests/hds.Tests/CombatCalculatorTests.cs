using hds;

namespace hds.Tests;

public class CombatCalculatorTests
{
    [Theory]
    [InlineData(CombatTactic.Speed, CombatTactic.Power, 1.25f)]
    [InlineData(CombatTactic.Power, CombatTactic.Grab, 1.25f)]
    [InlineData(CombatTactic.Grab, CombatTactic.Speed, 1.25f)]
    [InlineData(CombatTactic.Speed, CombatTactic.Grab, 0.75f)]
    [InlineData(CombatTactic.None, CombatTactic.Power, 1.0f)]
    public void GetTacticDamageModifierAppliesCounterSystem(
        CombatTactic attacker,
        CombatTactic defender,
        float expected)
    {
        Assert.Equal(expected, CombatCalculator.GetTacticDamageModifier(attacker, defender));
    }

    [Fact]
    public void CalculateISRegenReturnsAtLeastOne()
    {
        Assert.Equal((ushort)1, CombatCalculator.CalculateISRegen(0));
        Assert.Equal((ushort)5, CombatCalculator.CalculateISRegen(100));
    }

    [Theory]
    [InlineData(100, CombatTactic.Speed, 90)]
    [InlineData(100, CombatTactic.Power, 110)]
    [InlineData(100, CombatTactic.Grab, 100)]
    public void CalculateAbilityISCostAppliesTacticModifier(ushort baseCost, CombatTactic tactic, ushort expected)
    {
        Assert.Equal(expected, CombatCalculator.CalculateAbilityISCost(baseCost, tactic));
    }
}
