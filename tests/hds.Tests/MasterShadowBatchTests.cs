using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MasterShadowBatchTests
{
    private const ushort ImpartInvisibility = Stealth.ImpartInvisibilityAbility;     // 366
    private const ushort MasterShadowPassive = 367;
    private const ushort Vanish = Stealth.VanishAbility;                             // 368
    private const ushort InvisibilityImparted = Stealth.InvisibilityImpartedAbility; // 629
    private const ushort MasterShadowActive = Stealth.MasterShadowAbility;           // 1118
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
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        return client;
    }

    [Theory]
    [InlineData(CombatTactic.Grab, 4271u)]
    [InlineData(CombatTactic.Speed, 4272u)]
    [InlineData(CombatTactic.Power, 4273u)]
    public void VanishUsesTheNamedWithdrawalMoveWithoutDamage(CombatTactic tactic, uint clip)
    {
        var actor = Client();
        var move = new AbilityItem();
        move.setAbilityID(Vanish);
        Assert.True(CombatSpecials.IsWithdrawalSpecial(Vanish));
        Assert.True(CombatSpecials.KeepsQueuedOpponent(Vanish));
        Assert.Equal(InterlockInitiative.Attack(actor, move) + 30, WithdrawalInput.Attack(actor, move, false));
        Assert.Equal(clip, CombatAnimations.Special(Vanish, true, tactic, 'S').Id);
        Assert.Null(CombatAnimations.Special(Vanish, false, tactic, 'S'));

        var win = CombatRules.SuccessfulWithdrawal(escapeMove: true);
        Assert.True(win.SpecialMoveLands);
        Assert.True(win.Withdrew);
        Assert.Equal(0, win.DamageToNpc);
        Assert.Equal(0, win.DamageToPlayer);
    }

    [Fact]
    public void MasterShadow367RegistersAndUnregistersGrant1118AndPassiveStat1045()
    {
        Assert.True(AbilityGrantRegistration.ZeroGrant(AbilityCatalog.Find(MasterShadowPassive), MasterShadowActive));
        Assert.True(AbilityGrantRegistration.Handles(MasterShadowPassive, MasterShadowActive));
        Assert.DoesNotContain((MasterShadowPassive, MasterShadowActive), AbilityGrantRegistration.ObservedPairs);

        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (MasterShadowPassive, FixtureRank) });
        Assert.Equal(5, AbilityStats.Player(client, 1045));

        int beforeReadyCount = client.messageQueue.RPCMessagesQueue.Count;
        AbilityGrantRegistration.LoadedChanged(client);
        Assert.Equal(beforeReadyCount, client.messageQueue.RPCMessagesQueue.Count);

        AbilityGrantRegistration.LoadingComplete(client, new byte[] { 1 });
        byte[][] Packets() => client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => m.content.AsSpan(1).ToArray()).ToArray();

        byte[] load1118 = ServerPackets.BuildAbilityLoad(MasterShadowActive, 0, AbilityProgression.HiddenSlot);
        byte[] add1118 = AbilityGrantRegistration.Record(new(MasterShadowPassive, MasterShadowActive, 0), off: false);
        Assert.Contains(Packets(), p => p.SequenceEqual(load1118));
        Assert.Contains(Packets(), p => p.SequenceEqual(add1118));

        AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Player(client, 1045));
        AbilityGrantRegistration.LoadedChanged(client);

        byte[] unload1118 = ServerPackets.BuildAbilityUnload(MasterShadowActive);
        byte[] remove1118 = AbilityGrantRegistration.Record(new(MasterShadowPassive, MasterShadowActive, 0), off: true);
        Assert.Contains(Packets(), p => p.SequenceEqual(unload1118));
        Assert.Contains(Packets(), p => p.SequenceEqual(remove1118));
    }

    [Fact]
    public void MasterShadowBatchFixtureMemoryLeafUnloadOrderAndCostsCooldownsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-master-shadow-batch").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal(new ushort[] { CombatStyles.SpyStyle, MasterShadowActive, ImpartInvisibility, InvisibilityImparted, Vanish },
            plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(64, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        // 367 Master Shadow must be a pure leaf in plan.Abilities so '?qaunload 367' succeeds in one command.
        Assert.Contains(MasterShadowPassive, plan.Abilities);
        foreach (ushort other in plan.Abilities.Where(id => id != MasterShadowPassive))
        {
            var closure = AbilityPrerequisites.Current.Closure(other);
            Assert.False(closure.ContainsKey(MasterShadowPassive),
                $"Active ability {other} depends on 367, breaking single-step ?qaunload 367");
        }
        Assert.Equal(59, mem - AbilityMemory.Cost(MasterShadowPassive));

        // Verify costs, cast times, cooldowns, and stealth defs for all 4 active rows.
        Assert.Equal((ushort)12, AbilityCosts.Cost(MasterShadowActive));
        Assert.False(AbilityBuffs.CooldownStartsOnUse(MasterShadowActive));
        Assert.True(AbilityCooldowns.TryGet(MasterShadowActive, out var cd1118));
        Assert.Equal(30f, cd1118.Seconds);
        Assert.Equal(Stealth.ShadowObject, Stealth.Defs[MasterShadowActive].Bits);

        Assert.Equal((ushort)100, AbilityCosts.Cost(ImpartInvisibility));
        Assert.True(AbilityCooldowns.TryGet(ImpartInvisibility, out var cd366));
        Assert.Equal(10f, cd366.Seconds);
        Assert.Equal(1.5f, AbilityCatalog.Find(ImpartInvisibility)!.CastSeconds);

        Assert.Equal((ushort)0, AbilityCosts.Cost(InvisibilityImparted));
        Assert.True(AbilityCooldowns.TryGet(InvisibilityImparted, out var cd629));
        Assert.Equal(30f, cd629.Seconds);
        Assert.Equal(3f, AbilityCatalog.Find(InvisibilityImparted)!.CastSeconds);
        Assert.Equal(Stealth.Invisible, Stealth.Defs[InvisibilityImparted].Bits);

        Assert.Equal((ushort)30, AbilityCosts.Cost(Vanish));
        Assert.True(AbilityCooldowns.TryGet(Vanish, out var cd368));
        Assert.Equal(1800f, cd368.Seconds);
        Assert.True(Stealth.Handles(Vanish));
    }
}
