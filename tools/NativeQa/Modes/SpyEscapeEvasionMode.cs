using HeadlessClient;
using hds;
using MxoHd.PacketResearch;
using static NativeQa.Modes.SpyEscapeMovesMode;

namespace NativeQa.Modes;

public sealed class SpyEscapeEvasionMode : NativeQaMode
{
    private const ushort Side = 121, Cartwheel = 292, Evade = 217;
    private const int X = 17000, Y = 495, Z = 2300, DummyX = 17200, EnemyX = 16900, EnemyZ = 2400, Level = 50;
    private const string Home = "?tp 17000 495 2300 393 11 3 - 40";
    // Unselected toggle from StarterEvadeResistVirusesMode's retained stock capture.
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Side, Cartwheel, Evade, CombatStyles.SpyStyle)
        .Order().ToArray();

    public override string Name => "ability-parity-spy-escape-evasion";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot=107" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64 WHERE charId=110",
            $"UPDATE characters SET level=19,exp={PlayerProgression.ExperienceForLevel(19)},x={EnemyX},y={Y},z={EnemyZ},rotation=64 WHERE charId=6114",
            "DELETE FROM inventory WHERE charId=6114 AND slot=107",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", new ushort[] { 2, 17, 113 }.Select((id, index) =>
                    $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},19,1)"))
        }, DevCommands: true,
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Side, Cartwheel, Evade },
        testMobFixture: "awakened-power-shot-cap.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "121,292", ["NativeCloseableRows"] = 2,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Outdoor HOME {Home} only if displaced; face Melee Dummy 2 m east. If it is behind the camera, strafe/turn and single-click. With the dummy visible, type ?cd fixture-reset once before the first Close Combat. Wait for Fixture ready, then keep that same NPC selected. The live reset makes the dummy passive without replacing its view. QAEnemy is northwest on clear NAV and acts only from helper controls. Do not select it or press Escape.",
            "Page 2 Spy/Side Roll/Cartwheel/Evade. At rest with dummy selected, press slots 2 and 3. Expect stock suppression or interlock refusal; no IS or timer. Open radial Close Combat, reselect Spy and choose Speed, then slot 2. Confirm 121 costs 15 IS, 12 s group 1200/no own timer, zero special damage, [MODIFIER] 520 +20 for 30 s and ordinary leave. Level-3 NPC against L50 keeps the ordinary hit reliable. If an exchange misses, retry after group reuse.",
            """Immediately after leaving, press Evade slot 4 at rest and send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Expect [COMBAT-INSIGHT OURS] force103 evade130 allowedFalse (helper is on Power with ordinary Combat Insight +3, CSR is still Speed) and shield resistance; no [PVP] round. Wait for 121 [MODIFIER] remove, send the same action once more under the same shield, and expect evade110. Toggle Evade off and wait for its ordinary reuse before the next stage. No helper stop is needed on a resisted entry.""",
            "Re-open the same passive NPC without another reset, reselect Spy and Speed, and use slot 3. Expect 15 IS, 30 s own/12 s shared reuse, zero special damage, [EFFECTS] 292 on Melee Dummy State 91 for 4 s and the Confuse notice, 520 +25 for 30 s, and ordinary leave. Press Evade and repeat the same helper entry before and after 292 removal. Expect force103 evade135 then 110, with no [PVP] round. Preserve both stock ability requests and complete add/remove records. General Confuse gate/break checks are shared with the player-recipient Confuse native family."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerWithdrawBonusMode.VerifyStands(context.RepositoryRoot!);
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) &&
            (home.sector, home.region, home.polygon) == (0x393, 0x11, 3), "HOME matches NAV");
        Check.That(NavMesh.Locate(1, EnemyX, Y, EnemyZ, out var foeStand) && !foeStand.indoors && foeStand.floorY + 95 == Y &&
            NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, EnemyZ), "QAEnemy has verified same-floor outdoor NAV and clear sight");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc("42 01 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(enemy, 0, rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.SourceId == 1017 && bonus.TargetId == 1012,
            "QAEnemy selects ordinary Power");
        await StockAbilityProfile.ObserveAsync(enemy, 0, rpc => StockAbilityProfile.Bonus(rpc, 113, 1048, 3, 0, false),
            "QAEnemy has only the intended +3 force passive");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        int passiveAt = server.DebugLog().Length;
        actor.SendChat("?cd fixture-reset");
        await actor.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string dummyHex = WireFormat.ToHex(BitConverter.GetBytes(dummyHandle));
        var self = await enemy.WaitForViewNamedAsync(actor.Handle);
        string enemyTarget = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(self.ViewId, self.SpawnId)));
        await StarterHackerControlTargetMode.RefusedAsync(actor, Side, StarterHackerControlTargetMode.WithHandle(StockSideRoll, dummyHandle));
        await StarterHackerControlTargetMode.RefusedAsync(actor, Cartwheel, StarterHackerControlTargetMode.WithHandle(StockCartwheel, dummyHandle));
        foreach (var (id, request, evasion) in new[] { (Side, StockSideRoll, 20), (Cartwheel, StockCartwheel, 25) })
        {
            actor.SkipReceived();
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, dummyHandle), WireFormat.FromHex($"40 {dummyHex} 01 6b"));
            await actor.WaitForRpcAsync($"40 {dummyHex} 00 6b");
            await actor.WaitForRpcAsync("47");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockStyle, dummyHandle));
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

            int cast = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(request, dummyHandle));
            await actor.WaitForRpcAsync($"46 {dummyHex} 00");
            await StockAbilityProfile.ObserveAsync(actor, cast, rpc => StockAbilityProfile.Bonus(rpc, id, 520, evasion, 30, false),
                $"Escape {id} adds +{evasion} 520");
            await EvidenceRetestWire.TimerAsync(actor, cast, 1200, AbilityCooldowns.GroupTimer, 12);
            if (id == Cartwheel)
            {
                await EvidenceRetestWire.TimerAsync(actor, cast, Cartwheel, AbilityCooldowns.OwnTimer, 30);
                Check.That(server.DebugLog()[log..].Contains("[EFFECTS] 292 on Melee Dummy: State 91 0 4 s", StringComparison.Ordinal),
                    "Actual Cartwheel applies 4 s Confuse to the current opponent");
            }

            int shield = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StockEvade);
            await ShieldAsync(shield, false);
            await AttackAsync(110 + evasion);
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cast).Any(rpc =>
                StockAbilityProfile.Bonus(rpc, id, 520, evasion, 30, true)), $"Escape {id} naturally expires", TimeSpan.FromSeconds(34));
            if (id == Cartwheel)
                Check.That(server.DebugLog()[log..].Contains("source=292 stat=91 remove value=0 duration=4s", StringComparison.Ordinal),
                    "NPC Confuse naturally removes its original four-second record");
            await AttackAsync(110);
            if (id == Side)
            {
                int off = actor.World.RpcSnapshot().Count;
                actor.SendRpc(StockEvade);
                await ShieldAsync(off, true);
                byte[] timerOff = AbilityCooldowns.BuildTimer(Evade, AbilityCooldowns.OwnTimer, 30, false);
                await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(off).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(timerOff)),
                    "Ordinary Evade reuse before the second case", TimeSpan.FromSeconds(34));
            }
        }
        Check.That(!server.DebugLog()[passiveAt..].Contains("[AGGRO] Melee Dummy", StringComparison.Ordinal),
            "The passive target remains selectable and never re-engages after either escape");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Escape and entry framing");
        return Array.Empty<byte>();

        Task<RpcRecord> ShieldAsync(int at, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, at, rpc =>
                ManageBonusRecord.TryDecode(rpc.Bytes, out var rec) && rec!.SourceId == Evade &&
                rec.TargetId == EvasionShield.StateAbility && rec.AddRemoveType == (remove ? 1 : 0), $"Evade state remove={remove}");

        async Task AttackAsync(int expectedEvade)
        {
            int at = server.DebugLog().Length;
            enemy.SkipReceived();
            enemy.SendRpc($"40 {enemyTarget} 01 6b");
            await enemy.WaitForRpcAsync($"4d 0e 00 01 00 {enemyTarget}");
            Check.That(server.DebugLog()[at..].Contains($"force103 evade{expectedEvade} allowedFalse", StringComparison.Ordinal),
                $"Ordinary Power entry consumes the current 520 total ({expectedEvade})");
        }
    }
}
