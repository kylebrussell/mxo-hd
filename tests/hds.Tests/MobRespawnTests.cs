using hds;

namespace hds.Tests;

public class MobRespawnTests
{
    private static Mob DeadMob(ushort missionInstance = 0)
    {
        Mob mob = new Mob();
        mob.setName("Blackwoods");
        mob.setHealthM(500);
        mob.setHealthC(0);
        mob.xBase = 100;
        mob.yBase = 5;
        mob.zBase = -200;
        mob.setXPos(130);
        mob.setYPos(5);
        mob.setZPos(-180);
        mob.setIsDead(true);
        mob.setIsLootable(true);
        mob.missionInstance = missionInstance;
        return mob;
    }

    [Fact]
    public void CorpseStaysUntilItsTimeIsUp()
    {
        Mob mob = DeadMob();
        MobLifecycle.Died(mob, 1000);
        MobLifecycle.Tick(1000 + MobLifecycle.CorpseSeconds - 1);

        Assert.True(mob.getIsSpawned());
        Assert.True(mob.getIsDead());
    }

    [Fact]
    public void CorpseIsUnspawnedThenTheNpcComesBackAtItsSpawnPoint()
    {
        Mob mob = DeadMob();
        MobLifecycle.Died(mob, 2000);

        MobLifecycle.Tick(2000 + MobLifecycle.CorpseSeconds);
        Assert.False(mob.getIsSpawned());
        Assert.True(mob.getIsDead());

        MobLifecycle.Tick(2000 + MobLifecycle.RespawnSeconds);
        Assert.True(mob.getIsSpawned());
        Assert.False(mob.getIsDead());
        Assert.False(mob.getIsLootable());
        Assert.Equal(500, mob.getHealthC());
        Assert.Equal(100, mob.getXPos());
        Assert.Equal(-200, mob.getZPos());
    }

    [Fact]
    public void MissionNpcsDontRespawn()
    {
        Mob mob = DeadMob(missionInstance: 0x2f);
        MobLifecycle.Died(mob, 3000);
        MobLifecycle.Tick(3000 + MobLifecycle.RespawnSeconds + 1);

        Assert.True(mob.getIsSpawned());
        Assert.True(mob.getIsDead());
    }
}
