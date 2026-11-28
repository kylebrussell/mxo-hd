using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class BuddyListWriteRefusalMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, OtherX = 17220;
    private const uint Owner = 110, Friend = 6111, Other = 6113;

    public override string Name => "buddy-list-write-refusal";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"UPDATE characters SET y={Y} WHERE charId={Owner};" },
        new[] { $"""
            UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE charId={Friend};
            UPDATE characters SET x={OtherX},y={Y},z={Z} WHERE charId={Other};
            """, Scenarios.BuddySaveFixtureSql(Owner, Friend, Other) },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). Open Buddy List from the people icon above chat. QAOther should be the only buddy and should be online. No teleport or targeting.",
            "Type /friend QAFriend. The disposable CHECK refuses the add. Expect 80 d7 status 68 00 00 0e, stock failure text, and no QAFriend row. Select QAOther in Buddy List and choose Remove. The disposable foreign key refuses the delete. Expect 80 db status 68 00 00 0e, stock failure text and QAOther still shown.",
            """Send {"actor":"QAFriend","action":"buddy-save","step":"retry"}. The helper checks that only QAOther is saved, then releases both faults. In the same session, repeat /friend QAFriend and Remove QAOther. Expect 80 d7 and 80 db success and QAFriend online via 80 de.""",
            """Send {"actor":"QAFriend","action":"buddy-save","step":"verify"}. Only QAFriend should be saved. Relog and verify QAFriend is the only buddy."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, OtherX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        foreach (var (handle, id) in new[] { (Scenarios.BuddySaveAdd, Friend), (Scenarios.BuddySaveRemove, Other) })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, helper);
            Check.Equal(id, helper.CharId, $"The fixture pins the correct character for {handle}");
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        await Scenarios.CheckBuddySaveFixtureAsync(db, Owner);

        if (context.StockProbe is { } owner)
        {
            await owner.EnterWorldAsync();
            await Scenarios.ProveBuddySaveAsync(owner, db);
        }
        return Array.Empty<byte>();
    }

    public static async Task<string> SendActionAsync(HeadlessPlayer actor, JsonElement request, ThrowawayDatabase db)
    {
        if (actor.CharId != Friend) throw new ArgumentException("Use QAFriend to check the Buddy List.");
        switch (request.GetProperty("step").GetString())
        {
            case "retry":
                await Scenarios.CheckBuddySaveFixtureAsync(db, Owner);
                await Scenarios.ReleaseBuddySaveFaultsAsync(db);
                return "Only QAOther was saved. Both fixture faults released.";
            case "verify":
                await Scenarios.CheckBuddySaveRowsAsync(db, Owner, committed: true);
                return "Only QAFriend is saved.";
            default:
                throw new ArgumentException("Use step retry or verify.");
        }
    }
}
