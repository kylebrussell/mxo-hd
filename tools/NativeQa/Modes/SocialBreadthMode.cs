using System.Text;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public class SocialBreadthMode : NativeQaMode
{
    protected const uint CaptainCrew = 9101;
    private const uint PeerCrew = 9102;
    private const uint FactionId = 9103;
    private const string Captain = "CSR_Tester";
    private const string Member = "QAFriend";
    private const string PeerCaptain = "QAOther";
    private const string Hug = "fd 0d 00 58";
    private const byte Crew = 2, Faction = 1;
    private readonly bool seedFaction;

    public override string Name => "social-breadth";
    public override NativeQaModePlan Plan { get; }

    public SocialBreadthMode() : this(seedFaction: true) { }

    protected SocialBreadthMode(bool seedFaction, bool atomicFounding = false)
    {
        this.seedFaction = seedFaction;
        uint faction = seedFaction ? FactionId : 0;
        int captainRank = seedFaction ? 1 : 0;
        int peerRank = seedFaction ? 5 : 0;
        string factionInsert = seedFaction
            ? $"INSERT INTO factions (id,name,master_player_handle,money) VALUES ({FactionId},'QAFaction','{Captain}',0);"
            : string.Empty;
        string instructions = seedFaction
            ? "CSR_Tester captains QACrew, the leading crew of the pre-seeded QAFaction. QAOther captains QAPeerCrew at rank 5."
            : "CSR_Tester and QAOther captain unaffiliated crews. Target QAOther, open Organization → Faction, use Name, then Recruit.";
        int z = atomicFounding ? 6400 : 2300;
        string[] atomicSteps = {
            "CSR and QAOther face one another on open Mara NAV at (17000,495,6400) and (17220,495,6400). Use /target QAOther. Open the people icon above chat, then Organization > Faction.",
            "Use Modify to enter QARetryFaction. Click Recruit with QAOther targeted. The disposable SQL CHECK refuses the character membership write. Expect [SOCIAL] Faction founding rolled back, 'Faction could not be created.' and 'Your crew was kept.' in chat, creation error 7b and no invite. Verify Modify and Recruit become active in the same session.",
            "Open Crew from the people icon. Deposit 1 Info to move from 1000 to 999 and clear the fixture's fault predicate. Crew bank is now 1.",
            "Retarget QAOther if needed, open Faction, set the same name QARetryFaction and click Recruit again. Expect a 7c faction status for CSR and 6e 01 to QAOther.",
            """After the retry, send {"actor":"QAOther","action":"social","step":"faction-answer","accept":true}. Choose More in Faction. Expect QACrew at rank 1 and QAPeerCrew at rank 5. Check the roster after relog."""
        };
        var membership = new[] { $"""
            {factionInsert}
            INSERT INTO crews (id,crew_name,master_player_handle,money,org,faction_id,faction_rank) VALUES
                ({CaptainCrew},'QACrew','{Captain}',0,1,{faction},{captainRank}),
                ({PeerCrew},'QAPeerCrew','{PeerCaptain}',0,1,{faction},{peerRank});
            UPDATE characters SET crewId={CaptainCrew},factionId={faction} WHERE charId=110;
            UPDATE characters SET x=17120,y=495,z={z} WHERE handle='{Member}';
            UPDATE characters SET x=17220,y=495,z={z},crewId={PeerCrew},factionId={faction} WHERE handle='{PeerCaptain}';
            INSERT INTO crew_members (crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,1,0,NOW() FROM characters WHERE charId=110 OR handle='{PeerCaptain}';
            """ };
        if (atomicFounding)
            membership = membership.Append("""
                UPDATE characters SET cash=1000,rotation=64 WHERE charId=110;
                UPDATE characters SET rotation=192 WHERE handle='QAOther';
                UPDATE characters SET x=16880 WHERE handle='QAFriend';
                """)
                .Append(Scenarios.FactionAffiliationFaultSql(CaptainCrew)).ToArray();
        Plan = new(new ushort[] { 2 }, 17000, z,
            Array.Empty<string>(), new[] { "UPDATE characters SET y=495 WHERE charId=110;" },
            membership,
            environmentOverrides: atomicFounding ? new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" } : null,
            readyMessages: atomicFounding ? atomicSteps : new[] { instructions + " See docs/GAMEPLAY-BREADTH-PEERS.md." });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Social QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Social QA requires a live server.");
        foreach (string name in new[] { Member, PeerCaptain })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var member = context.Helpers[Member];
        var captain = context.Helpers[PeerCaptain];
        await member.WaitForViewNamedAsync(PeerCaptain);
        await captain.WaitForViewNamedAsync(Member);
        if (seedFaction)
            await CheckSeededFactionAsync(database, captain);
        else
        {
            var empty = await captain.WaitForRpcAsync($"7c {Le(captain.CharId)} 00 00 00 00 01 00 00 00 00 00");
            Check.Equal(15, empty.Bytes.Length, "QAOther received empty faction state at login");
            Check.Equal(2L, await database.LongAsync($"""
                SELECT COUNT(*) FROM characters c JOIN crew_members m ON m.char_id=c.charId AND m.crew_id=c.crewId
                  JOIN crews r ON r.id=c.crewId
                WHERE m.is_captain=1 AND r.org=1 AND r.master_player_handle=c.handle
                  AND c.factionId=0 AND r.faction_id=0 AND r.faction_rank=0
                  AND ((c.charId=110 AND r.id={CaptainCrew}) OR (c.charId={captain.CharId} AND r.id={PeerCrew}));
                """), "Both unaffiliated crew captains are ready to found a faction");
        }
        Check.Equal(1L, await database.LongAsync($"""
            SELECT COUNT(*) FROM characters WHERE charId={member.CharId} AND crewId=0 AND factionId=0;
            """), "QAFriend is available for a crew invitation");
        member.SkipReceived();
        captain.SkipReceived();
        return Array.Empty<byte>();
    }

    private static async Task CheckSeededFactionAsync(ThrowawayDatabase database, HeadlessPlayer captain)
    {
        await captain.WaitForRpcAsync($"7c {Le(captain.CharId)} {Le(FactionId)} 01");
        var roster = await captain.WaitForRpcAsync($"7c {Le(captain.CharId)} {Le(FactionId)} 02");
        const int rosterHeader = 17, crewSize = 81;
        Check.Equal(rosterHeader + 2 * crewSize, roster.Bytes.Length, "QAOther received a two-crew faction roster");
        var ranks = Enumerable.Range(0, 2).Select(row => rosterHeader + row * crewSize)
            .ToDictionary(offset => BitConverter.ToUInt32(roster.Bytes, offset), offset => roster.Bytes[offset + crewSize - 1]);
        Check.That(ranks.GetValueOrDefault(CaptainCrew) == 1 && ranks.GetValueOrDefault(PeerCrew) == 5,
            "QAOther received both starting ranks at login");
        Check.Equal(2L, await database.LongAsync($"""
            SELECT COUNT(*) FROM characters c JOIN crew_members m ON m.char_id=c.charId AND m.crew_id=c.crewId
              JOIN crews r ON r.id=c.crewId JOIN factions f ON f.id=r.faction_id AND f.id=c.factionId
            WHERE m.is_captain=1 AND r.org=1 AND r.master_player_handle=c.handle
              AND f.id={FactionId} AND f.name='QAFaction' AND f.master_player_handle='{Captain}'
              AND ((c.charId=110 AND r.id={CaptainCrew} AND r.faction_rank=1)
                OR (c.charId={captain.CharId} AND r.id={PeerCrew} AND r.faction_rank=5));
            """), "The seeded faction has both captains and their expected ranks");
    }

    public static async Task<object> SendActionAsync(HeadlessPlayer actor, JsonElement request)
    {
        string? step = request.GetProperty("step").GetString();
        switch (step)
        {
            case "crew-answer" when actor.Handle == Member:
                return await ReplyToInviteAsync(actor, Crew, "80 88 07 00", request.GetProperty("accept").GetBoolean());
            case "faction-answer" when actor.Handle == PeerCaptain:
                return await ReplyToInviteAsync(actor, Faction, "6e 01 06 00", request.GetProperty("accept").GetBoolean());
            case "crew-chat" when actor.Handle == Member:
                actor.SendChat("QAFriend crew chat check", 0x12);
                return "crew message sent";
            case "faction-chat" when actor.Handle == PeerCaptain:
                actor.SendChat("QAOther faction chat check", 0x13);
                return "faction message sent";
            case "rank-refusal" when actor.Handle == PeerCaptain:
                actor.SendRpc($"80 80 {Le(CaptainCrew)} 01");
                await actor.WaitForRpcAsync($"80 81 {Le(CaptainCrew)} 01 14 00 00 0d");
                return "server refused a subordinate captain's rank change";
            case "hug-answer" when actor.Handle == Member:
            {
                var target = await actor.WaitForViewNamedAsync(Captain);
                string view = ViewHandle(target);
                await actor.WaitForRpcAsync($"31 {Hug} {view}");
                byte reply = request.GetProperty("accept").GetBoolean() ? (byte)1 : (byte)2;
                actor.SendRpc($"32 {reply:x2} {Hug} {view} 00 00 00 00 00");
                return "hug answer sent";
            }
            case "hug-ask" when actor.Handle == Member:
            {
                var target = await actor.WaitForViewNamedAsync(Captain);
                actor.SendRpc($"30 {Hug} {ViewHandle(target)}");
                return "hug invitation sent";
            }
            default:
                throw new ArgumentException("Unknown social step or the selected helper cannot perform it.");
        }
    }

    private static async Task<string> ReplyToInviteAsync(HeadlessPlayer actor, byte group, string pattern, bool accept)
    {
        var prefix = BytePattern.Parse(pattern);
        await actor.WaitForRpcAsync(r => prefix.IsMatch(r.Bytes) &&
            BytePattern.IndexOf(r.Bytes, Encoding.ASCII.GetBytes(Captain + "\0"), 0) >= 0,
            $"pending group invitation from {Captain}");
        uint result = accept ? 0u : 0x0d00001eu;
        actor.SendRpc(new byte[] { 0x6f, group, 8, 0 }.Concat(BitConverter.GetBytes(result))
            .Concat(BitConverter.GetBytes((ushort)(Captain.Length + 1))).Concat(Encoding.ASCII.GetBytes(Captain)).Append((byte)0).ToArray());
        return accept ? "accepted" : "declined";
    }

    private static string ViewHandle(ViewCreation view) =>
        $"{WireFormat.ToHex(BitConverter.GetBytes(view.ViewId))} {view.SpawnId:x2} 00";
    private static string Le(uint value) => WireFormat.ToHex(BitConverter.GetBytes(value));
}

public sealed class FactionFoundingMode : SocialBreadthMode
{
    public FactionFoundingMode() : base(seedFaction: false) { }

    public override string Name => "faction-founding";
}

public sealed class AtomicFactionFoundingMode : SocialBreadthMode
{
    public AtomicFactionFoundingMode() : base(seedFaction: false, atomicFounding: true) { }

    public override string Name => "faction-founding-atomic";

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        await base.AfterStartAsync(context);
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { Plan.X, 16880, 17220 })
            Check.That(NavMesh.Locate(1, x, 495, Plan.Z, out var stand) && !stand.indoors && stand.floorY + 95 == 495,
                "CSR and the helpers start on open NAV");
        var db = context.OwnedDatabase!;
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='qa_faction_founder_update'"),
            "The disclosed membership write fault is installed");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await actor.WaitForViewNamedAsync("QAOther");
        await Scenarios.ProveAtomicFactionFoundingAsync(actor, context.Helpers["QAOther"], db, CaptainCrew);
        return Array.Empty<byte>();
    }
}
