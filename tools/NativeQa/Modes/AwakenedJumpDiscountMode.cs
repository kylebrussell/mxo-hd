using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedJumpDiscountMode : NativeQaMode
{
    private const int Level = 30, X = 17100, Y = 495, Z = 6400, LandingX = 20950;
    private const ushort Jump = 11, Sprint = 184, Efficiency = 321, Hacker = 1006;
    private const string Stand = "?tp 17100 495 6400 4e2 12 1 - 40";
    // Full PR689 stock CSEQ 56 forward jump from this exact stand, and the stock Sprint request retained in the movement fixture.
    private const string Forward = "80 c2 00 00 00 00 80 75 d4 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 00 b9 40 e2 04 02 00 00 00 00 f0 20 46 01 69 9f ed 16";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Hacker, Efficiency, Jump, Sprint }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-awakened-jump-discount";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=CASE ability_id WHEN {AbilityMessages.AbilityGameObject(Jump)} THEN 10 ELSE {Level} END WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "rotation=64,innerStrC=120,innerStrM=200 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new[] { Hacker, Efficiency, Sprint }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "11,184,321", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR level {Level} provides the Efficiency prerequisite while testing rank-10 Hyper-Jump. {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. NAV ({X},{Y},{Z}) 1250/18/1 faces east to ({LandingX},{Y},{Z}) 1250/2/0. No helpers or bag. Page 2 slots 1 Hacker, 2 Efficiency, 3 Sprint.",
            $"With all buffs off, use Ctrl+Space from normal view. Expect 38.5 m in 4.812 s, IS 120->100. Let it land, strafe and type {Stand}.",
            "Tap slots 1 and 2. Expect Efficiency's 2 s cast, 8 s reuse and [MODIFIER] char=110 source=321 stat=117 +15 duration=300s, IS 100->60. Tap slot 3 twice, about one second apart, to start then stop Sprint before its first 5 s drain. Expect 22 IS charged (60->38), +15 movement records and their removal. Save that discounted positive control.",
            $"With Efficiency still active and Sprint off, Ctrl+Space again. Expected server debit is a fixed 20, leaving 18. After landing and strafe type {Stand}. Try Ctrl+Space once at 18 IS. Record any local shortfall text and whether it sends a flag-1 C2 or only a lone takeoff report. If it sends C2, the current server refuses with reason 17, deficit 2. Plain Space stays free. No maximum [VITALS] or [PVP] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, LandingX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Jump position ({x},{Y},{Z}) is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.GetValueOrDefault(Jump) == 10 && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.GetValueOrDefault(need.Key) >= need.Value)), "Efficiency and rank-10 Jump fit memory with qualifying prerequisites");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await JumpAsync(120, 100);

        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        int cursor = actor.World.RpcSnapshot().Count;
        int view = actor.World.ViewSnapshot().Count;
        actor.SkipReceived();
        EvidenceRetestWire.Cast(actor, Efficiency);
        await EvidenceRetestWire.CastBarAsync(actor, Efficiency, 2);
        await EvidenceRetestWire.TimerAsync(actor, cursor, Efficiency, AbilityCooldowns.OwnTimer, 8);
        await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Efficiency, 117, 15, 300, false),
            "Efficiency applies direct stat 117 at +15");
        await IsAsync(view, 60);

        cursor = actor.World.RpcSnapshot().Count;
        view = actor.World.ViewSnapshot().Count;
        actor.SendRpc(SprintUse);
        await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Sprint, 877, 15, 0, false),
            "Sprint actually starts");
        await IsAsync(view, 38);
        await Task.Delay(TimeSpan.FromSeconds(1));
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SprintUse);
        await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Sprint, 877, 15, 0, true),
            "Sprint stops before a drain");
        await JumpAsync(38, 18);

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Forward);
        await StockAbilityProfile.ObserveAsync(actor, cursor, frame =>
            AbilityRefusals.TryDecode(frame.Bytes, out var refusal) && refusal.AbilityID == Jump &&
            refusal.ErrorID == AbilityRefusals.InsufficientInnerStrength && refusal.InnerstrengthDeficit == 2,
            "Current server uses fixed Hyper-Jump cost even when ordinary ability cost is discounted");
        Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Opcode == 0xc3), "Refused start has no clock echo");
        Check.Equal(0, actor.World.Problems.Count, "Jump and stock control request framing");
        return Array.Empty<byte>();

        async Task JumpAsync(ushort before, ushort after)
        {
            int logAt = server.DebugLog().Length;
            int from = actor.World.RpcSnapshot().Count;
            var request = WireFormat.FromHex(Forward);
            Check.That(JumpRecords.TryStart(request, out var start), "The exact stock forward request decodes");
            actor.SendRpc(request);
            await StockAbilityProfile.ObserveAsync(actor, from, frame =>
                frame.Bytes.AsSpan().SequenceEqual(JumpRecords.EncodeClockEcho(start.TimeStamp)), "The forward start has a clock echo");
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(
                $"[VITALS] char=110 Hyper-Jump 11 IS {before}->{after}", StringComparison.Ordinal),
                "The current server charges the fixed takeoff amount", HeadlessPlayer.DefaultTimeout);
            actor.SkipReceived();
            actor.SendChat(Stand);
            await actor.WaitForTextAsync("Teleported to 4e2/12");
        }

        Task IsAsync(int from, ushort expected) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(from)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(expected),
            $"The ordinary cast leaves {expected} IS", HeadlessPlayer.DefaultTimeout);
    }
}
