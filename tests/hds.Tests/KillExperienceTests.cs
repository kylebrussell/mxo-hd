using hds;

namespace hds.Tests;

public class KillExperienceTests
{
    /// <summary>Every kill reward (80 e6 .. 01) in the captures, as the player level and the NPC's level relative to it.</summary>
    [Theory]
    [InlineData(2, -1, KillExperience.WeaklingMultiplier, 335u)] // luizoe Cpy_of_recursion, eight times
    [InlineData(2, 0, KillExperience.WeaklingMultiplier, 391u)]  // Recursion, Unknown, Recursion_Char, crashover2k
    [InlineData(2, 1, KillExperience.WeaklingMultiplier, 458u)]  // Recursion
    [InlineData(2, 2, KillExperience.WeaklingMultiplier, 536u)]  // Recursion
    [InlineData(3, 0, KillExperience.WeaklingMultiplier, 783u)]  // Unknown, Recursion_Char
    [InlineData(3, 1, KillExperience.WeaklingMultiplier, 917u)]  // Recursion_Char
    [InlineData(4, 2, KillExperience.WeaklingMultiplier, 1609u)] // Recursion
    [InlineData(2, -1, KillExperience.ToughMultiplier, 675u)]    // Cpy_of_recursion
    [InlineData(2, 1, KillExperience.ToughMultiplier, 923u)]     // Unknown
    [InlineData(3, 1, KillExperience.ToughMultiplier, 1847u)]    // Recursion, Unknown
    public void RetailKillRewardsFitTheScriptsFormula(int playerLevel, int levelsAbove, int multiplier, uint expected)
    {
        Assert.Equal(expected, KillExperience.ForKill(playerLevel + levelsAbove, playerLevel, multiplier));
    }

    [Fact]
    public void LevelOneCountsAsLevelTwo()
    {
        Assert.Equal(KillExperience.ForKill(2, 2), KillExperience.ForKill(1, 1));
    }

    [Fact]
    public void LowerNpcsGiveLessAndShareScales()
    {
        Assert.True(KillExperience.ForKill(40, 50) < KillExperience.ForKill(50, 50));
        Assert.InRange(KillExperience.ForKill(50, 50, damageShare: 0.5), KillExperience.ForKill(50, 50) / 2 - 1, KillExperience.ForKill(50, 50) / 2 + 1);
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.2)]
    [InlineData(6, 2.1)]
    [InlineData(7, 1.0)]
    public void GroupBonusIsRetailsTable(int players, double bonus)
    {
        Assert.Equal(bonus, KillExperience.GroupBonus(players));
    }
}
