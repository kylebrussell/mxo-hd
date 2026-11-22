using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerSelectedFieldBoundariesMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, CentreZ = 6600;
    private const int FiveEdgeZ = 7100, SevenEdgeZ = 7300, OutsideZ = 7301, ControlEdgeZ = 7000, ControlOutsideZ = 7001;
    private const ushort Miasma = 343, Inefficiency = 401, Disruption = 403, Nuke = 60;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Miasma,
        Inefficiency, Disruption, Nuke).Order().ToArray();
    private static readonly (string Name, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2),
        ("QAOther", 6113, X, FiveEdgeZ, 2),
        ("QAFar", 6112, X, OutsideZ, 2),
        ("QAFriend", 6111, X + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-hacker-selected-field-boundaries";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X,CentreZ)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={stand.Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId={stand.Character}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Miasma, Inefficiency, Disruption, Nuke },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "343,401,403", ["NativeCloseableRows"] = 3 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{CentreZ}), 2 m away. QAOther starts at ({X},{Y},{FiveEdgeZ}), 5 m from QAEnemy. QAFar ({X},{Y},{OutsideZ}) is 7.01 m from QAEnemy, and QAFriend ({X + 100},{Y},{CentreZ}) is the in-radius ally. The stands are clear of the WinterFrost ice.",
            "Select Hacker Style on page 2 slot 1. Try slots 2–4 with empty selection, then with /target QAFriend. Expect local refusal or reason 24 without cost, cast, timer or effect. Then select /target QAEnemy, and keep that selection throughout.",
            "Press slot 2 for Miasma 2.0 (343). Save the full stock use request. Expect 2 s, 14 IS (700->686), 28 s reuse, and QAEnemy plus QAOther [MODIFIER] source=343 stat=1009/1010 -21 for 45 s. The field is a single apply centred on QAEnemy. QAOther is 7 m from CSR and still receives it.",
            $$"""Move QAOther to the 7.00 m edge with helper request {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":{{SevenEdgeZ}}}. Press slot 3 (401) and save its stock request. Expect 2 s, 20 IS (686->666), 60 s reuse, QAEnemy and QAOther source=401 stat=117 -20 and 114 -35 for 30 s. QAFar 7.01 m is excluded.""",
            $"Move QAOther back to z={FiveEdgeZ} and use slot 4 (403). Save its stock request. Expect 2 s, 15 IS (666->651), 30 s reuse, source=403 stat=183 -50 for 23 s on QAEnemy and QAOther. Each field should remove its own records after its duration. No copy on CSR/QAFriend/QAFar, no health damage or max [VITALS] change, and no [PVP] interlock round. Slot 5 is the known stock Code Nuke control used by bootstrap."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z))
            .Concat(new[] { (X, Z), (X, SevenEdgeZ), (X, ControlEdgeZ), (X, ControlOutsideZ) }))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z) &&
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
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out ushort rank) &&
                rank >= need.Value)), "The two complete ranked field trees fit in memory");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var helper in context.Helpers.Values) await caster.WaitForViewNamedAsync(helper.Handle);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        await MoveControlAsync("QAOther", ControlEdgeZ);
        await MoveControlAsync("QAFar", ControlOutsideZ);
        var primary = await caster.WaitForViewNamedAsync("QAEnemy");
        uint handle = DuelPackets.Handle(primary.ViewId, primary.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        var witnesses = context.Helpers.Values.Select(player => (Player: player, Cursor: player.World.RpcSnapshot().Count)).ToArray();
        int cast = caster.World.RpcSnapshot().Count;
        string stock = StarterHackerViralSightMode.Samples.Single(sample => sample.Ability == Nuke).Capture;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Nuke, 4);
        await EvidenceRetestWire.TimerAsync(caster, cast, Nuke, AbilityCooldowns.OwnTimer, 2);
        var damage = BytePattern.Parse("81 67 ?? ?? ?? ?? " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.DamageTarget)) +
            " 00 00 10 00 00 " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.AbilityGameObject(Nuke))));
        foreach (var (player, cursor) in witnesses)
        {
            if (player.Handle is "QAEnemy" or "QAOther")
                await StockAbilityProfile.ObserveAsync(player, cursor, rpc => damage.IsMatch(rpc.Bytes), $"Code Nuke hits {player.Handle}");
            else
            {
                player.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(player, cursor, rpc => rpc.Opcode == 0x149, "Excluded helper stays connected");
                Check.That(!player.World.RpcSnapshot().Skip(cursor).Any(rpc => damage.IsMatch(rpc.Bytes)), $"{player.Handle} is excluded");
            }
        }
        Check.Equal(0, caster.World.Problems.Count + witnesses.Sum(witness => witness.Player.World.Problems.Count), "Selected splash framing");
        Console.WriteLine("SELECTED FIELDS BOOTSTRAP PASS: exact stock Code Nuke control; 343/401/403 stock frames pending.");
        return Array.Empty<byte>();

        async Task MoveControlAsync(string name, int z)
        {
            var helper = context.Helpers[name];
            var view = await caster.WaitForViewNamedAsync(name);
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = caster.World.ViewSnapshot().Count;
            helper.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }
    }
}
