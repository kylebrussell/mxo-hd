using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

/// <summary>
/// Rank-50 Aikido Redirection (405) feeds all five active stat consumers (1030, 1042, 1142, 1143, 1148)
/// and publishes 1068 without altering unlinked Enraged state 93, restoring every baseline on unload.
/// </summary>
[Collection("AbilityParityWorld")]
public sealed class AikidoRedirectionClosureTests
{
    private const ushort Redirection = 405;
    private const int Level = 50;

    private static WorldClient Client()
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.setCharID(110);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerData.isBot = true;
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue((byte)Level);
        c.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        return c;
    }

    [Fact]
    public void AikidoRedirection405FeedsViralDefensePowerlessMeleeDamageDefenseAndViralResistance()
    {
        var c = Client();
        var viralSpecial = new AbilityItem();
        viralSpecial.setAbilityID(38); // TestFunction 2 (viral) special

        int baseViralDef = AbilityStats.SpecialDefense(c, viralSpecial);
        float basePowerless = AbilityControlDurations.Scale(c, TempMods.PowerlessAbility, 10f);
        float baseEnrage = AbilityControlDurations.Scale(c, TempMods.EnrageAbility, 10f);
        int baseMeleeDmg = AbilityStats.ScaleTypedAbilityDamage(c, 0, 200);
        int baseMeleeDef = AbilityStats.Defense(c, ranged: false);
        int baseViralRes = TypedResistanceBonuses.Player(c, 2, 0);

        AbilityPassives.SetLoaded(c, new (ushort, ushort)[] { (Redirection, Level) });

        // Exact rank-50 records verified natively in PR1124/PR1133/PR1142.
        Assert.Equal(6, AbilityStats.Player(c, 1030));
        Assert.Equal(10, AbilityStats.Player(c, 1042));
        Assert.Equal(5, AbilityStats.Player(c, 1068));
        Assert.Equal(1, AbilityStats.Player(c, 1142));
        Assert.Equal(5, AbilityStats.Player(c, 1143));
        Assert.Equal(12, AbilityStats.Player(c, 1148));

        // Live runtime consumers.
        Assert.Equal(baseViralDef + 3, AbilityStats.SpecialDefense(c, viralSpecial));          // 1030 +6 -> 50 x 6% = +3
        Assert.Equal(11f, AbilityControlDurations.Scale(c, TempMods.PowerlessAbility, 10f), 4); // 1042 +10 -> 10 -> 11 s
        Assert.Equal(baseEnrage, AbilityControlDurations.Scale(c, TempMods.EnrageAbility, 10f), 4); // 1068 unlinked state 93
        Assert.Equal((int)(200 * (double)(float)1.01), AbilityStats.ScaleTypedAbilityDamage(c, 0, 200)); // 1142 +1 -> 200 -> 201 (binary32)
        Assert.Equal(baseMeleeDef + 2, AbilityStats.Defense(c, ranged: false));                // 1143 +5 -> 50 x 5% = +2
        Assert.Equal(baseViralRes + 26, TypedResistanceBonuses.Player(c, 2, 0));               // 1148 +12 -> 220 x 12% = +26
        Assert.Equal((ushort)600, TypedAbilityResistance.Amount(640, Level, Level, 26));       // matches PR1133 640 -> 600

        // Unloading restores every consumer cleanly.
        AbilityPassives.SetLoaded(c, Array.Empty<(ushort, ushort)>());
        Assert.Equal(baseViralDef, AbilityStats.SpecialDefense(c, viralSpecial));
        Assert.Equal(basePowerless, AbilityControlDurations.Scale(c, TempMods.PowerlessAbility, 10f), 4);
        Assert.Equal(baseEnrage, AbilityControlDurations.Scale(c, TempMods.EnrageAbility, 10f), 4);
        Assert.Equal(baseMeleeDmg, AbilityStats.ScaleTypedAbilityDamage(c, 0, 200));
        Assert.Equal(baseMeleeDef, AbilityStats.Defense(c, ranged: false));
        Assert.Equal(baseViralRes, TypedResistanceBonuses.Player(c, 2, 0));
    }
}
