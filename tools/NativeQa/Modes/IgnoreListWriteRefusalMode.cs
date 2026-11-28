using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class IgnoreListWriteRefusalMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, OtherX = 17220;
    private const uint Owner = 110;
    private const string OwnerHandle = "CSR_Tester";

    public override string Name => "ignore-list-write-refusal";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"UPDATE characters SET y={Y} WHERE charId={Owner};" },
        new[] { $"""
            UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE handle='QAFriend';
            UPDATE characters SET x={OtherX},y={Y},z={Z} WHERE handle='QAOther';
            """, Scenarios.IgnoreSaveFixtureSql(Owner) },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). Buddy List > Ignore starts with QAOther only. No targeting or teleport.",
            "Type /ignore QAFriend. A disclosed disposable CHECK refuses the add. Expect 80 e1 status 68 00 00 0e, the stock failure text and no QAFriend row. Select QAOther in the Ignore tab and Remove. A fixture foreign key refuses the delete. Expect 80 e3 status 68 00 00 0e and QAOther still in the list.",
            """Send {"actor":"QAFriend","action":"ignore-save","step":"retry"}. It checks the saved list and sends two tells. Only "Before retry from QAFriend" should appear, with QAOther's tell refused. The helper then releases both fixture faults.""",
            """In the same session, /ignore QAFriend and Remove QAOther again. Expect 80 e1 and 80 e3 success, with QAFriend as the only row. Send {"actor":"QAFriend","action":"ignore-save","step":"verify"}. Only "After retry from QAOther" should appear. Relog and verify only QAFriend is ignored."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, OtherX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        foreach (string handle in new[] { Scenarios.IgnoreSaveAdd, Scenarios.IgnoreSaveRemove })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        await Scenarios.CheckIgnoreSaveRowsAsync(db, Owner, committed: false);
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='ignorelist' AND CONSTRAINT_NAME='{Scenarios.IgnoreSaveConstraint}'"),
            "The disclosed add CHECK is armed");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM {Scenarios.IgnoreSavePin} WHERE charId={Owner} AND handle='QAOther'"),
            "QAOther's ignore is pinned by the disposable foreign key");

        if (context.StockProbe is not { } owner) return Array.Empty<byte>();
        await owner.EnterWorldAsync();
        await Scenarios.ProveIgnoreSaveAsync(owner, context.Helpers[Scenarios.IgnoreSaveAdd],
            context.Helpers[Scenarios.IgnoreSaveRemove], db);
        return Array.Empty<byte>();
    }

    public static async Task<string> SendActionAsync(HeadlessPlayer actor, JsonElement request,
        IReadOnlyDictionary<string, HeadlessPlayer> helpers, ThrowawayDatabase db)
    {
        if (actor.Handle != Scenarios.IgnoreSaveAdd) throw new ArgumentException("Use QAFriend for this ignore check.");
        bool committed = request.GetProperty("step").GetString() switch
        {
            "retry" => false,
            "verify" => true,
            _ => throw new ArgumentException("Use step retry or verify.")
        };
        await Scenarios.CheckIgnoreSaveRowsAsync(db, Owner, committed);
        await Scenarios.CheckIgnoreSaveTellsAsync(actor, helpers[Scenarios.IgnoreSaveRemove], OwnerHandle, db, committed);
        if (committed) return "QAFriend ignored; QAOther delivered";
        await Scenarios.ReleaseIgnoreSaveFaultsAsync(db);
        return "Original ignore and tell filtering kept; fixture faults released";
    }
}
