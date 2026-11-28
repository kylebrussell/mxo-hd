using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewFoundingMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;

    public override string Name => "crew-founding-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y},repZion=100,repMachine=100,repMero=100 WHERE charId=110" },
        new[] { $"""
            UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE handle='QAFriend';
            UPDATE characters SET x={PeerX},y={Y},z={Z} WHERE handle='QAOther';
            """, Scenarios.CrewFoundingFaultSql },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV: CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). All are unaffiliated. No teleport or dev command.",
            "People icon > Crew > Modify QARetryCrew, tick Zion, /target QAFriend, then Recruit.",
            """Send {"actor":"QAFriend","action":"crew-founding-accept"} and wait for the reply. The disclosed disposable CHECK refuses assigning QAFriend to a crew. Expect [SOCIAL] Crew founding rolled back, creator result 80 85 with crewID 0/status 1, stock 'Crew could not be created (error = LT_ERROR).' and 'Both memberships were kept.' in chat. Recruit remains active.""",
            """In the same session keep QARetryCrew and Zion, /target QAOther and click Recruit. Send {"actor":"QAOther","action":"crew-founding-accept"}. Expect 80 85 with a nonzero crewID/status 0 and the QARetryCrew roster with CSR as captain and QAOther as member. Relog and verify it persists."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, PeerX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        foreach (string handle in new[] { "QAFriend", "QAOther" })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var refused = context.Helpers["QAFriend"];
        var accepted = context.Helpers["QAOther"];
        Check.Equal(3L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId IN (110,{refused.CharId},{accepted.CharId}) " +
            "AND crewId=0 AND factionId=0 AND repZion=100"), "CSR and both live helpers are eligible and unaffiliated");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.CrewFoundingFault}'"),
            "The disclosed crew affiliation fault is installed");
        if (context.StockProbe is not { } founder) return Array.Empty<byte>();
        await founder.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, founder);
        await Scenarios.ProveAtomicCrewFoundingAsync(founder, refused, accepted, db);
        return Array.Empty<byte>();
    }

    internal static async Task<string> AcceptAsync(HeadlessPlayer actor)
    {
        if (actor.Handle is not ("QAFriend" or "QAOther")) throw new ArgumentException("Use QAFriend or QAOther.");
        await Scenarios.AcceptCrewFoundingAsync(actor, "CSR_Tester");
        return $"{actor.Handle} accepted CSR_Tester's crew invitation";
    }
}
