using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerRangeBoosterMode : NativeQaMode
{
    private const ushort Booster = 345, Nuke = 60, Style = CombatStyles.HackerStyle;
    private const int Level = 28, X = 17000, Y = 495, Z = 2300, PrimaryX = 17500;
    // Vector28 lines 8542–8543. Rebind only the selected view and spawn.
    private const string StockBooster = "80b9590110007f000000000000000000000000000000000000000000000015000000d526086b0000000000";
    private static readonly ushort[] Hotbar = { Style, Nuke, Booster };
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z)[] Stands =
    {
        ("QAEnemy", 6114, PrimaryX, Z), ("QAOther", 6113, PrimaryX, Z + 400),
        ("QAFar", 6112, PrimaryX + 440, Z), ("QAFriend", 6111, PrimaryX, Z + 499)
    };

    public override string Name => "ability-parity-hacker-range-booster";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
                $"rotation={MobAggro.YawTowards(X, Z, PrimaryX, Z)},alignment=2,innerStrC=500,innerStrM=500 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment=1 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "345", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({PrimaryX},{Y},{Z}). Verified outdoor NAV: QAOther 4.00m from QAEnemy ({PrimaryX},{Y},{Z + 400}), QAFar 4.40m ({PrimaryX + 440},{Y},{Z}), QAFriend 4.99m ({PrimaryX},{Y},{Z + 499}). All four are eligible opposing players.",
            "Page 2: Hacker Style, Code Nuke 1.0, Range Booster. Choose Hacker Style and /target QAEnemy. Cast Code Nuke: [PVP] CSR_Tester's LogicBomb1Ability lands on QAEnemy and QAOther for 8 each. QAFar and QAFriend are out of splash radius. Stay still and keep QAEnemy selected.",
            "Press Range Booster. Expect an immediate cast, 37 IS paid with Dedicated Code loaded, own reuse 180 s and [MODIFIER] char=110 source=345 stat=177 add +21 for 30 s. Compare the stock Virus Area Radius bonus. After Code Nuke reuse, cast it again: 4.84 m splash now includes QAFar, with QAFriend still outside.",
            "Wait until the +21 record expires and the matching remove appears. Cast Code Nuke once more. QAEnemy and QAOther are hit again, and QAFar/QAFriend are outside. No max [VITALS] or interlock rounds. The 60 casts each pay 11 IS, cast for 4 s and use the ordinary 2 s own and group cooldown."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Range Booster needs the repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var server = context.ActualServer!;
        foreach (var stand in Stands.Append((Name: "CSR", Id: 110, X, Z)))
            Check.That(NavMesh.Locate(1, stand.X, Y, stand.Z, out var floor) && !floor.indoors &&
                Math.Abs(floor.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, stand.X, Y, stand.Z, PrimaryX, Y, Z),
                $"Verified outdoor sight and NAV for {stand.Name} ({stand.X},{Y},{stand.Z})");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var profile = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => profile.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                profile.TryGetValue(need.Key, out var rank) && rank >= need.Value)), "Complete Range Booster prerequisites fit level 28");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var target = await caster.WaitForViewNamedAsync("QAEnemy");
        foreach (var helper in context.Helpers.Values)
            await caster.WaitForViewNamedAsync(helper.Handle);
        uint targetHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        var nuke = StarterHackerViralSightMode.Samples.Single(sample => sample.Ability == Nuke);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await CheckSplashAsync(expanded: false);

        int cursor = caster.World.RpcSnapshot().Count;
        int logCursor = server.DebugLog().Length;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StockBooster, targetHandle));
        await StockAbilityProfile.ObserveAsync(caster, cursor, frame => StockAbilityProfile.Bonus(frame, Booster, 177, 21, 30, false),
            "Range Booster sends the archived +21 for 30 seconds");
        await EvidenceRetestWire.TimerAsync(caster, cursor, Booster, AbilityCooldowns.OwnTimer, 180);
        await caster.WaitUntilAsync(() => server.DebugLog()[logCursor..].Contains("[ABILITY-COST] char=110 ability=345 paid=37 "),
            "Range Booster costs 37 IS with Dedicated Code", HeadlessPlayer.DefaultTimeout);
        await CheckSplashAsync(expanded: true);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(cursor).Any(frame => StockAbilityProfile.Bonus(frame, Booster, 177, 21, 30, true)),
            "Range Booster removes the original +21 record at 30 seconds", TimeSpan.FromSeconds(32));
        await CheckSplashAsync(expanded: false);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count), "Range Booster stock protocol");
        return Array.Empty<byte>();

        async Task CheckSplashAsync(bool expanded)
        {
            int from = server.DebugLog().Length;
            int repeat = caster.World.RpcSnapshot().Count;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(nuke.Capture, targetHandle));
            await caster.WaitUntilAsync(() => IsHit(server.DebugLog()[from..], "QAEnemy") && IsHit(server.DebugLog()[from..], "QAOther"),
                "Code Nuke hits the primary and the 4.00 m recipient", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(caster, repeat, Nuke, AbilityCooldowns.OwnTimer, nuke.Reuse, ended: true);
            string log = server.DebugLog()[from..];
            Check.Equal(expanded, IsHit(log, "QAFar"), "The 4.40 m recipient is included only while boosted");
            Check.That(!IsHit(log, "QAFriend"), "The 4.99 m recipient is outside the boosted radius");
        }

        static bool IsHit(string log, string handle) =>
            log.Contains($"[PVP] CSR_Tester's LogicBomb1Ability lands on {handle} for 8", StringComparison.Ordinal);
    }
}
