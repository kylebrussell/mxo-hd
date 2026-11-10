using hds;

namespace hds.Tests;

public class LevelUpAdviceTests
{
    [Fact]
    public void LevelThreeAdviceMatchesRetail()
    {
        // luizoe Recursion line 42245: 81 8c 00 00 00 00 2e 00 00 80 0e 00 01 00 01 00 00
        Assert.Equal("818c000000002e0000800e000100010000",
            StringUtils.bytesToString_NS(PlayerProgression.BuildLevelUpAdvice(PlayerProgression.LevelUpAdvice(3))).ToLowerInvariant());
    }

    [Theory]
    [InlineData(2, 0u)]
    [InlineData(3, 0x8000002eu)]
    [InlineData(4, 0u)]
    [InlineData(8, 0x80000030u)]
    [InlineData(9, 0x04000531u)]
    [InlineData(10, 0x0400057cu)]
    [InlineData(15, 0x0400057du)]
    [InlineData(50, 0u)]
    public void AdviceComesAtRetailsLevels(int level, uint stringId)
    {
        Assert.Equal(stringId, PlayerProgression.LevelUpAdvice(level));
    }
}
