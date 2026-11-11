using hds;

namespace hds.Tests;

public class CombatRoundEntryTests
{
    private static byte[] B(string hex) => StringUtils.hexStringToBytes(hex);
    private static float F(string hex) => BitConverter.ToSingle(B(hex));

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
