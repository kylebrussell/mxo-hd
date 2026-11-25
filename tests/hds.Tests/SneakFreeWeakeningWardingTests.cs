using System.Runtime.CompilerServices;
using hds;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class SneakFreeWeakeningWardingTests
{
    private const ushort Weakening = SpySneakOpeners.WeakeningBlow, Warding = SpyMasterSneakOpeners.WardingBlow;

    [Theory]
    [InlineData(Weakening, 6, 113)]
    [InlineData(Warding, 4, 141)]
    public void BothBlowsAreShippedSneakFreeThrowsWithLevelScaledThrownDamage(ushort id, double range, int damageAt50)
    {
        var entry = AbilityCatalog.Find(id)!;
        Assert.True(SneakFreeThrowAdmission.Handles(id));
        Assert.Equal(1, entry.Number("Ability/CastWhenUsed"));
        Assert.Equal(50, entry.Number("Ability/CastOutsideTargetFOVAngle"));
        Assert.Equal(Stealth.StealthedFlag, (uint)entry.Number("Ability/CastCasterRequiredStates"));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(id));
        Assert.Equal(range, entry.Number("Ability/SelectionRange"));
        Assert.True(SpyAbilityRegistry.RetainsStealthDuringCast(id));
        Assert.True(SpyAbilityRegistry.TryResolveDamage(entry, 50, new Random(1), out ushort damage));
        Assert.Equal(damageAt50, damage);
    }

    [Theory]
    [InlineData(Weakening)]
    [InlineData(Warding)]
    public void TheRegistryUsesTheSneakFreeAdmissionRatherThanTheOpenerShortcut(ushort id)
    {
        var caster = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        caster.playerData = new ClientData();
        caster.playerData.setCharID(110);
        caster.playerInstance = new PlayerCharacter();
        caster.viewMan = new ViewManager();
        // Not in Spy Style: the previous opener path only checked combat and the selection.
        Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out uint reason));
        Assert.Equal(AbilityRefusals.WrongStyle, reason);
    }

    [Fact]
    public void ModePlanCarriesBothBlowsAtALegalLevel50Loadout()
    {
        var plan = NativeQaModes.RequireKnown("ability-parity-weakening-warding-blows").Plan;
        Assert.Equal(new ushort[] { CombatStyles.SpyStyle, Stealth.SneakGrantAbility, Weakening, Warding }, plan.HotbarAbilities);
        Assert.Contains(Weakening, plan.Abilities);
        Assert.Contains(Warding, plan.Abilities);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));
    }
}
