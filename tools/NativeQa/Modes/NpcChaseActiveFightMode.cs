using System.Diagnostics;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class NpcChaseActiveFightMode : NativeQaMode
{
    private const int Level = 2, Health = 5000, DetectionMetres = 42;
    private const string Pursuer = "QA Chase Goof", Partner = "QA Sparring Partner", IdlePursuer = "QA Idle Goof";
    private const string ChaseRsi = "B3010058";
    private const string EnterChase = "?tp 17000 495 6400 4e2 12 4 - 59";
    private const string Park = "?tp 105221 695 6654 465 c 8c - 00";
    private const string EnterIdle = "?tp 97769 695 10761 15b 4 0 - 00";
    private static readonly Stand Staging = new(16400, 495, 6400), Entry = new(17000, 495, 6400);
    private static readonly Stand ChaseSpawn = new(21000, 495, 6400), PartnerSpawn = new(17200, 495, 6260);
    private static readonly Stand ParkStand = new(105221, 695, 6654), IdleEntry = new(97769, 695, 10761);
    private static readonly Stand IdleSpawn = new(97769, 695, 11661);

    public override string Name => "npc-chase-active-fight";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, Staging.X, Staging.Z,
        Array.Empty<string>(), new[]
        {
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            "DELETE FROM inventory WHERE charId=110 AND slot IN (105,107)"
        }, new[]
        {
            Seed(110, Staging, Level, MobAggro.YawTowards(Staging.X, Staging.Z, PartnerSpawn.X, PartnerSpawn.Z)),
            Seed(6111, Staging, 20), Seed(6113, IdleEntry, 20)
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: "npc-chase-active-fight.csv",
        environmentOverrides: new()
        {
            ["MXO_MOBS"] = "test", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "1",
            ["MXO_AGGRO_RANGE"] = DetectionMetres.ToString(), ["MXO_AGGRO_LEVEL_GAP"] = "0",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, readyMessages: new[]
        {
            $"Stage 1 starts with level 2 CSR safe at {Staging} outdoors, facing {Partner} at {PartnerSpawn}. Use Self Defense and no weapon. Locate the partner before triggering the chase.",
            $"Type {EnterChase}. This puts CSR at {Entry}, 2.44 m from the partner. As soon as DebugLog shows '[AGGRO] {Pursuer} pursues 110', right-click {Partner} and select Close Combat. You have about nine seconds while the Goof walks in from {ChaseSpawn}. Keep still and use no other combat action.",
            $"Expect one 46 start for {Partner}. Watch the Goof walk to arm's length and stand, then observe another 45 for the same partner view/spawn, with no 46 start for {Pursuer} and no matching partner end. Save the NPC movement, start and the later round from PacketLog.",
            "If the Goof starts combat before the partner does, restart this disposable fixture to repeat Stage 1.",
            $"For Stage 2, use {Park} to move clear of both sites, then ?cd stop and wait for the fight to clear. Type {EnterIdle} to stand at {IdleEntry}. Stay idle and make no attack request. The independent {IdlePursuer} walks from {IdleSpawn} and opens its own 40 echo/46 start and 45."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        VerifyGeometry(context.RepositoryRoot!);
        var server = context.ActualServer!;
        var database = context.OwnedDatabase!;
        Check.Equal(1L, await database.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId=110 AND level={Level} " +
            $"AND x={Staging.X} AND y={Staging.Y} AND z={Staging.Z}"), "Native player starts outside the pursuit range");
        Check.That(server.DebugLog().Contains("[MOBS] mob_just_some_and_winter.csv: 0 repeat rows skipped, 3 NPCs now", StringComparison.Ordinal),
            "Only the three dedicated NPCs were spawned");

        using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await observer.EnterWorldAsync();
        await BirthAsync(observer, Pursuer, ChaseSpawn, Level);
        await BirthAsync(observer, Partner, PartnerSpawn, 1);
        using var controlObserver = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther");
        await controlObserver.EnterWorldAsync();
        await BirthAsync(controlObserver, IdlePursuer, IdleSpawn, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        NativeQaReadiness.RequireNoAmbientAggro(server, controlObserver);
        Check.Equal(0, observer.World.Problems.Count + controlObserver.World.Problems.Count, "Readiness observers have clean view framing");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        await BirthAsync(actor, Pursuer, ChaseSpawn, Level);
        var partner = await BirthAsync(actor, Partner, PartnerSpawn, 1);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        actor.SendChat(EnterChase);
        await actor.WaitForTextAsync("Teleported to 4e2/12");
        await actor.WaitUntilAsync(() => Chase(actor).LastMotion == Mob.Animations.WALK,
            "The Goof is already moving toward CSR", TimeSpan.FromSeconds(5));
        var inputWindow = Stopwatch.StartNew();
        // Reserve time for a human to read the chase cue and open the target radial.
        await Task.Delay(TimeSpan.FromSeconds(5));
        var approaching = Chase(actor);
        Check.That(approaching.LastMotion == Mob.Animations.WALK &&
            WorldNpcCornerFixture.Distance(approaching.Position, Entry.Position) > MobAggro.MeleeDistanceUnits + 400,
            "The grounded chaser is still over a second away after five seconds of operator delay");
        string partnerHandle = Handle(partner);
        actor.SendRpc($"40 {partnerHandle} 01 6b"); // Retained stock Close Combat request with the live handle.
        await actor.WaitForRpcAsync($"46 {partnerHandle} 01");
        await actor.WaitUntilAsync(() =>
        {
            var current = Chase(actor);
            return current.LastMotion == Mob.Animations.STAND &&
                WorldNpcCornerFixture.Distance(current.Position, Entry.Position) <= MobAggro.MeleeDistanceUnits + 1;
        }, "The older chase visibly reaches arm's length and stops", TimeSpan.FromSeconds(10));
        inputWindow.Stop();
        actor.SkipReceived();
        byte[] round = (await actor.WaitForRpcAsync("45", TimeSpan.FromSeconds(8))).Bytes;
        uint expected = DuelPackets.Handle(partner.ViewId, partner.SpawnId);
        Check.That(round.Length >= 40 && BitConverter.ToUInt32(round, round.Length - 40) == expected &&
            BitConverter.ToUInt32(round, round.Length - 36) == expected &&
            actor.Received($"46 {Handle(approaching.View)} 01").Count == 0 &&
            actor.Received($"46 {partnerHandle} 00").Count == 0, "The same partner's fight keeps running after the old chase arrives");
        Console.WriteLine($"NPC CHASE ACTIVE BOOTSTRAP PASS: The first walking view preceded arrival by {inputWindow.Elapsed.TotalSeconds:0.0}s. {Partner} combat still opened after a five-second input delay.");

        actor.SendChat(Park);
        await actor.WaitForTextAsync("Teleported to 465/c");
        actor.SendChat("?cd stop");
        await actor.WaitForRpcAsync($"46 {partnerHandle} 00");
        await actor.WaitForTextAsync("Fixture combat ended");
        actor.SkipReceived();
        int idleAt = server.DebugLog().Length;
        actor.SendChat(EnterIdle);
        await actor.WaitForTextAsync("Teleported to 15b/4");
        var idleNpc = await BirthAsync(actor, IdlePursuer, IdleSpawn, Level);
        string idleHandle = Handle(idleNpc);
        await actor.WaitForRpcAsync($"40 {idleHandle} 00 6b");
        await actor.WaitForRpcAsync($"46 {idleHandle} 01");
        await actor.WaitForRpcAsync("45");
        string idleLog = server.DebugLog()[idleAt..];
        Check.That(idleLog.Contains($"[AGGRO] {IdlePursuer} pursues 110", StringComparison.Ordinal) &&
            idleLog.Contains($"[AGGRO] {IdlePursuer} attacks 110", StringComparison.Ordinal),
            "A different NPC still pursues and starts ordinary combat against idle CSR");
        Check.Equal(0, actor.World.Problems.Count, "Both fixture stages use valid stock framing");
        Console.WriteLine("NPC CHASE IDLE BOOTSTRAP PASS: different Goof pursues and starts 40/46/45 without a player attack.");
        return Array.Empty<byte>();
    }

    private static void VerifyGeometry(string root)
    {
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var stand in new[] { Staging, Entry, ChaseSpawn, PartnerSpawn, ParkStand, IdleEntry, IdleSpawn })
            Check.That(NavMesh.Locate(1, stand.X, stand.Y, stand.Z, out var nav) && !nav.indoors && nav.floorY + 95 == stand.Y,
                $"Grounded outdoor NAV for {stand}");
        foreach (var (from, to) in new[] { (ChaseSpawn, Entry), (Staging, PartnerSpawn), (Entry, PartnerSpawn), (IdleSpawn, IdleEntry) })
            Check.That(WorldNpcCornerFixture.Straight(from.Position, to.Position), $"Clear sight path from {from} to {to}");
        foreach (var (stand, sector, region, polygon) in new[] { (Entry, 0x4e2, 0x12, 4), (ParkStand, 0x465, 0xc, 0x8c), (IdleEntry, 0x15b, 4, 0) })
        {
            Check.That(NavMesh.Locate(1, stand.X, stand.Y, stand.Z, out var nav) &&
                (nav.sector, nav.region, nav.polygon) == (sector, region, polygon), $"Printed teleport matches NAV for {stand}");
        }
        Check.That(Staging.Distance(ChaseSpawn) > DetectionMetres * 100 && Entry.Distance(ChaseSpawn) == 4000 &&
            Entry.Distance(PartnerSpawn) is > 200 and < 300 && IdleEntry.Distance(IdleSpawn) == 900 &&
            ParkStand.Distance(IdleSpawn) > DetectionMetres * 100 && ParkStand.Distance(ChaseSpawn) > DetectionMetres * 100,
            "Staging, partner reach, chase window and independent control have the stated distances");
    }

    private static string Seed(int id, Stand stand, int level, byte yaw = 0) =>
        $"UPDATE characters SET x={stand.X},y={stand.Y},z={stand.Z},rotation={yaw},level={level}," +
        $"exp={PlayerProgression.ExperienceForLevel(level)},healthC={Health},healthM={Health},pvpflag=0 WHERE charId={id}";

    private static string Handle(ViewCreation npc) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(npc.ViewId, npc.SpawnId)));

    private static OpenWorldCurrentNpc.Current Chase(HeadlessPlayer actor) =>
        OpenWorldCurrentNpc.Require(actor.World.CurrentOwnViewLife(), ChaseSpawn.X, ChaseSpawn.Y, ChaseSpawn.Z,
            Pursuer, Level, Convert.FromHexString(ChaseRsi), birthRadius: 1);

    private static async Task<ViewCreation> BirthAsync(HeadlessPlayer actor, string name, Stand stand, byte level)
    {
        var view = await actor.WaitForViewNamedAsync(name);
        Check.That(view.Goid == 599 && view.Attributes["Level"][0] == level &&
            BitConverter.ToUInt16(view.Attributes["Health"]) == Health &&
            EngageFoesFixture.Position(view).SequenceEqual(stand.Position), $"The live {name} has the fixture level and birth stand {stand}");
        return view;
    }

    private readonly record struct Stand(int X, int Y, int Z)
    {
        internal double[] Position => new double[] { X, Y, Z };
        internal double Distance(Stand other) => WorldNpcCornerFixture.Distance(Position, other.Position);
        public override string ToString() => $"({X},{Y},{Z})";
    }
}
