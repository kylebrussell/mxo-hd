using hds;
using hds.shared;

namespace hds.Tests;

public class AbilityTargetTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void RetailDamageRangesAndEffects()
    {
        // Logic Bomb 3 (actions_combat 10:27:51, NPC view 16): fb 03 -> db 02 = 288 with EffectID 0x28000a0d
        AbilityTargets.Effect lb3 = AbilityTargets.Retail[337];
        Assert.InRange(288, lb3.MinDamage, lb3.MaxDamage);
        Assert.Equal(0x28000a0du, lb3.HitFx);
        // Code Nuke's effect (abilitychangecoder 10:31:53)
        Assert.Equal(0x28000a11u, AbilityTargets.Retail[359].HitFx);

        var random = new Random(1);
        for (int i = 0; i < 200; i++)
        {
            Assert.InRange(AbilityTargets.RollDamage(lb3, random), (ushort)283, (ushort)346);
        }
        Assert.Equal(424, AbilityTargets.RollDamage(AbilityTargets.Retail[330], random));
    }

    [Fact]
    public void PlagueZoneIsDamageOverTime()
    {
        // No direct damage; its damage over time is a tempmod: 424-444 over 15 s, ticks of 71 every 2.5 s
        AbilityTargets.Effect plague = AbilityTargets.Retail[466];
        Assert.Equal(0, AbilityTargets.RollDamage(plague, new Random()));
        TempMods.Mod dot = Assert.Single(TempMods.Retail[466].Mods);
        Assert.Equal(TempMods.Proc.Dot, dot.Proc);
        Assert.Equal(15f, dot.Seconds);
        Assert.Equal(71, TempMods.DotTick(424, 15, TempMods.DotIntervalSeconds));
        Assert.Equal(0x28000430u, TempMods.VirusCrashFx);
    }

    [Fact]
    public void HitUpdateCarriesTheAbilityEffect()
    {
        // NPC view: 04 80 80 80 c0 <health> c0 <effect> 01 <counter> (retail Logic Bomb 3 on view 16: db 02, 0d 0a 00 28, 08)
        Assert.Equal("048080" + "80c0" + "db02" + "c0" + "0d0a0028" + "01" + "08",
            Hex(Mob.BuildHitUpdate(0x02db, 0x28000a0d, 8)));
    }

    [Fact]
    public void CloseCombatRequestMatchesMobAggro()
    {
        var view = new ClientView(599, 0x2f, 1234) { spawnId = 0x22 };
        Assert.Equal("2f002200016b", Hex(AbilityTargets.CloseCombatRequest(view)));
    }

    [Fact]
    public void StunnedNpcDoesNotAttack()
    {
        var mob = new Mob();
        double now = TimeUtils.GetSimTimeSeconds();
        Assert.False(mob.IsStunned(now));
        mob.Stun(now + TempMods.Retail[333].Mods[0].Seconds);
        Assert.True(mob.IsStunned(now + 3.9));
        Assert.False(mob.IsStunned(now + 5.1));
        // A shorter stun doesn't cut a longer one short
        mob.Stun(now + 1);
        Assert.True(mob.IsStunned(now + 3.9));
    }
}
