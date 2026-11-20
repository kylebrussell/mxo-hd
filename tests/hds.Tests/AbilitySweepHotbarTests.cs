using System.Buffers.Binary;
using System.Security.Cryptography;
using HeadlessClient;
using NativeQa;

namespace hds.Tests;

public class AbilitySweepHotbarTests
{
    private static readonly ushort[] Abilities = { 132, 267, 133, 260, 262, 428, 430, 412 };

    [Fact]
    public void SweepLoadRequestMatchesNativeCaptureInFull()
    {
        var request = MarginSession.StockCharacterLoadRequest(110);
        Assert.Equal(190, request.Length);
        Assert.Equal("C68FC42E76B2300574151AE4DCF23AB4D71F1A14394ED0752EC986677494500A",
            Convert.ToHexString(SHA256.HashData(request)));
    }

    [Fact]
    public void ReplacesOnlyPageTwoSlotsOneThroughEightAndKeepsAllFollowingBytes()
    {
        byte[] oldMacro = { 0, 3, 4, 0, 0, 0, 1, 0, 0, 0, (byte)'x', 0 };
        byte[] replaced = { 1, 2, 0, 0, 0, 0, 99, 0, 0, 0 };
        byte[] tactic = { 1, 8, 7, 0, 0, 0, 11, 12, 13 };
        byte[] inventory = { 7, 0, 1, 0, 0, 0, 11, 0, 0, 0, 12, 0, 0, 0, 13, 0, 0, 0 };
        byte[] macro = { 8, 1, 4, 0, 0, 0, 255, 255, 255, 255, 1, 0, 0, 0, (byte)'M', 0, 1, 0, 0, 0, (byte)'x', 0 };
        byte[] tail = Enumerable.Range(0, 100).Select(value => (byte)value).ToArray();
        byte[] template = Template(tail, oldMacro, replaced, tactic, inventory, macro);

        byte[] seeded = AbilitySweepHotbar.Seed(template, Abilities);
        int recordsStart = 16 + oldMacro.Length;
        Assert.Equal(12u, BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(12)));
        Assert.Equal(oldMacro, seeded[16..recordsStart]);
        for (byte slot = 0; slot < Abilities.Length; slot++)
        {
            int start = recordsStart + 10 * slot;
            Assert.Equal(1, seeded[start]);
            Assert.Equal(slot, seeded[start + 1]);
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(start + 2)));
            Assert.Equal((uint)Abilities[slot], BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(start + 6)));
        }
        int tailStart = recordsStart + 80 + tactic.Length + inventory.Length + macro.Length;
        Assert.Equal(tactic.Concat(inventory).Concat(macro).Concat(tail).ToArray(), seeded[(recordsStart + 80)..]);
        Assert.Equal((uint)(tailStart + 32), 4 + BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(4)));
        Assert.Equal(seeded, AbilitySweepHotbar.Seed(seeded, Abilities));
    }

    [Fact]
    public void ShortPlanUsesItsOwnOrderAndKeepsTheFollowingSlots()
    {
        ushort[] actions = { 1071, 1073, 158 };
        byte[] untouched = { 1, 3, 0, 0, 0, 0, 99, 0, 0, 0 };
        byte[] original = Template(new byte[48], untouched);
        var plan = new NativeQaModePlan(new ushort[] { 245, 251, 158 }, 0, 0,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), hotbarAbilities: actions);
        actions[0] = 1;

        byte[] seeded = AbilitySweepHotbar.Seed(original, plan.HotbarAbilities);
        Assert.Equal(new ushort[] { 1071, 1073, 158 }, plan.HotbarAbilities);
        Assert.Equal(new[] { "Open stock hotbar page 2.", "slot 1 = ability 1071", "slot 2 = ability 1073", "slot 3 = ability 158" }, plan.ReadyMessages);
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(12)));
        Assert.Equal(1071u, BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(22)));
        Assert.Equal(1073u, BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(32)));
        Assert.Equal(158u, BinaryPrimitives.ReadUInt32LittleEndian(seeded.AsSpan(42)));
        Assert.Equal(untouched, seeded[46..56]);
    }

    [Fact]
    public void RefusesMalformedMacroAndUnrecognizedShortcutTypes()
    {
        byte[] tail = new byte[48];
        Assert.Throws<InvalidDataException>(() => AbilitySweepHotbar.Seed(
            Template(tail, new byte[] { 0, 0, 4, 0, 0, 0, 255, 255, 255, 255, 2, 0, 0, 0 }), Abilities));
        Assert.Throws<InvalidDataException>(() => AbilitySweepHotbar.Seed(
            Template(tail, new byte[] { 0, 0, 8, 0, 0, 0 }), Abilities));
    }

    private static byte[] Template(byte[] tail, params byte[][] records)
    {
        int recordBytes = records.Sum(record => record.Length);
        byte[] header = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 2);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)(12 + recordBytes + 32));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)records.Length);
        return header.Concat(records.SelectMany(record => record)).Concat(tail).ToArray();
    }
}
