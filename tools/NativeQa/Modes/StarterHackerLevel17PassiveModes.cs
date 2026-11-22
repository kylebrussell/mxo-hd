using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

internal static class StarterHackerPassiveFixture
{
    internal const int Level = 19, X = 17000, Y = 495, Z = 6400;
    internal const ushort Style = CombatStyles.HackerStyle;
    internal const string StyleFrame = StarterHackerViralSightMode.StockStyle;

    internal static ushort[] Loadout(params ushort[] roots) => roots.Concat(new ushort[] { 2, 17, Style })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
        .Distinct().Order().ToArray();

    internal static string PutActor(int targetX) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
        $"rotation={MobAggro.YawTowards(X,Z,targetX,Z)},innerStrC=700,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110";

    internal static string PutHelper(int character, int x, int organization, int health, int maxHealth) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={x},y={Y},z={Z}," +
        $"healthC={health},healthM={maxHealth},alignment={organization},pvpflag=0 WHERE charId={character}";

    internal static string CapRanks => $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110";

    internal static void CheckNav(NativeQaBootstrapContext context, int targetX)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Repository NAV is required for this fixture.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, targetX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verified outdoor stand ({x},{Y},{Z}) on floor {Y - 95}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, targetX, Y, Z), "The actor and helper have unobstructed sight");
    }

    internal static Dictionary<ushort, AbilityMemoryUiFixture.Tuple> VerifyProfile(HeadlessPlayer actor,
        ushort[] loaded, params ushort[] archived)
    {
        var active = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[7])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        foreach (var (id, row) in active) owned.TryAdd(id, row);
        Check.That(active.Keys.ToHashSet().SetEquals(loaded) && loaded.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            "Margin has the exact, memory-legal loaded profile");
        foreach (ushort id in archived)
            Check.That(owned.ContainsKey(id) && !active.ContainsKey(id), $"{id} begins archived");
        foreach (ushort id in loaded.Concat(archived))
            foreach (var (required, rank) in AbilityPrerequisites.Current.Closure(id))
                Check.That(active.TryGetValue(required, out var parent) && parent.Level >= rank,
                    $"{id} has required parent {required} at rank {rank} or higher");
        return owned;
    }

    internal static async Task<uint> StartAsync(HeadlessPlayer actor, HeadlessPlayer target, ServerInstance server, bool pvp = false)
    {
        if (pvp) await PvpOpponentsFixture.EnterAsync(actor);
        else await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(target.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        actor.SendRpc(StyleFrame);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        return handle;
    }

    internal static async Task RecordsAsync(HeadlessPlayer actor, int cursor, ushort source, (ushort Stat, int Value)[] stats, bool remove = false)
    {
        foreach (var (stat, value) in stats)
            await StockAbilityProfile.ObserveAsync(actor, cursor, packet => StockAbilityProfile.Bonus(packet, source, stat, value, 0, remove),
                $"Passive {source}/{stat} +{value}, removal={remove}");
    }
}

public sealed class StarterHackerDefensivePassivesMode : NativeQaMode
{
    private const ushort ClearHead = 233, Dedication = 370, Resistance = 376, Patcher = 55;
    private const int FriendX = StarterHackerPassiveFixture.X + 200;
    private static readonly ushort[] Loaded = StarterHackerPassiveFixture.Loadout(ClearHead, Dedication);
    private static readonly ushort[] Owned = Loaded.Append(Resistance).ToArray();
    // Stock proxy_024_chatting_hyperjump_fight:11689–11690, as replayed in StarterPatcherTradeoffMode.
    private const string PatcherFrame = "80b937000d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly Dictionary<ushort, (ushort Stat, int Value)[]> Expected = new()
    {
        [ClearHead] = new (ushort, int)[] { (1047, 3), (1141, 1) },
        [Dedication] = new (ushort, int)[] { (1141, 1), (1143, 1), (1027, 1), (1052, 1) },
        [Resistance] = new (ushort, int)[] { (1148, 4), (1146, 4), (1142, 1), (1139, 1) }
    };

    public override string Name => "ability-parity-starter-hacker-defensive-passives";
    public override NativeQaModePlan Plan { get; } = new(Owned, StarterHackerPassiveFixture.X, StarterHackerPassiveFixture.Z,
        Array.Empty<string>(),
        new[]
        {
            StarterHackerPassiveFixture.CapRanks,
            $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Resistance)}"
        },
        new[]
        {
            StarterHackerPassiveFixture.PutActor(FriendX),
            "UPDATE characters SET healthC=100,healthM=500 WHERE charId=110",
            StarterHackerPassiveFixture.PutHelper(6111, FriendX, 1, 100, 500),
            "DELETE FROM char_abilities WHERE char_id=6111"
        },
        DevCommands: true, hotbarAbilities: new[] { StarterHackerPassiveFixture.Style, Patcher },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "233,370,376", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loaded.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 19. Outdoor NAV floor 400: CSR (17000,495,6400) faces QAFriend (17200,495,6400). CSR and QAFriend each start at 100 current HP. Regen is held. 48/49 memory used. Clear Head 233 and Dedication 370 start loaded; Advanced Damage Resistance 376 starts owned and archived. Use Enter for chat. Select Hacker Style in page 2 slot 1 and /target QAFriend.",
            "At entry check [MODIFIER] char=110 source=233 stat 1047 +3 and 1141 +1; source=370 stats 1141/1143/1027/1052 each +1. Every record is while-loaded (0 s). Slot 2 Patcher. Expect [ABILITY] Heal 55 -> CSR_Tester for 24 (100 -> 124), 22 IS and 43 s own reuse. QAFriend stays at 100 HP.",
            "?qaunload 233. Check both removals. After the 43 s reuse, use Patcher for 23 (124 -> 147). ?qaload 233 restores both records. ?qaunload 370 removes all four. After the next 43 s reuse, use Patcher for 23 (147 -> 170). ?qaload 370 restores its four records.",
            "Swap against the memory cap: ?qaunload 370, ?qaload 376. Check four adds for 376 (1148/1146 +4 each and 1142/1139 +1 each). Unload/reload 376 once to verify all matching removals and adds. No interlock, no max IS changes or additional damage. 376 checks stock modifier records only. Sweep and unresolved retail coefficients still need independent evidence."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, FriendX);
        var server = context.ActualServer!;
        var ally = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(ally.Handle, ally);
        await ally.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, ally);
        Check.Equal((ushort)100, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(ally), "The ally starts wounded");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loaded, Resistance);
        var db = context.OwnedDatabase!;
        Check.Equal((long)Loaded.Length, await db.LongAsync("SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND is_loaded=1"),
            "The disposable profile starts at its disclosed memory usage");
        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, ally, server);
        await RecordsAsync(0, ClearHead);
        await RecordsAsync(0, Dedication);
        Check.That(!actor.World.RpcSnapshot().Any(packet => Expected[Resistance].Any(s =>
            StockAbilityProfile.Bonus(packet, Resistance, s.Stat, s.Value, 0, false))), "The archived 376 starts without live modifiers");

        byte[] patch = StarterHackerControlTargetMode.WithHandle(PatcherFrame, handle);
        var first = await HealAsync(124);
        await ChangeAsync(ClearHead, load: false);
        await WaitReuseAsync(first);
        var second = await HealAsync(147);
        await ChangeAsync(ClearHead, load: true);
        await ChangeAsync(Dedication, load: false);
        await WaitReuseAsync(second);
        await HealAsync(170);
        await ChangeAsync(Dedication, load: true);
        await ChangeAsync(Dedication, load: false);
        await ChangeAsync(Resistance, load: true);
        await ChangeAsync(Resistance, load: false);
        await ChangeAsync(Resistance, load: true);
        Check.Equal((ushort)100, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(ally), "The selected ally receives no Patcher heal");
        Check.Equal(0, actor.World.Problems.Count + ally.World.Problems.Count, "Defensive passive framing");
        Console.WriteLine("HACKER DEFENSIVE PASSIVES PASS: records round-trip for 233/370/376; Patcher self-healed 24/23/23.");
        return Array.Empty<byte>();

        Task RecordsAsync(int cursor, ushort id, bool remove = false) =>
            StarterHackerPassiveFixture.RecordsAsync(actor, cursor, id, Expected[id], remove);

        async Task ChangeAsync(ushort id, bool load)
        {
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { id }, load);
            await RecordsAsync(cursor, id, remove: !load);
        }

        async Task<int> HealAsync(ushort health)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(patch);
            await EvidenceRetestWire.ViewSinceAsync(actor, views, 2, "02 80 80 80 40 " + EvidenceRetestWire.Le(health));
            await EvidenceRetestWire.TimerAsync(actor, cursor, Patcher, AbilityCooldowns.OwnTimer, 43);
            return cursor;
        }

        Task WaitReuseAsync(int cursor)
        {
            byte[] ready = AbilityCooldowns.BuildTimer(Patcher, AbilityCooldowns.OwnTimer, 43, false);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(packet => packet.Bytes.AsSpan().SequenceEqual(ready)),
                "Patcher's natural reuse cleared", TimeSpan.FromSeconds(45));
        }
    }
}

public sealed class StarterHackerHostileProgrammingMode : NativeQaMode
{
    private const ushort Hostile = 349, Contagion = 73, Blast = 58;
    private const int EnemyX = StarterHackerPassiveFixture.X + 400;
    private static readonly ushort[] Loadout = StarterHackerPassiveFixture.Loadout(Hostile, Blast);
    private static readonly (ushort Stat, int Value)[] Stats = { (1056, 5), (1147, 1) };

    public override string Name => "ability-parity-starter-hacker-hostile-programming";
    public override NativeQaModePlan Plan { get; } = new(Loadout, StarterHackerPassiveFixture.X, StarterHackerPassiveFixture.Z,
        Array.Empty<string>(), new[]
        {
            StarterHackerPassiveFixture.CapRanks,
            $"UPDATE char_abilities SET level=17 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Contagion)}"
        },
        new[]
        {
            StarterHackerPassiveFixture.PutActor(EnemyX),
            StarterHackerPassiveFixture.PutHelper(6114, EnemyX, 2, 3000, 3000),
            "DELETE FROM char_abilities WHERE char_id=6114"
        },
        DevCommands: true, hotbarAbilities: new[] { StarterHackerPassiveFixture.Style, Blast },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "349", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 19, 38/49 memory. Outdoor NAV floor 400: CSR (17000,495,6400) faces QAEnemy (17400,495,6400). Resist Contagion 73 is trained at the required rank 17 so the rounded viral input can distinguish 349. Select Hacker Style page 2 slot 1, /target QAEnemy, and keep selection and Speed tactic.",
            "With Hostile Programming 349 loaded, expect [MODIFIER] char=110 source=349 stat 1056 +5 and 1147 +1, both 0 s. Logic Blast slot 2 sends [FREECAST-VIRAL] ability=58 inputs=35/0. Wait for 4 s reuse. ?qaunload 349 removes both records and [VITALS] maxIS 1080 -> 1030. Cast again; inputs=34/0.",
            "?qaload 349 restores both records and [VITALS] maxIS 1030 -> 1080. Cast again after reuse; inputs=35/0. No interlock [PVP] rounds. The linked viral accuracy coefficient remains outside this proof."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, EnemyX);
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        Check.Equal((ushort)17, archive[Contagion].Level, "The control source uses the documented rank");
        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, enemy, server, pvp: true);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal((ushort)1080, BitConverter.ToUInt16(self.Attributes["InnerStrengthMax"]), "349 +5 stacks with Execute Program +3");
        await StarterHackerPassiveFixture.RecordsAsync(actor, 0, Hostile, Stats);
        await SampleAsync(35);
        await ChangeAsync(load: false, before: 1080, after: 1030);
        await SampleAsync(34);
        await ChangeAsync(load: true, before: 1030, after: 1080);
        await SampleAsync(35);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Hostile Programming framing");
        Console.WriteLine("HOSTILE PROGRAMMING PASS: max IS 1080/1030/1080; rounded live viral input 35/34/35.");
        return Array.Empty<byte>();

        async Task ChangeAsync(bool load, int before, int after)
        {
            int log = server.DebugLog().Length;
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Hostile }, load);
            await StarterHackerPassiveFixture.RecordsAsync(actor, cursor, Hostile, Stats, remove: !load);
            await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains($"[VITALS] char=110 maxIS {before} -> {after}", StringComparison.Ordinal),
                "Hostile Programming also changes actual max IS", HeadlessPlayer.DefaultTimeout);
        }

        async Task SampleAsync(int attack)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            string captured = StarterHackerViralSightMode.Samples.Single(sample => sample.Ability == Blast).Capture;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(captured, handle));
            var contest = new Regex($@"\[FREECAST-VIRAL\] ability=58 source=CSR_Tester target=QAEnemy inputs={attack}/0\b");
            await actor.WaitUntilAsync(() => contest.IsMatch(server.DebugLog()[log..]),
                $"Logic Blast uses viral input {attack}/0", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, cursor, Blast, AbilityCooldowns.OwnTimer, 4, ended: true);
        }
    }
}
