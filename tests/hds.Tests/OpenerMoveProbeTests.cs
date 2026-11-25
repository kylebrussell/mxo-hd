using System;
using hds;
using Xunit;

namespace hds.Tests;

public class OpenerMoveProbeTests
{
    [Theory]
    [InlineData((ushort)286, 19593u, "Twist Throw")]
    [InlineData((ushort)288, 19594u, "Topsy Turvy")]
    [InlineData((ushort)289, 19590u, "Punt")]
    [InlineData((ushort)385, 19592u, "Diving Frankensteiner")]
    [InlineData((ushort)387, 19591u, "Running Bulldog")]
    [InlineData((ushort)388, 19595u, "Reverse Pulldown")]
    public void EachVariantNamesTheOpenersIldbMoveAndTheFailedSneakControl(ushort opener, uint move, string name)
    {
        foreach (char variant in "ab")
        {
            var landed = OpenerMoveProbe.Move(opener, variant)!;
            Assert.Equal(move, landed.Id);
            Assert.Equal(name, landed.Name);
            Assert.Equal("Sneak Attacks", landed.Group);
        }
        var control = OpenerMoveProbe.Move(opener, 'c')!;
        Assert.Equal("Failed Sneak v Self-Defense", control.Name);
        Assert.Equal(19603u, control.Id);
    }

    [Fact]
    public void AtRestVariantUsesTheThrowRecordWithANamedAction()
    {
        uint target = 0x000b0004;
        byte[] own = OpenerMoveProbe.BuildNamedMove(true, 7, 19593, 286, target);
        Assert.Equal(Convert.FromHexString("0200012007010c25894c1e0104000b00"), own);
        byte[] other = OpenerMoveProbe.BuildNamedMove(false, 7, 19593, 286, target);
        Assert.Equal(own[2..], other);
        // Same shape as the stock-verified SneakFree knife throw, except the action field.
        byte[] knife = StealthShieldAbilities.BuildThrow(true, 7, 286, target);
        Assert.Equal(knife[..7], own[..7]);
        Assert.Equal(knife[^6..], own[^6..]);
        Assert.Equal(StealthShieldAbilities.KnifeThrow, knife[7]);
        Assert.Equal(0x25, own[7]);
    }

    [Fact]
    public void ProbeIsGatedToTheDisposableFixture()
    {
        Assert.True(OpenerMoveProbe.Allowed("1", "1", "mxotest_probe", 110, true, false));
        Assert.False(OpenerMoveProbe.Allowed(null, "1", "mxotest_probe", 110, true, false));
        Assert.False(OpenerMoveProbe.Allowed("1", "0", "mxotest_probe", 110, true, false));
        Assert.False(OpenerMoveProbe.Allowed("1", "1", "mxo", 110, true, false));
        Assert.False(OpenerMoveProbe.Allowed("1", "1", "mxotest_probe", 111, true, false));
        Assert.Equal(new ushort[] { 286, 288, 289, 385, 387, 388 }, OpenerMoveProbe.Openers);
    }
}
