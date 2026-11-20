using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class StarterSpySneakAndEvasionTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                       // 1112
    private const ushort SneakPassive = 151;
    private const ushort SneakGrant = Stealth.SneakGrantAbility;                 // 1111
    private const ushort SideRollEscape = CombatSpecials.SideRollEscape;         // 121
    private const ushort UncannySenses = 161;
    private const ushort DisableEvasion = StealthShieldAbilities.DisableEvasion; // 1125
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
        return client;
    }

    [Fact]
    public void Sneak151AndUncannySenses161PassiveTotalsAndUnloadOrderAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-starter-spy-sneak-and-evasion").Plan;
        Assert.Equal(new ushort[] { SpyStyle, SneakGrant, DisableEvasion }, plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(23, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        // Verify strict leaf-to-root unload order: 1125 -> 161 -> 121 -> 151.
        var active = new HashSet<ushort>(plan.Abilities);
        ushort[] unloadOrder = { DisableEvasion, UncannySenses, SideRollEscape, SneakPassive };
        int[] expectedMemAfterUnload = { 21, 20, 18, 9 };
        for (int i = 0; i < unloadOrder.Length; i++)
        {
            ushort id = unloadOrder[i];
            Assert.Contains(id, active);
            foreach (ushort other in active.Where(a => a != id))
            {
                var closure = AbilityPrerequisites.Current.Closure(other);
                Assert.False(closure.ContainsKey(id),
                    $"Remaining ability {other} depends on {id}, breaking unload step {i + 1}");
            }
            active.Remove(id);
            Assert.Equal(expectedMemAfterUnload[i], active.Sum(AbilityMemory.Cost));
        }

        // Verify root-to-leaf reload order: 151 -> 121 -> 161 -> 1125.
        foreach (ushort id in unloadOrder.Reverse())
        {
            var closure = AbilityPrerequisites.Current.Closure(id);
            foreach (ushort req in closure.Keys)
                Assert.True(active.Contains(req), $"Reloading {id} requires prerequisite {req} to already be loaded");
            active.Add(id);
        }
        Assert.Equal(mem, active.Sum(AbilityMemory.Cost));

        // Verify exact passive stat deltas of 161 (+40 on 577) and 151 (+5 on 1027/1143/1047, +10 on 998).
        var full = Client();
        AbilityPassives.SetLoaded(full, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        var without161 = Client();
        AbilityPassives.SetLoaded(without161, plan.Abilities.Where(id => id != UncannySenses)
            .Select(id => (id, (ushort)FixtureRank)).ToArray());
        var without151Chain = Client();
        AbilityPassives.SetLoaded(without151Chain, plan.Abilities.Except(unloadOrder)
            .Select(id => (id, (ushort)FixtureRank)).ToArray());

        Assert.Equal(40, AbilityStats.Player(full, 577) - AbilityStats.Player(without161, 577));
        Assert.Equal(5, AbilityStats.Player(without161, 1027) - AbilityStats.Player(without151Chain, 1027));
        Assert.Equal(5, AbilityStats.Player(without161, 1143) - AbilityStats.Player(without151Chain, 1143));
        Assert.Equal(10, AbilityStats.Player(without161, 998) - AbilityStats.Player(without151Chain, 998));
        Assert.Equal(5, AbilityStats.Player(without161, 1047) - AbilityStats.Player(without151Chain, 1047));
    }

    [Fact]
    public void DisableEvasion1125CostsCastTimeReuseAndAdmissionMatchShippedTemplate()
    {
        Assert.True(StealthShieldAbilities.Handles(DisableEvasion));
        Assert.True(StealthShieldAbilities.IsTool(StealthShieldFixture.Tool));
        Assert.Equal((ushort)25, AbilityCosts.Cost(DisableEvasion));
        Assert.Equal(4f, AbilityCatalog.Find(DisableEvasion)!.CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(DisableEvasion, out var cd));
        Assert.Equal(45f, cd.Seconds);
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(DisableEvasion));

        var actor = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(actor, DisableEvasion, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }
}
