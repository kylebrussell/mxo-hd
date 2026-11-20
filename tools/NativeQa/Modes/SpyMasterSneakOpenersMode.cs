using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SpyMasterSneakOpenersMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle, SneakPassive = 151, SneakGrant = 1111;
    private const ushort StaggeringThrow = 348, DivingFrankensteiner = 385, FreedomToJoints = 386,
        InfuriatingAssault = 1130, WardingBlow = 1134;
    private const byte FixtureLevel = 50, FixtureRank = 50;
    // pr475-PacketLog: full style (607), Sneak (810), and Warding Blow (2164).
    private const string StockSpyStyle = "80b95804000000000000000000000000000000000000000000000000585cac0001000000086b0000000000";
    private const string StockSneak = "80b95704000000000000000000000000000000000000000000000000000000e08f767241086b0000000000";
    private const string StockWardingBlow = "80b96e0409000b0000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, SneakPassive, StaggeringThrow, DivingFrankensteiner, FreedomToJoints,
        InfuriatingAssault, WardingBlow
    };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-spy-master-sneak-openers";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2500,innerStrM=2500 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { SpyStyle, SneakGrant, StaggeringThrow, InfuriatingAssault, WardingBlow, DivingFrankensteiner, FreedomToJoints },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "348,385,386,1130,1134",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "348,385,386,1130,1134",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            SpyFixtureReset.Reselect,
            "Between openers, run ?combatdepth fixture-reset, wait for the 15-second family timer, strafe just enough to uncover the dummy, then step toward it before selecting and using Sneak. Warding Blow only reaches 4 m. The successful 1130 stand was near 24341/495/-1818, 3.57 m from the dummy. On an out-of-range refusal, step closer without switching Sneak off.",
            "Level 50 rank 50 unarmed, spawned 2.5 m directly behind Melee Dummy (99/100 memory; Spy Style 1112 full closure = +54% thrown dmg). Press slot 1 (Spy Style 1112).",
            "Step A (2 s cast sneak free throws — start outside combat). Slot 3 (Staggering Throw 348): 45 IS, 2s cast retaining Sneak, 45s/grp691 15s, 292 dmg (VF3 190 + 54%), Staggered 12 s (State 201). Slot 4 (Infuriating Assault 1130): 40 IS, 2s cast retaining Sneak, 45s/grp691 15s, 201 dmg (VF3 131 + 54%), Enraged 4 s (State 93). Slot 5 (Warding Blow 1134): 45 IS, 2s cast retaining Sneak, 45s/grp691 15s, 217 dmg (VF3 141 + 54%), Melee/Ballistic Acc (522/524) -30 for 8 s.",
            "Step B (Instant interlock sneak openers). Slot 6 (Diving Frankensteiner 385): 80 IS, 60s/grp691 15s, choreo 19592, 1369 dmg ((164+725)+54%). Slot 7 (Freedom to Joints 386): 60 IS, 60s/grp691 15s, choreo 19600, 1527 dmg ((92+900)+54%), Powerless 10 s with this profile (8 s base, State 96)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        await SpyFixtureReset.VerifyAsync(context);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var prior = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        actor.SendChat("?combatdepth fixture-reset");
        await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
        await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Last(view => view.Name == CombatFixtureLoadout.DummyName).ViewId != prior.ViewId,
            "The Warding Blow test has a fresh selectable dummy", HeadlessPlayer.DefaultTimeout);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);

        actor.SendRpc(StockSpyStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        actor.SendChat("?tp 24243 495 -1880 0 0 0");
        await actor.WaitForTextAsync("Teleported to 0/0");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        actor.SendRpc(StockSneak);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[STEALTH] CSR_Tester 1111 on"),
            "Ordinary Sneak is active", HeadlessPlayer.DefaultTimeout);

        int refusedAt = actor.World.RpcSnapshot().Count;
        byte[] request = StarterHackerControlTargetMode.WithHandle(StockWardingBlow, handle);
        actor.SendRpc(request);
        await StockAbilityProfile.ObserveAsync(actor, refusedAt,
            frame => AbilityRefusals.TryDecode(frame.Bytes, out var denial) &&
                denial.AbilityID == WardingBlow && denial.ErrorID == AbilityRefusals.TargetOutOfRange,
            "Warding Blow refuses at the native 4.60 m stand");
        var castBar = BytePattern.Parse("80 ac 6e 04");
        var ownTimer = BytePattern.Parse("80 bd 01 6e 04");
        var familyTimer = BytePattern.Parse("80 bd 01 b3 02");
        Check.That(!actor.World.RpcSnapshot().Skip(refusedAt)
            .Any(frame => castBar.IsMatch(frame.Bytes) || ownTimer.IsMatch(frame.Bytes) || familyTimer.IsMatch(frame.Bytes)),
            "Out-of-range Warding Blow starts no cast or reuse");

        actor.SendChat("?tp 24341 495 -1818 0 0 0");
        await actor.WaitForTextAsync("Teleported to 0/0");
        int cursor = actor.World.RpcSnapshot().Count;
        int landedAt = server.DebugLog().Length;
        actor.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(actor, WardingBlow, 2);
        await EvidenceRetestWire.TimerAsync(actor, cursor, WardingBlow, AbilityCooldowns.OwnTimer, 45);
        await EvidenceRetestWire.TimerAsync(actor, cursor, 691, AbilityCooldowns.GroupTimer, 15);
        await actor.WaitUntilAsync(() =>
        {
            string log = server.DebugLog()[landedAt..];
            return log.Contains($"[ABILITY] 1134 lands on view {dummy.ViewId} for 217") &&
                log.Contains("Stat 522 -30 8 s") && log.Contains("Stat 524 -30 8 s") &&
                log.Contains("1111 off (landed ability 1134)");
        }, "In-range Warding Blow lands for 217 with both eight-second penalties and ends Sneak", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Warding Blow protocol framing");
        return Array.Empty<byte>();
    }
}
