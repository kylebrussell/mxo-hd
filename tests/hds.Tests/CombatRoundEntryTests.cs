using hds;

namespace hds.Tests;

public class CombatRoundEntryTests
{
    private static byte[] B(string hex) => StringUtils.hexStringToBytes(hex);
    private static float F(string hex) => BitConverter.ToSingle(B(hex));

    [Theory]
    [InlineData(0x00100000u, "00001000")]
    [InlineData(0x01100000u, "00001001")]
    public void ParticipantCombatPreludeMatchesRetailAndPreservesNextViewBoundary(uint flags, string tail)
    {
        // mxopackets1_luizoe_margins_Syntax 15436: the next view starts immediately after the flags.
        byte[] packet = ServerPackets.BuildEnterCombatMode(flags);
        Assert.Equal(B("020003010c008084008080808001" + tail), packet);
        Assert.Equal(18, packet.Length);
        Assert.Equal(flags, BitConverter.ToUInt32(packet, 14));
    }

    [Fact]
    public void LastdayDuelEntrySerializesExactly()
    {
        // lastday2, 21:54:49, LocalSeq2435, view12 update counter10. The 121-byte entry follows its count.
        var entry = new CombatRoundEntry
        {
            Sequence = 10, StartMs = 37869, FirstDurationMs = 2000, SecondDurationMs = 2500, Flags = 2,
            FirstOffset = new[] { F("806e3f41"), 0, F("90f089c2") },
            SecondOffset = new[] { F("806e3fc1"), 0, F("90f08942") },
            Animation = CombatRoundEntry.Reference(748),
            ResultData = B("4d0000006400000066030010000000000200000000000000020000000000000000000000")
        };
        Assert.Equal(B("010200000000806e3f410000000090f089c2806e3fc10000000090f0894200000a00ed9300000000d007c40902" +
            "00000000000000000000000000000000ec0200008b0b002400000000000000000000000000000000" +
            "4d0000006400000066030010000000000200000000000000020000000000000000000000"), entry.Serialize());
        Assert.Equal(121, entry.Serialize().Length);
    }

    [Fact]
    public void NpcOpeningPreservesApproachFacingDelayAndTransitions()
    {
        // actions_combat 10:26:42, ServerSeq113, line12406: asymmetric approach and nonzero facing/delay.
        var entry = new CombatRoundEntry
        {
            FirstApproachMs = 775, FirstFacing = 5, Sequence = 1, StartMs = 275, SecondDelayMs = 775,
            FirstOffset = new[] { F("6031ab43"), -10, F("40bd3a43") },
            SecondOffset = new[] { 0, -10, F("000080b7") },
            FirstDurationMs = 2166, SecondDurationMs = 1995, Flags = 3,
            FirstTransition = CombatRoundEntry.Reference(21012), SecondTransition = CombatRoundEntry.Reference(21012),
            Animation = CombatRoundEntry.Reference(9086),
            ResultData = B("7c0000004500000000100010100000000100000010000000010000000000000000000000")
        };
        Assert.Equal(B("0102070300006031ab43000020c140bd3a4300000000000020c1000080b7050001001301000007037608cb0703" +
            "145200008b0b0024145200008b0b00247e2300008b0b002400000000000000000000000000000000" +
            "7c0000004500000000100010100000000100000010000000010000000000000000000000"), entry.Serialize());
    }

    [Fact]
    public void CombatUpdateHasCompleteFramingAndOneEntry()
    {
        var entry = new CombatRoundEntry { Sequence = 2, Animation = CombatRoundEntry.Reference(20899) };
        byte[] packet = ServerPackets.BuildCombatViewRound(new byte[24], 7, entry);
        Assert.Equal(155, packet.Length);
        Assert.Equal(1, packet[0]);
        Assert.Equal(7u, BitConverter.ToUInt32(packet, 25));
        Assert.Equal(0u, BitConverter.ToUInt32(packet, 29));
        Assert.Equal(1, packet[33]);
        Assert.Equal(entry.Serialize(), packet[34..]);
        Assert.Throws<ArgumentException>(() => ServerPackets.BuildCombatViewRound(new byte[12], 0, entry));
    }

    [Fact]
    public void WholeFightEndDetachesBothSlotsBeforeHandlerDeletion()
    {
        byte[] final = ServerPackets.BuildCombatViewFinalRecord(new byte[24], 14);
        Assert.Equal(38, final.Length);
        Assert.Equal(0x80000000u, BitConverter.ToUInt32(final,29));
        Assert.Equal(3u, BitConverter.ToUInt32(final,33)); // slot1 and slot2, not a captured slot2-only removal
        Assert.Equal(0, final[37]);
    }

    [Fact]
    public void MasterInitLengthIncludesItsOwnFieldAndAllEntries()
    {
        byte[] empty = ServerPackets.BuildCombatInitialize(6, new byte[24], 1, 2);
        byte[] withEntry = ServerPackets.BuildCombatInitialize(6, new byte[24], 1, 2, new CombatRoundEntry());
        Assert.Equal((ushort)46, BitConverter.ToUInt16(empty, 3));
        Assert.Equal((ushort)167, BitConverter.ToUInt16(withEntry, 3)); // captured a7 00
        Assert.Equal(empty.Length - 3, BitConverter.ToUInt16(empty, 3));
        Assert.Equal(withEntry.Length - 3, BitConverter.ToUInt16(withEntry, 3));
        Assert.Equal(0, empty[48]); Assert.Equal(1, withEntry[48]);
    }

    [Fact]
    public void AnimationIdsResolveToShippedNamesAndAllTacticOutcomesHaveMoves()
    {
        Assert.Equal("AikidoAbility KickRevBlowout", CombatAnimations.All.Single(m => m.Id == 748).Name);
        Assert.Equal((ushort)2500, CombatAnimations.All.Single(m => m.Id == 748).DurationMs);
        Assert.Equal("Trans Stand-SS", CombatAnimations.All.Single(m => m.Id == 21012).Name);
        foreach (CombatTactic first in Enum.GetValues<CombatTactic>())
        foreach (CombatTactic second in Enum.GetValues<CombatTactic>())
        foreach (uint flags in new uint[] { 1, 4, 8, 0x20, 0x41, 0x48, 0x100, 0x801, 0x820 })
        {
            var move = CombatAnimations.Choose(first, second, flags, 2);
            Assert.True(move.DurationMs > 0);
            Assert.NotEqual(0ul, move.Reference);
        }
    }
}
