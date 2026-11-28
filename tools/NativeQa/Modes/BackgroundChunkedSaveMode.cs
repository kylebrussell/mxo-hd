using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class BackgroundChunkedSaveMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120;
    private const uint Owner = 110;

    public override string Name => "background-chunked-save";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"UPDATE characters SET y={Y} WHERE charId={Owner};" },
        new[] { Scenarios.ChunkedBackgroundFixtureSql(Owner),
            $"UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE handle='QAFriend';" },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Verified open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400). No teleport or target.",
            $"Type /setbackground. The seeded {Scenarios.SeededLongBackground.Length}-byte profile starts with Mara transmission verified. and ends with END. Click inside the text, press Ctrl+End, and append one exclamation point to make END!. Save.",
            $"Expect 81 ab with length {Scenarios.EditedLongBackground.Length}, chunk size 300 and flags 00 00 00, then 81 ac lengths 300/300/{Scenarios.EditedLongBackground.Length - 600}, 81 96 00 00 01, and server 81 97 00 00 00 00 (Background updated successfully.).",
            """Send {"actor":"QAFriend","action":"profile-long-verify"}. The helper compares the entire saved text, including END!. Relog and reopen /setbackground to check the beginning and end of the saved profile."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", helper);
        await helper.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        Check.Equal(Scenarios.SeededLongBackground, await Scenarios.SavedBackgroundAsync(db, Owner),
            "Complete long background seeded");
        if (context.StockProbe is { } probe)
        {
            await probe.EnterWorldAsync();
            await Scenarios.ProveChunkedBackgroundAsync(probe, db);
        }
        return Array.Empty<byte>();
    }

    public static async Task<string> VerifyAsync(HeadlessPlayer actor, ThrowawayDatabase db)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use QAFriend to check the background.");
        Check.Equal(Scenarios.EditedLongBackground, await Scenarios.SavedBackgroundAsync(db, Owner),
            "The saved background matches every byte of the expected long edit");
        return $"All {Scenarios.EditedLongBackground.Length} bytes saved, ending in END!.";
    }
}
