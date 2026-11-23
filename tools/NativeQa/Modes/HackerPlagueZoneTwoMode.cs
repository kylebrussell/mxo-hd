using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerPlagueZoneTwoMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, CasterZ = 6400, AnchorZ = 6600, FarZ = 8001;
    private const ushort Plague = 466, DamageOverTime = 1157;
    // abilities_use_plague_zone_2_0_at_corrupted.log.txt, lines 53–55. Rebind only the live target handle.
    private const string StockUse = "80b9d20106002e0000000000000000000000000000000000000000002000000074f212002a6b0000000000";
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Plague).Order().ToArray();
    private static readonly (string Name, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, AnchorZ, 2),
        ("QAOther", 6113, X, 7100, 2),
        ("QAFar", 6112, X, 7101, 2),
        ("QAFriend", 6111, X + 100, AnchorZ, 1)
    };

    public override string Name => "ability-parity-hacker-plague-zone-two";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, CasterZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={CasterZ}," +
                $"rotation={MobAggro.YawTowards(X, CasterZ, X, AnchorZ)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={stand.Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1500,healthM=1500 WHERE charId={stand.Character}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Plague },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "466", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{AnchorZ}) at 2 m. QAOther ({X},{Y},7100) is at 5.00 m from QAEnemy; QAFar ({X},{Y},7101) is at 5.01 m. QAFriend ({X + 100},{Y},{AnchorZ}) is in-radius and allied. All are clear of WinterFrost's ice.",
            "Select Hacker Style on page 2 slot 1. With no target and then with /target QAFriend, try Plague Zone 2.0 on slot 2. If sent, expect reason 24, no cast bar, IS debit, reuse or damage.",
            $$"""Move QAEnemy to 16.01 m using the helper move with x={{X}}, y={{Y}}, z={{FarZ}}. /target QAEnemy and try slot 2. Expect a local range block or reason 34 and no cost, cast bar or reuse. Move QAEnemy back to z={{AnchorZ}} and reselect it. The helper action is {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{FarZ}}}, with only z changed for the return.""",
            "Press slot 2 with QAEnemy selected and retain the full first landed stock 80 b9 d2 01 request. Optional after the cast bar appears: /target QAFar; the splash must stay centred on QAEnemy.",
            "Expect a 2 s cast, [ABILITY-COST] ability=466 paid=20 IS=700->680 and 12 s own reuse. [MODIFIER] source=466 stat=1157 adds a 15 s pool to QAEnemy and QAOther only. Record each pool separately. Expect five [EFFECTS] ticks per recipient, each raw equal to the pool divided by six rounded to the nearest integer, and matching removals at 15 s. There is no sixth tick or interlock.",
            "These damage records describe the current server policy. Shipped base 97 and historical effective pools 424/444 do not resolve the original scaling."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(new[] { (X, CasterZ), (X, FarZ) }))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, AnchorZ, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Open sight to ({x},{Y},{z}) from caster and anchor");
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
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete ranked Plague Zone 2.0 loadout fits in memory");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var views = new Dictionary<string, ViewCreation>();
        foreach (var helper in context.Helpers.Values) views.Add(helper.Handle, await caster.WaitForViewNamedAsync(helper.Handle));

        byte[] Request(string name) => StarterHackerControlTargetMode.WithHandle(StockUse, Handle(name));
        uint Handle(string name) => DuelPackets.Handle(views[name].ViewId, views[name].SpawnId);
        void Select(string name) => caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(Handle(name)))}");

        const string anchor = "QAEnemy";
        await StarterHackerControlTargetMode.RefusedAsync(caster, Plague, Request(anchor), AbilityRefusals.WrongStyle);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await StarterHackerControlTargetMode.RefusedAsync(caster, Plague, StarterHackerControlTargetMode.WithHandle(StockUse, 0));
        await StarterHackerControlTargetMode.RefusedAsync(caster, Plague, Request("QAFriend"));
        await MoveAnchorAsync(FarZ);
        await StarterHackerControlTargetMode.RefusedAsync(caster, Plague, Request(anchor), AbilityRefusals.TargetOutOfRange);
        await MoveAnchorAsync(AnchorZ);

        int logAt = server.DebugLog().Length;
        int castAt = caster.World.RpcSnapshot().Count;
        var witnesses = context.Helpers.Values.Select(helper => (Player: helper, Cursor: helper.World.RpcSnapshot().Count)).ToArray();
        Select(anchor);
        caster.SendRpc(Request(anchor));
        await EvidenceRetestWire.CastBarAsync(caster, Plague, 2);
        Select("QAFar");
        await EvidenceRetestWire.TimerAsync(caster, castAt, Plague, AbilityCooldowns.OwnTimer, 12);

        await Task.WhenAll(witnesses.Where(witness => witness.Player.Handle is anchor or "QAOther")
            .Select(witness => CheckDotAsync(caster, witness.Player, witness.Cursor)));
        foreach (var (player, cursor) in witnesses.Where(witness => witness.Player.Handle is "QAFar" or "QAFriend"))
            Check.That(!player.World.RpcSnapshot().Skip(cursor).Any(frame => Bonus(frame, false) || Bonus(frame, true) ||
                TransmitCodeFixture.Damage(frame.Bytes, Plague, caster.Handle, player.Handle, out _, out _)),
                $"{player.Handle} receives no 466 modifier or damage");

        await EvidenceRetestWire.TimerAsync(caster, castAt, Plague, AbilityCooldowns.OwnTimer, 12, ended: true);
        int fizzleAt = server.DebugLog().Length;
        castAt = caster.World.RpcSnapshot().Count;
        int[] fizzleCursors = witnesses.Select(witness => witness.Player.World.RpcSnapshot().Count).ToArray();
        Select(anchor);
        caster.SendRpc(Request(anchor));
        await StockAbilityProfile.ObserveAsync(caster, castAt, frame => frame.Bytes is [0x80, 0xac, 0xd2, 0x01, ..],
            "Fresh 466 cast bar for the lost anchor");
        await MoveAnchorAsync(FarZ);
        Select("QAFar");
        await caster.WaitUntilAsync(() => server.DebugLog()[fizzleAt..].Contains("[ABILITY] 466 fizzles: the selected viral target changed or left range"),
            "Original target leaving range fizzles even with another hostile selected", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(caster, castAt, Plague, AbilityCooldowns.OwnTimer, 12);
        foreach (var ((player, _), cursor) in witnesses.Zip(fizzleCursors))
        {
            int reply = player.World.RpcSnapshot().Count;
            player.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(player, reply, frame => frame.Opcode == 0x149, "Post-fizzle reply");
            Check.That(!player.World.RpcSnapshot().Skip(cursor).Any(frame => Bonus(frame, false) ||
                TransmitCodeFixture.Damage(frame.Bytes, Plague, caster.Handle, player.Handle, out _, out _)),
                $"The fizzled cast applies no DOT to {player.Handle}");
        }

        string[] costs = server.DebugLog()[logAt..].Split('\n').Where(line => line.Contains("[ABILITY-COST] char=110 ability=466")).ToArray();
        Check.That(costs.Length == 2 && costs[0].Contains("paid=20 IS=700->680") && costs[1].Contains("paid=20 IS=680->660"),
            "Only the two admitted requests pay 20 IS, including the fizzle");
        Check.Equal(0, caster.World.Problems.Count + witnesses.Sum(witness => witness.Player.World.Problems.Count), "Plague Zone replay framing");
        Console.WriteLine("PLAGUE ZONE TWO PASS: exact captured frame, original-anchor fizzle, 5.00 m splash and five ticks per recipient.");
        return Array.Empty<byte>();

        async Task MoveAnchorAsync(int z)
        {
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = caster.World.ViewSnapshot().Count;
            context.Helpers[anchor].World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, cursor, views[anchor].ViewId, $"01 08 {Convert.ToHexString(position)}");
        }
    }

    private static bool Bonus(RpcRecord frame, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) &&
        record!.SourceId == Plague && record.TargetId == DamageOverTime && record.AddRemoveType == (remove ? 1 : 0);

    private static async Task CheckDotAsync(HeadlessPlayer caster, HeadlessPlayer recipient, int cursor)
    {
        var add = await StockAbilityProfile.ObserveAsync(recipient, cursor, frame => Bonus(frame, false), $"{recipient.Handle} receives 466/1157");
        Check.That(ManageBonusRecord.TryDecode(add.Bytes, out var record) && record!.Duration == 15 && record.SignedValue > 0,
            $"{recipient.Handle} has a positive 15-second DOT pool");
        await recipient.WaitUntilAsync(() => recipient.World.RpcSnapshot().Skip(cursor).Any(frame => Bonus(frame, true) &&
            ManageBonusRecord.TryDecode(frame.Bytes, out var removal) && removal!.RecordId == record!.RecordId),
            $"{recipient.Handle} removes the matching 466/1157 record", TimeSpan.FromSeconds(19));

        int expected = (int)Math.Floor(record!.SignedValue / 6.0 + 0.5);
        var ticks = recipient.World.RpcSnapshot().Skip(cursor).Where(frame =>
            TransmitCodeFixture.Damage(frame.Bytes, Plague, caster.Handle, recipient.Handle, out _, out _)).ToArray();
        Check.Equal(5, ticks.Length, $"{recipient.Handle} takes five ticks before removal");
        foreach (var tick in ticks)
        {
            TransmitCodeFixture.Damage(tick.Bytes, Plague, caster.Handle, recipient.Handle, out int damage, out int resisted);
            Check.Equal(expected, damage + resisted, $"{recipient.Handle} raw tick follows its applied pool");
            Check.Equal(expected, damage, $"{recipient.Handle} unresisted tick");
        }
        Console.WriteLine($"PLAGUE ZONE TWO DOT {recipient.Handle}: pool={record.SignedValue} 15s ticks=5 raw={expected} matching removal.");
    }
}
