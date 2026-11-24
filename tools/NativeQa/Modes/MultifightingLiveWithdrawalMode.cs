using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MultifightingLiveWithdrawalMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, Level = 27;
    private const ushort Multi = 140;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Multi).Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z)[] Opponents =
    {
        ("QAEnemy", 6114, 17120, 2300), ("QAFar", 6112, 17120, 2420), ("QAOther", 6113, 17190, 2360)
    };

    public override string Name => "ability-parity-multifighting-live-withdrawal";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        StarterHackerPassiveFixture.CapRanks,
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Multi)}"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64,alignment=1 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=20000,healthM=20000 WHERE charId IN (110,6112,6113,6114)",
        "DELETE FROM inventory WHERE charId IN (110,6112,6113,6114) AND slot BETWEEN 105 AND 107"
    }.Concat(Opponents.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},rotation=192,alignment=2 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "140", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "CSR starts on outdoor HOME facing QAEnemy. No teleport or target selection is needed. Select Speed by double-clicking it in Actions > Awakened. Multi-Fighting starts archived; its parents are loaded.",
            """Send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}; inspect ordinary [PVP] rounds and [MULTIFIGHTING OURS] opponents1 loadedFalse penalty0. Repeat the same helper action with QAFar, then QAOther. Wait for the next CSR attack and defence after each join: opponents2 penalty-10, opponents3 penalty-15.""",
            """Type ?qaload 140. Expect opponents3 loadedTrue penalty-5 on both inputs. Send {"actor":"QAOther","action":"stop"} and wait for that helper to withdraw naturally. CSR and the remaining helpers keep fighting. Expect opponents2 loadedTrue penalty0. Type ?qaunload 140 for opponents2 loadedFalse penalty-10.""",
            """Send {"actor":"QAFar","action":"stop"} and wait for the natural withdrawal. The following CSR attack and defence must show opponents1 loadedFalse penalty0, even if departing helpers are still visible. End the remaining fight with ?cd stop.""",
            "Setup aids are seeded helpers and HP, dev loadout switches and a final fixture stop. Ordinary 40/44 and normal RNG are used. This covers the shipped subtraction in live player rounds. Original console defaults, historical opponentsFought lifetime and arbitrary multi-NPC membership remain unknown."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        var server = context.ActualServer!;
        foreach (var stand in Opponents)
        {
            Console.WriteLine($"READY {stand.Handle} {InvisibilityBeltWatcherMode.Warp(stand.X, Y, stand.Z, 192, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, stand.X, Y, stand.Z), $"Open sight to {stand.Handle}");
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout.Where(id => id != Multi).ToArray(), Multi);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Multi-Fighting profile fits memory");

        for (int index = 0; index < Opponents.Length; index++)
        {
            var helper = context.Helpers[Opponents[index].Handle];
            var view = await helper.WaitForViewNamedAsync(actor.Handle);
            await actor.WaitForViewNamedAsync(helper.Handle);
            var handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
            int cursor = server.DebugLog().Length;
            helper.SendRpc($"40 {handle} 01 6b");
            await helper.WaitForRpcAsync($"46 {handle} 01");
            await ExpectAsync(cursor, index + 1, false, index == 0 ? 0 : -5 * (index + 1));
        }

        int from = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Multi }, load: true);
        await ExpectAsync(from, 3, true, -5);
        from = server.DebugLog().Length;
        context.Helpers["QAOther"].SendRpc("44 00");
        await ExpectAsync(from, 2, true, 0);
        from = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Multi }, load: false);
        await ExpectAsync(from, 2, false, -10);
        from = server.DebugLog().Length;
        context.Helpers["QAFar"].SendRpc("44 00");
        await ExpectAsync(from, 1, false, 0);
        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended.");
        Console.WriteLine("MULTIFIGHTING BOOTSTRAP PASS: live 1/2/3/2/1 opponent counts and loaded-state attack and defence inputs.");
        return Array.Empty<byte>();

        Task ExpectAsync(int cursor, int count, bool loaded, int penalty) => actor.WaitUntilAsync(() =>
        {
            string log = server.DebugLog()[cursor..];
            string prefix = $"[MULTIFIGHTING OURS] {actor.Handle} opponents{count} loaded{loaded} penalty{penalty}";
            return log.Contains(prefix + " attack", StringComparison.Ordinal) &&
                log.Contains(prefix + " defence", StringComparison.Ordinal);
        }, $"Current CSR attack and defence inputs with {count} opponents and {Multi} loaded={loaded}", TimeSpan.FromSeconds(65));
    }
}
