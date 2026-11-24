using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HowitzerStockAreaMode : NativeQaMode
{
    private const int X = 17000, Y = 495, HomeZ = 6400, CentreZ = 6600, FarZ = 8001, Level = 50;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    private const ushort Style = CombatStyles.HackerStyle;
    // Stock retail actions_combat_marketplace_abilitychangecoder, lines 20423–24.
    private const string Bomb = "80b965010400a600000000000000000000000000000000000000000065010000006b0000086b0000000000";
    // Stock retail abilities_use_code_nuke_30_at_corrupted, lines 43–44.
    private const string Nuke = "80b967011b002d00000000000000000000000000000000000000000028168328000000002a6b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, 357, 359).Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, 8).Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Alignment)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2), ("QAFar", 6112, X, 7000, 2),
        ("QAOther", 6113, X, FarZ, 2), ("QAFriend", 6111, 17100, CentreZ, 1)
    };

    public override string Name => "ability-parity-howitzer-stock-area";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, HomeZ, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=12000,healthM=12000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={HomeZ},rotation=0,alignment=1,innerStrC=700,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Defender.Select((id, i) =>
            $"(6112,{i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }.Concat(Stands.Select(s =>
        $"UPDATE characters SET x={s.X},y={Y},z={s.Z},alignment={s.Alignment} WHERE charId={s.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { Style, 357, 359 }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "357,359", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Page 2 slots 1–3: Hacker Style, Logic Bomb 2.0, Code Nuke 3.0. CSR faces QAEnemy 2 m ahead at verified HOME. Select Hacker Style, try empty, /target QAFriend and /target QAOther (16.01 m). Expect invalid/range refusal and no payment or area hit. Guarded fixture pins the hit contest only.",
            """For Logic Bomb move QAOther to z7001 via {"actor":"QAOther","action":"move","x":17000,"y":495,"z":7001}. Keep QAFar at z7000 and /target QAEnemy. Slot 2: 17 IS, 4 s, 2 s own/shared 682, only centre QAEnemy and 4.00 m QAFar hit. QAOther at 4.01 m and QAFriend excluded. Capture first full request. With +47 Viral influence, expect 448–576 raw from captured base 305–392.""",
            """For Code Nuke move QAFar to z7200 and QAOther to z7201, with the same helper move action. Keep QAEnemy selected. Slot 3: 22 IS, 4 s, 4 s own/shared 683, only QAEnemy and 6.00 m QAFar hit. QAOther at 6.01 m and QAFriend excluded. Capture full request. With +47 Viral influence, expect 948–1739 raw from captured base 645–1183.""",
            """After reuse, apply {"actor":"QAFar","action":"aura-resist"} and repeat the two spells with their respective edge stands. Expect source 8 stat157 +75; only QAFar shows type2 total75, rawN and reduced finalM with the damage record matching M. Current damage uses the retained band, then profile influence and typed mitigation; the original retail conversion from authored 23/25 remains open.""",
            "For sight send the printed WALL TARGET request, use READY WALL, then /target QAOther across the solid office wall. Expect reason 9 when sent and no cast, charge or splash. Restore READY HOME afterwards."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Howitzer NAV is required.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(s => (s.X, s.Z)).Concat(new[] { (X, HomeZ), (X, 7001), (X, 7200), (X, 7201) }))
        {
            InvisibilityBeltWatcherMode.Warp(x, Y, z, 0, inside: false);
            Check.That(NavMesh.HasSightPath(1, X, Y, HomeZ, x, Y, z) && NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z),
                $"Open Howitzer NAV stand and sight ({x},{Y},{z})");
        }
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, HomeZ, 0, inside: false)}");
        Console.WriteLine($"READY WALL {InvisibilityBeltWatcherMode.Warp(WallX, WallY, WallZ, 64, inside: true)}");
        string wallTarget = InvisibilityBeltWatcherMode.Warp(AcrossWallX, WallY, WallZ, 192, inside: true);
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Actual mapped wall blocks Howitzer selection");
        Console.WriteLine($$"""WALL TARGET {"actor":"QAOther","action":"chat","text":"{{wallTarget}}"}""");

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var primary = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAFriend");
        var far = await actor.WaitForViewNamedAsync("QAOther");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Style, 172, 27, 0, false), "Stock Hacker Style");
        foreach (var (id, frame) in new (ushort, string)[] { (357, Bomb), (359, Nuke) })
        {
            await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, id, frame, primary, ally);
            int at = actor.World.RpcSnapshot().Count;
            Send(frame, far);
            await StockAbilityProfile.ObserveAsync(actor, at, packet => AbilityRefusals.TryDecode(packet.Bytes, out var denied) &&
                denied.AbilityID == id && denied.ErrorID == AbilityRefusals.TargetOutOfRange, "Howitzer range refusal");
        }

        var edge = context.Helpers["QAFar"];
        var other = context.Helpers["QAOther"];
        await InvisibilityBeltWatcherMode.WarpAsync(other, X, Y, 7001, 128, inside: false);
        await CastAsync(357, Bomb, 2, 682, resisted: false);

        await InvisibilityBeltWatcherMode.WarpAsync(edge, X, Y, 7200, 128, inside: false);
        await InvisibilityBeltWatcherMode.WarpAsync(other, X, Y, 7201, 128, inside: false);
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(edge);
        await CastAsync(359, Nuke, 4, 683, resisted: true);
        return Array.Empty<byte>();

        void Send(string frame, ViewCreation view) =>
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(frame, DuelPackets.Handle(view.ViewId, view.SpawnId)));

        async Task CastAsync(ushort id, string frame, float reuse, ushort group, bool resisted)
        {
            int at = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            var cursors = context.Helpers.Values.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
            Send(frame, primary);
            await EvidenceRetestWire.CastBarAsync(actor, id, 4);
            await EvidenceRetestWire.TimerAsync(actor, at, id, AbilityCooldowns.OwnTimer, reuse);
            await EvidenceRetestWire.TimerAsync(actor, at, group, AbilityCooldowns.GroupTimer, reuse);
            bool Hit(HeadlessPlayer p, RpcRecord record) => record.Bytes.AsSpan().StartsWith(AbilityMessages.Damage(false, id, actor.Handle, p.Handle, 0, 0)[..^8]);
            foreach (string name in new[] { "QAEnemy", "QAFar" })
            {
                var p = context.Helpers[name];
                await StockAbilityProfile.ObserveAsync(p, cursors[p], record => Hit(p, record), $"{id} reaches {name}");
            }
            foreach (string name in new[] { "QAFriend", "QAOther" })
            {
                var p = context.Helpers[name];
                p.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(p, cursors[p], record => record.Opcode == 0x149, "Excluded witness still in world");
                Check.That(!p.World.RpcSnapshot().Skip(cursors[p]).Any(record => Hit(p, record)), $"{name} stays outside {id}");
            }
            if (resisted)
                Check.That(server.DebugLog()[log..].Contains($"[TYPED-RESISTANCE OURS] {id} type2 total75 raw"), "Typed mitigation applies on the included edge");
        }
    }
}
