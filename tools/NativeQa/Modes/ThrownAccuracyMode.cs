using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ThrownAccuracyMode : NativeQaMode
{
    private const ushort WeakPoints = 164, LegSweep = 134, Spy = 1112;
    private const int X = 17000, Y = 495, Z = 2300, EnemyX = 17120, Level = 50;
    private static readonly ushort[] Loadout = ThrownAccuracyFixture.Abilities;
    // PR1016 first stock Leg Sweep request as retained in the native PacketLog, line 3648.
    private const string StockLegSweep = "80b9860008000700000000000000000000000000000000000000000086000000006b0000086b0000000000";

    public override string Name => "thrown-accuracy-bonus";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(WeakPoints)}"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        "UPDATE characters SET healthC=20000,healthM=20000 WHERE charId IN (110,6114)",
        "UPDATE characters SET rotation=64 WHERE charId=110",
        "UPDATE characters SET rotation=192 WHERE charId=6114",
        "DELETE FROM inventory WHERE charId IN (110,6114) AND slot BETWEEN 105 AND 107",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
    }, DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false,
        testMobFixture: "martial-player-controls.csv", hotbarAbilities: new[] { Spy, LegSweep },
        environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_PACKET_LOG"] = "1",
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "164", ["NativeCloseableRows"] = 1, ["WeakPointsInitiallyLoaded"] = false,
            ["RandomOverrides"] = false, ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            "Rank 50, full prerequisites. CSR spawns at READY HOME facing QAEnemy on open Mara NAV, away from ambient FX. No ?tp needed. Type /target QAEnemy and use right-click Close Combat. The helper is already in normal Block and stays there. No helper command is needed.",
            "Page 2 slot 1 Spy Style, slot 2 Leg Sweep. After Close Combat reselect Spy (slot 1). Expect [MODIFIER] source=1112 stat=1032 add value=66. With Weak Points archived, press slot 2 and expect [COMBAT-ATTACK OURS] special 134 stat 1032 total 66 in the actual [PVP] round. Keep the first 134 request.",
            "Stay in combat. Enter ?qaload 164 and verify [MODIFIER] char=110 source=164 stat=1145 add value=10. Wait out Leg Sweep's 12 s own reuse and press slot 2 again. Expect actual queued Leg Sweep input 71. Enter ?qaunload 164, confirm the removal and wait the 12 s reuse before the third slot 2. Expect live input 66 again, with Spy Style still active.",
            "QA setup uses level/rank and HP seeds, ordinary helper Block, and the disposable loadout bridge. Natural hit and damage rolls stay live. A miss is valid in every stage. The test measures the linked input and its reversal."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(EnemyX, Y, Z, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Unobstructed outdoor NAV lane");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc("42 03 00 00 00 00 00");
        var block = await enemy.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 f5 03 00 00 f4 03");
        if (context.StockProbe is not { } actor) return block.Bytes;

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(r => AbilityMemory.IsAbility(r.Descriptor)).ToDictionary(r => AbilityCatalog.IdOf(r.Descriptor), r => r.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Loadout.Where(id => id != WeakPoints)),
            $"Margin profile before 164 load: {string.Join(",", loaded.Keys.Order())}");
        Check.That(Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(parent =>
            loaded.TryGetValue(parent.Key, out var rank) && rank >= parent.Value)), "All Weak Points fixture prerequisites are loaded");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Loaded stage fits memory");
        var row = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Single(r =>
            AbilityMemory.IsAbility(r.Descriptor) && AbilityCatalog.IdOf(r.Descriptor) == WeakPoints);
        Check.Equal((ushort)Level, row.Level, "Weak Points has its exact owned rank");
        var archive = new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [WeakPoints] = row };
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.World.SendRpcs(WithHandle(AwakenedInterlockAdmissionMode.StockOpen), WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"46 {hex} 01");
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(WithHandle(AwakenedStockStealthSpeedMode.SpyUse));
        await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Spy, 1032, 66, 0, false),
            "Stock Spy Style sets direct thrown accuracy to 66");
        await SampleAsync(66);
        await LoadAsync(true);
        await SampleAsync(71);
        await LoadAsync(false);
        await SampleAsync(66);
        Console.WriteLine("WEAK POINTS BOOTSTRAP PASS: stock Leg Sweep 66 / 71 / 66 with exact QA archive add/remove, natural Block.");
        return block.Bytes;

        byte[] WithHandle(string request) => StarterHackerControlTargetMode.WithHandle(request, handle);

        async Task LoadAsync(bool load)
        {
            int from = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { WeakPoints }, load);
            await StockAbilityProfile.ObserveAsync(actor, from, f => StockAbilityProfile.Bonus(f, WeakPoints, 1145, 10, 0, !load),
                $"Weak Points linked bonus remove={!load}");
        }

        async Task SampleAsync(int total)
        {
            int from = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SendRpc(WithHandle(StockLegSweep));
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(from).Any(r => ThrownAccuracyFixture.QueuedLegSweep(r.Bytes)),
                "Fresh kind-6 queued Leg Sweep", HeadlessPlayer.DefaultTimeout);
            await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains(
                $"[COMBAT-ATTACK OURS] special 134 stat 1032 total {total}", StringComparison.Ordinal),
                $"Actual queued thrown input {total}", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, from, LegSweep, AbilityCooldowns.OwnTimer, 12);
            var ended = BytePattern.Parse("80 bd 01 86 00 04 00 00 40 41 00");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(from).Any(r => ended.IsMatch(r.Bytes)),
                "Own 12 s Leg Sweep timer ends", TimeSpan.FromSeconds(16));
        }
    }
}
