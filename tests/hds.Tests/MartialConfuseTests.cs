using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using NativeQa;
using NativeQa.Modes;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class MartialConfuseTests
{
    private const ushort KungFu = CombatStyles.KungFu;                                                     // 133
    private const ushort SuicidalButterfly = KungFuGrandmasterSpecials.SuicidalButterfly;                  // 428
    private const ushort Aikido = CombatStyles.Aikido;                                                     // 101
    private const ushort CounterThrow = KarateAikidoMasterSpecials.CounterThrow;                           // 273
    private const ushort AikidoSpinningClayPigeon = MartialArtsRemainingSpecials.AikidoSpinningClayPigeon; // 296
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
        if (style != 0) CombatStyles.UseStyle(client, style);
        return client;
    }

    [Fact]
    public void QaGateFixes428AndGrabConditionOnlyInDisposableCsrFixture()
    {
        Assert.Equal(new ushort[] { 428 }, MartialConfuseQa.FixedAbilityIds);
        Assert.True(MartialConfuseQa.Allowed("1", "1", "mxotest_confuse", 110, true, false));
        Assert.False(MartialConfuseQa.Allowed("0", "1", "mxotest_confuse", 110, true, false));
        Assert.False(MartialConfuseQa.Allowed("1", "0", "mxotest_confuse", 110, true, false));
        Assert.False(MartialConfuseQa.Allowed("1", "1", "mxo", 110, true, false));
        Assert.False(MartialConfuseQa.Allowed("1", "1", "mxotest_confuse", 2, true, false));
        Assert.False(MartialConfuseQa.Allowed("1", "1", "mxotest_confuse", 110, false, false));
        Assert.False(MartialConfuseQa.Allowed("1", "1", "mxotest_confuse", 110, true, true));
    }

    [Theory]
    [InlineData(TempMods.DazeAbility, CombatTactic.Speed)]
    [InlineData(TempMods.OffBalanceAbility, CombatTactic.Grab)]
    [InlineData(TempMods.StaggeredAbility, CombatTactic.Power)]
    public void OpponentStateGateAcceptsRealTacticConditionFlagsOnPlayersAndNpcs(ushort state, CombatTactic tactic)
    {
        uint bit = CombatInterlock.ConditionBit(tactic);

        var player = Client(Array.Empty<ushort>(), 0, charId: 6114);
        var playerSubject = TimedEffects.ForPlayer(player);
        Assert.False(CombatHandler.HasOpponentState(playerSubject, state, 10.0));
        player.playerData.conditionStateFlags = bit;
        Assert.True(CombatHandler.HasOpponentState(playerSubject, state, 10.0));
        player.playerData.conditionStateFlags = 0;
        Assert.False(CombatHandler.HasOpponentState(playerSubject, state, 10.0));

        var mob = new Mob();
        mob.setEntityId(9100);
        mob.setName("Dummy");
        var mobSubject = TimedEffects.ForMob(mob);
        Assert.False(CombatHandler.HasOpponentState(mobSubject, state, 10.0));
        typeof(Mob).GetField("conditionFlags", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(mob, bit);
        Assert.True(CombatHandler.HasOpponentState(mobSubject, state, 10.0));
    }

    [Fact]
    public void PlanMemoryStandsAndExactPerRowValuesAreVerified()
    {
        var plan = NativeQaModes.RequireKnown("ability-parity-ma-confuse").Plan;

        Assert.True(plan.DevCommands);
        Assert.Equal("martial-confuse.csv", plan.TestMobFixture);
        Assert.Equal("1", plan.EnvironmentOverrides[MartialConfuseQa.Mode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_PVP_SERVER"]);
        Assert.Equal(new ushort[] { KungFu, SuicidalButterfly, Aikido, CounterThrow, AikidoSpinningClayPigeon }, plan.HotbarAbilities);
        Assert.Equal(24751, plan.X);
        Assert.Equal(-1498, plan.Z);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(94, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        var kfClient = Client(plan.Abilities.ToArray(), KungFu);
        var aikClient = Client(plan.Abilities.ToArray(), Aikido);
        Assert.Equal(4f, AbilityControlDurations.Scale(kfClient, TempMods.ConfuseAbility, 4f));
        Assert.Equal(4f, AbilityControlDurations.Scale(aikClient, TempMods.ConfuseAbility, 4f));
        Assert.Equal(8f, AbilityControlDurations.Scale(aikClient, TempMods.ConfuseAbility, 8f));
        Assert.Equal(41, AbilityStats.Influence(kfClient, 159));
        Assert.Equal(40, AbilityStats.Influence(aikClient, 159));

        (ushort Id, WorldClient Caster, ushort Base, ushort Scaled, ushort Cost, float OwnCd, ushort Group, float GroupCd, float ConfuseSec, float Chance, uint ReqState)[] expected =
        {
            (SuicidalButterfly, kfClient, 25, 35, 35, 10f, 688, 5f, 4f, 0.3f, 0u),
            (CounterThrow, aikClient, 56, 78, 15, 10f, 687, 5f, 4f, 1.0f, 4u),
            (AikidoSpinningClayPigeon, aikClient, 375, 524, 40, 40f, 0, 0f, 8f, 1.0f, 4u)
        };

        foreach (var e in expected)
        {
            var item = new AbilityItem();
            item.setAbilityID(e.Id);
            ushort raw = AbilityHandler.AbilityDamage(item, new Random(0), characterLevel: FixtureLevel);
            ushort scaled = AbilityStats.ScaleAbilityDamage(e.Caster, e.Id, raw);
            Assert.Equal(e.Base, raw);
            Assert.Equal(e.Scaled, scaled);
            Assert.Equal(e.Cost, AbilityCosts.Cost(e.Id));
            Assert.True(AbilityCooldowns.TryGet(e.Id, out var cd));
            Assert.Equal(e.OwnCd, cd.Seconds);
            Assert.Equal(e.Group, cd.Group);
            Assert.Equal(e.GroupCd, cd.GroupSeconds);
            Assert.True(TempMods.TryGet(e.Id, out var eff));
            var mod = Assert.Single(eff.Mods, m => m.Target == TempMods.ConfuseAbility);
            Assert.Equal(TempMods.KindState, mod.Kind);
            Assert.Equal(e.ConfuseSec, mod.Seconds);
            Assert.Equal(e.Chance, mod.Chance, 3);
            Assert.Equal(e.ReqState, (uint)AbilityCatalog.Find(e.Id).Number("Ability/CastRequiredStates"));
        }

        // Both spawn stands are on outdoor walkable NavMesh floor at 2.52 m separation.
        Assert.True(NavMesh.Locate(1, 24751, 495, -1498, out var csrSpot) && !csrSpot.indoors && Math.Abs(csrSpot.floorY + 95 - 495) < 10);
        Assert.True(NavMesh.Locate(1, 24499, 495, -1498, out var enemySpot) && !enemySpot.indoors && Math.Abs(enemySpot.floorY + 95 - 495) < 10);
        Assert.Equal(252, Math.Abs(24751 - 24499));
    }

    [Fact]
    public void ConditionFlagWireDecodersMatchPvpFlagPackets()
    {
        uint flags = PvpFlag.PvpBit | PvpFlag.InCombatBit | CombatInterlock.ConditionBit(CombatTactic.Grab);
        byte[] selfPacket = PvpFlag.BuildSelfFlags(flags);
        Assert.True(MartialConfuseMode.TryReadSelfConditionFlags(selfPacket, out uint decodedSelf));
        Assert.Equal(flags, decodedSelf);

        byte[] otherTail = PvpFlag.BuildOtherFlags(flags);
        byte[] otherPacket = new byte[2 + otherTail.Length];
        BitConverter.GetBytes((ushort)4).CopyTo(otherPacket, 0);
        otherTail.CopyTo(otherPacket, 2);
        Assert.True(MartialConfuseMode.TryReadOtherConditionFlags(otherPacket, 4, out uint decodedOther));
        Assert.Equal(flags, decodedOther);
        Assert.False(MartialConfuseMode.TryReadOtherConditionFlags(otherPacket, 5, out _));

        // Coalesced view-list payload (e.g., health updates + condition flags in one packet).
        byte[] coalesced = new byte[8 + otherPacket.Length + selfPacket.Length];
        otherPacket.CopyTo(coalesced, 8);
        selfPacket.CopyTo(coalesced, 8 + otherPacket.Length);
        Assert.True(MartialConfuseMode.TryReadOtherConditionFlags(coalesced, 4, out uint coalescedOther));
        Assert.Equal(flags, coalescedOther);
        Assert.True(MartialConfuseMode.TryReadSelfConditionFlags(coalesced, out uint coalescedSelf));
        Assert.Equal(flags, coalescedSelf);
    }
}
