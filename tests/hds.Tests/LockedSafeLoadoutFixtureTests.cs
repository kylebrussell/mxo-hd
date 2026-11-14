using HeadlessClient;
using hds;
namespace hds.Tests;
public class LockedSafeLoadoutFixtureTests
{
    [Fact]public void ClosedOwnedBatchIsInitiallyUnloadedAndPassesActualAdmission()
    {
        var rows=LockedSafeLoadoutFixture.Rows();
        Assert.Equal(new ushort[]{2,113,120,121,123,130,143,151,152,155,185,1112,1125,142},rows.Select(r=>r.Ability));
        Assert.All(rows,r=>Assert.False(r.Loaded));Assert.Equal((ushort)10,rows.Last().Level);Assert.Equal((ushort)300,rows.Last().Slot);
        var raw=LockedSafeLoadoutFixture.LoadRequest();Assert.Equal(new byte[]{0x80,0xae},raw.Take(2));
        Assert.True(AbilityProgression.TryParseLoadout(raw.Skip(2).ToArray(),out bool unload,out var entries));Assert.False(unload);Assert.Equal((ushort)142,entries.Last().Ability);
        var result=AbilityMemory.Plan(rows,entries,false,PlayerProgression.ExperienceForLevel(50),50);
        Assert.Null(result.Refusal);Assert.Equal(14,result.Accepted.Length);Assert.Equal(32,result.Used);Assert.Equal(100,result.Maximum);
    }
    [Fact]public void MissingRealParentStillRefusesAndNoOptionalFolderRowsAreSeeded()
    {
        var rows=LockedSafeLoadoutFixture.Rows();var raw=LockedSafeLoadoutFixture.LoadRequest();AbilityProgression.TryParseLoadout(raw.Skip(2).ToArray(),out _,out var entries);
        var missing=rows.Where(r=>r.Ability!=120).ToArray();
        Assert.NotNull(AbilityMemory.Plan(missing,entries,false,PlayerProgression.ExperienceForLevel(50),50).Refusal);
        Assert.Equal(AbilityPrerequisites.Current.Closure(142).Keys.Order(),rows.Where(r=>r.Ability!=142).Select(r=>r.Ability).Order());
        foreach(var parent in rows.Where(r=>r.Ability!=142))Assert.Equal(Math.Max(1,AbilityPrerequisites.Current.Closure(142)[parent.Ability]),parent.Level);
    }
}
