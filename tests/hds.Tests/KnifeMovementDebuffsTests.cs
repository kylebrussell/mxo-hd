using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public sealed class KnifeMovementDebuffsTests
{
    [Theory]
    [InlineData(MasterKnifeThrowerBatch.NeuroDart, -.57f, -.85f, -.72f, -20)]
    [InlineData(DeadlyThrowDamage.Id, -.57f, -.85f, -.72f, -15)]
    [InlineData(CombatSpecials.SeverArtery, -.50f, -.50f, -.50f, -10)]
    public void KnifeSlowThrowsProjectPerStanceMovementChannelsComposeWithSpeedAndRestore(
        ushort ability, float runScale, float sneakScale, float invisScale, int forceCombat)
    {
        Assert.True(AbilityBuffs.ProjectsTimedMovementDebuff(ability));
        var target = Client(50);
        var subject = TimedEffects.ForPlayer(target);
        Assert.True(TempMods.TryGet(ability, out var effect));
        try
        {
            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
            Assert.Equal(runScale, Scale(target), 4);
            Assert.Equal(forceCombat, AbilityStats.Player(target, 519));

            // Switching stances selects the matching movement channel (877 Sneak / 878 Invisibility).
            Assert.NotNull(Stealth.Begin(target, Stealth.Defs[Stealth.SneakGrantAbility], 50));
            Assert.Equal(sneakScale, Scale(target), 4);
            Stealth.Cleared(target);
            Assert.Equal(runScale, Scale(target), 4);

            Assert.NotNull(Stealth.Begin(target, Stealth.Defs[Stealth.InvisibilityGrantAbility], 50));
            Assert.Equal(invisScale, Scale(target), 4);
            Stealth.Cleared(target);
            Assert.Equal(runScale, Scale(target), 4);

            // Composes additively with Hyper-Speed (+0.45 on Run) and restores +0.45 when debuff expires.
            Assert.True(AbilityBuffs.Apply(target, AbilityBuffs.HyperSpeed));
            Assert.Equal((float)((decimal)runScale + 0.45m), Scale(target), 4);

            Assert.True(TimedEffects.RemoveParent(subject, ability));
            Assert.Equal(.45f, Scale(target), 4);
            Assert.Equal(0, AbilityStats.Player(target, 519));
        }
        finally
        {
            Stealth.Cleared(target);
            AbilityBuffs.RemoveAll(target);
        }
    }

    [Fact]
    public void NeuroDartPublishesCombatAccuracyDefenseAndForceCombatStatsIntoPlayerTotals()
    {
        var target = Client(50);
        var subject = TimedEffects.ForPlayer(target);
        Assert.True(TempMods.TryGet(MasterKnifeThrowerBatch.NeuroDart, out var effect));
        try
        {
            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
            Assert.Equal(-20, AbilityStats.Player(target, 519));  // Force Combat
            Assert.Equal(-15, AbilityStats.Player(target, 522));  // Melee Accuracy
            Assert.Equal(-15, AbilityStats.Player(target, 524));  // Ballistic Accuracy
            Assert.Equal(-13, AbilityStats.Player(target, 1009)); // Melee Defense
            Assert.Equal(-21, AbilityStats.Player(target, 1010)); // Ballistic Defense
            Assert.Equal(-.57f, Scale(target), 4);

            Assert.True(TimedEffects.RemoveParent(subject, MasterKnifeThrowerBatch.NeuroDart));
            Assert.Equal(0, AbilityStats.Player(target, 519));
            Assert.Equal(0, AbilityStats.Player(target, 522));
            Assert.Equal(0, AbilityStats.Player(target, 524));
            Assert.Equal(0, AbilityStats.Player(target, 1009));
            Assert.Equal(0, AbilityStats.Player(target, 1010));
            Assert.Equal(0f, Scale(target), 4);
        }
        finally { AbilityBuffs.RemoveAll(target); }
    }

    [Fact]
    public void CripplingThrowRootOverridesMovementScaleToMinusOneHoldsFirstHitAndRestoresOnBreakOrExpiry()
    {
        var target = Client(50);
        var subject = TimedEffects.ForPlayer(target);
        Assert.True(TempMods.TryGet(SpyAbilityDamage.CripplingThrow, out var effect));
        try
        {
            Assert.True(AbilityBuffs.Apply(target, AbilityBuffs.HyperSpeed));
            Assert.Equal(.45f, Scale(target), 4);

            double now = TimeUtils.GetSimTimeSeconds();
            TimedEffects.Apply(subject, effect, now);
            Assert.Equal(-1f, Scale(target), 4);
            Assert.Equal(-40, AbilityStats.Player(target, 519));
            Assert.False(ControlActions.CanMove(subject.Effects));

            // First health change is protected by Crippling Throw's stored -100 sibling callback.
            TimedEffects.HealthChanged(subject, 1, belowMaximum: true, damageEvent: true);
            Assert.True(subject.Effects.Has(TempMods.RootedAbility));
            Assert.Equal(-1f, Scale(target), 4);

            // Second health change breaks the root and restores Hyper-Speed's 0.45 MovementScale.
            TimedEffects.HealthChanged(subject, 10, belowMaximum: true, damageEvent: true);
            Assert.False(subject.Effects.Has(TempMods.RootedAbility));
            Assert.Equal(.45f, Scale(target), 4);
            Assert.Equal(0, AbilityStats.Player(target, 519));

            // Reapply and verify clean expiry restoration back to 0.45.
            TimedEffects.Apply(subject, effect, now);
            Assert.Equal(-1f, Scale(target), 4);
            Assert.True(TimedEffects.RemoveParent(subject, SpyAbilityDamage.CripplingThrow));
            Assert.Equal(.45f, Scale(target), 4);
        }
        finally { AbilityBuffs.RemoveAll(target); }
    }

    private static float Scale(WorldClient client) => BitConverter.ToSingle(client.playerInstance.MovementScale.getValue());

    private static WorldClient Client(byte level)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(50941);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.LivenessClock = () => 0;
        client.Alive = true;
        return client;
    }
}
