using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewEjectionMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120;
    private const string Target = "QAFriend";
    private const uint Crew = Scenarios.CrewEjectionCrew, Faction = Scenarios.CrewEjectionFaction;

    public override string Name => "crew-member-removal-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y} WHERE charId=110" },
        new[] { Scenarios.CrewEjectionSeedSql("CSR_Tester"), $"""
            UPDATE characters SET crewId={Crew},factionId={Faction} WHERE charId=110;
            UPDATE characters SET x={FriendX},y={Y},z={Z},crewId={Crew},factionId={Faction},cash=1000 WHERE handle='{Target}';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT {Crew},charId,(charId=110),0,NOW() FROM characters WHERE charId=110 OR handle='{Target}';
            """, Scenarios.CrewEjectionFaultSql },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "CSR and QAFriend start on open Mara NAV at (17000,495,6400) and (17120,495,6400). No teleport, world selection or dev command. CSR captains QACrew in QAFaction. QAFriend is on the Crew roster and the bank has 200 Info.",
            "Open Crew from the people icon above chat. Right-click QAFriend in the member roster and choose Remove. Confirm if asked. A disclosed disposable SQL CHECK rejects the character update after the roster delete. Expect [SOCIAL] Crew member removal rolled back, 'The roster was kept.' in chat, no server 72 02, and QAFriend still in the list.",
            """Send {"actor":"QAFriend","action":"crew-removal-retry"} and wait for "QAFriend deposited 1 Info (999 remains)". The bank becomes 201 and the fixture now allows the update.""",
            "Remove QAFriend again in the same session. Expect server 72 02 with QAFriend's ID and exactly that row disappears. CSR stays captain in the same faction. Reopen Crew after relog and check QAFriend is still absent and the bank is 201."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        var member = await HeadlessPlayer.LoginAsync(context.Address, Target, Target);
        context.Helpers.Add(Target, member);
        await member.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, member);
        string memberId = WireFormat.ToHex(BitConverter.GetBytes(member.CharId));
        string crewId = WireFormat.ToHex(BitConverter.GetBytes(Crew));
        await member.WaitForRpcAsync($"80 86 {memberId} {crewId}");
        Check.Equal(2L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p
            JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=p.crewId
            JOIN crews c ON c.id=m.crew_id AND c.faction_id=p.factionId
            WHERE c.id={Crew} AND c.faction_id={Faction} AND c.money=200
              AND ((p.charId=110 AND m.is_captain=1) OR (p.charId={member.CharId} AND m.is_captain=0 AND p.cash=1000))
            """), "The crew roster contains CSR and QAFriend with the expected balance");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.CrewEjectionFault}'"),
            "The disposable crew departure fault is installed");
        if (context.StockProbe is not { } leader) return Array.Empty<byte>();
        await leader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, leader);
        await Scenarios.ProveAtomicCrewEjectionAsync(leader, member, db);
        return Array.Empty<byte>();
    }

    internal static async Task<string> ReleaseFaultAsync(HeadlessPlayer actor)
    {
        if (actor.Handle != Target) throw new ArgumentException("Use QAFriend for the crew removal retry.");
        await Scenarios.DepositOneCrewInfoAsync(actor);
        return "QAFriend deposited 1 Info (999 remains)";
    }
}
