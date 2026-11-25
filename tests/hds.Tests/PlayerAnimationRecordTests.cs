using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class PlayerAnimationRecordTests
{
    [Fact]
    public void TheOwnViewCastAnimationEndsWithItsSimTimeLikeRetail()
    {
        var previous = Store.currentClient;
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(100, 200, 300));
        Store.currentClient = client;
        try
        {
            byte[] record = ServerPackets.BuildPlayerAnimation("a5000028");
            // 02 00 01 28 <counter> 40 00 29 <anim> 00 01 <x y z f32> <sim ms u32>; no trailing 00 00 (retail ends the group here).
            Assert.Equal(new byte[] { 0x02, 0x00, 0x01, 0x28 }, record[..4]);
            Assert.Equal(new byte[] { 0x40, 0x00, 0x29, 0xa5, 0x00, 0x00, 0x28 }, record[5..12]);
            Assert.Equal(26 + 4, record.Length);
            Assert.Equal(new byte[] { 0x20, 0x9f, 0x1e, 0x20 }, record[^4..]);
        }
        finally { Store.currentClient = previous; }
    }
}
