using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

/// <summary>
/// Executable closure coverage for the Gemini Spy / Cutthroat / Infiltrator / Master Shadow lane rows
/// (119, 149, 151, 152, 153, 365, 1130, 1131) plus Holy Water (547, 1023): stored costs, reuse timers,
/// cast times, selectors, passive/consumer effects, visibility/detection contests, and stock-measured level-50 hits.
/// </summary>
[Collection("AbilityParityWorld")]
public class GeminiLaneClosureTests
{
    private static WorldClient Client(ushort[] loadout, byte level = 50, bool selectSpy = true)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.setCharID(110);
        c.playerData.setEntityId(110);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerData.isBot = true;
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue(level);
        c.playerInstance.Health.setValue((ushort)5000);
        c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        AbilityPassives.SetLoaded(c, loadout.Select(id => (id, (ushort)level)));
        if (selectSpy) CombatStyles.Select(c, CombatStyles.SpyStyle);
        return c;
    }

    [Fact]
    public void Sneak151FeedsMovementSpeedDefensesDeterminationGrantAndIsolatesSneakGait()
    {
        var c = Client(Array.Empty<ushort>(), level: 50, selectSpy: false);
        int baseMeleeDef = AbilityStats.Defense(c, ranged: false);
        int baseRangedDef = AbilityStats.Defense(c, ranged: true);
        float baseStun = DeflectCode.Duration(10f, 50, 0, DeflectCode.PlayerBonus(c), TempMods.StunAbility);
        float baseRun = AbilityBuffs.MovementScaleFor(c, null);
        Assert.Equal(0f, baseRun, 4);
        Assert.Equal(10f, baseStun, 4);

        AbilityPassives.SetLoaded(c, new (ushort, ushort)[] { (151, 50) });

        // Rank-50 151 Sneak: 998 +10 (Run +0.10), 1027 +5 (+2 ranged def), 1143 +5 (+2 melee def), 1047 +5 (Stun 10 -> 9.5 s), grant 1111.
        Assert.Equal(10, AbilityStats.Player(c, 998));
        Assert.Equal(5, AbilityStats.Player(c, 1027));
        Assert.Equal(5, AbilityStats.Player(c, 1143));
        Assert.Equal(5, AbilityStats.Player(c, 1047));
        Assert.Equal(0.10f, AbilityBuffs.MovementScaleFor(c, null), 4);
        Assert.Equal(baseRangedDef + 2, AbilityStats.Defense(c, ranged: true));
        Assert.Equal(baseMeleeDef + 2, AbilityStats.Defense(c, ranged: false));
        Assert.Equal(9.5f, DeflectCode.Duration(10f, 50, 0, DeflectCode.PlayerBonus(c), TempMods.StunAbility), 4);

        // 998 applies only to the ordinary Run channel (GOB 115), never to Sneak gait.
        Assert.NotNull(Stealth.Begin(c, Stealth.Defs[Stealth.SneakGrantAbility], 50));
        try
        {
            Assert.Equal(0f, AbilityBuffs.MovementScaleFor(c, null), 4);
        }
        finally
        {
            Stealth.Cleared(c);
        }
        Assert.Equal(0.10f, AbilityBuffs.MovementScaleFor(c, null), 4);

        // Unloading 151 restores every stat consumer cleanly.
        AbilityPassives.SetLoaded(c, Array.Empty<(ushort, ushort)>());
        Assert.Equal(baseRun, AbilityBuffs.MovementScaleFor(c, null), 4);
        Assert.Equal(baseRangedDef, AbilityStats.Defense(c, ranged: true));
        Assert.Equal(baseMeleeDef, AbilityStats.Defense(c, ranged: false));
        Assert.Equal(baseStun, DeflectCode.Duration(10f, 50, 0, DeflectCode.PlayerBonus(c), TempMods.StunAbility), 4);
    }

    [Fact]
    public void DisarmTraps119EnforcesDetectTrapVisibilityGateAndContestThresholds()
    {
        Assert.Equal(10, AbilityCosts.Cost(UtilityAbilities.DisarmTraps));
        Assert.Equal(UtilityAbilities.DisarmResult.NotTrapped,
            UtilityAbilities.DisarmTrap(armed: false, level: 50, toolBonus: 50, disarmDifficulty: 0, detected: true, roll: 99));

        // Undetected trap (DetectTrapFX unset) critically fails regardless of roll or skill.
        var undetected = UtilityAbilities.DisarmTrap(armed: true, level: 50, toolBonus: 50, disarmDifficulty: 0, detected: false, roll: 99);
        Assert.Equal(UtilityAbilities.DisarmResult.Critical, undetected);
        Assert.Equal(UtilityAbilities.TrapDisarmCritical, UtilityAbilities.DisarmMessage(undetected));

        // Detected trap evaluates Utility.evalContest(level, toolBonus, 0, disarmDifficulty, roll): >0 disarmed, >-25 failed, <=-25 critical.
        var disarmed = UtilityAbilities.DisarmTrap(armed: true, level: 10, toolBonus: 0, disarmDifficulty: 0, detected: true, roll: 50);
        Assert.Equal(UtilityAbilities.DisarmResult.Disarmed, disarmed);
        Assert.Equal(UtilityAbilities.TrapDisarmed, UtilityAbilities.DisarmMessage(disarmed));

        var failed = UtilityAbilities.DisarmTrap(armed: true, level: 10, toolBonus: 0, disarmDifficulty: 0, detected: true, roll: 30);
        Assert.Equal(UtilityAbilities.DisarmResult.Failed, failed);
        Assert.Equal(UtilityAbilities.TrapDisarmFailure, UtilityAbilities.DisarmMessage(failed));

        var crit = UtilityAbilities.DisarmTrap(armed: true, level: 10, toolBonus: 0, disarmDifficulty: 0, detected: true, roll: 10);
        Assert.Equal(UtilityAbilities.DisarmResult.Critical, crit);
        Assert.Equal(UtilityAbilities.TrapDisarmCritical, UtilityAbilities.DisarmMessage(crit));
    }

    [Fact]
    public void SixthSense149PublishesRankScaledDetectionRadiusThresholdAndCost()
    {
        Assert.Equal(10, AbilityCosts.Cost(SixthSense.Ability));
        var c = Client(Array.Empty<ushort>(), level: 50, selectSpy: false);
        Assert.Equal(0, AbilityStats.Player(c, SixthSense.Detection));

        AbilityPassives.SetLoaded(c, new (ushort, ushort)[] { (SixthSense.Ability, 10) });
        Assert.Equal(40, AbilityStats.Player(c, SixthSense.Detection));

        AbilityPassives.SetLoaded(c, new (ushort, ushort)[] { (SixthSense.Ability, 50) });
        Assert.Equal(200, AbilityStats.Player(c, SixthSense.Detection));

        AbilityPassives.SetLoaded(c, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Player(c, SixthSense.Detection));

        Assert.True(SixthSense.WithinRadius((0, 0, 0), (1500, 0, 0)));
        Assert.False(SixthSense.WithinRadius((0, 0, 0), (1501, 0, 0)));
        Assert.False(SixthSense.Detects(roll: 5, detection: 40, stealth: 0));
        Assert.True(SixthSense.Detects(roll: 20, detection: 40, stealth: 0));
        Assert.True(SixthSense.Detects(roll: 5, detection: 40, stealth: 1));
    }

    [Theory]
    // id, native mode, stealth-started, stock neutral, stock +75 Thrown, IS, own, group, group s, cast s, range, fov
    [InlineData((ushort)152, "ability-parity-spy-instant-sneak-openers", true, 158, 130, 20, 30f, 691, 15f, 0f, 6, 50d)]
    [InlineData((ushort)153, "ability-parity-drop-kick-resisted-throws", true, 311, 255, 20, 30f, 691, 15f, 0f, 6, 50d)]
    [InlineData((ushort)365, "ability-parity-ghost-strike-parity", false, 37, 30, 60, 10f, 0, 0f, 1.5f, 10, 0d)]
    [InlineData((ushort)1130, "ability-parity-leg-infuriating-staggering-throws", false, 201, 165, 40, 45f, 691, 15f, 2f, 4, 50d)]
    [InlineData((ushort)1131, "ability-parity-leg-infuriating-staggering-throws", false, 237, 195, 25, 45f, 691, 15f, 2f, 6, 50d)]
    public void SpyLaneCombatRowsReproduceStoredTermsAndStockMeasuredLevel50Hits(ushort id, string mode, bool stealthStarted,
        int neutral, int resisted, int cost, float own, int group, float groupSeconds, float cast, int range, double fov)
    {
        var entry = AbilityCatalog.Find(id)!;
        Assert.Equal(cost, AbilityCosts.Cost(id));
        Assert.True(AbilityCooldowns.TryGet(id, out var cd));
        Assert.Equal(own, cd.Seconds);
        Assert.Equal(group, cd.Group);
        Assert.Equal(groupSeconds, cd.GroupSeconds);
        Assert.Equal(cast, entry.CastSeconds);
        Assert.Equal(range, entry.Number("Ability/SelectionRange"));
        Assert.Equal(fov, entry.Number("Ability/CastOutsideTargetFOVAngle"));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(id));
        Assert.Equal(7, TypedAbilityResistance.StoredType(id));

        if (stealthStarted)
            Assert.True(SneakFreeThrowAdmission.GatesInstantOpener(id) || SpyInstantSneakOpenerAdmission.Handles(id));
        else if (id == 365)
            Assert.True(GhostStrikeAdmission.Handles(id));
        else
            Assert.True(SneakFreeThrowAdmission.Handles(id));

        var plan = NativeQa.NativeQaModes.Find(mode).Plan;
        Assert.Contains(id, plan.Abilities);
        var caster = Client(plan.Abilities.ToArray());
        var item = new AbilityItem();
        item.setAbilityID(id);
        ushort resolved = AbilityHandler.AbilityDamage(item, new Random(1), characterLevel: 50);
        ushort hit = AbilityStats.ScaleAbilityDamage(caster, id, SneakAttacks.Add(resolved, id, stealthStarted));
        Assert.Equal(neutral, hit);
        Assert.Equal(resisted, TypedAbilityResistance.Amount(hit, 50, 50, 75));

        if (id == 1130)
        {
            Assert.True(TempMods.TryGet(id, out TempMods.Effect eff));
            Assert.Contains(eff.Mods, m => m.Target == TempMods.EnrageAbility && m.Seconds == 4f);
        }
        else if (id == 1131)
        {
            Assert.True(TempMods.TryGet(id, out TempMods.Effect eff));
            Assert.Contains(eff.Mods, m => m.Target == TempMods.OffBalanceAbility && m.Seconds == 12f && Math.Abs(m.Chance - 0.5) < 0.001);
        }
    }

    [Fact]
    public void HolyWater547And1023ResolveExactUninfectedAndInfectedPlayerDamage()
    {
        Assert.Equal(20, AbilityCosts.Cost(CombatSpecials.HolyWater));
        Assert.Equal(20, AbilityCosts.Cost(CombatSpecials.HolyWaterPlus));

        var caster = Client(Array.Empty<ushort>(), level: 19, selectSpy: false);
        var target = Client(Array.Empty<ushort>(), level: 19, selectSpy: false);
        target.playerData.setCharID(51099);
        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWater, target));
        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWaterPlus, target));

        HolyWaterCure.Grant(target, HolyWaterCure.ZombieVirus2Base);
        HolyWaterCure.Grant(target, HolyWaterCure.ZombieVirus2Bite);
        HolyWaterCure.Grant(target, HolyWaterCure.KilledOnlyByHolyWater, 1);
        HolyWaterCure.ActivateRsiCapture(target, HolyWaterCure.RsiCapture);
        try
        {
            Assert.Equal((ushort)100, CombatSpecials.PlayerDamage(CombatSpecials.HolyWater, target));
            Assert.Equal((ushort)750, CombatSpecials.PlayerDamage(CombatSpecials.HolyWaterPlus, target));
        }
        finally
        {
            HolyWaterCure.LandedOnPlayer(caster, target, CombatSpecials.HolyWater);
        }
        Assert.Equal((ushort)10, CombatSpecials.PlayerDamage(CombatSpecials.HolyWater, target));
    }
}
