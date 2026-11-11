using hds;

namespace hds.Tests;

/// <summary>Splitting (68) and merging (67) stacks in the bag: client.dll's layouts and the server's rules.</summary>
public class StackMovesTests
{
    private static Func<int, StackMoves.Stack> Bag(params (int Slot, uint Goid, int Count)[] items) =>
        slot => items.Where(i => i.Slot == slot).Select(i => new StackMoves.Stack { Goid = i.Goid, Count = i.Count }).FirstOrDefault();

    [Fact]
    public void ParsesTheClientsRecords()
    {
        // 68 <source u8> <dest u8> <amount u32> <central u8> (0x6209b8e0); 67 <slot1 u16> <slot2 u16> <central u8> (0x6209b7c0)
        Assert.True(StackMoves.TryParseSplit(Convert.FromHexString("036b0400000000"), out var split));
        Assert.Equal((3, StackMoves.NoSlot, 4u, false), (split.Source, split.Dest, split.Amount, split.CentralStorage));
        Assert.True(StackMoves.TryParseMerge(Convert.FromHexString("0700030000"), out var merge));
        Assert.Equal((7, 3, false), (merge.Source, merge.Dest, merge.CentralStorage));
        Assert.False(StackMoves.TryParseSplit(new byte[6], out _));
        Assert.False(StackMoves.TryParseMerge(new byte[4], out _));
    }

    [Fact]
    public void SplitIntoTheFirstFreeSlot()
    {
        var plan = StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = StackMoves.NoSlot, Amount = 4 },
            Bag((3, 7332, 10)), () => 5, 64);
        Assert.Null(plan.Refusal);
        Assert.Equal((6, 4, (ushort)5, true), (plan.SourceCount, plan.DestCount, plan.DestSlot, plan.DestIsNew));
    }

    [Fact]
    public void SplitOntoASlot()
    {
        var bag = Bag((3, 7332, 10), (8, 7332, 60), (9, 7333, 1));
        var empty = StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = 12, Amount = 10 }, bag, () => 5, 64);
        Assert.Equal((0, 10, (ushort)12, true), (empty.SourceCount, empty.DestCount, empty.DestSlot, empty.DestIsNew));
        var same = StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = 8, Amount = 4 }, bag, () => 5, 64);
        Assert.Equal((6, 64, (ushort)8, false), (same.SourceCount, same.DestCount, same.DestSlot, same.DestIsNew));
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = 8, Amount = 5 }, bag, () => 5, 64).Refusal);
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = 9, Amount = 1 }, bag, () => 5, 64).Refusal);
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = 100, Amount = 1 }, bag, () => 5, 64).Refusal);
    }

    [Fact]
    public void SplitRefusals()
    {
        var bag = Bag((3, 7332, 10));
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = StackMoves.NoSlot, Amount = 11 }, bag, () => 5, 64).Refusal);
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = StackMoves.NoSlot, Amount = 0 }, bag, () => 5, 64).Refusal);
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 4, Dest = StackMoves.NoSlot, Amount = 1 }, bag, () => 5, 64).Refusal);
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = StackMoves.NoSlot, Amount = 1 }, bag, () => null, 64).Refusal);
        Assert.NotNull(StackMoves.PlanSplit(new StackMoves.Split { Source = 3, Dest = StackMoves.NoSlot, Amount = 1, CentralStorage = true }, bag, () => 5, 64).Refusal);
    }

    [Fact]
    public void MergeMovesWhatFits()
    {
        var bag = Bag((3, 7332, 10), (8, 7332, 60), (9, 7333, 1), (10, 7332, 5));
        var all = StackMoves.PlanMerge(new StackMoves.Merge { Source = 10, Dest = 3 }, bag, 64);
        Assert.Equal((0, 15), (all.SourceCount, all.DestCount));
        var part = StackMoves.PlanMerge(new StackMoves.Merge { Source = 3, Dest = 8 }, bag, 64);
        Assert.Equal((6, 64), (part.SourceCount, part.DestCount));
        Assert.NotNull(StackMoves.PlanMerge(new StackMoves.Merge { Source = 3, Dest = 9 }, bag, 64).Refusal);
        Assert.NotNull(StackMoves.PlanMerge(new StackMoves.Merge { Source = 3, Dest = 3 }, bag, 64).Refusal);
        Assert.NotNull(StackMoves.PlanMerge(new StackMoves.Merge { Source = 3, Dest = 11 }, bag, 64).Refusal);
        Assert.NotNull(StackMoves.PlanMerge(new StackMoves.Merge { Source = 3, Dest = 8, CentralStorage = true }, bag, 64).Refusal);
    }
}
