using hds;

namespace hds.Tests;

public class MobAggroTests
{
    private static Mob Hostile(ushort level)
    {
        Mob mob = new Mob();
        mob.setLevel(level);
        return mob;
    }

    [Fact]
    public void HostileNpcAttacksNearbyPlayersOfSimilarLevel()
    {
        Mob mob = Hostile(10);
        Assert.True(MobAggro.WouldAttack(mob, MobAggro.RangeUnits - 1, 12));
        Assert.False(MobAggro.WouldAttack(mob, MobAggro.RangeUnits + 1, 12));
        Assert.False(MobAggro.WouldAttack(mob, 100, 10 + MobAggro.LevelGap + 1)); // far above it
    }

    [Fact]
    public void FriendlyDeadUnspawnedOrBusyNpcsDontAttack()
    {
        Mob friendly = Hostile(10);
        friendly.isFriendly = true;
        Mob dead = Hostile(10);
        dead.setIsDead(true);
        Mob gone = Hostile(10);
        gone.setIsSpawned(false);
        Mob busy = Hostile(10);
        busy.setCombatTarget((WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient)));

        Assert.False(MobAggro.WouldAttack(friendly, 100, 10));
        Assert.False(MobAggro.WouldAttack(dead, 100, 10));
        Assert.False(MobAggro.WouldAttack(gone, 100, 10));
        Assert.False(MobAggro.WouldAttack(busy, 100, 10));
    }

    [Fact]
    public void NpcStopsAtArmsLengthFacingThePlayer()
    {
        MobAggro.StopPoint(0, 0, 1000, 0, 150, out double x, out double z);
        Assert.Equal(150, x, 3);
        Assert.Equal(0, z, 3);

        // Already closer than that: stays put
        MobAggro.StopPoint(0, 0, 100, 0, 150, out x, out z);
        Assert.Equal(100, x, 3);

        Assert.Equal(0, MobAggro.YawTowards(0, 0, 0, 10));    // +z
        Assert.Equal(64, MobAggro.YawTowards(0, 0, 10, 0));   // +x, a quarter turn
        Assert.Equal(128, MobAggro.YawTowards(0, 0, 0, -10)); // -z
    }
}
