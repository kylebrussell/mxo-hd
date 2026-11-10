using hds;

namespace hds.Tests;

/// <summary>Gunshots by retail's free-attack scripts, checked against the captures' 81 68 combat-log lines.</summary>
public class FreeAttackTests
{
    [Theory]
    [InlineData(4.5f, 4)]
    [InlineData(4.0f, 4)]
    [InlineData(3.5f, 3)]
    [InlineData(3.0f, 3)]
    [InlineData(2.5f, 2)]
    [InlineData(5.5f, 5)]
    [InlineData(0.5f, 1)]
    public void WeaponSpeedIsTheReuseTimerRoundedDown(float reuse, int speed)
    {
        Assert.Equal(speed, FreeAttacks.WeaponSpeed(reuse));
    }

    [Theory]
    [InlineData(CombatTactic.Speed, 12)]
    [InlineData(CombatTactic.Power, 12)]
    [InlineData(CombatTactic.Grab, 8)]
    [InlineData(CombatTactic.Block, 0)]
    public void RedpillSpecialAtLevelOneHitsLikeRetail(CombatTactic tactic, int expected)
    {
        // Tutorial captures: new characters' Redpill Special (BaseDamage 1, 4.5 s) hit Alex for 12 before absorption
        // on Speed and Power, 8 on Grab. int(3 x 1.2) = 3 is why Power doesn't add anything here.
        var rng = new Random(3);
        int damage = FreeAttacks.Damage(rng, FreeAttacks.DamageBonus(1), 0, 1, 0, FreeAttacks.WeaponSpeed(4.5f), tactic, CombatTacticType.Normal);
        Assert.Equal(expected, damage);
    }

    [Fact]
    public void DamageIsAlwaysAWholeMultipleOfTheWeaponSpeed()
    {
        var rng = new Random(11);
        for (int i = 0; i < 500; i++)
        {
            int baseDamage = rng.Next(1, 16), level = rng.Next(1, 51);
            int speed = FreeAttacks.WeaponSpeed(new[] { 2.5f, 3.0f, 3.5f, 4.0f, 4.5f }[rng.Next(5)]);
            var tactic = (CombatTactic)rng.Next(3);
            int damage = FreeAttacks.Damage(rng, FreeAttacks.DamageBonus(level), 0, baseDamage, 0, speed, tactic, CombatTacticType.Normal);
            Assert.Equal(0, damage % speed);
            Assert.True(damage > 0);
        }
    }

    [Fact]
    public void HighLevelGunsLandInRetailsRange()
    {
        // A level 50 rifle (BaseDamage 15, 3.5 s): retail's level 50 rifles and shotguns hit for about 31-37 a second
        // before absorption, so 93-111 for a 3 s weapon speed
        var rng = new Random(5);
        for (int i = 0; i < 200; i++)
        {
            int damage = FreeAttacks.Damage(rng, FreeAttacks.DamageBonus(50), 0, 15, 0, 3, CombatTactic.Speed, CombatTacticType.Normal);
            Assert.InRange(damage, 3 * (21 + 5), 3 * (21 + 20));
        }
    }

    [Fact]
    public void EnergizedAndPreciseChangeTheDamage()
    {
        var rng = new Random(1);
        Assert.Equal(20 * 4, FreeAttacks.Damage(rng, 20, 0, 0, 0, 4, CombatTactic.Speed, CombatTacticType.Normal));
        Assert.Equal(23 * 4, FreeAttacks.Damage(rng, 20, 0, 0, 0, 4, CombatTactic.Speed, CombatTacticType.Energized));
        Assert.Equal(17 * 4, FreeAttacks.Damage(rng, 20, 0, 0, 0, 4, CombatTactic.Speed, CombatTacticType.Precise));
    }

    [Fact]
    public void AbsorptionMatchesRetailsAgentHits()
    {
        // Agent's SemiAutoPistolSystem on a much lower player: 680 before absorption, 1292 done, -612 absorbed (cap -0.9)
        Assert.Equal(1292, FreeAttacks.Absorb(680, 50, 30, 0, out short absorbed));
        Assert.Equal(-612, absorbed);
        // Level 1 against level 1, no armor: 22% absorbed; at level 10 the no-armor part is already -11%, adding damage
        Assert.Equal(78, FreeAttacks.Absorb(100, 1, 1, 0, out absorbed));
        Assert.Equal(22, absorbed);
        Assert.Equal(111, FreeAttacks.Absorb(100, 10, 10, 0, out absorbed));
        Assert.InRange(absorbed, (short)-11, (short)-10); // -10.999..., truncated as the script's int() does
        // Block's 0 stays 0
        Assert.Equal(0, FreeAttacks.Absorb(0, 10, 10, 0, out absorbed));
    }

    [Fact]
    public void ShotsHitAboutAsOftenAsRetails()
    {
        // Retail's captured shots hit 423 of 542 times (78%)
        var rng = new Random(77);
        foreach (int level in new[] { 1, 50 })
        {
            int hits = 0;
            for (int i = 0; i < 4000; i++)
            {
                var shot = FreeAttacks.Fire(rng, level, CombatTactic.Speed, CombatTacticType.Normal, level, CombatTactic.Speed, 2565);
                Assert.Equal(shot.Hit, shot.AttackRoll > shot.DefenseRoll);
                if (shot.Hit)
                {
                    hits++;
                    Assert.True(shot.Damage > 0);
                }
                else
                {
                    Assert.Equal(0, shot.Damage);
                }
            }
            Assert.InRange(hits, 2800, 3400);
        }
    }

    [Fact]
    public void BlockNeverHits()
    {
        var rng = new Random(8);
        for (int i = 0; i < 300; i++)
        {
            Assert.False(FreeAttacks.Fire(rng, 30, CombatTactic.Block, CombatTacticType.Normal, 30, CombatTactic.Speed, 692).Hit);
        }
    }
}
