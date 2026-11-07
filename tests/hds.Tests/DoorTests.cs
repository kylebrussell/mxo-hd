using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes come from the retail door capture
// (afterwhoruneo second_pc_005, 09:55:16 open, 09:55:36 closing, 09:55:37 closed, 09:55:42 reset + delete).
public class DoorTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void ClosingAndClosedUpdatesMatchRetail()
    {
        Assert.Equal("01800238080000", Hex(DoorLifecycle.BuildStateUpdate(DoorLifecycle.StateClosing)));
        Assert.Equal("01800235080000", Hex(DoorLifecycle.BuildStateUpdate(DoorLifecycle.StateClosed)));
    }

    [Fact]
    public void ResetBeforeDeleteMatchesRetail()
    {
        Assert.Equal("01ff" + "00000000000000000000000000" + "03" + "00000000" + "35080000",
            Hex(DoorLifecycle.BuildResetUpdate()));
    }

    [Fact]
    public void HardlineStatusMatchesRetail()
    {
        // Retail (trinitys second_pc_010): 6d 01 00 00 00 08 00 01 29 00 | 00 30 00 00 00 00 | 00 31 00 ...
        Assert.Equal("6d" + "0100" + "0000" + "0800" + "01" + "0300" + "003000000000" + "003100000000" + "003700000000",
            Hex(ServerPackets.BuildHardlineStatus(1, new ushort[] { 0x30, 0x31, 0x37 })));
    }

    [Fact]
    public void DoorClosesAfterTwentySecondsAndIsRemovedAtTwentySix()
    {
        OpenDoor door = new OpenDoor { ViewId = 0x12, OpenedAt = 100, Stage = DoorStage.Open };

        Assert.Null(DoorLifecycle.NextStage(door, 119.9));
        Assert.Equal(DoorStage.Closing, DoorLifecycle.NextStage(door, 120));
        door.Stage = DoorStage.Closing;

        Assert.Null(DoorLifecycle.NextStage(door, 120.5));
        Assert.Equal(DoorStage.Closed, DoorLifecycle.NextStage(door, 121));
        door.Stage = DoorStage.Closed;

        Assert.False(DoorLifecycle.ShouldRemove(door, 125.9));
        Assert.True(DoorLifecycle.ShouldRemove(door, 126));
    }
}
