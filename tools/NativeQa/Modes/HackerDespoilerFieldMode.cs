using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerDespoilerFieldMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, Centre = 6900, Edge = 7300, Outside = 7301, Far = 8001;
    private const ushort Despoiler = 404;
    // Vector6, 20:03:50, CSEQ 1913. Bind only the captured target handle to the live view.
    private const string StockUse = "80b994011e00e300000000000000000000000000000000000000000048133b0000000000086b0000000000";
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Despoiler).Order().ToArray();
    private static readonly (ushort Stat, int Amount)[] Effects = { (159, -14), (171, -14), (522, -39), (524, -39) };
    private static readonly (string Name, int Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Centre, 2),
        ("QAOther", 6113, X, Edge, 2),
        ("QAFar", 6112, X, Outside, 2),
        ("QAFriend", 6111, X + 100, Centre, 1)
    };

    public override string Name => "ability-parity-hacker-despoiler-field";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X, Z, X, Centre)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={stand.Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1000,healthM=1000 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Despoiler }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "404", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Centre}) at 5 m. QAOther ({X},{Y},{Edge}) is at 4.00 m from QAEnemy; QAFar ({X},{Y},{Outside}) at 4.01 m. Ally QAFriend ({X + 100},{Y},{Centre}). Verified outdoor NAV clear of ice.",
            "Select Hacker Style on page 2 slot 1. Try slot 2 with no target, then on /target QAFriend. If the client sends the request, expect reason 24 with no cast, IS, reuse or modifiers.",
            $$"""Select /target QAEnemy. Move it out to 16.01 m with helper request {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{Far}}} and try slot 2. Expect local block or reason 34 with no cost. Use the same helper move with z={{Centre}} to return it, then reselect QAEnemy.""",
            "Cast Despoiler from rest and retain the first full landed stock request. Expect 6 s, paid 40 IS (700 to 660) and 30 s reuse. QAEnemy and QAOther get [MODIFIER] source=404 stats 159/171 -14 and 522/524 -39 for 30 s, then remove all four. No effects on the caster, QAFriend or QAFar. No health damage, max [VITALS] change or [PVP] interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(new[] { (X, Z), (X, Far) }))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, Centre, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Open sight to ({x},{Y},{z}) from caster and centre");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) && Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Despoiler's complete ranked tree fits in memory");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var helper in context.Helpers.Values) await caster.WaitForViewNamedAsync(helper.Handle);

        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var primary = await caster.WaitForViewNamedAsync("QAEnemy");
        var ally = await caster.WaitForViewNamedAsync("QAFriend");
        await StarterHackerControlTargetMode.RefusedAsync(caster, Despoiler, StarterHackerControlTargetMode.WithHandle(StockUse, 0),
            AbilityRefusals.InvalidTarget);
        await StarterHackerControlTargetMode.RefusedAsync(caster, Despoiler,
            StarterHackerControlTargetMode.WithHandle(StockUse, DuelPackets.Handle(ally.ViewId, ally.SpawnId)), AbilityRefusals.InvalidTarget);

        uint handle = DuelPackets.Handle(primary.ViewId, primary.SpawnId);
        byte[] use = StarterHackerControlTargetMode.WithHandle(StockUse, handle);
        await MovePrimaryAsync(Far);
        await StarterHackerControlTargetMode.RefusedAsync(caster, Despoiler, use, AbilityRefusals.TargetOutOfRange);
        await MovePrimaryAsync(Centre);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int cursor = caster.World.RpcSnapshot().Count, logStart = server.DebugLog().Length;
        var recipients = context.Helpers.Values.Select(player => (Player: player, Cursor: player.World.RpcSnapshot().Count)).ToArray();
        caster.SendRpc(use);
        await EvidenceRetestWire.CastBarAsync(caster, Despoiler, 6);
        await EvidenceRetestWire.TimerAsync(caster, cursor, Despoiler, AbilityCooldowns.OwnTimer, 30);
        Check.That(server.DebugLog()[logStart..].Contains("[ABILITY-COST] char=110 ability=404 paid=40 "), "A landed Despoiler costs 40 IS");

        foreach (var (recipient, at) in recipients.Where(recipient => recipient.Player.Handle is "QAEnemy" or "QAOther"))
            foreach (var (stat, amount) in Effects)
            {
                await StockAbilityProfile.ObserveAsync(recipient, at,
                    frame => StockAbilityProfile.Bonus(frame, Despoiler, stat, amount, 30, false), $"{recipient.Handle} adds 404/{stat}");
            }

        foreach (var (recipient, at) in recipients.Where(recipient => recipient.Player.Handle is "QAEnemy" or "QAOther"))
            foreach (var (stat, amount) in Effects)
            {
                await recipient.WaitUntilAsync(() => recipient.World.RpcSnapshot().Skip(at).Any(
                    frame => StockAbilityProfile.Bonus(frame, Despoiler, stat, amount, 30, true)),
                    $"{recipient.Handle} removes 404/{stat}", TimeSpan.FromSeconds(35));
            }

        foreach (var (recipient, at) in recipients.Where(recipient => recipient.Player.Handle is "QAFar" or "QAFriend").Append((caster, cursor)))
            Check.That(!recipient.World.RpcSnapshot().Skip(at).Any(frame =>
                ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Despoiler), $"{recipient.Handle} receives no 404 records");

        Check.Equal(0, caster.World.Problems.Count + recipients.Sum(recipient => recipient.Player.World.Problems.Count),
            "Despoiler replay framing");
        Console.WriteLine("DESPOILER FIELD PASS: exact capture, invalid selections, +4.00 m edge, four records and independent expiry.");
        return Array.Empty<byte>();

        async Task MovePrimaryAsync(int z)
        {
            var helper = context.Helpers["QAEnemy"];
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int at = caster.World.ViewSnapshot().Count;
            helper.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, at, primary.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }
    }
}
