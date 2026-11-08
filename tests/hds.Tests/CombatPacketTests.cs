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

    private static float F32(string hex) => BitConverter.ToSingle(StringUtils.hexStringToBytes(hex), 0);

    [Fact]
    public void FirstGunshotMatchesRetail()
    {
        // Retail first shot, luizoe old-sniffer Unknown log line 11790 (research/packet-dumps/community-intake/
        // 20260607T030922Z-hdneo-public-luizoe-old-sniffer-selected): action 28, NoiseLevel 0x32,
        // ConditionStateFlags 0x00100000, CombatantMode 0x21.
        string retail = "020003" + "2802010c388100" + "0400a300" + "0010d645" + "0060c744" + "785d3ac6" + "2af96411" +
                        "8084" + "32" + "80808080" + "03" + "00001000" + "21";
        Assert.Equal(retail, Hex(ServerPackets.BuildRangedShot(0x02, 0x00a30004,
            F32("0010d645"), F32("0060c744"), F32("785d3ac6"), 0x1164f92a, 0x32, true)));
    }

    [Fact]
    public void FollowUpGunshotMatchesRetail()
    {
        // Retail, tutorial capture 13:30:17 (crashover2k ..._uriah.log.txt line 13898): NoiseLevel 0x4b only.
        string retail = "020003" + "2802010c388100" + "0600f200" + "a810d645" + "0060c744" + "166a37c6" + "33bee71e" +
                        "8004" + "4b";
        Assert.Equal(retail, Hex(ServerPackets.BuildRangedShot(0x02, 0x00f20006,
            F32("a810d645"), F32("0060c744"), F32("166a37c6"), 0x1ee7be33, 0x4b, false)));
    }

    [Fact]
    public void MissedGunshotCarriesTheMissMarker()
    {
        // A miss has 00 10 where a hit has 81 00 (retail miss: luizoe Unknown log line 12029,
        // "28 03 01 0c 38 00 10 04 00 a3 00 ..."); otherwise the same record as the follow-up shot.
        string expected = "020003" + "2802010c380010" + "0600f200" + "a810d645" + "0060c744" + "166a37c6" + "33bee71e" +
                          "8004" + "4b";
        Assert.Equal(expected, Hex(ServerPackets.BuildRangedShot(0x02, 0x00f20006,
            F32("a810d645"), F32("0060c744"), F32("166a37c6"), 0x1ee7be33, 0x4b, false, false)));
    }

    private static string HexText(string s) => Hex(System.Text.Encoding.ASCII.GetBytes(s));

    [Fact]
    public void CombatLogHitMatchesRetail()
    {
        // Tutorial capture (crashover2k ..._uriah.log.txt line 13974): mxoemu hits Alex for 19 with the Redpill Special.
        string retail = "8168" + "2400" + "1d00" + "03000000" + "1300" + "f9ff" + "4700" + "2d00" + "00000000" + "01" +
                        "050a0000" + "0000" + "0500" + HexText("Alex") + "00" + "0700" + HexText("mxoemu") + "00";
        Assert.Equal(retail, Hex(ServerPackets.BuildCombatLog(true, 0x13, -7, 0x47, 0x2d, true, 0x0a05, "Alex", "mxoemu")));
    }

    [Fact]
    public void CombatLogMissMatchesRetail()
    {
        // Same capture, line 13781: mxoemu's first shot misses Alex (attack roll 19 against 65).
        string retail = "8168" + "2400" + "1d00" + "04000000" + "0000" + "0000" + "1300" + "4100" + "00000000" + "01" +
                        "050a0000" + "0000" + "0500" + HexText("Alex") + "00" + "0700" + HexText("mxoemu") + "00";
        Assert.Equal(retail, Hex(ServerPackets.BuildCombatLog(false, 0, 0, 0x13, 0x41, true, 0x0a05, "Alex", "mxoemu")));
    }

    [Fact]
    public void CombatLogNpcHitMatchesRetail()
    {
        // Same capture, line 13852: Alex hits mxoemu for 4. The defender's name comes first; the second offset moves.
        string retail = "8168" + "2600" + "1d00" + "03000000" + "0400" + "0000" + "5f00" + "1d00" + "00000000" + "00" +
                        "050a0000" + "0000" + "0700" + HexText("mxoemu") + "00" + "0500" + HexText("Alex") + "00";
        Assert.Equal(retail, Hex(ServerPackets.BuildCombatLog(true, 4, 0, 0x5f, 0x1d, false, 0x0a05, "mxoemu", "Alex")));
    }

    [Fact]
    public void GunshotRpcMatchesRetail()
    {
        // Retail sent 02 4e 00 (length 2, RPC 4e 00) with each shot.
        Assert.Equal("4e00", Hex(ServerPackets.BuildRangedShotRpc()));
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
    public void NpcDeathCarriesTheDeadStanceAndAnimation()
    {
        // 07 01 | 1d 0d00 <seq> 0100 2b00 2500 <yaw> <pos> | the retail part 04 below
        Assert.Equal("0701" + "1d" + "0d00" + "05" + "0100" + "2b00" + "2500" + "80" + "0000803f" + "00000040" + "00004040" +
                     "c202008080e0010000808003fd07000000000000",
            Hex(ServerPackets.BuildNpcDeathUpdate(5, 0x80, 1f, 2f, 3f)));
    }

    [Fact]
    public void NpcDeathMatchesRetail()
    {
        // Parts 01 and 04: UseCount=2, StopFollowActiveTracker=0, IsDead=1, Health=0,
        // CurrentState=0x7fd, ConditionStateFlags=0.
        Assert.Equal("0501c202008080e0010000808003fd07000000000000", Hex(ServerPackets.BuildNpcDeathUpdate()));
    }
}
