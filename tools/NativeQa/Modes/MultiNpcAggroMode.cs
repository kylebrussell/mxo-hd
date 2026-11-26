using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MultiNpcAggroMode : NativeQaMode
{
    private const int X = 19500, Y = -705, Z = 60600, Health = 5000;
    private const string MixedStand = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly double[][] Runners = OpenWorldFirstLevelFixture.Spawns;

    public override string Name => "multi-npc-aggro";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z,
        new[] { "DELETE FROM inventory WHERE charId=110" },
        new[] { "UPDATE char_abilities SET level=1 WHERE char_id=110" },
        new[] { $"UPDATE characters SET level=1,exp=0,cash=1000,y={Y},healthC={Health},healthM={Health},innerStrC=100,innerStrM=100,rotation=224,pvpflag=0 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "multi-npc-aggro.csv",
        environmentOverrides: new()
        {
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "1", ["MXO_MOB_WANDER"] = "0",
            ["MXO_AGGRO_RANGE"] = "12", ["MXO_PACKET_LOG"] = "1"
        }, readyMessages: new[]
        {
            $"CSR starts on verified NAV ({X},{Y},{Z}) next to two level-one Chopper Runners. 5,000 HP is a disposable survival aid. Stay at spawn and make no attack request. The near Runner opens interlock. The far Runner approaches and holds at melee range while the first fight continues.",
            "Fight normally in Self Defense. When the first dies, loot it during the five-second grace. Let the second start its own fight without a new attack request, then kill and loot it too. Expect two separate +391 XP awards, two corpse windows and 782 total XP. Ordinary loot can roll zero Info.",
            $"Optional headless-tested gun control after both kills: {MixedStand}. Stay idle. QA Melee Guard starts interlock; QA Rifle Guard sends ordinary ranged shots during that same interlock. The Rifle Guard must not open a second combat view. Log out to end this stage."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (var pos in Runners.Append(new double[] { X, Y, Z }))
            Check.That(NavMesh.Locate(1, pos[0], pos[1], pos[2], out var nav) && !nav.indoors && nav.floorY + 95 == Y,
                "Each Runner and CSR starts on grounded outdoor NAV");
        Check.That(NavMesh.Locate(1, 17000, 495, 6400, out var mixed) && !mixed.indoors &&
            (mixed.sector, mixed.region, mixed.polygon) == (0x4e2, 0x12, 4), "Verified mixed group stand");
        Check.That(NavMesh.HasSightPath(1, 17000, 495, 6400, 17000, 495, 7200), "Rifle support has a clear shot");
        var server = context.ActualServer!;
        var database = context.OwnedDatabase!;
        Check.That(server.DebugLog().Contains("4 NPCs now"), "Only the four dedicated NPCs loaded");
        Check.Equal(1L, await database.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId=110 AND exp=0 AND x={X} AND y={Y} AND z={Z}"),
            "CSR is seeded once on the first test stand");
        if (context.StockProbe is not { } player) return Array.Empty<byte>();

        await player.EnterWorldAsync();
        var first = await RunnerAsync(0);
        var second = await RunnerAsync(1);
        string firstHandle = Handle(first.View), secondHandle = Handle(second.View);
        await player.WaitForRpcAsync($"46 {firstHandle} 01");

        bool HoldsAtMelee()
        {
            try
            {
                var actor = OpenWorldCurrentNpc.Require(player.World.CurrentOwnViewLife(), Runners[1][0], Y, Runners[1][2]);
                return actor.LastMotion == Mob.Animations.STAND && actor.Positions.Count > 1 &&
                    Math.Sqrt(Math.Pow(actor.Position[0] - X, 2) + Math.Pow(actor.Position[2] - Z, 2)) <= MobAggro.MeleeDistanceUnits + 1;
            }
            catch (InvalidDataException) { return false; }
        }

        await player.WaitUntilAsync(HoldsAtMelee, "Second Runner moves to melee range and holds", TimeSpan.FromSeconds(20));
        Check.That(player.Received($"46 {secondHandle} 01").Count == 0 && player.Received($"46 {firstHandle} 00").Count == 0,
            "Only the first interlock is active while the second Runner holds");
        await KillAndLootAsync(firstHandle, 1);
        await player.WaitForRpcAsync($"46 {secondHandle} 01", TimeSpan.FromSeconds(20));
        await KillAndLootAsync(secondHandle, 2);
        Check.Equal(782L, await database.LongAsync($"SELECT exp FROM characters WHERE charId={player.CharId}"),
            "Two ordinary defeats award 391 XP each");

        player.SendChat(MixedStand);
        await player.WaitForTextAsync("Teleported to 4e2/12");
        var melee = await player.WaitForViewNamedAsync("QA Melee Guard");
        var rifle = await player.WaitForViewNamedAsync("QA Rifle Guard");
        string meleeHandle = Handle(melee);
        await player.WaitForRpcAsync($"46 {meleeHandle} 01");
        await player.WaitUntilAsync(() => server.DebugLog().Contains("[AGGRO] QA Rifle Guard fires in support", StringComparison.Ordinal),
            "Rifle Guard fires while the melee fight is active", TimeSpan.FromSeconds(12));
        Check.That(player.Received($"46 {Handle(rifle)} 01").Count == 0 && player.Received($"46 {meleeHandle} 00").Count == 0,
            "Supporting fire neither starts a second interlock nor disrupts the first");
        Check.Equal(0, player.World.Problems.Count, "Stock combat and loot dependencies use valid wire framing");
        Console.WriteLine("MULTI NPC PASS: second Runner holds and takes over, two XP awards and two corpse claims, ranged support during melee.");
        return Array.Empty<byte>();

        async Task<OpenWorldCurrentNpc.Current> RunnerAsync(int index)
        {
            OpenWorldCurrentNpc.Current? found = null;
            double[] at = Runners[index];
            await player.WaitUntilAsync(() =>
            {
                try { found = OpenWorldCurrentNpc.Require(player.World.CurrentOwnViewLife(), at[0], at[1], at[2]); return true; }
                catch (InvalidDataException) { return false; }
            }, $"Current Runner born at {at[0]},{at[1]},{at[2]}", TimeSpan.FromSeconds(20));
            return found!;
        }

        async Task KillAndLootAsync(string handle, int number)
        {
            await player.WaitUntilAsync(() => player.World.RpcSnapshot().Count(r => OpenWorldFirstLevelFixture.FightingGain(r.Bytes, 391)) == number,
                $"Runner {number} dies in its ordinary interlock", TimeSpan.FromSeconds(120));
            Check.That(player.World.RpcSnapshot().Any(r => BytePattern.Parse($"46 {handle} 00").IsMatch(r.Bytes)),
                $"Runner {number} combat ends before the next attack");
            long before = await database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
            player.SkipReceived();
            player.SendRpc($"80 c7 {handle} 05 00");
            var window = await player.WaitForRpcAsync("81 19");
            uint info = OpenWorldFirstLevelFixture.LootInfo(window.Bytes);
            Check.That(info <= 495 && info % 5 == 0, $"Runner {number} has its own ordinary loot window");
            player.SendRpc("81 1f");
            await player.WaitForRpcAsync("81 20");
            Check.Equal(before + info, await database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"),
                $"Runner {number}'s Info is credited once");
            player.SendRpc("81 48");
            await player.WaitForRpcAsync("81 49");
        }
    }

    private static string Handle(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
}
