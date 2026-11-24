using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HowitzerParentRadiusMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Home = 6400, Centre = 6600, Level = 50;
    private const ushort Style = CombatStyles.HackerStyle, Parent = 356, Upgrade = 1087, Bomb = 357;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Parent, Bomb).Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Alignment)[] Stands =
    {
        ("QAEnemy", 6114, X, Centre, 2), ("QAFar", 6112, X, 7010, 2),
        ("QAOther", 6113, X, 7080, 2), ("QAFriend", 6111, X + 100, Centre, 1)
    };
    // Retained stock actions_combat_marketplace_abilitychangecoder lines 20423–24.
    private const string BombStock = "80b965010400a600000000000000000000000000000000000000000065010000006b0000086b0000000000";
    // Retained Vector28 lines 4687–88; only the selected view/spawn is rebound.
    private const string UpgradeStock = "80b93f0419006d0000000000000000000000000000000000000000000000002084d75741086b0000000000";

    public override string Name => "ability-parity-howitzer-parent-radius";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Home, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=12000,healthM=12000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Home},rotation=0,alignment=1,innerStrC=700,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6114"
    }.Concat(Stands.Select(p => $"UPDATE characters SET x={p.X},y={Y},z={p.Z},alignment={p.Alignment} WHERE charId={p.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Bomb, Upgrade }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "356", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/100 memory. Stay on verified HOME and /target QAEnemy 2 m ahead. Page 2 Hacker Style, Logic Bomb 2.0, Howitzer Upgrade. The disposable fixture pins the Viral hit contest only; damage rolls remain normal.",
            "At login check the single Actions grant 1087 and source=356 stat=1055 +5. Use slot 1 then cast slot 2. Expect 17 IS / 4 s cast / 2 s own and shared reuse, and only QAEnemy hit. QAFar is 4.10 m from the target and QAOther is 4.80 m. The stock client link 177 → 1055 starts with a zero owner coefficient.",
            "Repeat slot 2 after ?qaunload 356 and after ?qaload 356. Wait out the two-second reuse each time. Expect the same recipients, with 1055 +5 and 1087 both removed then restored. Keep the primary selected.",
            "After reload press slot 3. Expect [MODIFIER] source=1087 stat=177 +17. Cast slot 2: QAEnemy and the 4.10 m QAFar are hit. QAOther and the ally are excluded. ?dismissbuff 1087 and wait for reuse. Repeat slot 2 and confirm exclusion of QAFar again."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Repository NAV is required.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Home, 0, inside: false)}");
        var server = context.ActualServer ?? throw new InvalidOperationException("Disposable server required.");
        foreach (var helper in Stands)
        {
            Console.WriteLine($"READY {helper.Name} {InvisibilityBeltWatcherMode.Warp(helper.X, Y, helper.Z, 128, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Centre, helper.X, Y, helper.Z), $"Outdoor sight to {helper.Name}");
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync("QAEnemy");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, record => StockAbilityProfile.Bonus(record, Style, 172, 27, 0, false), "Hacker Style");
        var proof = new List<object>();
        var passive = new (ushort, ushort, (ushort, int)[])[] { (Parent, Upgrade, new (ushort, int)[] { (1055, 5) }) };
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, passive, 0, false);
        await CastAsync(expanded: false);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, passive, removed, true);
        await CastAsync(expanded: false);
        int restored = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, passive, restored, false);
        await CastAsync(expanded: false);

        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(UpgradeStock, handle));
        await StockAbilityProfile.ObserveAsync(actor, cursor, rpc => StockAbilityProfile.Bonus(rpc, Upgrade, 177, 17, 0, false), "Direct Howitzer range");
        await CastAsync(expanded: true);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1087");
        await StockAbilityProfile.ObserveAsync(actor, cursor, rpc => StockAbilityProfile.Bonus(rpc, Upgrade, 177, 17, 0, true), "Direct range cleanup");
        await CastAsync(expanded: false);
        return Array.Empty<byte>();

        async Task CastAsync(bool expanded)
        {
            int start = actor.World.RpcSnapshot().Count;
            var cursors = context.Helpers.Values.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BombStock, handle));
            await EvidenceRetestWire.CastBarAsync(actor, Bomb, 4);
            await EvidenceRetestWire.TimerAsync(actor, start, 682, AbilityCooldowns.GroupTimer, 2, ended: true);
            foreach (var (player, at) in cursors)
            {
                bool Hit(RpcRecord rpc) => rpc.Bytes.AsSpan().StartsWith(AbilityMessages.Damage(false, Bomb, actor.Handle, player.Handle, 0, 0)[..^8]);
                bool expected = player.Handle == "QAEnemy" || expanded && player.Handle == "QAFar";
                if (expected)
                    await StockAbilityProfile.ObserveAsync(player, at, Hit, $"Logic Bomb reaches {player.Handle}");
                else
                {
                    player.SendRpc("81 48");
                    await StockAbilityProfile.ObserveAsync(player, at, rpc => rpc.Opcode == 0x149, "Witness still in world");
                    Check.That(!player.World.RpcSnapshot().Skip(at).Any(Hit), $"Logic Bomb excludes {player.Handle}");
                }
            }
        }
    }
}
