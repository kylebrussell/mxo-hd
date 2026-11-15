using HeadlessClient;
namespace hds.Tests;
public class GhostStrikeObserverFlagsTests
{
    [Fact]public void RetainedNativeEquivalentFailureContainsPvpAndStealthAndOffPreservesPvp()
    {
        // Literal received61.9s failure: the old fixture expected the final03 byte to be02.
        byte[] baseline=Convert.FromHexString("040002808080800400000001");
        byte[] actualOn=Convert.FromHexString("040002808080C004000400000003");
        var before=GhostStrikeObserverFlags.Latest(new[]{baseline},4)!;
        var on=GhostStrikeObserverFlags.Latest(new[]{baseline,actualOn},4)!;
        Assert.Equal(0x01000000u,before.Flags);Assert.Null(before.Dissembling);
        Assert.Equal((ushort)4,on.Dissembling);Assert.Equal(GhostStrikeObserverFlags.Expected(before.Flags,true),on.Flags);
        Assert.Equal(0x01000000u,GhostStrikeObserverFlags.Expected(before.Flags,false));
    }
    [Fact]public void UnknownBaselineBitsArePreservedAndCurrentSampleUsesActualListAndOffsetOrder()
    {
        byte[] first=Convert.FromHexString("040002808080800440000081"); //0x81000040, not an assumed clean baseline
        byte[] later=Convert.FromHexString("040002808080C000000440000081040002808080C004000440000083");
        var baseline=GhostStrikeObserverFlags.Latest(new[]{first},4)!;
        var current=GhostStrikeObserverFlags.Latest(new[]{first,later},4)!;
        Assert.Equal(0x83000040u,GhostStrikeObserverFlags.Expected(baseline.Flags,true));
        Assert.Equal(0x81000040u,GhostStrikeObserverFlags.Expected(current.Flags,false));
        Assert.Equal(1,current.ViewList);Assert.Equal(14,current.Offset);Assert.Equal((ushort)4,current.Dissembling);
    }
    [Fact]public void WrongViewTruncationMaskAndOldCursorCannotSupplyBaselineEvidence()
    {
        var valid=Convert.FromHexString("040002808080800400000001");
        Assert.Null(GhostStrikeObserverFlags.Latest(new[]{valid},5));Assert.Null(GhostStrikeObserverFlags.Latest(new[]{valid},4,1));
        Assert.Null(GhostStrikeObserverFlags.Latest(new[]{valid[..^1]},4));
        Assert.Null(GhostStrikeObserverFlags.Latest(new[]{Convert.FromHexString("040002808080C004000200000003")},4));
    }
}
