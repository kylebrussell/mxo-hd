using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class MartialBlindEnrageAreaTests
{
    private const ushort KungFu = CombatStyles.KungFu;                                      // 133
    private const ushort MisdirectPunch = KungFuMasterSpecials.MisdirectPunch;              // 260
    private const ushort Karate = CombatStyles.Karate;                                      // 132
    private const ushort SidekickCombo = KarateAikidoMasterSpecials.SidekickCombo;          // 266
    private const ushort SwirlingKiSummon = MartialArtsRemainingSpecials.SwirlingKiSummon;  // 268
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient Client(ushort[] loadout, ushort style, uint charId = 110)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(FixtureLevel);
        client.playerInstance.Health.setValue((ushort)30000);
        client.playerInstance.MaxHealth.setValue((ushort)30000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        AbilityPassives.SetLoaded(client, loadout.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.UseStyle(client, style);
        return client;
    }

    [Fact]
    public void PlanMemoryStandsAndExactPerRowValuesAreVerified()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-ma-blind-enrage-area").Plan;
        Assert.Equal(new[] { KungFu, MisdirectPunch, Karate, SidekickCombo, SwirlingKiSummon }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_PVP_SERVER"]);
        Assert.Equal("1", plan.EnvironmentOverrides[AreaTargetDiagnostics.Variable]);
        Assert.Equal("1", plan.EnvironmentOverrides[MartialBlindEnrageAreaQa.Mode]);
        Assert.Equal("martial-blind-enrage-area.csv", plan.TestMobFixture);
        Assert.Equal(24751, plan.X);
        Assert.Equal(-1498, plan.Z);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(77, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var kfClient = Client(plan.Abilities.ToArray(), KungFu);
        var karClient = Client(plan.Abilities.ToArray(), Karate);
        Assert.Equal(39, AbilityStats.Influence(kfClient, 159));
        Assert.Equal(41, AbilityStats.Influence(karClient, 159));

        (ushort Id, WorldClient Caster, ushort Base, ushort Scaled, ushort Cost, float OwnCd, ushort Group)[] expected =
        {
            (MisdirectPunch, kfClient, 24, 33, 30, 10f, 688),
            (SidekickCombo, karClient, 28, 39, 25, 10f, 688),
            (SwirlingKiSummon, karClient, 2, 2, 60, 12f, 687)
        };

        foreach (var e in expected)
        {
            var item = new AbilityItem();
            item.setAbilityID(e.Id);
            ushort raw = AbilityHandler.AbilityDamage(item, new Random(0), characterLevel: FixtureLevel);
            ushort scaled = AbilityStats.ScaleAbilityDamage(e.Caster, e.Id, raw);
            Assert.True(AbilityCooldowns.TryGet(e.Id, out var cd));
            Assert.Equal(e.Base, raw);
            Assert.Equal(e.Scaled, scaled);
            Assert.Equal(e.Cost, AbilityCosts.Cost(e.Id));
            Assert.Equal(e.OwnCd, cd.Seconds);
            Assert.Equal(5f, cd.GroupSeconds);
            Assert.Equal(e.Group, cd.Group);
        }

        // Verify exact shipped mods and ExecutionFX.
        Assert.True(TempMods.TryGet(MisdirectPunch, out var e260));
        var m260 = Assert.Single(e260.Mods);
        Assert.Equal(TempMods.KindState, m260.Kind);
        Assert.Equal(TempMods.BlindAbility, m260.Target);
        Assert.Equal(4f, m260.Seconds);
        Assert.Equal(1f, m260.Chance);

        Assert.True(TempMods.TryGet(SidekickCombo, out var e266));
        var m266 = Assert.Single(e266.Mods);
        Assert.Equal(TempMods.KindState, m266.Kind);
        Assert.Equal(TempMods.EnrageAbility, m266.Target);
        Assert.Equal(8f, m266.Seconds);
        Assert.Equal(0.2f, m266.Chance);

        Assert.True(TempMods.TryGet(SwirlingKiSummon, out var e268));
        Assert.Equal(2, e268.Mods.Length);
        Assert.Contains(e268.Mods, m => m.Kind == TempMods.KindStat && m.Target == 522 && m.MinValue == -60 && m.Seconds == 12f);
        Assert.Contains(e268.Mods, m => m.Kind == TempMods.KindStat && m.Target == 524 && m.MinValue == -60 && m.Seconds == 12f);
        Assert.Equal(0x28000a43u, (uint)AbilityCatalog.Find(SwirlingKiSummon).Number("Ability/ExecutionFX"));
        Assert.Equal(3.0, AbilityCatalog.Find(SwirlingKiSummon).Number("Ability/RadiusOfEffect"));

        // Verify all four stands are outdoor walkable floor at y=495 and meet exact 3 m PBAE boundaries.
        foreach (var (name, x, z) in new[] { ("CSR", 24751, -1498), ("QAEnemy", 24499, -1498), ("QAFriend", 24625, -1298), ("QAFar", 25151, -1498) })
        {
            Assert.True(NavMesh.Locate(1, x, 495, z, out var spot) && !spot.indoors && Math.Abs(spot.floorY + 95 - 495) < 10,
                $"{name} stand ({x},495,{z}) must be outdoor walkable floor");
        }
        static double DistMetres(int x1, int z1, int x2, int z2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2)) / 100.0;
        Assert.True(DistMetres(24751, -1498, 24499, -1498) < 3.0); // QAEnemy 2.52 m <= 3 m
        Assert.True(DistMetres(24751, -1498, 24625, -1298) < 3.0); // QAFriend 2.36 m <= 3 m
        Assert.True(DistMetres(24751, -1498, 25151, -1498) > 3.0); // QAFar 4.00 m > 3 m
    }

    [Fact]
    public void DisposableProcGateOnlyActivatesForCsrToQaEnemy266OnDisposableDb()
    {
        Assert.True(MartialBlindEnrageAreaQa.Allowed("1", "1", "mxotest_1234", 110, inWorld: true, dead: false));
        Assert.False(MartialBlindEnrageAreaQa.Allowed("0", "1", "mxotest_1234", 110, inWorld: true, dead: false));
        Assert.False(MartialBlindEnrageAreaQa.Allowed("1", "0", "mxotest_1234", 110, inWorld: true, dead: false));
        Assert.False(MartialBlindEnrageAreaQa.Allowed("1", "1", "mxo", 110, inWorld: true, dead: false));
        Assert.False(MartialBlindEnrageAreaQa.Allowed("1", "1", "mxotest_1234", 2, inWorld: true, dead: false));
        Assert.False(MartialBlindEnrageAreaQa.Allowed("1", "1", "mxotest_1234", 110, inWorld: true, dead: true));

        // Verify AlwaysProcRandom guarantees Enraged (266) instantiates every time while normal 20% can miss.
        Assert.True(TempMods.TryGet(SidekickCombo, out var effect));
        var alwaysType = typeof(MartialBlindEnrageAreaQa).GetNestedType("AlwaysProcRandom", BindingFlags.NonPublic)!;
        var alwaysRng = (Random)Activator.CreateInstance(alwaysType)!;
        for (int i = 0; i < 25; i++)
        {
            var mods = TimedEffects.Instantiate(effect, alwaysRng);
            var enrage = Assert.Single(mods);
            Assert.Equal(TempMods.EnrageAbility, enrage.Target);
            Assert.Equal(8f, enrage.Seconds);
        }
    }
}
