using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class NpcCornerChaseMode : NativeQaMode
{
    public override string Name => "npc-corner-chase";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 },
        WorldNpcCornerFixture.StandX, WorldNpcCornerFixture.StandZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET x={WorldNpcCornerFixture.StandX},y={WorldNpcCornerFixture.StandY},z={WorldNpcCornerFixture.StandZ}," +
            $"level=2,exp={PlayerProgression.ExperienceForLevel(2)},healthC=2000,healthM=2000,rotation=0,pvpflag=0 " +
            "WHERE charId=110 OR handle='QAFriend';"
        },
        environmentOverrides: WorldNpcCornerFixture.Environment, readyMessages: new[] { WorldNpcCornerFixture.Directions });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing corner chase fixture server.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing corner chase repository.");
        WorldNpcCornerFixture.CheckGeometry(root);

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
