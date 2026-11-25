using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

/// <summary>
/// One executable account of every listed shipped behavior for the OpusB Shadow / Assassin lane rows: stored cost, own and
/// family reuse, cast time, selector, the admission gate each row uses, its stored controls, and the level-50 hit the
/// stock client measured with the same native-mode loadout (neutral and, where measured, under +75 Thrown resistance).
/// </summary>
[Collection("AbilityParityWorld")]
public class ShadowAssassinLaneBehaviorTests
{
    private static WorldClient Caster(ushort[] loadout, byte level = 50)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.setCharID(110);
        c.playerData.setEntityId(110);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue(level);
        c.playerInstance.Health.setValue((ushort)5000);
        c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, long.MaxValue);
        AbilityPassives.SetLoaded(c, loadout.Select(id => (id, (ushort)level)));
        CombatStyles.Select(c, CombatStyles.SpyStyle);
        return c;
    }

    [Theory]
    // id, native mode, stealth-started, stock neutral, stock +75 Thrown (-1: shares the typed path), IS, own, group, group s, cast s, range
    [InlineData((ushort)286, "ability-parity-assassin-sneak-attacks", true, 528, 434, 50, 38f, 691, 15f, 0f, 6)]
    [InlineData((ushort)288, "ability-parity-assassin-sneak-attacks", true, 1128, -1, 60, 38f, 691, 15f, 0f, 6)]
    [InlineData((ushort)289, "ability-parity-assassin-sneak-attacks", true, 1070, -1, 40, 38f, 691, 15f, 0f, 6)]
    [InlineData((ushort)388, "ability-parity-assassin-sneak-attacks", true, 1745, -1, 60, 60f, 691, 15f, 0f, 6)]
    [InlineData((ushort)385, "ability-parity-diving-frankensteiner", true, 1146, 942, 80, 60f, 691, 15f, 0f, 6)]
    [InlineData((ushort)387, "ability-parity-spy-instant-sneak-openers", true, 1578, 1298, 37, 60f, 691, 15f, 0f, 6)]
    [InlineData((ushort)1133, "ability-parity-weakening-warding-blows", false, 157, 129, 35, 45f, 691, 15f, 2f, 6)]
    [InlineData((ushort)1134, "ability-parity-weakening-warding-blows", false, 195, 160, 45, 45f, 691, 15f, 2f, 4)]
    [InlineData((ushort)1135, "ability-parity-assassin-timed-throws", false, 41, 33, 30, 6f, 1216, 3f, 2f, 16)]
    public void StoredTermsAndTheStockMeasuredLevel50HitAreReproduced(ushort id, string mode, bool stealthStarted, int neutral,
        int resisted, int cost, float own, int group, float groupSeconds, float cast, int range)
    {
        var entry = AbilityCatalog.Find(id)!;
        Assert.Equal(cost, AbilityCosts.Cost(id));
        Assert.True(AbilityCooldowns.TryGet(id, out var cd));
        Assert.Equal(own, cd.Seconds);
        Assert.Equal(group, cd.Group);
        Assert.Equal(groupSeconds, cd.GroupSeconds);
        Assert.Equal(cast, entry.CastSeconds);
        Assert.Equal(range, entry.Number("Ability/SelectionRange"));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(id));
        Assert.Equal(7, TypedAbilityResistance.StoredType(id));

        // The admission gate each row stores: the instant rear opener, the SneakFree rear throw, or the timed selected throw.
        if (stealthStarted)
            Assert.True(SneakFreeThrowAdmission.GatesInstantOpener(id) || SpyInstantSneakOpenerAdmission.Handles(id));
        else if (id == 1135)
            Assert.True(SpyTimedThrows.Handles(id));
        else
            Assert.True(SneakFreeThrowAdmission.Handles(id));
        Assert.Equal(stealthStarted || id != 1135 ? 50d : 0d, entry.Number("Ability/CastOutsideTargetFOVAngle"));

        var plan = NativeQa.NativeQaModes.Find(mode).Plan;
        Assert.Contains(id, plan.Abilities);
        var caster = Caster(plan.Abilities.ToArray());
        var item = new AbilityItem();
        item.setAbilityID(id);
        ushort resolved = AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: 50);
        ushort hit = AbilityStats.ScaleAbilityDamage(caster, id, SneakAttacks.Add(resolved, id, stealthStarted));
        Assert.Equal(neutral, hit);
        if (resisted >= 0)
            Assert.Equal(resisted, TypedAbilityResistance.Amount(hit, 50, 50, 75));
    }

    [Theory]
    [InlineData((ushort)288, TempMods.ConfuseAbility)]
    [InlineData((ushort)1135, TempMods.RootedAbility)]
    public void StoredControlsAreAppliedByTheLandedEffect(ushort id, ushort state)
    {
        Assert.True(TempMods.TryGet(id, out TempMods.Effect effect));
        Assert.Contains(effect.Mods, m => m.Target == state);
    }

    [Fact]
    public void ReverseBullDogKeepsItsStunCallbackAndIndependentImmunity()
    {
        var mods = AbilityCatalog.Find(388)!.Modifiers;
        Assert.Contains(mods, m => m.Text("StatModTermFunc") == "ApplyStunEffect" && m.Number("MaxValue") == 8);
        Assert.Contains(mods, m => m.Text("StatModTermFunc") == "AddControlImmunityTimerVal" && m.Number("MaxValue") == 16);
    }

    [Fact]
    public void SpyBullDogStoresOneHalfSecondNeutralTerminalOfTen()
    {
        var entry = AbilityCatalog.Find(StealthShieldAbilities.BullDog)!;
        var m = Assert.Single(entry.Modifiers, x => x.Text("StatModTermFunc") == "NeutralDOT");
        Assert.Equal(0.5, m.Number("Duration"));
        Assert.Equal(10, m.Number("MinValue"));
        Assert.Equal(10, m.Number("MaxValue"));
        Assert.False(m.Has("UpdateFuncInterval"));
        Assert.Equal(10, AbilityCosts.Cost(StealthShieldAbilities.BullDog));
        Assert.Equal(4, entry.Number("Ability/SelectionRange"));
        Assert.Equal(50, entry.Number("Ability/CastOutsideTargetFOVAngle"));
        Assert.Equal((uint)Stealth.StealthedFlag, (uint)entry.Number("Ability/CastCasterRequiredStates"));
    }
}
