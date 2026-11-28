using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class BackgroundSaveResultMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120;
    private const uint Owner = 110;

    public override string Name => "background-save-result";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"UPDATE characters SET y={Y} WHERE charId={Owner};" },
        new[] { Scenarios.BackgroundFixtureSql(Owner, "CSR_Tester"),
            $"UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE handle='QAFriend';" },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Verified open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400). No teleport or target.",
            "Type /setbackground to open Background Editor. Expect Saved profile. Replace it with Mara trial and save. The disclosed disposable CHECK refuses this write. Expect 81 97 01 00 00 00 and stock Background update failed. Reopen /setbackground and confirm Saved profile.",
            """Send {"actor":"QAFriend","action":"profile-save","step":"retry"} to verify Saved profile and release the fault. Save Mara trial again. Expect 81 97 00 00 00 00 and stock Background updated successfully.""",
            """Send {"actor":"QAFriend","action":"profile-save","step":"verify"} and relog. /setbackground should open with Mara trial."""
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
        await Scenarios.CheckBackgroundFaultAsync(db);
        Check.Equal(Scenarios.OriginalBackground, await Scenarios.SavedBackgroundAsync(db, Owner), "Background Editor seed text");
        if (context.StockProbe is { } probe)
        {
            await probe.EnterWorldAsync();
            await Scenarios.ProveBackgroundSaveAsync(probe, db);
        }
        return Array.Empty<byte>();
    }

    public static async Task<string> SendActionAsync(HeadlessPlayer actor, JsonElement request, ThrowawayDatabase db)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use QAFriend to check the background.");
        switch (request.GetProperty("step").GetString())
        {
            case "retry":
                Check.Equal(Scenarios.OriginalBackground, await Scenarios.SavedBackgroundAsync(db, Owner),
                    "Original background preserved");
                await Scenarios.ReleaseBackgroundFaultAsync(db);
                return "Saved profile was kept. Fixture fault released.";
            case "verify":
                Check.Equal(Scenarios.UpdatedBackground, await Scenarios.SavedBackgroundAsync(db, Owner),
                    "Updated background committed");
                return "Mara trial is saved.";
            default:
                throw new ArgumentException("Use step retry or verify.");
        }
    }
}
