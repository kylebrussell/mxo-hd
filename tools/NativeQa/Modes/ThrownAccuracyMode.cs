using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ThrownAccuracyMode : ThrownAccuracyContestMode
{
    public override string Name => "thrown-accuracy-bonus";

    public ThrownAccuracyMode() : base(ThrownAccuracyFixture.Abilities, new[] { (Id: (ushort)164, Bonus: 10) },
        66, new[] { 71 }, new[]
        {
            "Page 2 slot 1 Spy Style, slot 2 Leg Sweep. After Close Combat reselect Spy (slot 1). Expect [MODIFIER] source=1112 stat=1032 add value=+66. With Weak Points archived, press slot 2 and expect [COMBAT-ATTACK OURS] special 134 stat 1032 total 66 in the actual [PVP] round.",
            "Stay in combat. Use ?qaload 164 for source=164 stat=1145 add value=+10. After Leg Sweep's 12 s own reuse use slot 2 for 71. Use ?qaunload 164 and after reuse cast for 66 again."
        }) { }
}

public sealed class MasterThrownAccuracyMode : ThrownAccuracyContestMode
{
    private const ushort WickedAim = 278, MasterKnifeThrower = 338;
    private static readonly ushort[] Loadout = ThrownAccuracyFixture.Abilities
        .Concat(new[] { WickedAim, MasterKnifeThrower })
        .Concat(AbilityPrerequisites.Current.Closure(MasterKnifeThrower).Keys).Distinct().ToArray();

    public override string Name => "ability-parity-wicked-aim-knife-thrower";

    public MasterThrownAccuracyMode() : base(Loadout, new[] { (WickedAim, 15), (MasterKnifeThrower, 5) },
        71, new[] { 78, 81 }, new[]
        {
            "Page 2 slot 1 Spy Style, slot 2 Leg Sweep. After Close Combat reselect Spy (slot 1). Expect [MODIFIER] source=1112 stat=1032 add value=+66. Weak Points 164 is loaded for +10 to 1145 throughout. 278 and 338 start archived. Press slot 2 for the actual [COMBAT-ATTACK OURS] special 134 stat 1032 total 71.",
            "Stay in the fight. Use ?qaload 278 to add source=278 stat=1145 value=+15. Wait Leg Sweep's 12 s reuse and use slot 2 for 78. Then ?qaload 338 adds source=338 stat=1145 value=+5 and one 1121 grant. Leave 1121 inactive. Use slot 2 after reuse for 81.",
            "Use ?qaunload 338. The +5 and 1121 disappear, and the next Leg Sweep input returns to 78. Use ?qaunload 278 to remove +15 and return to 71. Let 12 s reuse end between casts. The observed direct Spy value stays +66. A miss is valid at any stage."
        }) { }
}

public abstract class ThrownAccuracyContestMode : NativeQaMode
{
    private const ushort LegSweep = 134, Spy = 1112;
    private const int X = 17000, Y = 495, Z = 2300, EnemyX = 17120, Level = 50;
    // PR1016 first stock Leg Sweep request as retained in the native PacketLog, line 3648.
    private const string StockLegSweep = "80b9860008000700000000000000000000000000000000000000000086000000006b0000086b0000000000";

    private readonly ushort[] loadout;
    private readonly (ushort Id, int Bonus)[] passives;
    private readonly int baseline;
    private readonly int[] loadedTotals;

    public override NativeQaModePlan Plan { get; }

    protected ThrownAccuracyContestMode(ushort[] loadout, (ushort Id, int Bonus)[] passives, int baseline,
        int[] loadedTotals, string[] stages)
    {
        this.loadout = loadout;
        this.passives = passives;
        this.baseline = baseline;
        this.loadedTotals = loadedTotals;
        Plan = new(loadout, X, Z, Array.Empty<string>(),
            passives.Select(row => $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(row.Id)}").ToArray(),
            new[]
            {
                PvpOpponentsFixture.NativeSql,
                "UPDATE characters SET healthC=20000,healthM=20000 WHERE charId IN (110,6114)",
                "UPDATE characters SET rotation=64 WHERE charId=110",
                "UPDATE characters SET rotation=192 WHERE charId=6114",
                "DELETE FROM inventory WHERE charId IN (110,6114) AND slot BETWEEN 105 AND 107",
                $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
            }, DevCommands: true, hotbarAbilities: new[] { Spy, LegSweep }, testMobFixture: "martial-player-controls.csv",
            environmentOverrides: new()
            {
                ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_PACKET_LOG"] = "1",
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
            },
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = string.Join(",", passives.Select(row => row.Id)),
                ["NativeCloseableRows"] = passives.Length, ["RandomOverrides"] = false,
                ["LoadedMemory"] = loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
            },
            readyMessages: new[]
            {
                "Rank 50, full prerequisites. CSR starts on printed HOME facing QAEnemy. /target QAEnemy then right-click Close Combat. No teleport needed. QAEnemy automatically selects ordinary Block. No helper actions during this plan."
            }.Concat(stages).Append(
                "QA aids are seeded ranks and HP, the normal helper tactic, and the disposable archive bridge. Hit and damage rolls remain natural. Keep the first stock 134 request.").ToArray());
    }

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
        var archivedIds = passives.Select(row => row.Id).ToHashSet();
        Check.That(loaded.Keys.ToHashSet().SetEquals(loadout.Where(id => !archivedIds.Contains(id))),
            $"Margin profile with tested passives archived: {string.Join(",", loaded.Keys.Order())}");
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Where(r =>
            AbilityMemory.IsAbility(r.Descriptor) && archivedIds.Contains(AbilityCatalog.IdOf(r.Descriptor)))
            .ToDictionary(r => AbilityCatalog.IdOf(r.Descriptor));
        Check.That(archivedIds.SetEquals(archive.Keys) && archive.Values.All(row => row.Level == Level),
            "Tested passives are archived at exact rank");
        Check.That(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(parent =>
                (loaded.TryGetValue(parent.Key, out var rank) ? rank : archive.GetValueOrDefault(parent.Key)?.Level ?? 0) >= parent.Value)),
            "All prerequisite ranks fit the fully loaded stage");
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
        await SampleAsync(baseline);
        for (int i = 0; i < passives.Length; i++)
        {
            await LoadAsync(passives[i], true);
            await SampleAsync(loadedTotals[i]);
        }
        for (int i = passives.Length - 1; i >= 0; i--)
        {
            await LoadAsync(passives[i], false);
            await SampleAsync(i == 0 ? baseline : loadedTotals[i - 1]);
        }
        Console.WriteLine($"THROWN ACCURACY BOOTSTRAP PASS: {Name}, baseline {baseline}, loaded {string.Join(",", loadedTotals)}, restored.");
        return block.Bytes;

        byte[] WithHandle(string request) => StarterHackerControlTargetMode.WithHandle(request, handle);

        async Task LoadAsync((ushort Id, int Bonus) passive, bool load)
        {
            int from = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { passive.Id }, load);
            await StockAbilityProfile.ObserveAsync(actor, from, f => StockAbilityProfile.Bonus(f, passive.Id, 1145, passive.Bonus, 0, !load),
                $"Thrown accuracy source {passive.Id} remove={!load}");
            if (passive.Id == 338)
                await StockAbilityProfile.ObserveAsync(actor, from, StockAbilityProfile.GrantRecord(1121, !load),
                    $"Master Knife Thrower grant remove={!load}");
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
