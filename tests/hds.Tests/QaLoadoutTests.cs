using hds;
namespace hds.Tests;

public class QaLoadoutTests
{
    [Theory]
    [InlineData("1", "mxotest_native_123", 110u, true, false, true)]
    [InlineData("0", "mxotest_native_123", 110u, true, false, false)]
    [InlineData("true", "mxotest_native_123", 110u, true, false, false)]
    [InlineData("1", "reality_hd", 110u, true, false, false)]
    [InlineData("1", "mxotest_", 110u, true, false, false)]
    [InlineData("1", "mxotest_bad-name", 110u, true, false, false)]
    [InlineData("1", "MXOTEST_native", 110u, true, false, false)]
    [InlineData("1", "mxotest_native", 2u, true, false, false)]
    [InlineData("1", "mxotest_native", 0u, true, false, false)]
    [InlineData("1", "mxotest_native", 110u, false, false, false)]
    [InlineData("1", "mxotest_native", 110u, true, true, false)]
    public void ScopeCannotBeEnabledByIdentityOrLifecycle(string flag, string db, uint id, bool world, bool dead, bool expected) =>
        Assert.Equal(expected, QaLoadout.Allowed(flag, db, id, world, dead));

    [Theory]
    [InlineData("?qaload 7", true)] [InlineData("?qaunload 871", true)]
    [InlineData("?qaload -1", false)] [InlineData("?qaload 0", false)] [InlineData("?qaload 4096", false)]
    [InlineData("?qaload +7", false)] [InlineData("?qaload 7 110", false)] [InlineData("?qaload", false)]
    [InlineData("?qaload 0x7", false)] [InlineData("?qaload  7", false)]
    public void CommandAcceptsOnlyOneDecimalId(string command, bool valid) =>
        Assert.Equal(valid, QaLoadout.Parse(command.Split(' '), out _, out _));

    static MarginAbilityItem Row(ushort slot, ushort id, ushort level) => new() {
        slot=slot, AbilityID=unchecked((int)(0x80000000u | (uint)id << 10)), level=level };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanonicalRequestUsesOwnedSlotAndLevel(bool unload)
    {
        byte[] packet=QaLoadout.Request(new[]{ Row(393,628,50), Row(2,37,15) },628,unload);
        Assert.Equal(unload ? "000000000a0000000100008901740200003200" : "0000000000000a000100008901740200003200", Convert.ToHexString(packet).ToLowerInvariant());
        Assert.True(AbilityProgression.TryParseLoadout(packet,out bool parsed,out var entries));
        Assert.Equal(unload,parsed); Assert.Equal((ushort)393,Assert.Single(entries).Slot);
    }
    [Fact]
    public void NeverSynthesizesHiddenGrantsUnownedOrAmbiguousRows()
    {
        Assert.Null(QaLoadout.Request(new[]{Row(1,37,15)},970,false));
        Assert.Null(QaLoadout.Request(new[]{Row(1,2,5)},7,false));
        Assert.Null(QaLoadout.Request(new[]{Row(1,7,1),Row(2,7,1)},7,false));
        Assert.Null(QaLoadout.Request(new[]{Row(520,7,1)},7,false));
    }
}
