using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StunPulseSelectedAreaMode : NativeQaMode
{
    private const int Level = 50, PulseRank = 38, X = 17000, Y = 495, CasterZ = 6400, CentreZ = 6600;
    private const int EdgeZ = 7300, OutsideZ = 7301;
    private const ushort Pulse = 595, StaticBlast = 33;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.CoderStyle, Pulse, StaticBlast)
        .Order().ToArray();
    private static readonly (string Name, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2),
        ("QAOther", 6113, X, EdgeZ, 2),
        ("QAFar", 6112, X, OutsideZ, 2),
        ("QAFriend", 6111, X + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-stun-pulse-selected-area";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, CasterZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={PulseRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Pulse)}" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={CasterZ}," +
                $"rotation={MobAggro.YawTowards(X, CasterZ, X, CentreZ)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={stand.Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId={stand.Character}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { CombatStyles.CoderStyle, Pulse, StaticBlast },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "595", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, Stun Pulse rank {PulseRank}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{CentreZ}) at 2 m.",
            $"QAOther ({X},{Y},{EdgeZ}) is 7.00 m from QAEnemy, QAFar ({X},{Y},{OutsideZ}) is 7.01 m, and QAFriend ({X + 100},{Y},{CentreZ}) is the in-radius ally. The stand is clear of WinterFrost and Trainman.",
            "Select Coder Style (page 2 slot 1). With no target, then with /target QAFriend, press Stun Pulse (slot 2). Expect client target refusal or reason 24 with no cast, debit, timer or effects. Then use /target QAEnemy.",
            "Press slot 2 at rest. Keep QAEnemy selected. Save the first complete stock 80 b9 request for 595. Expect a 6 s cast, [ABILITY-COST] ability=595 paid=29 IS=700->671 under Proxy Master's +5% cost reduction, and 30 s own reuse. Do not use Static Blast during observation.",
            "Expect [FREECAST-VIRAL] ability=595 hit=True on QAEnemy and QAOther only. Both char=6114 and char=6113 receive [MODIFIER] source=595 stat=102 add value=0 duration=6s and source=734 stat=681 add value=0 duration=12s. Verify corresponding removals at 6 and 12 s. QAFriend is NotOpponent and QAFar is outside the radius. No [VITALS] HP change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Prepend((X, CasterZ)))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Open sight to ({x},{Y},{z}) from caster and centre");
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
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) && loaded.GetValueOrDefault(Pulse) == PulseRank &&
            Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.GetValueOrDefault(need.Key) >= need.Value)), "The complete ranked Proxy Master profile fits memory");

        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var helper in context.Helpers.Values) await caster.WaitForViewNamedAsync(helper.Handle);
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var enemy = await caster.WaitForViewNamedAsync("QAEnemy");
        uint handle = DuelPackets.Handle(enemy.ViewId, enemy.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        // Retained stock Static Blast; Claude will record the first full stock Stun Pulse.
        const string stock = "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
        int cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, handle));
        await EvidenceRetestWire.CastBarAsync(caster, StaticBlast, 2);
        await EvidenceRetestWire.TimerAsync(caster, cursor, StaticBlast, AbilityCooldowns.OwnTimer, 6);
        var damage = BytePattern.Parse("81 67 ?? ?? ?? ?? " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.DamageTarget)) +
            " 00 00 10 00 00 " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.AbilityGameObject(StaticBlast))));
        await StockAbilityProfile.ObserveAsync(context.Helpers["QAEnemy"], 0, rpc => damage.IsMatch(rpc.Bytes),
            "Captured Static Blast control reached the live selected target");
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Static Blast control framing");
        Console.WriteLine("STUN PULSE BOOTSTRAP PASS. Exact stock Coder Style and Static Blast; stock 595 request pending.");
        return Array.Empty<byte>();
    }
}
