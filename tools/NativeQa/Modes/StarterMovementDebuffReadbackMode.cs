using System.Diagnostics;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterMovementDebuffReadbackMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EndZ = 8000;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private const ushort Gunman = 128, Handguns = 129, Hindering = 130, Slow = 86, Accelerator = 62, Hacker = CombatStyles.HackerStyle;
    private static readonly ushort[] RunnerProfile = { 2, 17 };

    private sealed record Caster(string Name, uint Character, int X, int Z, int Organization, ushort Style,
        ushort Ability, string StyleCapture, string CastCapture)
    {
        public ushort[] Loadout => (Style == Handguns ? new ushort[] { 2, 17, Gunman, Style, Ability } : new ushort[] { 2, 17, Style, Ability })
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    }

    // Stock PR554b (Handguns and Hindering), PR439 (Slow) and fa273b663f5e (Accelerator).
    private static readonly Caster[] Casters =
    {
        new("QAEnemy", 6114, 17300, 6400, 2, Handguns, Hindering,
            "80b981000400020000000000000000000000000000000000000000000000000090767241086b0000000000",
            "80b98200040002000000000000000000000000000000000000000000585cba0001000000086b0000000000"),
        new("QAOther", 6113, 17300, 6600, 2, Hacker, Slow, StarterHackerViralSightMode.StockStyle,
            "80b95600040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000"),
        new("QAFriend", 6111, 17200, 6800, 1, Hacker, Accelerator, StarterHackerViralSightMode.StockStyle,
            "80b93e000d000b000000000000000000000000000000000000000000785bb60001000000086b0000000000")
    };

    public override string Name => "ability-parity-starter-movement-debuff-readback";
    public override NativeQaModePlan Plan { get; } = new(RunnerProfile, X, Z, Array.Empty<string>(), new[]
    {
        StarterHackerPassiveFixture.CapRanks,
        "DELETE FROM inventory WHERE charId IN (110,6111,6113,6114) AND slot=107",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(6114,{CombatFixtureWeapons.Handgun},{CombatFixtureWeapons.HandSlot},1,0)"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        PutCharacter(110, X, Z, 1),
        "DELETE FROM char_abilities WHERE char_id IN (6111,6113,6114)"
    }.Concat(Casters.SelectMany(caster => new[]
    {
        PutCharacter(caster.Character, caster.X, caster.Z, caster.Organization),
        "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
        string.Join(",", caster.Loadout.Select((id, i) => $"({caster.Character},{400 + i},{0x80000000L | ((long)id << 10)},{Level},1)"))
    })).ToArray(), DevCommands: true, hotbarAbilities: new ushort[] { 17 },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "130,86,62", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR ({X},{Y},{Z}) faces +Z down the outdoor NAV strip to z={EndZ}. Use {Home} for every sample. Leave page 2 slot 1 and run/walk settings alone. No targeting, sprint, bag clicks or Escape.",
            "Run straight forward for about two seconds. Before every repeat return to HOME and wait for the position to settle. Keep direction and held time the same. Save PacketLog start/stop positions and timestamps for each run so the comparison can use actual elapsed time.",
            """For Hindering Shot, from HOME submit {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":130}. On [MODIFIER] source=130 stats 115/877/878 -50 and 519 -10 (8 s), run immediately and stop before expiry. After the removes and combat icon clears, repeat from HOME.""",
            """For Slow, from HOME submit {"actor":"QAOther","action":"cast","target":"CSR_Tester","ability":86}. Sample after source=86 adds 115/877/878 -50, 519 -15 and 524 -19 (28 s). Wait for removal and combat to clear. Repeat from HOME.""",
            """For the positive control submit {"actor":"QAFriend","action":"cast","target":"CSR_Tester","ability":62} from HOME. Sample after source=62 stat=115 +10 and own MovementScale +0.10. Type ?dismissbuff 62, confirm removal and zero scale, then repeat from HOME.""",
            "There should be no interlock [PVP] rounds or max [VITALS] changes. Record whether the stock negative modifiers slow the run, then compare each recovery run. The headless fixture checks wire records and NAV. It cannot measure client speed."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (EndZ - Z) / 100 + 1).Select(i => (X, Z + 100 * i));
        foreach (var (x, z) in Casters.Select(caster => (caster.X, caster.Z)).Concat(lane))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor NAV at ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear outdoor path to ({x},{Y},{z})");
        }
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && home.sector == 0x4e2 && home.region == 0x12 && home.polygon == 4 &&
            MobAggro.YawTowards(X, Z, X, EndZ) == 0, "Home position and yaw match the printed reset");

        var server = context.ActualServer!;
        foreach (var caster in Casters)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, caster.Name, caster.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            Console.WriteLine($"{caster.Name} ({caster.X},{Y},{caster.Z}): {caster.Ability}, {caster.Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory.");
            StarterHackerPassiveFixture.VerifyProfile(helper, caster.Loadout);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            var helperOwn = helper.World.CreationSnapshot().Last(view => view.ViewId == 2);
            Check.Equal(((double)caster.X, (double)Y, (double)caster.Z), HackerParitySweepMode.Position(helperOwn),
                $"{caster.Name} starts at its verified stand");
            if (caster.Style == Handguns)
                Check.Equal(CombatFixtureWeapons.Handgun, BitConverter.ToUInt32(helperOwn.Attributes["EquippedItemID"]),
                    "The Hindering Shot helper has its handgun at login");
            helper.SendRpc(caster.Style == Handguns
                ? StarterHackerControlTargetMode.WithHandle(caster.StyleCapture, 0)
                : Convert.FromHexString(caster.StyleCapture));
            await helper.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {LittleEndianHex(caster.Style)} 00 00");
        }

        if (context.StockProbe is not { } runner) return Array.Empty<byte>();
        StarterHackerPassiveFixture.VerifyProfile(runner, RunnerProfile);
        await PvpOpponentsFixture.EnterAsync(runner);
        NativeQaReadiness.RequireNoAmbientAggro(server, runner);
        var own = runner.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(((double)X, (double)Y, (double)Z), HackerParitySweepMode.Position(own), "Runner starts at HOME");
        foreach (var caster in Casters)
            await runner.WaitForViewNamedAsync(caster.Name);
        int resetAt = runner.World.ViewSnapshot().Count;
        runner.SendChat(Home);
        string homePosition = WireFormat.ToHex(new[] { (float)X, (float)Y, (float)Z }.SelectMany(BitConverter.GetBytes).ToArray());
        await EvidenceRetestWire.ViewSinceAsync(runner, resetAt, 2, $"01 1c ?? e2 04 12 00 04 00 00 {homePosition}");

        await CheckDebuffAsync(runner, context.Helpers["QAEnemy"], Hindering, 8, (519, -10));
        await CheckDebuffAsync(runner, context.Helpers["QAOther"], Slow, 28, (519, -15), (524, -19));

        var friend = context.Helpers["QAFriend"];
        var friendView = await friend.WaitForViewNamedAsync(runner.Handle);
        int ownerAt = runner.World.ViewSnapshot().Count;
        int observerAt = friend.World.ViewSnapshot().Count;
        int start = runner.World.RpcSnapshot().Count;
        await SendSampleAsync(friend, runner.Handle, Accelerator);
        await ObserveAsync(runner, start, frame => Bonus(frame, Accelerator, 115, 10, 600, false), "Accelerator adds +10 movement");
        await EvidenceRetestWire.ViewSinceAsync(runner, ownerAt, 2, "02 80 88 cd cc cc 3d 80 80 80 02 00 00");
        await EvidenceRetestWire.ViewSinceAsync(friend, observerAt, friendView.ViewId,
            WireFormat.ToHex(PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(.10f)))));
        ownerAt = runner.World.ViewSnapshot().Count;
        observerAt = friend.World.ViewSnapshot().Count;
        runner.SendChat("?dismissbuff 62");
        await ObserveAsync(runner, start, frame => Bonus(frame, Accelerator, 115, 10, 600, true), "Dismiss removes the original +10");
        await EvidenceRetestWire.ViewSinceAsync(runner, ownerAt, 2, "02 80 88 00 00 00 00 80 80 80 02 00 00");
        await EvidenceRetestWire.ViewSinceAsync(friend, observerAt, friendView.ViewId,
            WireFormat.ToHex(PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(0)))));

        Check.Equal(0, runner.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Movement readback framing");
        Console.WriteLine("130/86 stock casts added and expired all records. 62 added and reset owner/observer scale. Stock running remains the native check.");
        return Array.Empty<byte>();
    }

    internal static async Task<object> SendSampleAsync(HeadlessPlayer helper, string target, ushort ability)
    {
        var caster = Casters.SingleOrDefault(caster => caster.Name == helper.Handle && caster.Ability == ability);
        if (caster is null || target != "CSR_Tester")
            throw new InvalidOperationException("Use the printed movement sample and target CSR_Tester.");
        var view = await helper.WaitForViewNamedAsync(target);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        byte[] request = StarterHackerControlTargetMode.WithHandle(caster.CastCapture, handle);
        helper.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        helper.SendRpc(request);
        return new { Ability = ability, Target = target, Request = Convert.ToHexString(request) };
    }

    private static async Task CheckDebuffAsync(HeadlessPlayer runner, HeadlessPlayer helper, ushort ability, int duration,
        params (ushort Stat, int Value)[] extra)
    {
        int start = runner.World.RpcSnapshot().Count;
        int castAt = helper.World.RpcSnapshot().Count;
        await SendSampleAsync(helper, runner.Handle, ability);
        await EvidenceRetestWire.CastBarAsync(helper, ability, 2);
        var expected = new (ushort Stat, int Value)[] { (115, -50), (877, -50), (878, -50) }.Concat(extra).ToArray();
        foreach (var (stat, value) in expected)
            await ObserveAsync(runner, start, frame => Bonus(frame, ability, stat, value, duration, false), $"{ability}/{stat} adds {value}");
        var active = Stopwatch.StartNew();
        await EvidenceRetestWire.TimerAsync(helper, castAt, ability, AbilityCooldowns.OwnTimer, 8);
        await runner.WaitUntilAsync(() =>
        {
            var frames = runner.World.RpcSnapshot().Skip(start).ToArray();
            return expected.All(stat => frames.Any(frame => Bonus(frame, ability, stat.Stat, stat.Value, duration, true)));
        }, $"{ability} expires all original records", TimeSpan.FromSeconds(duration + 6));
        Check.That(Math.Abs(active.Elapsed.TotalSeconds - duration) < 2, $"{ability} real add-to-remove interval matches {duration} s");
        Console.WriteLine($"Stock {ability}: verified {expected.Length} modifiers and natural expiry after {active.Elapsed.TotalSeconds:F2} s.");
    }

    private static string PutCharacter(uint id, int x, int z, int organization) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={x},y={Y},z={z}," +
        $"rotation={(id == 110 ? 0 : MobAggro.YawTowards(x, z, X, Z))},alignment={organization},pvpflag=0 WHERE charId={id}";
}
