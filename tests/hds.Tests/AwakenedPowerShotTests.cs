using hds;

namespace hds.Tests;

public class AwakenedPowerShotTests
{
    [Theory]
    [InlineData(1, 55)]
    [InlineData(12, 55)]
    [InlineData(31, 73)]
    [InlineData(50, 91)]
    public void CatalogValueFunctionCentersBothEffectsAtTheCasterLevel(int level, int expected)
    {
        var mods = AbilityCatalog.Find(14).Modifiers.ToArray();
        Assert.Equal(2, mods.Length);
        Assert.All(mods, modifier => Assert.Equal(expected, AwakenedPowerShot.Sample(modifier, level, new GaussianDraw(0))));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(.5, 10)]
    public void CatalogValueFunctionClampsEachIndependentSample(double phase, int expected)
    {
        var target = AbilityCatalog.Find(14).Modifiers.Single(modifier => modifier.Target == 108);
        Assert.Equal(expected, AwakenedPowerShot.Sample(target, 50, new GaussianDraw(phase, 7)));
    }

    [Fact]
    public void PowerShotRestoresOnlyWhatFitsAndDoesNotRestoreADeadCaster()
    {
        var caster = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        caster.playerInstance = new PlayerCharacter();
        caster.playerData = new ClientData();
        caster.playerInstance.InnerStrengthMax.setValue((ushort)300);
        caster.playerInstance.InnerStrengthAvailable.setValue((ushort)291);
        Assert.Equal((ushort)9, AwakenedPowerShot.RestoreInnerStrength(caster, 55));
        Assert.Equal((ushort)300, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal((ushort)0, AwakenedPowerShot.RestoreInnerStrength(caster, 55));

        caster.playerData.isDead = true;
        caster.playerInstance.InnerStrengthAvailable.setValue((ushort)10);
        Assert.Equal((ushort)0, AwakenedPowerShot.RestoreInnerStrength(caster, 55));
        Assert.Equal((ushort)10, AbilityHandler.CurrentInnerStrength(caster));
    }

    private sealed class GaussianDraw(double phase, int standardDeviations = 0) : Random
    {
        private int index;
        public override double NextDouble() =>
            index++ == 0 ? 1 - Math.Exp(-standardDeviations * standardDeviations / 2d) : phase;
    }
}
