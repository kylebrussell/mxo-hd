using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPhageEvadeEntryMode : NativeQaMode
{
    private const int Level = 16;
    private const ushort ExecuteProgram = 45, Virologist = 76, Phage = 83, Insight = 113, Evade = 217, Upgrade = 1082;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, ExecuteProgram, Virologist, Phage, Evade).Order().ToArray();
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, Insight).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] PhageStats = { (1030, 1), (1045, 1), (1049, 1) };
    // PR524 Evade Combat and the ordinary Power tactic action.
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string Power = "42 01 00 00 00 00 00";

    public override string Name => "ability-parity-starter-phage-evade-entry";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PvpOpponentsFixture.X, PvpOpponentsFixture.Z,
        Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=CASE ability_id WHEN {AbilityMessages.AbilityGameObject(ExecuteProgram)} THEN 2 ELSE {Level} END WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},rotation=64 WHERE charId=110",
            "DELETE FROM inventory WHERE charId IN (110,6114) AND slot IN (105,107)",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", HelperLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},50,1)"))
        }, DevCommands: true, hotbarAbilities: new ushort[] { Evade }, hotbarWithdraw: true, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "45,76,83,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR (17000,495,2300) faces QAEnemy (17120,495,2300), 1.2 m east. QAEnemy is level 50, in Power with fully qualified Combat Insight. Stay in Speed on CSR, with no other action active.",
            """Press page 2 slot 1 Evade Combat. Expect [EVASION] state on and 40 IS spent. Startup [MODIFIER] 83/1030,1045,1049 are +1 each; Virologist 76/1049 is +2 and Execute Program 45/1049 is 0. Send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Expect [COMBAT-INSIGHT OURS] force110 evade113 allowedFalse and no interlock.""",
            "Type ?qaunload 83, then repeat the helper melee. Expect all three +1 records and the 1082 grant removed, with Evade still on, and force110 evade112 allowedFalse. Type ?qaload 83 and repeat; expect returned records/grant and force110 evade113 allowedFalse. This is the implemented entry formula.",
            "Toggle Evade Combat off in slot 1 and wait 3 s. Repeat the same melee. With the shield down it starts an interlock, with no shield entry score. Withdraw and send helper stop if needed. Save [EVASION], [MODIFIER] and [PVP] output."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        VerifyProfile(enemy, HelperLoadout, 50);
        enemy.SendRpc(Power);
        await StockAbilityProfile.ObserveAsync(enemy, 0, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var bonus) && bonus!.SourceId == 1017 && bonus.TargetId == 1012,
            "QAEnemy selected Power");

        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { PvpOpponentsFixture.X, PvpOpponentsFixture.EnemyX })
            Check.That(NavMesh.Locate(1, x, PvpOpponentsFixture.Y, PvpOpponentsFixture.Z, out var nav) &&
                !nav.indoors && Math.Abs(nav.floorY + 95 - PvpOpponentsFixture.Y) < 1, $"Clear outdoor NAV at X={x}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = VerifyProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var seen = await enemy.WaitForViewNamedAsync(actor.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(seen.ViewId, seen.SpawnId)));
        await ExpectPhageAsync(0, false);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Virologist, 1049, 2, 0, false), "Virologist baseline");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, ExecuteProgram, 1049, 0, 0, false), "Execute Program baseline");
        int shield = actor.World.RpcSnapshot().Count;
        actor.SendRpc(EvadeUse);
        await ShieldAsync(shield, false);
        await EnterAsync(113);

        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Phage }, load: false);
        await ExpectPhageAsync(unloaded, true);
        await EnterAsync(112);

        int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Phage }, load: true);
        await ExpectPhageAsync(reloaded, false);
        await EnterAsync(113);

        int removed = actor.World.RpcSnapshot().Count;
        actor.SendRpc(EvadeUse);
        await ShieldAsync(removed, true);
        await EvidenceRetestWire.TimerAsync(actor, removed, Evade, AbilityCooldowns.OwnTimer, 30);
        enemy.SkipReceived();
        enemy.SendRpc($"40 {handle} 01 6b");
        await enemy.WaitForRpcAsync($"46 {handle} 01");
        await enemy.WaitForRpcAsync("45");
        enemy.SendRpc("44 00");
        actor.SendRpc("44 00");
        await enemy.WaitForRpcAsync($"46 {handle} 00", TimeSpan.FromSeconds(20));
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Stock scope and interlock replay framing");
        return Array.Empty<byte>();

        async Task EnterAsync(int evade)
        {
            int initialLog = server.DebugLog().Length;
            enemy.SkipReceived();
            enemy.SendRpc($"40 {handle} 01 6b");
            await enemy.WaitForRpcAsync($"4d 0e 00 01 00 {handle}");
            await enemy.WaitUntilAsync(() => server.DebugLog()[initialLog..].Contains(
                $"QAEnemy -> CSR_Tester force110 evade{evade} allowedFalse", StringComparison.Ordinal),
                $"Selective Phage entry has Evade {evade}", HeadlessPlayer.DefaultTimeout);
        }

        async Task ExpectPhageAsync(int from, bool off)
        {
            foreach (var (stat, value) in PhageStats)
                await StockAbilityProfile.ObserveAsync(actor, from, frame => StockAbilityProfile.Bonus(frame, Phage, stat, value, 0, off),
                    $"Phage {stat}, remove={off}");
            await StockAbilityProfile.ObserveAsync(actor, from, StockAbilityProfile.GrantRecord(Upgrade, off), $"Phage grant, remove={off}");
        }

        Task<RpcRecord> ShieldAsync(int from, bool off) =>
            StockAbilityProfile.ObserveAsync(actor, from,
                frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, off), $"Evade shield, remove={off}");
    }

    private static Dictionary<ushort, AbilityMemoryUiFixture.Tuple> VerifyProfile(HeadlessPlayer p, ushort[] ids, int level)
    {
        var rows = AbilityMemoryUiFixture.Decode(p.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(ids.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level) &&
            ids.All(id => rows.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), $"{p.Handle} has full ranked prerequisites");
        return rows;
    }
}

