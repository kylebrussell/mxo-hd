using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class ZenMasterBalancePurityTests
{
    private const ushort Balance = 861, BalanceEffect = 862, Purity = 868, DestroyHealth = 476;

    private static WorldClient Client(byte level = 99)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)1000);
        client.playerInstance.MaxHealth.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void BalanceAndPurityMetadataCostsAndCooldownsMatchShippedCatalog()
    {
        Assert.Equal((ushort)50, AbilityCosts.Cost(Balance));
        Assert.True(AbilityCooldowns.TryGet(Balance, out var balanceCd));
        Assert.Equal(60f, balanceCd.Seconds);
        Assert.Equal((ushort)20, AbilityCosts.Cost(Purity));
        Assert.True(AbilityCooldowns.TryGet(Purity, out var purityCd));
        Assert.Equal(15f, purityCd.Seconds);
        Assert.True(AbilityHealthCallbacks.Template(Balance));
        Assert.True(AbilityHealthCallbacks.InternalEffect(BalanceEffect));
        Assert.True(AbilityHostile.IsSweep(Purity));
        Assert.True(AbilityHostile.Cleansable(DestroyHealth));
        Assert.True(TempMods.TryGet(DestroyHealth, out _));
    }

    [Fact]
    public void PurityCleansesDestroyHealth476OnWinningRollAndLeavesNoLastingBuff()
    {
        var client = Client();
        var subject = TimedEffects.ForPlayer(client);
        Assert.True(TempMods.TryGet(DestroyHealth, out var debuff));
        TimedEffects.Apply(subject, debuff!, TimeUtils.GetSimTimeSeconds(), client, "DuelBot");
        Assert.True(subject.Effects.HasParent(DestroyHealth));

        var previous = AbilityHostile.PulseRandom;
        try
        {
            AbilityHostile.PulseRandom = new FixedRollRandom(99);
            Assert.True(AbilityHostile.ApplySweep(client, client, Purity));
            Assert.True(subject.Effects.HasParent(DestroyHealth));
            Assert.False(subject.Effects.HasParent(Purity));

            AbilityHostile.PulseRandom = new FixedRollRandom(0);
            Assert.True(AbilityHostile.ApplySweep(client, client, Purity));
            Assert.False(subject.Effects.HasParent(DestroyHealth));
            Assert.False(subject.Effects.HasParent(Purity));
        }
        finally
        {
            AbilityHostile.PulseRandom = previous;
            TimedEffects.RemoveParent(subject, DestroyHealth);
        }
    }

    [Fact]
    public void PurityRespectsScopedDisposableRollOnlyOnTestDbWithDevCommands()
    {
        string? prevDev = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
        string? prevDb = Environment.GetEnvironmentVariable("MXO_DB_NAME");
        string? prevRoll = Environment.GetEnvironmentVariable("MXO_BUFF_DISPEL_QA_ROLL100");
        var client = Client();
        var subject = TimedEffects.ForPlayer(client);
        try
        {
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", "1");
            Environment.SetEnvironmentVariable("MXO_DB_NAME", "mxotest_purity");
            Environment.SetEnvironmentVariable("MXO_BUFF_DISPEL_QA_ROLL100", "0");

            Assert.True(TempMods.TryGet(DestroyHealth, out var debuff));
            TimedEffects.Apply(subject, debuff!, TimeUtils.GetSimTimeSeconds(), client, "DuelBot");
            Assert.True(AbilityHostile.ApplySweep(client, client, Purity));
            Assert.False(subject.Effects.HasParent(DestroyHealth));
            Assert.False(subject.Effects.HasParent(Purity));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", prevDev);
            Environment.SetEnvironmentVariable("MXO_DB_NAME", prevDb);
            Environment.SetEnvironmentVariable("MXO_BUFF_DISPEL_QA_ROLL100", prevRoll);
            TimedEffects.RemoveParent(subject, DestroyHealth);
        }
    }

    [Fact]
    public void ZenMasterBatchModeFitsMemoryAndPreSeedsBalanceAndPurity()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-zen-master-balance-purity").Plan;
        Assert.Equal(new ushort[] { Balance, Purity }, plan.HotbarAbilities);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));
        Assert.Equal("0", plan.EnvironmentOverrides["MXO_BUFF_DISPEL_QA_ROLL100"]);
        Assert.False(plan.FixedCallbackRolls);
    }

    private sealed class FixedRollRandom : Random
    {
        private readonly int value;
        public FixedRollRandom(int value) => this.value = value;
        public override int Next(int maxValue) => Math.Min(value, Math.Max(0, maxValue - 1));
    }
}
