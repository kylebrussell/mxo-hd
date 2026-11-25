using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

/// <summary>
/// Each listed stat of the loaded Code Branching 328, Code Shield 358, Enhanced Potency 422, SMG Specialization 491
/// and Handgun Artistry 498 passives reaches its runtime consumer at rank 50, and the consumer returns to its baseline
/// when the passive is unloaded.
/// </summary>
[Collection("AbilityParityWorld")]
public class LoadedPassiveConsumerTests
{
    private const int Level = 50;

    private static WorldClient Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(110);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)Level);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        return client;
    }

    private sealed record Reading(int MeleeDefense, int RangedDefense, int RangedAttack, int MeleeDamage, int BallisticDamage,
        int ViralResistance, int ThrownResistance, int ViralDefense, int ViralAccuracy, float Powerless, float Stun,
        float Enrage, int RangedToughness);

    private static Reading Read(WorldClient c)
    {
        var viralSpecial = new AbilityItem();
        viralSpecial.setAbilityID(38); // a shipped TestFunction 2 (viral) special
        return new Reading(
            AbilityStats.Defense(c, false), AbilityStats.Defense(c, true), AbilityStats.Attack(c, true),
            AbilityStats.ScaleTypedAbilityDamage(c, 0, 100), AbilityStats.ScaleTypedAbilityDamage(c, 1, 100),
            TypedResistanceBonuses.Player(c, 2, 0), TypedResistanceBonuses.Player(c, 7, 0),
            AbilityStats.SpecialDefense(c, viralSpecial), AbilityStats.ViralAccuracy(c),
            AbilityControlDurations.Scale(c, TempMods.PowerlessAbility, 10),
            DeflectCode.Duration(10, Level, 0, DeflectCode.PlayerBonus(c), TempMods.StunAbility),
            AbilityControlDurations.Scale(c, TempMods.EnrageAbility, 10),
            AbilityStats.Toughness(c, true));
    }

    private static (Reading Base, Reading Loaded) Cycle(ushort ability)
    {
        var c = Client();
        var before = Read(c);
        AbilityPassives.SetLoaded(c, new[] { (ability, (ushort)Level) });
        var loaded = Read(c);
        AbilityPassives.SetLoaded(c, Array.Empty<(ushort, ushort)>());
        Assert.Equal(before, Read(c)); // unloading restores every consumer
        return (before, loaded);
    }

    [Fact]
    public void CodeBranching328FeedsDefenceAccuracyMeleeDamageAndViralResistance()
    {
        var (b, l) = Cycle(328);
        Assert.Equal(b.MeleeDefense + 2, l.MeleeDefense);      // 1143 +5: level 50 x 5%
        Assert.Equal(b.RangedDefense + 2, l.RangedDefense);    // 1027 +5
        Assert.Equal(b.RangedAttack + 5, l.RangedAttack);      // 996 +10
        Assert.Equal(b.MeleeDamage + 10, l.MeleeDamage);       // 1142 +10 through linked melee influence 159
        Assert.Equal(b.BallisticDamage, l.BallisticDamage);
        Assert.Equal(b.ViralResistance + 22, l.ViralResistance); // 1148 +10: (4 x 50 + 20) x 10%
        Assert.Equal(b.ThrownResistance, l.ThrownResistance);
    }

    [Fact]
    public void CodeShield358FeedsInterlockViralDefenceViralAccuracyAndPowerlessDuration()
    {
        var (b, l) = Cycle(358);
        Assert.Equal(b.ViralDefense + 5, l.ViralDefense);      // 1030 +11 in the TestFunction 2 special defence
        Assert.Equal(b.ViralAccuracy + 2, l.ViralAccuracy);    // 1147 +5
        Assert.Equal(11f, l.Powerless, 4);                      // 1042 +10 lengthens an applied Powerless 10 s -> 11 s
        Assert.Equal(10f, b.Powerless, 4);
        Assert.Equal(b.MeleeDefense, l.MeleeDefense);
    }

    [Fact]
    public void EnhancedPotency422FeedsMeleeAndBallisticDamageAndTypedResistances()
    {
        var (b, l) = Cycle(422);
        Assert.Equal(b.MeleeDamage + 10, l.MeleeDamage);       // 1142 +10
        Assert.Equal(b.BallisticDamage + 10, l.BallisticDamage); // 1139 +10 through linked ballistic influence 171
        Assert.Equal(b.ThrownResistance + 11, l.ThrownResistance); // 1146 +5: 220 x 5%
        Assert.Equal(b.ViralResistance + 11, l.ViralResistance);   // 1148 +5
    }

    [Fact]
    public void SmgSpecialization491FeedsDeterminationMeleeDefenceAndBallisticDamage()
    {
        var (b, l) = Cycle(491);
        Assert.Equal(10f, b.Stun, 4);
        Assert.Equal(9f, l.Stun, 4);                            // 1047 +10 shortens a new hostile 10 s Stun by 10%
        Assert.Equal(b.MeleeDefense + 2, l.MeleeDefense);       // 1143 +5
        // 1139 +5 through linked influence 171; the binary32 influence factor truncates 100 x 1.05f to 104.
        Assert.Equal((int)(100 * (double)(float)1.05), l.BallisticDamage);
        Assert.Equal(100, b.BallisticDamage);
        // 1068 Enrage Duration Bonus is published, but shipped Enraged state 93 has no linked-duration field.
        Assert.Equal(b.Enrage, l.Enrage);
    }

    [Fact]
    public void HandgunArtistry498FeedsPowerlessDurationMeleeDefenceDeterminationAndBallisticResistance()
    {
        var (b, l) = Cycle(498);
        Assert.Equal(11f, l.Powerless, 4);                      // 1042 +10
        Assert.Equal(b.MeleeDefense + 5, l.MeleeDefense);       // 1143 +10
        Assert.Equal(9.5f, l.Stun, 4);                          // 1047 +5
        Assert.Equal(b.RangedToughness + 11, l.RangedToughness); // 1140 +5: 220 x 5%
    }
}
