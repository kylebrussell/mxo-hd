using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using MxoHd.PacketResearch;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtsStyleBehaviorTests
{
    private const ushort Aikido = CombatStyles.Aikido;     // 101
    private const ushort Karate = CombatStyles.Karate;     // 132
    private const ushort KungFu = CombatStyles.KungFu;     // 133

    private static WorldClient Client(byte level = 50)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)500);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)500);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(Aikido, 50, 'A', 64, 26, 57, 57, 57, 57, 74)]
    [InlineData(Karate, 50, 'K', 64, 29, 45, 45, 45, 45, 72)]
    [InlineData(KungFu, 50, 'W', 74, 27, 49, 49, 49, 49, 70)]
    [InlineData(Aikido, 12, 'A', 15, 9, 21, 21, 21, 21, 25)]
    [InlineData(Karate, 12, 'K', 15, 10, 9, 9, 9, 9, 23)]
    [InlineData(KungFu, 12, 'W', 25, 9, 13, 13, 13, 13, 21)]
    public void StarterMartialStylesSupplyAllSevenModifiersAndCleanUpOnSwitch(
        ushort style, ushort rank, char expectedLetter,
        int expectedMeleeAcc, int expectedMeleeDmg,
        int expectedMeleeDef, int expectedBallisticDef, int expectedViralDef, int expectedThrownDef,
        int expectedWithdraw)
    {
        var client = Client((byte)rank);
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, rank), (style, rank) });

        int baselineAcc = AbilityStats.Attack(client, false);
        int baselineDef = AbilityStats.Defense(client, false);
        int baselineWithdraw = WithdrawalInput.Attack(client, null!, true);

        Assert.True(CombatStyles.IsStyle(style));
        Assert.Equal(expectedLetter, CombatStyles.Letter(style));
        Assert.Equal(7, CombatStyles.Modifiers(style).Count());

        client.messageQueue.RPCMessagesQueue.Clear();
        Assert.True(CombatStyles.UseStyle(client, style));
        Assert.Equal(style, CombatStyles.Active(client));
        Assert.Equal(expectedLetter, CombatStyles.Letter(client));

        // Verify all 7 shipped active-style modifiers via both passive totals and live combat consumers.
        Assert.Equal(expectedMeleeAcc, AbilityPassives.Total(client, 522));
        Assert.Equal(expectedMeleeDmg, AbilityPassives.Total(client, 159));
        Assert.Equal(expectedMeleeDef, AbilityPassives.Total(client, 1009));
        Assert.Equal(expectedBallisticDef, AbilityPassives.Total(client, 1010));
        Assert.Equal(expectedViralDef, AbilityPassives.Total(client, 183));
        Assert.Equal(expectedThrownDef, AbilityPassives.Total(client, 1011));
        Assert.Equal(expectedWithdraw, AbilityPassives.Total(client, 1005));

        Assert.Equal(expectedMeleeAcc, AbilityStats.Attack(client, false));
        ushort expectedScaled100 = (ushort)(int)(100 * (double)(float)(1 + expectedMeleeDmg / 100.0));
        Assert.Equal(expectedScaled100, AbilityStats.ScaleTypedAbilityDamage(client, 0, 100));
        Assert.Equal(expectedMeleeDef, AbilityStats.Defense(client, false));
        Assert.Equal(expectedBallisticDef, AbilityStats.Defense(client, true));
        Assert.Equal(expectedViralDef, AbilityStats.Player(client, AbilityStats.SpecialDefenseStat(2)));
        Assert.Equal(expectedThrownDef, AbilityStats.Player(client, AbilityStats.SpecialDefenseStat(4)));
        Assert.Equal(expectedWithdraw, WithdrawalInput.Attack(client, null!, true));

        // Verify 80 bc 45 wire records were emitted for all 7 active-style modifiers.
        var added = client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => ManageBonusRecord.TryDecode(m.content.Skip(1).ToArray(), out var r) ? r : null)
            .Where(r => r != null && r.SourceId == style && r.AddRemoveType == 0)
            .ToDictionary(r => r!.TargetId, r => r!.SignedValue);
        Assert.Equal(expectedMeleeAcc, added[522]);
        Assert.Equal(expectedMeleeDmg, added[159]);
        Assert.Equal(expectedMeleeDef, added[1009]);
        Assert.Equal(expectedBallisticDef, added[1010]);
        Assert.Equal(expectedViralDef, added[183]);
        Assert.Equal(expectedThrownDef, added[1011]);
        Assert.Equal(expectedWithdraw, added[1005]);

        Assert.NotNull(CombatStyles.ActivationLine(client, style));

        // Switching back to Self Defense removes all 7 records and restores baseline consumer inputs.
        client.messageQueue.RPCMessagesQueue.Clear();
        CombatStyles.Select(client, CombatStyles.SelfDefense);
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(client));
        Assert.Equal(baselineAcc, AbilityStats.Attack(client, false));
        Assert.Equal(baselineDef, AbilityStats.Defense(client, false));
        Assert.Equal(baselineWithdraw, WithdrawalInput.Attack(client, null!, true));

        var removed = client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => ManageBonusRecord.TryDecode(m.content.Skip(1).ToArray(), out var r) ? r : null)
            .Where(r => r != null && r.SourceId == style && r.AddRemoveType == 1)
            .Select(r => r!.TargetId)
            .ToHashSet();
        Assert.Equal(new ushort[] { 159, 183, 522, 1005, 1009, 1010, 1011 }, removed.Order().ToArray());
    }

    [Fact]
    public void SwitchingBetweenAllThreeMartialStylesReplacesPreviousStyleExclusively()
    {
        var client = Client(50);
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (Aikido, 50), (Karate, 50), (KungFu, 50) });

        CombatStyles.Select(client, Aikido);
        Assert.Equal(57, AbilityStats.Defense(client, false));
        Assert.Equal(125, AbilityStats.ScaleTypedAbilityDamage(client, 0, 100)); // binary32 1.26f * 100

        CombatStyles.Select(client, Karate);
        Assert.Equal(45, AbilityStats.Defense(client, false));
        Assert.Equal(128, AbilityStats.ScaleTypedAbilityDamage(client, 0, 100)); // binary32 1.29f * 100
        Assert.Equal(64, AbilityStats.Attack(client, false));

        CombatStyles.Select(client, KungFu);
        Assert.Equal(49, AbilityStats.Defense(client, false));
        Assert.Equal(126, AbilityStats.ScaleTypedAbilityDamage(client, 0, 100)); // binary32 1.27f * 100
        Assert.Equal(74, AbilityStats.Attack(client, false));

        // Special-move stance enforcement switches only when the required martial style is loaded.
        Assert.True(CombatStyles.EnsureStyleFor(client, 139)); // Iron Guard -> Aikido
        Assert.Equal(Aikido, CombatStyles.Active(client));
        Assert.True(CombatStyles.EnsureStyleFor(client, 535)); // Berserker Attack -> Karate
        Assert.Equal(Karate, CombatStyles.Active(client));
        Assert.True(CombatStyles.EnsureStyleFor(client, 138)); // Guard Breaker -> Kung-Fu
        Assert.Equal(KungFu, CombatStyles.Active(client));
    }

    [Fact]
    public void SevenMartialPassiveBatchFitsMemoryAndReloadsInPrerequisiteOrder()
    {
        ushort[] passives = { 136, 137, 264, 269, 258, 257, 431 };
        ushort[] loadout = new ushort[] { 2, 17 }
            .Concat(passives)
            .Concat(passives.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct()
            .ToArray();

        Assert.Contains(Aikido, loadout);
        Assert.Contains(Karate, loadout);
        Assert.Contains(KungFu, loadout);
        Assert.True(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50),
            $"Loadout memory {loadout.Sum(AbilityMemory.Cost)} must fit level 50 ({AbilityMemory.ForLevel(50)})");
        var plan = NativeQa.NativeQaModes.Find("ability-parity-ma-passives-and-styles").Plan;
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50),
            $"NativeQa mode memory {plan.Abilities.Sum(AbilityMemory.Cost)} must fit level 50 ({AbilityMemory.ForLevel(50)})");

        // Verify root-to-leaf reload order satisfies every prerequisite when the other loadout entries stay loaded.
        var active = loadout.Except(passives).ToDictionary(id => id, _ => (ushort)50);
        foreach (ushort id in passives)
        {
            foreach (var prereq in AbilityPrerequisites.Current.Closure(id))
            {
                Assert.True(active.TryGetValue(prereq.Key, out ushort loadedRank) && loadedRank >= prereq.Value,
                    $"Reloading {id} requires {prereq.Key} at rank {prereq.Value} to already be loaded");
            }
            active[id] = 50;
        }

        // Verify both Martial Arts class upgrade grants are registered in AbilityGrantRegistration.
        Assert.True(AbilityGrantRegistration.Handles(137, 1062));
        Assert.True(AbilityGrantRegistration.Handles(257, 1063));

        // Verify exact rank-50 passive bonus totals for all seven passives individually.
        var expected = new (ushort Id, (ushort Stat, int Value)[] Stats)[]
        {
            (136, new (ushort, int)[] { (997, 5), (1142, 5) }),
            (137, new (ushort, int)[] { (1046, 2) }),
            (257, new (ushort, int)[] { (1046, 1), (1142, 1) }),
            (258, new (ushort, int)[] { (1057, 5), (997, 7), (1142, 3) }),
            (264, new (ushort, int)[] { (1057, 5), (997, 7), (1142, 3) }),
            (269, new (ushort, int)[] { (997, 5), (1057, 15), (1142, 3) }),
            (431, new (ushort, int)[] { (1142, 1), (1143, 10), (995, 10), (1030, 6) })
        };
        foreach (var (id, stats) in expected)
        {
            var client = Client(50);
            AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (id, 50) });
            foreach (var (stat, value) in stats)
                Assert.Equal(value, AbilityPassives.Total(client, stat));
        }
    }
}
