using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterAreaDisruptionCompletionMode : NativeQaMode
{
    private const int Level = 19, StartX = 17000, ShiftX = 17200, Y = 495, Z = 2300;
    private const int EdgeX = 17800, OutsideX = 17801, AllyX = 17100;
    private const ushort Disruption = 38;
    private const string StockCast = "80b92600060004000000000000000000000000000000000000000000585cc60001000000086b0000000000";
    private static readonly ushort[] Loadout = StarterHackerPassiveFixture.Loadout(Disruption);

    public override string Name => "ability-parity-starter-area-disruption-completion";
    public override NativeQaModePlan Plan { get; } = new(Loadout, StartX, Z, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level}, exp={PlayerProgression.ExperienceForLevel(Level)}, x={StartX}, y={Y}, z={Z}, " +
                $"rotation={MobAggro.YawTowards(StartX,Z,EdgeX,Z)}, innerStrC=500, innerStrM=500, alignment=1 WHERE charId=110",
            $"UPDATE characters SET x=CASE charId WHEN 6114 THEN {EdgeX} WHEN 6112 THEN {OutsideX} ELSE {AllyX} END, " +
                "alignment=CASE WHEN charId=6111 THEN 1 ELSE 2 END WHERE charId IN (6111,6112,6114)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Disruption },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "38", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"CSR ({StartX},{Y},{Z}) faces QAEnemy exactly 8 m ahead. QAFar is 8.01 m ahead and QAFriend is an ally 1 m ahead. All stands are on verified outdoor NAV. Page 2 slots 1 and 2 are Hacker Style and Area Disruption.",
            "Select Hacker Style, leave the target clear, and press slot 2 without moving. Expect the 2 s bar, 14 IS and 43 s reuse. On QAEnemy only, [MODIFIER] source=38 stat=102 value=0 for 3 s and source=734 stat=681 value=0 for 8 s, each with matching removal and no HP loss. AREA-QA excludes QAFar by radius and QAFriend by relationship.",
            "After reuse select QAFar and press slot 2 again. As soon as the bar starts, take one short step straight toward the helpers. If the cast lands after you step, AREA-QA should show the changed caster centre and both hostile recipients inside 8 m, while QAFriend stays excluded. If the local client cancels while you move, record the client response and cast from the printed SHIFT stand. The headless replay separately verifies the completion snapshot during a single uninterrupted cast."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { StartX, ShiftX, EdgeX, OutsideX, AllyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, StartX, Y, Z, x, Y, Z) && NavMesh.HasSightPath(1, ShiftX, Y, Z, x, Y, Z),
                $"Verified outside stand with sight ({x},{Y},{Z})");
        Check.That(NavMesh.Locate(1, ShiftX, Y, Z, out var shiftedStand), "Shift stand is mapped");
        string shift = $"?tp {ShiftX} {Y} {Z} {shiftedStand.sector:x} {shiftedStand.region:x} {shiftedStand.polygon:x} - 40";
        Console.WriteLine($"SHIFT {shift}");

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (string name in context.Helpers.Keys)
            await actor.WaitForViewNamedAsync(name);
        actor.SendRpc(StarterHackerPassiveFixture.StyleFrame);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var far = await actor.WaitForViewNamedAsync("QAFar");
        uint selection = DuelPackets.Handle(far.ViewId, far.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(selection))}");

        int actorAt = actor.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        var helperAt = context.Helpers.ToDictionary(item => item.Key, item => item.Value.World.RpcSnapshot().Count);
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCast, selection));
        await EvidenceRetestWire.CastBarAsync(actor, Disruption, 2);
        actor.SendChat(shift);
        await actor.WaitForTextAsync("Teleported to");
        await EvidenceRetestWire.TimerAsync(actor, actorAt, Disruption, AbilityCooldowns.OwnTimer, 43);

        foreach (string name in new[] { "QAEnemy", "QAFar" })
        {
            var helper = context.Helpers[name];
            int start = helperAt[name];
            await StockAbilityProfile.ObserveAsync(helper, start, frame => IsStun(frame, false), $"{name} is inside the completion centre");
            await StockAbilityProfile.ObserveAsync(helper, start, frame => StockAbilityProfile.Bonus(frame,
                TempMods.ControlImmunityTimerAbility, 681, 0, 8, false), $"{name} starts independent immunity");
            await StockAbilityProfile.ObserveAsync(helper, start, frame => IsStun(frame, true), $"{name}'s stun expires naturally");
            await StockAbilityProfile.ObserveAsync(helper, start, frame => StockAbilityProfile.Bonus(frame,
                TempMods.ControlImmunityTimerAbility, 681, 0, 8, true), $"{name}'s independent immunity expires");
        }
        var ally = context.Helpers["QAFriend"];
        Check.That(!ally.World.RpcSnapshot().Skip(helperAt["QAFriend"]).Any(frame => IsStun(frame, false)), "An ally inside the completion radius gets no stun");
        string trace = server.DebugLog()[logAt..];
        var candidates = trace.Split('\n').Where(line => line.Contains("[HOSTILE][AREA-QA]")).ToArray();
        Check.That(candidates.Any(line => line.Contains("candidate=QAFar ") && line.Contains($"centre={ShiftX},{Y},{Z}") &&
            line.Contains("distance=6.0100") && line.TrimEnd().EndsWith("reason=included")), "The admitted selected witness is centred on the caster's shifted position");
        Check.That(candidates.Any(line => line.Contains("candidate=QAFriend ") && line.TrimEnd().EndsWith("reason=pvp-NotOpponent")),
            "The allied witness is excluded by relationship inside the radius");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Area disruption completion framing");
        return Array.Empty<byte>();
    }

    private static bool IsStun(RpcRecord frame, bool removed) =>
        StockAbilityProfile.Bonus(frame, Disruption, 102, 0, 3, removed);
}
