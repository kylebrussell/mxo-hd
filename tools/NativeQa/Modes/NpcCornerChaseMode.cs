using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class NpcCornerChaseMode : NativeQaMode
{
    public override string Name => "npc-corner-chase";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 },
        WorldNpcCornerFixture.FarX, WorldNpcCornerFixture.FarZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET x={WorldNpcCornerFixture.FarX},y={WorldNpcCornerFixture.FarY},z={WorldNpcCornerFixture.FarZ}," +
            $"level=2,exp={PlayerProgression.ExperienceForLevel(2)},healthC=2000,healthM=2000,rotation={WorldNpcCornerFixture.FarYaw},pvpflag=0 " +
            "WHERE charId=110;",
            // The bootstrap observer can see the spawn without drawing it away before CSR logs in.
            $"UPDATE characters SET x={WorldNpcCornerFixture.StandX},y={WorldNpcCornerFixture.StandY},z={WorldNpcCornerFixture.StandZ}," +
            $"level=20,exp={PlayerProgression.ExperienceForLevel(20)},pvpflag=0 WHERE handle='QAFriend';"
        },
        environmentOverrides: WorldNpcCornerFixture.StationaryEnvironment, readyMessages: new[] { WorldNpcCornerFixture.Directions });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing corner chase fixture server.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing corner chase repository.");
        WorldNpcCornerFixture.CheckGeometry(root);
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Missing disposable corner chase database.");
        Check.Equal(1L, await database.LongAsync("SELECT COUNT(*) FROM characters WHERE charId=110 AND level=2 AND " +
            $"x={WorldNpcCornerFixture.FarX} AND y={WorldNpcCornerFixture.FarY} AND z={WorldNpcCornerFixture.FarZ}"),
            "CSR is seeded at the stationary far-side mark");

        using var probe = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await probe.EnterWorldAsync();
        OpenWorldCurrentNpc.Current? goof = null;
        await probe.WaitUntilAsync(() =>
        {
            try { goof = WorldNpcCornerFixture.Observe(probe); return goof != null; }
            catch (InvalidDataException) { return false; }
        }, "starting view of the real Blackwood Goof", TimeSpan.FromSeconds(15));
        Check.That(goof!.View.Attributes.TryGetValue("Health", out var health) &&
            health.Length == 2 && BitConverter.ToUInt16(health) > 0, "The chase NPC is alive");
        await Task.Delay(TimeSpan.FromSeconds(MobAggro.RetrySeconds + 1));
        NativeQaReadiness.RequireNoAmbientAggro(server, probe);
        Check.Equal(0, probe.World.Problems.Count, "Corner setup probe has no wire errors");
        Console.WriteLine(WorldNpcCornerFixture.Directions);
        return Array.Empty<byte>();
    }
}
