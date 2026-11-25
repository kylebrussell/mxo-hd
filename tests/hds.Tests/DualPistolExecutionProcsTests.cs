using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class DualPistolExecutionProcsTests
{
    private sealed class FixedDraws : Random
    {
        private readonly double[] draws;
        private int index;
        public FixedDraws(params double[] draws) => this.draws = draws;
        public override double NextDouble() => index < draws.Length ? draws[index++] : 0.99;
    }

    private static WorldClient Client(uint charId = 6114)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)46);
        client.playerInstance.Health.setValue((ushort)12000);
        client.playerInstance.MaxHealth.setValue((ushort)12000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)900);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)900);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void PlayerAndNpcCatalogResolveBothIndependentFortyPercentProcs()
    {
        Assert.True(TempMods.TryGet(502, out var playerEffect, npcCaster: false));
        Assert.Equal(2, playerEffect.Mods.Length);
        var m1010 = Assert.Single(playerEffect.Mods, m => m.Target == 1010);
        var m522 = Assert.Single(playerEffect.Mods, m => m.Target == 522);
        Assert.Equal(-31, m1010.MinValue);
        Assert.Equal(-31, m1010.MaxValue);
        Assert.Equal(8f, m1010.Seconds);
        Assert.Equal(0.4f, m1010.Chance, 3);
        Assert.Equal(-27, m522.MinValue);
        Assert.Equal(-27, m522.MaxValue);
        Assert.Equal(8f, m522.Seconds);
        Assert.Equal(0.4f, m522.Chance, 3);

        Assert.True(TempMods.TryGet(502, out var npcEffect, npcCaster: true));
        Assert.Equal(2, npcEffect.Mods.Length);
        var n1010 = Assert.Single(npcEffect.Mods, m => m.Target == 1010);
        var n522 = Assert.Single(npcEffect.Mods, m => m.Target == 522);
        Assert.Equal(-44, n1010.MinValue);
        Assert.Equal(0.4f, n1010.Chance, 3);
        Assert.Equal(-34, n522.MinValue);
        Assert.Equal(0.4f, n522.Chance, 3);
    }

    [Theory]
    [InlineData(0.1, 0.1, true, true)]
    [InlineData(0.1, 0.9, true, false)]
    [InlineData(0.9, 0.1, false, true)]
    [InlineData(0.9, 0.9, false, false)]
    public void IndependentFortyPercentDrawsProduceAllFourOutcomesAndConsumeLiveStats(
        double firstDraw, double secondDraw, bool expectFirst, bool expectSecond)
    {
        Assert.True(TempMods.TryGet(502, out var effect, npcCaster: false));
        ushort firstStat = effect.Mods[0].Target;
        ushort secondStat = effect.Mods[1].Target;
        bool expect1010 = firstStat == 1010 ? expectFirst : expectSecond;
        bool expect522 = firstStat == 522 ? expectFirst : expectSecond;

        var victim = Client(6114);
        int meleeAtkBefore = AbilityStats.Attack(victim, ranged: false);
        int rangedDefBefore = AbilityStats.Defense(victim, ranged: true);

        var subject = TimedEffects.ForPlayer(victim);
        TimedEffects.Apply(subject, effect, 100.0, null, "CSR_Tester", rng: new FixedDraws(firstDraw, secondDraw));

        var active = subject.Effects.Snapshot();
        Assert.Equal(expect1010, active.Any(m => m.Target == 1010 && m.Value == -31 && m.Seconds == 8f));
        Assert.Equal(expect522, active.Any(m => m.Target == 522 && m.Value == -27 && m.Seconds == 8f));
        Assert.Equal(meleeAtkBefore - (expect522 ? 27 : 0), AbilityStats.Attack(victim, ranged: false));
        Assert.Equal(rangedDefBefore - (expect1010 ? 31 : 0), AbilityStats.Defense(victim, ranged: true));

        TimedEffects.RemoveParent(subject, 502);
        Assert.Empty(subject.Effects.Snapshot());
        Assert.Equal(meleeAtkBefore, AbilityStats.Attack(victim, ranged: false));
        Assert.Equal(rangedDefBefore, AbilityStats.Defense(victim, ranged: true));
    }

    [Fact]
    public void QaGateAndModePlanAreStrictlyScoped()
    {
        Assert.True(DualPistolExecutionQa.Allowed("1", "1", "mxotest_502", 110, true, false));
        Assert.False(DualPistolExecutionQa.Allowed("0", "1", "mxotest_502", 110, true, false));
        Assert.False(DualPistolExecutionQa.Allowed("1", "0", "mxotest_502", 110, true, false));
        Assert.False(DualPistolExecutionQa.Allowed("1", "1", "mxo", 110, true, false));
        Assert.False(DualPistolExecutionQa.Allowed("1", "1", "mxotest_502", 2, true, false));

        var plan = NativeQaModes.RequireKnown("ability-parity-dual-pistol-execution-procs").Plan;
        Assert.Equal("1", plan.EnvironmentOverrides[DualPistolExecutionQa.Mode]);
        Assert.Equal(new ushort[] { CombatStyles.Handguns, 502 }, plan.HotbarAbilities);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(46));
    }
}
