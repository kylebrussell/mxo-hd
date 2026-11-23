using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerInsidiousRavagerMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, CasterZ = 6400, CenterZ = 6700, RangeRefusalZ = 8001;
    private const ushort Insidious = 360, Ravager = 470, Downgrade = 1081, Style = CombatStyles.HackerStyle;
    // actions_combat_marketplace_abilitychangecoder, line 27208.
    private const string StockPower = "42 01 00 00 00 00 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Insidious, Ravager).Order().ToArray();
    private static readonly (string Name, int Char, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, X, CenterZ, 2),
        ("QAOther", 6113, X, 7200, 2),
        ("QAFar", 6112, X, 7201, 2),
        ("QAFriend", 6111, X + 100, CenterZ, 1)
    };
    private static readonly (ushort Stat, int Value)[] InsidiousMods = { (157, -100), (159, -14), (180, -100) };

    public override string Name => "ability-parity-hacker-insidious-ravager";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CasterZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,CenterZ)},innerStrC=700,innerStrM=1000 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Org} WHERE charId={stand.Char}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Insidious, Downgrade },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "360,1081", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}; full ranked Code Shield/Ravager profile {Loadout.Sum(AbilityMemory.Cost)}/100 memory. Clear outdoor NAV CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{CenterZ}), QAOther z7200 is the 5.00 m edge, QAFar z7201 is 5.01 m, and ally QAFriend is x17100,z6700.",
            "Select Hacker Style. Try both spells with no target and then /target QAFriend. Target QAEnemy after the OUT helper move to check the 16.01 m refusal, then send IN. Expect local refusal or reason 24/34 and no cast, IS or reuse.",
            $$"""OUT helper request {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{RangeRefusalZ}}}. IN helper request {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{CenterZ}}}.""",
            "Select QAEnemy and press slot 2. Insidious Code: 4 s, expected paid 23 (700 to 677), own58. QAEnemy and QAOther only get [MODIFIER] source360 stats157/180 -100 and 159 -14 for 24 s; then see all matched removals. HP and maximum vitals stay steady.",
            "After the 360 sample, keep QAEnemy selected at rest. Confirm Ravager Downgrade in Actions or use slot 3. Save the first full stock 80 b9 39 04. Expect 6 s, paid 28 (677 to 649), own120. QAEnemy alone gets source1081 Confuse state91 for 8 s and stat183 -12 for 30 s. QAEnemy tries the retained Power tactic once while confused and retries after state removal. Preserve the refused and accepted tactic lines plus both timed removals.",
            "Optional stale Archive check: unload 470, try slot 3 for local refusal or reason 7, then reload 470. The grant should disappear and return. Keep the helper on the street and leave selection without Escape."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, CasterZ)).Append((X, RangeRefusalZ)))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, CenterZ, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Open sight to ({x},{Y},{z})");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        var enemy = context.Helpers["QAEnemy"];
        if (context.StockProbe is not { } caster)
        {
            _ = WatchRavagerAsync(context, enemy, server);
            return Array.Empty<byte>();
        }

        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(owned.Keys.ToHashSet().SetEquals(Loadout) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                owned.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete ranked Archive profile");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var selected = await caster.WaitForViewNamedAsync(enemy.Handle);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        var passives = new[] { (Ravager, Downgrade, new (ushort Stat, int Value)[] { (1056, 5), (1059, 5) }) };
        var proof = new List<object>();
        int cursor = await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, new[] { Ravager }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(caster, proof, passives, cursor, remove: true);
        cursor = await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, new[] { Ravager }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(caster, proof, passives, cursor, remove: false);

        var witnesses = context.Helpers.Values.Select(player => (Player: player, Cursor: player.World.RpcSnapshot().Count)).ToArray();
        int cast = caster.World.RpcSnapshot().Count;
        caster.SendRpc(ConcentrationFixture.InsidiousRequest(selected));
        await EvidenceRetestWire.CastBarAsync(caster, Insidious, 4);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("[ABILITY-COST] char=110 ability=360 paid=23 IS=700->677"),
            "Insidious pays its calculated IS cost with the full profile", TimeSpan.FromSeconds(6));
        await EvidenceRetestWire.TimerAsync(caster, cast, Insidious, AbilityCooldowns.OwnTimer, 58);
        foreach (var (player, before) in witnesses.Where(witness => witness.Player.Handle is "QAEnemy" or "QAOther"))
        {
            foreach (var (stat, value) in InsidiousMods)
                await StockAbilityProfile.ObserveAsync(player, before, rpc => StockAbilityProfile.Bonus(rpc, Insidious, stat, value, 24, false),
                    $"Insidious Code reaches {player.Handle} with stat {stat}");
        }
        foreach (var (player, before) in witnesses.Where(witness => witness.Player.Handle is "QAFriend" or "QAFar"))
        {
            player.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(player, before, rpc => rpc.Opcode == 0x149, "Excluded helper continues processing");
            Check.That(!player.World.RpcSnapshot().Skip(before).Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) &&
                bonus!.SourceId == Insidious), $"Insidious Code excludes {player.Handle}");
        }
        foreach (var (player, before) in witnesses.Where(witness => witness.Player.Handle is "QAEnemy" or "QAOther"))
        {
            await player.WaitUntilAsync(() => InsidiousMods.All(mod => player.World.RpcSnapshot().Skip(before).Any(rpc =>
                    StockAbilityProfile.Bonus(rpc, Insidious, mod.Stat, mod.Value, 24, true))),
                $"All three Insidious modifiers expire for {player.Handle}", TimeSpan.FromSeconds(30));
        }

        Console.WriteLine("INSIDIOUS/RAVAGER BOOTSTRAP PASS: retained 360 and grant Archive, native 1081 request pending.");
        return Array.Empty<byte>();
    }

    private static async Task WatchRavagerAsync(NativeQaBootstrapContext context, HeadlessPlayer enemy, ServerInstance server)
    {
        string stop = Path.Combine(context.OutputDirectory, "stop");
        int cursor = enemy.World.RpcSnapshot().Count;
        for (; !File.Exists(stop) && !server.HasExited; await Task.Delay(150))
        {
            var packets = enemy.World.RpcSnapshot();
            bool hasConfuse = packets.Skip(cursor).Any(rpc => StockAbilityProfile.Bonus(rpc, Downgrade, TempMods.ConfuseAbility, 0, 8, false));
            cursor = packets.Count;
            if (!hasConfuse) continue;
            try
            {
                int offset = server.DebugLog().Length;
                enemy.SendRpc(StockPower);
                await enemy.WaitUntilAsync(() => server.DebugLog()[offset..].Contains("Tactic request refused for QAEnemy: Confused"),
                    "Ravager rejects QAEnemy's tactic while Confused", TimeSpan.FromSeconds(4));
                await StockAbilityProfile.ObserveAsync(enemy, cursor, rpc => StockAbilityProfile.Bonus(rpc, Downgrade, TempMods.ConfuseAbility, 0, 8, true),
                    "Ravager Confuse expires before the defensive debuff");
                enemy.SendRpc(StockPower);
                await enemy.WaitUntilAsync(() => server.DebugLog()[offset..].Contains("Tactic changed to Power (Normal) for QAEnemy"),
                    "Ravager permits QAEnemy's tactic after expiry", TimeSpan.FromSeconds(5));
                Console.WriteLine("RAVAGER CONFUSE PASS: QAEnemy's Power request was refused during the eight-second state and accepted after expiry.");
                return;
            }
            catch (Exception error)
            {
                if (File.Exists(stop) || server.HasExited) return;
                Console.WriteLine($"RAVAGER CONFUSE RETRY: {error.Message}");
            }
        }
    }
}
