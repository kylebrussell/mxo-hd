using hds;
using hds.shared;

namespace hds.Tests;

/// <summary>Central storage moves (62, and 67/68/5d for storage): client.dll's records and the server's rules.</summary>
public class CentralStorageTests
{
    private const uint CodeBit1 = 7332, CodeBit2 = 7333, Fragment = 2440, Shirt = 1000;

    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static Func<int, StackMoves.Stack> Bag(params (int Slot, uint Goid, int Count)[] items) =>
        slot => items.Where(i => i.Slot == slot).Select(i => new StackMoves.Stack { Goid = i.Goid, Count = i.Count }).FirstOrDefault();

    /// <summary>Storage: an ability in slots 0-2, then whatever the test puts there.</summary>
    private static Func<int, CentralStorage.Entry> Storage(params (int Slot, uint Goid, int Count)[] codes) =>
        slot => slot <= 2 ? CentralStorage.Entry.Ability
            : codes.Where(c => c.Slot == slot).Select(c => CentralStorage.Entry.Code(c.Goid, c.Count)).FirstOrDefault();

    private static CentralStorage.Move ToStorage(int bagSlot, int dest = CentralStorage.NoSlot) =>
        new CentralStorage.Move { Source = (ushort)bagSlot, Dest = (ushort)dest, FromBag = true };

    private static CentralStorage.Move ToBag(int storageSlot, int dest = CentralStorage.NoSlot) =>
        new CentralStorage.Move { Source = (ushort)storageSlot, Dest = (ushort)dest, FromBag = false };

    private static readonly Func<uint, bool> KnowsNothing = _ => false;

    private static CentralStorage.Plan Upload(CentralStorage.Move move, Func<int, StackMoves.Stack> bag, Func<int, CentralStorage.Entry> storage,
        int? free = 3) => CentralStorage.PlanUpload(move, bag, storage, () => free, KnowsNothing);

    private static CentralStorage.Plan Download(CentralStorage.Move move, Func<int, StackMoves.Stack> bag, Func<int, CentralStorage.Entry> storage,
        int? free = 5) => CentralStorage.PlanDownload(move, bag, storage, Enumerable.Range(1, 96), () => free);

    [Fact]
    public void ParsesTheClientsRecord()
    {
        // 62 <SourceSlot u16> <DestinationSlot u16> <PersonalInventoryItem u8> (0x621d2590, size 6)
        Assert.True(CentralStorage.TryParseMove(Convert.FromHexString("07006b0001"), out var up));
        Assert.Equal(((ushort)7, (ushort)CentralStorage.NoSlot, true), (up.Source, up.Dest, up.FromBag));
        Assert.True(CentralStorage.TryParseMove(Convert.FromHexString("0a01050000"), out var down));
        Assert.Equal(((ushort)0x10a, (ushort)5, false), (down.Source, down.Dest, down.FromBag));
        Assert.False(CentralStorage.TryParseMove(new byte[4], out _));
    }

    [Fact]
    public void RecordsMatchTheClientsHandlers()
    {
        Assert.Equal("5f1001" + "00", Hex(CentralStorage.BuildRemove(0x110)));
        // Retail single-item upload (crashover2k uploadcodebits 239): 81 18 01 00 00 00 00 00
        Assert.Equal("8118010000000000", Hex(CentralStorage.BuildUploadResult(1, false, CentralStorage.Success)));
        Assert.Equal("81180000c802000c", Hex(CentralStorage.BuildUploadResult(0, false, CentralStorage.StorageFull)));
        Assert.Equal("811800008500008e", Hex(CentralStorage.BuildUploadResult(5, false, CentralStorage.PartialUpload)));
    }

    [Fact]
    public void UploadMakesANewEntryInTheFirstFreeSlot()
    {
        var plan = Upload(ToStorage(7), Bag((7, CodeBit1, 10)), Storage());
        Assert.False(plan.Refused);
        Assert.Equal((CentralStorage.Success, (ushort)7, 0, (ushort)3, 10, true, 10),
            (plan.Status, plan.BagSlot, plan.BagCount, plan.StorageSlot, plan.StorageCount, plan.StorageIsNew, plan.Moved));
    }

    [Fact]
    public void UploadJoinsAnEntryOfTheSameItemFirst()
    {
        var plan = Upload(ToStorage(7), Bag((7, CodeBit1, 10)), Storage((4, CodeBit2, 3), (9, CodeBit1, 20)));
        Assert.Equal(((ushort)9, 30, false), (plan.StorageSlot, plan.StorageCount, plan.StorageIsNew));
    }

    [Fact]
    public void UploadOntoAChosenSlot()
    {
        var empty = Upload(ToStorage(7, 12), Bag((7, Fragment, 1)), Storage());
        Assert.Equal(((ushort)12, 1, true), (empty.StorageSlot, empty.StorageCount, empty.StorageIsNew));

        var same = Upload(ToStorage(7, 12), Bag((7, CodeBit1, 4)), Storage((12, CodeBit1, 6)));
        Assert.Equal(((ushort)12, 10, false), (same.StorageSlot, same.StorageCount, same.StorageIsNew));
    }

    [Fact]
    public void UploadOfMoreThanAnEntryHoldsLeavesTheRestInTheBag()
    {
        var plan = Upload(ToStorage(7, 12), Bag((7, CodeBit1, 10)), Storage((12, CodeBit1, 125)));
        Assert.Equal((CentralStorage.PartialUpload, 7, 128, 3), (plan.Status, plan.BagCount, plan.StorageCount, plan.Moved));
        Assert.False(plan.Refused);
    }

    [Fact]
    public void UploadRefusals()
    {
        Assert.Equal(CentralStorage.InvalidBagLocation, Upload(ToStorage(97), Bag((97, CodeBit1, 1)), Storage()).Status);
        Assert.Equal(CentralStorage.BagEmpty, Upload(ToStorage(7), Bag(), Storage()).Status);
        Assert.Equal(CentralStorage.InvalidItemType, Upload(ToStorage(7), Bag((7, Shirt, 1)), Storage()).Status);
        Assert.Equal(CentralStorage.InvalidStorageLocation, Upload(ToStorage(7, 0x208), Bag((7, CodeBit1, 1)), Storage()).Status);
        Assert.Equal(CentralStorage.InvalidStorageLocation, Upload(ToStorage(7, 1), Bag((7, CodeBit1, 1)), Storage()).Status);
        Assert.Equal(CentralStorage.StorageNotEmpty, Upload(ToStorage(7, 12), Bag((7, CodeBit1, 1)), Storage((12, CodeBit2, 1))).Status);
        Assert.Equal(CentralStorage.StorageFull, Upload(ToStorage(7, 12), Bag((7, CodeBit1, 1)), Storage((12, CodeBit1, 128))).Status);
        Assert.Equal(CentralStorage.StorageFull, Upload(ToStorage(7), Bag((7, CodeBit1, 1)), Storage(), free: null).Status);
    }

    [Fact]
    public void UploadingAnAbilitysCodeLearnsIt()
    {
        const uint abilityCode = 0x80000000 | (0x1a5 << 10);
        var plan = Upload(ToStorage(7), Bag((7, abilityCode, 1)), Storage());
        Assert.True(plan.LearnAbility);
        Assert.Equal(((ushort)3, 0), (plan.StorageSlot, plan.BagCount));

        var known = CentralStorage.PlanUpload(ToStorage(7), Bag((7, abilityCode, 1)), Storage(), () => 3, _ => true);
        Assert.Equal(CentralStorage.InvalidBagItem, known.Status);
    }

    [Fact]
    public void DownloadTakesWhatOneBagSlotHolds()
    {
        var all = Download(ToBag(9), Bag(), Storage((9, CodeBit1, 20)));
        Assert.Equal(((ushort)5, 20, true, 0), (all.BagSlot, all.BagCount, all.BagIsNew, all.StorageCount));

        var some = Download(ToBag(9), Bag(), Storage((9, CodeBit1, 100)));
        Assert.Equal((64, 36), (some.BagCount, some.StorageCount));

        var fragment = Download(ToBag(9), Bag(), Storage((9, Fragment, 3)));
        Assert.Equal((3, 0), (fragment.BagCount, fragment.StorageCount));
    }

    [Fact]
    public void DownloadJoinsABagStackOfTheSameItem()
    {
        var any = Download(ToBag(9), Bag((2, CodeBit2, 3), (4, CodeBit1, 60)), Storage((9, CodeBit1, 10)));
        Assert.Equal(((ushort)4, 64, false, 6), (any.BagSlot, any.BagCount, any.BagIsNew, any.StorageCount));

        var chosen = Download(ToBag(9, 2), Bag((2, CodeBit1, 3)), Storage((9, CodeBit1, 10)));
        Assert.Equal(((ushort)2, 13, 0), (chosen.BagSlot, chosen.BagCount, chosen.StorageCount));
    }

    [Fact]
    public void DownloadRefusals()
    {
        Assert.Equal(CentralStorage.InvalidStorageLocation, Download(ToBag(0x208), Bag(), Storage()).Status);
        Assert.Equal(CentralStorage.StorageEmpty, Download(ToBag(9), Bag(), Storage()).Status);
        Assert.Equal(CentralStorage.InvalidStorageItem, Download(ToBag(1), Bag(), Storage()).Status);
        Assert.Equal(CentralStorage.InvalidBagLocation, Download(ToBag(9, 97), Bag(), Storage((9, CodeBit1, 1))).Status);
        Assert.Equal(CentralStorage.BagNotEmpty, Download(ToBag(9, 2), Bag((2, Shirt, 1)), Storage((9, CodeBit1, 1))).Status);
        Assert.Equal(CentralStorage.BagFull, Download(ToBag(9, 2), Bag((2, CodeBit1, 64)), Storage((9, CodeBit1, 1))).Status);
        Assert.Equal(CentralStorage.BagFull, Download(ToBag(9), Bag(), Storage((9, CodeBit1, 1)), free: null).Status);
    }

    [Fact]
    public void MergeInsideStorage()
    {
        var plan = CentralStorage.PlanMerge(9, 4, Storage((9, CodeBit1, 30), (4, CodeBit1, 110)));
        Assert.Equal(((ushort)4, 128, (ushort)9, 12), (plan.StorageSlot, plan.StorageCount, plan.SourceSlot, plan.SourceCount));

        Assert.Equal(CentralStorage.StorageNotEmpty, CentralStorage.PlanMerge(9, 4, Storage((9, CodeBit1, 1), (4, CodeBit2, 1))).Status);
        Assert.Equal(CentralStorage.StorageFull, CentralStorage.PlanMerge(9, 4, Storage((9, CodeBit1, 1), (4, CodeBit1, 128))).Status);
        Assert.Equal(CentralStorage.InvalidStorageItem, CentralStorage.PlanMerge(9, 1, Storage((9, CodeBit1, 1))).Status);
        Assert.Equal(CentralStorage.InvalidStorageLocation, CentralStorage.PlanMerge(9, 9, Storage((9, CodeBit1, 1))).Status);
    }

    [Fact]
    public void SplitInsideStorage()
    {
        var plan = CentralStorage.PlanSplit(9, CentralStorage.NoSlot, 4, Storage((9, CodeBit1, 10)), () => 3);
        Assert.Equal(((ushort)9, 6, (ushort)3, 4, true), (plan.SourceSlot, plan.SourceCount, plan.StorageSlot, plan.StorageCount, plan.StorageIsNew));

        var onto = CentralStorage.PlanSplit(9, 4, 4, Storage((9, CodeBit1, 10), (4, CodeBit1, 2)), () => 3);
        Assert.Equal(((ushort)4, 6, false), (onto.StorageSlot, onto.StorageCount, onto.StorageIsNew));

        Assert.Equal(CentralStorage.InvalidTransaction, CentralStorage.PlanSplit(9, 4, 11, Storage((9, CodeBit1, 10)), () => 3).Status);
        Assert.Equal(CentralStorage.StorageNotEmpty, CentralStorage.PlanSplit(9, 4, 1, Storage((9, CodeBit1, 10), (4, CodeBit2, 1)), () => 3).Status);
        Assert.Equal(CentralStorage.InvalidStorageLocation, CentralStorage.PlanSplit(9, 2, 1, Storage((9, CodeBit1, 10)), () => 3).Status);
    }

    [Fact]
    public void FirstFreeSlotSkipsAbilitiesAndCode()
    {
        Assert.Equal(2, CentralStorage.FirstFreeSlot(new HashSet<int> { 0, 1, 3 }));
        Assert.Null(CentralStorage.FirstFreeSlot(new HashSet<int>(Enumerable.Range(0, CentralStorage.Slots))));
    }
}
