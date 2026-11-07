using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes come from retail captures under research/ and were checked against the 7.6005 client
// (health bars drop on both sides, the dead NPC stops offering combat and its corpse can be looted).
public class CombatPacketTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void PlainHitUpdatesTheNpcHealthAlone()
    {
        // Retail sent this form for most hits: groups select view attribute 27 (Health) only.
        Assert.Equal("0480808040d101", Hex(Mob.BuildHitUpdate(0x01d1, 0, 7)));
    }

    [Fact]
    public void AbilityHitAddsEffectAndCounter()
    {
        // Retail: 04 80 80 80 c0 75 03 c0 b7 01 00 28 01 01 (health, effect 0x280001b7, counter).
        Assert.Equal("04808080c07503c0b701002801" + "01", Hex(Mob.BuildHitUpdate(0x0375, 0x280001b7, 1)));
    }

    [Fact]
    public void PlayerHealthUsesTheOwnViewLayout()
    {
        // View 2, own-view update (02), groups 80 80 80 40 = own-view attribute 27, Health.
        // Retail: 02 00 02 80 80 80 40 ec 13.
        Assert.Equal("0200028080804088" + "13", Hex(ServerPackets.BuildSelfHealthUpdate(0x1388)));
    }

    [Fact]
    public void AppearanceUpdateSelectsOnlyRsiDescription()
    {
        // Retail: 02 00 02 80 01 <15 RSI bytes>. Male, hat 44, coat 23, everything else 0.
        int[] rsi = new int[22];
        rsi[2] = 44;
        rsi[5] = 23;
        byte[] rsiBytes = PacketsUtils.getRSIBytes(rsi);

        Assert.Equal(13, rsiBytes.Length); // packed parts; RSIDescription itself is 15 bytes
        Assert.Equal("0200028001" + "160005c0" + "000000000000000000" + "0000",
            Hex(ServerPackets.BuildAppearanceUpdate(rsiBytes)));
    }

    [Fact]
    public void HyperSpeedUsesRetailMovementScaleAndRealInnerStrength()
    {
        // Retail: 02 00 02 88 05 8c 1e 66 66 e6 3e 80 90 <IS> b0 56 06 00 28 <ctr> 02 ...
        string hex = Hex(AbilityPackets_BuildHyperSpeed(207, 2));
        Assert.StartsWith("0200" + "0288058c1e" + "6666e63e" + "8090" + "cf00" + "b056060028" + "02", hex);
    }

    private static byte[] AbilityPackets_BuildHyperSpeed(ushort innerStrength, byte counter) =>
        ServerPackets.BuildHyperSpeedUpdate(innerStrength, counter);

    [Fact]
    public void CombatTargetFlagMatchesRetail()
    {
        // Retail: 46 2f 00 22 00 01 at the start of a fight and 46 2f 00 22 00 00 at the kill
        // (target view 0x2f, spawn 0x22; our packed id is spawn << 16 | view).
        Assert.Equal("462f00220001", Hex(ServerPackets.BuildCombatTargetFlag(0x0022002f, 0x01)));
        Assert.Equal("462f00220000", Hex(ServerPackets.BuildCombatTargetFlag(0x0022002f, 0x00)));
    }

    [Fact]
    public void PositionHandoffMatchesRetailLayout()
    {
        // Retail: 02 00 01 0c 66 <x y z f32> <time u32>
        Assert.Equal("0200010c" + "66" + "0000803f" + "00000040" + "00004040" + "78563412",
            Hex(ServerPackets.BuildSelfPositionHandoff(0x66, 1f, 2f, 3f, 0x12345678)));
    }

    [Fact]
    public void CombatStartTimeIsSimMilliseconds()
    {
        // Retail StartTime 55 d3 9a 03 = 60,478,293 ms while the packet clock read 60,479.35 s.
        // The float-seconds bytes this used to send kept the client locked in combat after every fight.
        long ms = CombatHandler.CombatStartTime();
        long expected = (long)(TimeUtils.GetSimTimeSeconds() * 1000);
        Assert.InRange(ms, expected - 1000, expected + 1000);
    }

    [Fact]
    public void CloseCombatEchoMatchesRetail()
    {
        // Client 40 2f 00 22 00 01 6b, server 40 2f 00 22 00 00 6b
        byte[] request = { 0x2f, 0x00, 0x22, 0x00, 0x01, 0x6b };
        Assert.Equal("402f002200006b", Hex(CombatHandler.BuildCloseCombatEcho(request)));
    }

    [Fact]
    public void CombatViewFinalRecordFollowsTheRetailHeader()
    {
        // Retail final record: 01 <centre> 0d 00 00 00 | 00 00 00 80 | 02 00 00 00 | <entries>
        byte[] centre = new byte[24];
        centre[7] = 0x40;
        Assert.Equal("01" + "0000000000000040" + new string('0', 32) + "0d000000" + "00000080" + "02000000" + "00",
            Hex(ServerPackets.BuildCombatViewFinalRecord(centre, 0x0d)));
    }

    [Fact]
    public void NpcDeathMatchesRetail()
    {
        // Parts 01 and 04: UseCount=2, StopFollowActiveTracker=0, IsDead=1, Health=0,
        // CurrentState=0x7fd, ConditionStateFlags=0.
        Assert.Equal("0501c202008080e0010000808003fd07000000000000", Hex(ServerPackets.BuildNpcDeathUpdate()));
    }
}
