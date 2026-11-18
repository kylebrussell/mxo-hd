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
        readyMessages: new[] { $"CSR starts at level 3 and {InitialXp} XP. Kill one Chopper Runner normally. "
            + $"Expect +{KillXp} XP, level 4 at {InitialXp + KillXp} XP, max HP 300 and max IS 109. "
            + "Quit and relog in the same fixture to check persistence." });

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
        OpenWorldCurrentNpc.Current? target = null;
        await probe.WaitUntilAsync(() =>
        {
            try { target = OpenWorldCurrentNpc.Require(probe.World.CurrentOwnViewLife(), source[0], source[1], source[2]); }
            catch (InvalidDataException) { return false; }
            return true;
        }, "the level-one Chopper Runner in the current world", TimeSpan.FromSeconds(20));
        Check.That(target!.View.Attributes.TryGetValue("Health", out var health) && health.Length == 2 && BitConverter.ToUInt16(health) > 0,
            "The selected Runner is alive");
        var point = OpenWorldFirstLevelFixture.Approach(target.Position);
        NativeQaReadiness.RequireNoAmbientAggro(server, probe);
        Check.Equal(0, probe.World.Problems.Count, "The Chopper's view decodes cleanly");
        Console.WriteLine($"Chopper Runner at {target.Position[0]:0.###},{target.Position[1]:0.###},{target.Position[2]:0.###}. "
            + $"Approach stand {point.x:0.###},{point.y:0.###},{point.z:0.###}.");
        return Array.Empty<byte>();
    }
}
