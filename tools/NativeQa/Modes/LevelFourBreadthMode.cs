using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class LevelFourBreadthMode : NativeQaMode
{
    private const int InitialXp = 20900, KillXp = 572;

    public override string Name => "level-four-breadth";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 19403, 60401,
        new[] { "DELETE FROM inventory WHERE charId=110" },
        new[] { "UPDATE char_abilities SET level=1 WHERE char_id=110" },
        new[] { $"UPDATE characters SET level=3,exp={InitialXp},cash=1000,y=-705,rotation=0,"
            + "healthC=250,healthM=250,innerStrC=106,innerStrM=106,pvpflag=0 WHERE charId=110" },
        environmentOverrides: new() { ["MXO_MOBS"] = "world", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            $"CSR starts at level 3 and {InitialXp} XP. Kill one Chopper Runner normally. "
            + $"Expect +{KillXp} XP, level 4 at {InitialXp + KillXp} XP, max HP 300 and max IS 109. "
            + "Quit and relog in the same fixture to check persistence.",
            "Corpse-window 4-kill step: kill all four printed nearby Chopper Runners in a row in normal close combat, "
            + "closing each Corpse window with Take All or X before the next fight. Expect 4/4 automatic Corpse windows "
            + "shortly after combat teardown (SendNpcDies, SendCombatEnd, and 81 1a + 81 19 in separate ordered packets)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Level QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Level QA requires an isolated server.");
        Check.Equal(1L, await database.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId=110 AND level=3 AND exp={InitialXp} AND healthM=250 AND innerStrM=106"),
            "CSR has the expected one-kill-away profile");
        long threshold = PlayerProgression.ExperienceForLevel(4);
        Check.That(InitialXp < threshold && InitialXp + KillXp >= threshold, "One ordinary kill crosses the threshold");

        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Level QA requires world data.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        using var probe = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await probe.EnterWorldAsync();
        var source = OpenWorldFirstLevelFixture.Spawns[0];
        OpenWorldCurrentNpc.Current? first = null;
        ViewCreation[] runners = Array.Empty<ViewCreation>();
        await probe.WaitUntilAsync(() =>
        {
            try { first = OpenWorldCurrentNpc.Require(probe.World.CurrentOwnViewLife(), source[0], source[1], source[2]); }
            catch (InvalidDataException) { return false; }
            runners = probe.ViewsOfGoid(599)
                .Where(v => v.Name == "Chopper Runner"
                    && v.Attributes.TryGetValue("Level", out var lvl) && lvl.Length == 1 && lvl[0] == 1
                    && v.Attributes.TryGetValue("Health", out var hp) && hp.Length == 2 && BitConverter.ToUInt16(hp) > 0
                    && v.Attributes.TryGetValue("Position", out var pos) && pos.Length == 24)
                .OrderBy(v =>
                {
                    byte[] p = v.Attributes["Position"];
                    double dx = BitConverter.ToDouble(p, 0) - 19403d;
                    double dz = BitConverter.ToDouble(p, 16) - 60401d;
                    return dx * dx + dz * dz;
                })
                .Take(4)
                .ToArray();
            return runners.Length == 4;
        }, "four live level-one Chopper Runners in the current world", TimeSpan.FromSeconds(20));

        Check.That(first!.View.Attributes.TryGetValue("Health", out var health) && health.Length == 2 && BitConverter.ToUInt16(health) > 0,
            "The primary Runner is alive");
        var primaryPoint = OpenWorldFirstLevelFixture.Approach(first.Position);
        Console.WriteLine($"Chopper Runner at {first.Position[0]:0.###},{first.Position[1]:0.###},{first.Position[2]:0.###}. "
            + $"Approach stand {primaryPoint.x:0.###},{primaryPoint.y:0.###},{primaryPoint.z:0.###}.");
        for (int i = 0; i < runners.Length; i++)
        {
            byte[] p = runners[i].Attributes["Position"];
            double[] pos = { BitConverter.ToDouble(p, 0), BitConverter.ToDouble(p, 8), BitConverter.ToDouble(p, 16) };
            Console.WriteLine($"Corpse-window target #{i + 1}: Chopper Runner at {pos[0]:0.###},{pos[1]:0.###},{pos[2]:0.###}.");
        }
        NativeQaReadiness.RequireNoAmbientAggro(server, probe);
        Check.Equal(0, probe.World.Problems.Count, "The Chopper views decode cleanly");
        return Array.Empty<byte>();
    }
}
