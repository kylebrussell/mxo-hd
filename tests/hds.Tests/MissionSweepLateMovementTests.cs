using HeadlessClient;
namespace hds.Tests;
public class MissionSweepLateMovementTests
{
    [Fact] public void CompleteLateMovementHasABoundaryWithoutClaimingAViewIdentity()
    {
        // MobPackets.SendNpcUpdateAnimation: view, parts, animation, yaw, float position.
        var bytes=Convert.FromHexString("1300020E00A8002019C50000BE42006086C6");
        Assert.True(MervFollowupViewFrames.TryRead(bytes,0,null,out var frame));
        Assert.Equal(18,frame.Length);Assert.Equal(19,frame.View);Assert.Equal(6,frame.PositionOffset);
        for(int length=0;length<bytes.Length;length++)Assert.False(MervFollowupViewFrames.TryRead(bytes[..length],0,null,out _));
        Assert.False(MervFollowupViewFrames.TryRead(bytes,0,417,out _));
        var unknown=(byte[])bytes.Clone();unknown[3]=0x0f;
        Assert.False(MervFollowupViewFrames.TryRead(unknown,0,null,out _));
    }
}
