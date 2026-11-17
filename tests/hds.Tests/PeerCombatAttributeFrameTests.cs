using HeadlessClient;
using hds;

namespace hds.Tests;

public class PeerCombatAttributeFrameTests
{
    [Fact]
    public void ObserverFramesUseTheActualOtherPlayerFlagAndHealthMasks()
    {
        byte[] flags=Convert.FromHexString("4F0002808080800400001000");
        Assert.Equal(flags,BitConverter.GetBytes((ushort)79).Concat(PvpFlag.BuildOtherFlags(0x00100000)).ToArray());
        Assert.True(MervFollowupViewFrames.TryRead(flags,0,12,out var frame));Assert.Equal(12,frame.Length);
        Assert.False(MervFollowupViewFrames.TryRead(flags[..^1],0,12,out _));
        Assert.False(MervFollowupViewFrames.TryRead(flags,0,599,out _));
        Assert.False(MervFollowupViewFrames.TryRead(flags,0,null,out _));
        var health=BitConverter.GetBytes((ushort)79).Concat(PlayerViewAttributes.Build((18,PlayerViewAttributes.U16(4984)))).ToArray();
        Assert.Equal("4F00028080107813",Convert.ToHexString(health));
        Assert.True(MervFollowupViewFrames.TryRead(health,0,12,out frame));Assert.Equal(8,frame.Length);
        Assert.False(MervFollowupViewFrames.TryRead(health[..^1],0,12,out _));
    }
}
