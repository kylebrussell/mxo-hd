using System.Collections.Generic;
using hds;
using hds.databases;

namespace hds.Tests;

public class BagSlotTests
{
    [Fact]
    public void LowestFreeBagSlotSkipsUsedSlotsAndNeverReturnsAWornSlot()
    {
        Assert.Equal(1, MyWorldDbAccess.LowestFreeBagSlot(new List<int>()));          // empty bag: slot 1, not 0
        Assert.Equal(3, MyWorldDbAccess.LowestFreeBagSlot(new List<int> { 1, 2, 4 })); // first gap

        var full = new List<int>();
        for (int i = 1; i <= 96; i++) full.Add(i);
        Assert.Equal(0, MyWorldDbAccess.LowestFreeBagSlot(full));                    // full: 0, not 97 (worn)

        full.Remove(50);
        Assert.Equal(50, MyWorldDbAccess.LowestFreeBagSlot(full));
    }
}
