using hds;

namespace hds.Tests;

public class NpcLootCacheTests
{
    private static Mob Corpse(ulong entity)
    {
        Mob mob = new Mob();
        mob.setEntityId(entity);
        mob.setIsDead(true);
        return mob;
    }

    [Fact]
    public void ASecondKillPreservesTheFirstCorpsesRemainingContents()
    {
        var cache = new NpcLootCache();
        NpcLootCache.Corpse first = cache.Add(Corpse(1), 500, 6791, new uint[] { 7332, 2399 });
        first.Info = 0;
        first.Items.Remove(7332);
        cache.Add(Corpse(2), 900, 0, new uint[] { 48150 });

        Assert.Same(first, cache.Select(1));
        Assert.Equal(0u, cache.Current!.Info);
        Assert.Equal(6791u, cache.Current.MissionItem);
        Assert.Equal(new uint[] { 2399 }, cache.Current.Items);
        Assert.Equal(900u, cache.Select(2)!.Info);
    }

    [Fact]
    public void APlayersCacheGrantsAccessOnlyToTheirOwnCorpses()
    {
        var owner = new NpcLootCache();
        var other = new NpcLootCache();
        owner.Add(Corpse(1), 500, 0, new uint[] { 7332 });
        other.Add(Corpse(2), 900, 0, Array.Empty<uint>());

        Assert.Null(other.Select(1));
        Assert.Null(other.Current);
        Assert.Equal(500u, owner.Select(1)!.Info);
    }

    [Fact]
    public void ARespawnAndSecondDeathCannotReopenThePreviousLifesLoot()
    {
        var cache = new NpcLootCache();
        Mob mob = Corpse(1);
        cache.Add(mob, 500, 0, new uint[] { 7332 });
        mob.ResetForRespawn();
        Assert.Null(cache.Current);
        Assert.True(mob.TryClaimDeath());
        Assert.Null(cache.Select(1));

        cache.Add(mob, 900, 0, new uint[] { 48150 });
        Assert.Equal(900u, cache.Select(1)!.Info);
    }

    [Fact]
    public void DespawnedAndRemovedMissionCorpsesCannotBeLooted()
    {
        var cache = new NpcLootCache();
        Mob mob = Corpse(1);
        cache.Add(mob, 500, 0, new uint[] { 7332 });
        mob.setIsSpawned(false);
        Assert.Null(cache.Select(1));

        Mob mission = Corpse(2);
        cache.Add(mission, 900, 6791, Array.Empty<uint>());
        cache.Forget(mission);
        Assert.Null(cache.Current);
        Assert.Null(cache.Select(2));
    }
}
