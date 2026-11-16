using hds;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class BuffDrainFixtureTests
{
    [Fact]public void InnerStrengthReaderMatchesPrimaryPlayerPacketsLiteralAndRejectsOtherShapes()
    {
        var bytes=new byte[]{2,0,2,0x80,0x80,0x80,0x10,0xcf,7};
        Assert.True(BuffDrainLifetimeFixture.TryInnerStrength(bytes,out var value));Assert.Equal(1999,value);
        foreach(int size in Enumerable.Range(0,bytes.Length))Assert.False(BuffDrainLifetimeFixture.TryInnerStrength(bytes[..size],out _));
        Assert.False(BuffDrainLifetimeFixture.TryInnerStrength(bytes.Concat(new byte[]{0}).ToArray(),out _));
        bytes[0]=3;Assert.False(BuffDrainLifetimeFixture.TryInnerStrength(bytes,out _));bytes[0]=2;bytes[6]=0x40;Assert.False(BuffDrainLifetimeFixture.TryInnerStrength(bytes,out _));
    }
    [Fact]public void CurrentCharacterDiagnosticDoesNotAdoptOtherCharacterOrOtherAbility()
    {
        const string raw="[BUFF-DRAIN] char110 ability16 generation2 amount1 remaining1999\n[BUFF-DRAIN] char111 ability16 generation2 amount1 remaining1998\n[BUFF-DRAIN] char110 ability13 generation2 amount25 remaining1974";
        Assert.Equal((2,1,1999),Assert.Single(BuffDrainLifetimeFixture.Drains(raw,110)));
    }
    [Fact]public void NativeModeUsesNormalLoaded16AndNoAutomaticActivationOrRngControl()
    {
        var mode=NativeQaModes.RequireKnown("buff-drain-lifetime");
        Assert.Equal(new ushort[]{2,17,16},mode.Plan.Abilities);Assert.False(mode.Plan.DevCommands);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Empty(mode.Plan.BeforeLoadout);Assert.Empty(mode.Plan.AfterLoadout);Assert.Empty(mode.Plan.EnvironmentOverrides);
        Assert.Contains("SAME CSR110",Assert.Single(mode.Plan.ReadyMessages));
    }
}
