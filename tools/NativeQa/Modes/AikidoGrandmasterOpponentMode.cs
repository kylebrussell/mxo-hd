using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AikidoGrandmasterOpponentMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, DummyZ = 6700, OtherX = 17200, OtherZ = 6500, Level = 50;
    private const ushort Aikido = 101;
    // First landed stock requests retained from PR473. Rebind only the receiver-local handle.
    private static readonly (ushort Id, ushort Group, int Damage, string Frame)[] Rows =
    {
        (407, 688, 28, "80b9970104000200000000000000000000000000000000000000000097010000006b0000086b0000000000"),
        (411, 687, 129, "80b99b010400020000000000000000000000000000000000000000009b010000006b0000086b0000000000"),
        (412, 687, 152, "80b99c010400020000000000000000000000000000000000000000009c010000006b0000086b0000000000")
    };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Aikido, 407, 411, 412);

    public override string Name => "ability-parity-aikido-grandmaster-opponent";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            "alignment=1,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={OtherX},y={Y},z={OtherZ},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},alignment=2 WHERE charId=6113",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6113) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "wounding-throw.csv", hotbarAbilities: new ushort[] { Aikido, 407, 411, 412 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.MartialFixedDamageMode] = "aikido-grandmaster"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "407,411,412", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stay at verified HOME facing the 30,000 HP dummy 3 m north. Strafe briefly and click if it is behind the camera. No teleport. Page 2 Aikido / 407 / 411 / 412.",
            "Select Aikido and the dummy while at rest. Try slots 2–4. Expect local interlock restrictions or 24 with no cost or reuse. Open radial Close Combat on the dummy and reselect Aikido. Double-click Speed in Actions > Awakened.",
            "Try /target QAOther. If selection changes, try slots 2–4 and expect no special on QAOther. If selection stays locked to the dummy, record the stock lock and skip those casts. Headless sends all three wrong-opponent and stale-spawn requests explicitly.",
            "Reselect the dummy. Before setup try 411/412. Expect an Off Balance restriction from stock or the server. Arm ?cd ma 407 then slot 2, ?cd ma 411 then slot 3, ?cd ma 412 then slot 4. Wait 5 s between all casts. Expect 28/129/152 on the dummy, with 827/735/751 and normal timers. QA grants one win and any required state per cast. Natural secondary effect rolls unchanged."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Melee Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 128, inside: false)}");
        Console.WriteLine($"READY QAOther {InvisibilityBeltWatcherMode.Warp(OtherX, Y, OtherZ, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ) &&
            NavMesh.HasSightPath(1, X, Y, Z, OtherX, Y, OtherZ), "Both participants have clear sight");
        var server = context.ActualServer!;
        var bystander = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther", stockCharacterLoad: true);
        context.Helpers.Add(bystander.Handle, bystander);
        await PvpOpponentsFixture.EnterAsync(bystander);
        NativeQaReadiness.RequireNoAmbientAggro(server, bystander);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var other = await caster.WaitForViewNamedAsync(bystander.Handle);
        uint original = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        uint bystanderHandle = DuelPackets.Handle(other.ViewId, other.SpawnId);
        await Select();
        foreach (var row in Rows) await RefusedAsync(caster, row.Id, WithHandle(row.Frame, original));
        await StarterInterlockDamageMode.Open(caster, dummy);
        await Select();
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(original))}");
        foreach (var row in Rows)
        {
            await RefusedAsync(caster, row.Id, WithHandle(row.Frame, bystanderHandle));
            await RefusedAsync(caster, row.Id, WithHandle(row.Frame, original ^ 0x10000));
            if (row.Id != 407)
                await RefusedAsync(caster, row.Id, WithHandle(row.Frame, original), AbilityRefusals.TargetStateRequired);
        }
        foreach (var row in Rows)
        {
            caster.SendChat($"?cd ma {row.Id}");
            await caster.WaitForTextAsync($"Martial starter fixture armed {row.Id}.");
            int from = caster.World.RpcSnapshot().Count;
            int logAt = server.DebugLog().Length;
            caster.SendRpc(WithHandle(row.Frame, original));
            await EvidenceRetestWire.TimerAsync(caster, from, row.Id, AbilityCooldowns.OwnTimer, 10);
            await caster.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[ABILITY] {row.Id} lands on view {dummy.ViewId} for {row.Damage}", StringComparison.Ordinal),
                $"The exact eligible {row.Id} hits the original opponent for {row.Damage}", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(caster, from, row.Group, AbilityCooldowns.GroupTimer, 5, ended: true);
        }
        Console.WriteLine("AIKIDO GRANDMASTER OPPONENT PASS: invalid requests free; exact live opponent and state accepted.");
        return Array.Empty<byte>();

        async Task Select()
        {
            caster.SendRpc(WithHandle(MartialArtsStarterAdmissionMode.StockAikido, original));
            await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
        }
    }
}
