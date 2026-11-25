using System;
using hds;
using Xunit;

namespace hds.Tests;

public class RangedSpecialContestTests
{
    [Theory]
    [InlineData((ushort)130)]
    [InlineData((ushort)454)]
    public void AtRestRangedSpecialsUseTheTestFunctionOneContest(ushort ability)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.Equal(1, entry.Number("Ability/TestFunction"));
        Assert.Equal(524, AbilityStats.SpecialAttackStat(1));
        Assert.Equal(1010, AbilityStats.SpecialDefenseStat(1));
        Assert.True(PoisonKnifeContest.Applies(ability, AbilityCastOrigin.NormalFreeCast));
        Assert.False(PoisonKnifeContest.Applies(ability, AbilityCastOrigin.AlreadyResolved));
        Assert.False(PoisonKnifeContest.Applies(CombatSpecials.WoundingShot, AbilityCastOrigin.NormalFreeCast));
    }

    [Fact]
    public void DeadlyShotReproducesTheRetainedVector26HitAndDeflectionEnvelopes()
    {
        var hit = new FreeCastViralContest.Result { Attack = 34, Defense = 32 };
        var miss = new FreeCastViralContest.Result { Attack = 43, Defense = 75 };
        // Vector26 454 victim trace: hit 34/32 (0x7a0002d6) and deflection 43/75 (0x7a000218), ability c6 01.
        Assert.Equal(Convert.FromHexString("816717002000D602007A01C6010000000000003000000807004B6F72726F6E000E00416674657257686F72754E656F00040022000000000000002000000000000000"),
            PoisonKnifeContest.Message("Korron", "AfterWhoruNeo", hit, CombatSpecials.DeadlyShot));
        Assert.Equal(Convert.FromHexString("8167170020001802007A01C6010000000000003000000807004B6F72726F6E000E00416674657257686F72754E656F0004002B000000000000004B00000000000000"),
            PoisonKnifeContest.Message("Korron", "AfterWhoruNeo", miss, CombatSpecials.DeadlyShot));
        Assert.Equal("Deadly Shot", PoisonKnifeContest.Name(CombatSpecials.DeadlyShot));
        Assert.Equal("Hindering Shot", PoisonKnifeContest.Name(CombatSpecials.HinderingShot));
        Assert.Equal("Poison Knife", PoisonKnifeContest.Name(CombatSpecials.PoisonKnife));
    }
}
