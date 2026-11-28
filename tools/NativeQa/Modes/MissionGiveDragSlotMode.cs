using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MissionGiveDragSlotMode : NativeQaMode
{
    private const int X = Scenarios.GiveStandX, Y = Scenarios.GiveStandY, Z = Scenarios.GiveStandZ;

    public override string Name => "mission-give-drag-slot";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[] { $"""
            UPDATE characters SET alignment=3,y={Y},rotation=128 WHERE charId=110;
            UPDATE characters SET alignment=3,x={X},y={Y},z={Z} WHERE handle='QAOther';
            DELETE FROM inventory WHERE slot IN (1,6) AND
                (charId=110 OR charId IN (SELECT charId FROM characters WHERE handle='QAOther'));
            """ },
        missionFixture: "give-item.xml",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "CSR starts outdoors on verified NAV at (17000,495,6400). Phone > Flood > Call > Critical > Give Item Fixture. Accept. Courier spawns east at (17200,495,6400), Receiver at (17000,495,6600). No elevator or teleport.",
            "Right-click Courier > Get. Open Inventory and drag the disk to empty bag slot 6. Verify the disk moved. Try dragging onto Courier. The disk stays in the bag and Give stays open. Record whether the client sent 66 or declined locally.",
            "Drag the disk from its new slot onto Receiver. Expect client 66 naming the new slot, 5f for that slot, one Give goal completion and the completion message. Headless also covers stale slot/spawn refusals and the separate right-click Give control."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (17200, 6400), (17000, 6600) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{z}");
        using var helper = context.StockProbe == null
            ? await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther")
            : null;
        var player = context.StockProbe ?? helper!;
        await player.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
        await Scenarios.ProveGiveItemAsync(player, context.OwnedDatabase!);
        return Array.Empty<byte>();
    }
}
