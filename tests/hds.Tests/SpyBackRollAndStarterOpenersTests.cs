using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyBackRollAndStarterOpenersTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort BackRollEscape = CombatSpecials.BackRollEscape; // 122
    private const ushort CannyStrike = CannyStrikeDamage.CannyStrike;    // 295
    private const ushort Suplex = SuplexDamage.Suplex;                   // 287
    private const ushort ShadowBullDog = BullDogDamage.Id;               // 387
    private const ushort SpyBullDog = StealthShieldAbilities.BullDog;    // 1110
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient Client(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(110);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void BackRollEscapeHandlesZeroDamageEnraged10sFamily1200AndInterlockAdmission()
    {
        Assert.True(CombatSpecials.Handles(BackRollEscape));
        Assert.True(CombatSpecials.TargetsEnemy(BackRollEscape));
        Assert.False(AbilitySupport.Handles(BackRollEscape));
        Assert.False(AbilityBuffs.TryGet(BackRollEscape, out _));
        Assert.Equal((ushort)0, CombatSpecials.Damage(BackRollEscape, null));
        Assert.Equal((ushort)25, AbilityCosts.Cost(BackRollEscape));
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(BackRollEscape));

        Assert.True(AbilityCooldowns.TryGet(BackRollEscape, out var cd));
        Assert.Equal(0f, cd.Seconds);
        Assert.Equal((ushort)1200, cd.Group);
        Assert.Equal(12f, cd.GroupSeconds);

        var actor = Client();
        AbilityCooldowns.Start(actor, BackRollEscape, 100);
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, BackRollEscape, 105));
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, CombatSpecials.SideRollEscape, 105));
        Assert.True(AbilityCooldowns.IsCoolingDown(actor.playerData, CombatSpecials.CartwheelEscape, 105));
        Assert.False(AbilityCooldowns.IsCoolingDown(actor.playerData, BackRollEscape, 112.1));

        byte[] groupStart = AbilityCooldowns.BuildTimer(1200, AbilityCooldowns.GroupTimer, 12, true);
        byte[] zeroOwn = AbilityCooldowns.BuildTimer(BackRollEscape, AbilityCooldowns.OwnTimer, 0, true);
        var sent = actor.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.Contains(sent, p => p.SequenceEqual(groupStart));
        Assert.DoesNotContain(sent, p => p.SequenceEqual(zeroOwn));

        Assert.True(TempMods.TryGet(BackRollEscape, out var effect));
        var enraged = Assert.Single(effect.Mods, m => m.Target == 93);
        Assert.Equal(10f, enraged.Seconds);

        // Refuses outside close combat without spending IS or starting cooldown.
        Assert.True(SpyAbilityRegistry.TryRefuse(actor, BackRollEscape, 42, 1, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void BatchFixtureMemoryScaledDamagesChoreographiesAndCooldownsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-backroll-and-starter-openers").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal(new ushort[] { SpyStyle, 1111, BackRollEscape, CannyStrike, Suplex, ShadowBullDog, SpyBullDog },
            plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(65, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, SpyStyle);

        int thrownInfluence = AbilityStats.Influence(client, 1034);
        Assert.Equal(54, thrownInfluence);

        Assert.True(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(CannyStrike)!, FixtureLevel, new Random(0), out ushort base295));
        Assert.Equal((ushort)9, base295);
        Assert.Equal((ushort)13, AbilityStats.ScaleAbilityDamage(client, CannyStrike, base295));

        Assert.True(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(Suplex)!, FixtureLevel, new Random(0), out ushort base287));
        Assert.Equal((ushort)120, base287);
        Assert.Equal((ushort)646, AbilityStats.ScaleAbilityDamage(client, Suplex, SneakAttacks.Add(base287, Suplex, true)));
        Assert.Equal(19597u, CombatAnimations.Special(Suplex, true, CombatTactic.Speed)!.Id);

        Assert.True(BullDogDamage.TryResolve(AbilityCatalog.Find(ShadowBullDog)!, out ushort base387));
        Assert.Equal((ushort)75, base387);
        Assert.Equal((ushort)1578, AbilityStats.ScaleAbilityDamage(client, ShadowBullDog, SneakAttacks.Add(base387, ShadowBullDog, true)));
        Assert.Equal(19591u, CombatAnimations.Special(ShadowBullDog, true, CombatTactic.Speed)!.Id);

        Assert.True(StealthShieldAbilities.Handles(SpyBullDog));
        Assert.Equal((ushort)10, AbilityCosts.Cost(SpyBullDog));
        Assert.True(AbilityCooldowns.TryGet(SpyBullDog, out var cd1110));
        Assert.Equal(10f, cd1110.Seconds);
        Assert.Equal((ushort)691, cd1110.Group);
        Assert.Equal(15f, cd1110.GroupSeconds);
    }
}
