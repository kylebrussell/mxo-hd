using System.Text;
using System.Text.Json;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class SocialBreadthMode : NativeQaMode
{
    private const uint CaptainCrew = 9101;
    private const uint PeerCrew = 9102;
    private const string Captain = "CSR_Tester";
    private const string Member = "QAFriend";
    private const string PeerCaptain = "QAOther";
    private const string Hug = "fd 0d 00 58";
    private const byte Crew = 2, Faction = 1;

    public override string Name => "social-breadth";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { "UPDATE characters SET y=495 WHERE charId=110;" },
        new[] { $"""
            INSERT INTO crews (id,crew_name,master_player_handle,money,org,faction_id,faction_rank) VALUES
                ({CaptainCrew},'QACrew','{Captain}',0,1,0,0),({PeerCrew},'QAPeerCrew','{PeerCaptain}',0,1,0,0);
            UPDATE characters SET crewId={CaptainCrew} WHERE charId=110;
            UPDATE characters SET x=17120,y=495,z=2300 WHERE handle='{Member}';
            UPDATE characters SET x=17220,y=495,z=2300,crewId={PeerCrew} WHERE handle='{PeerCaptain}';
            INSERT INTO crew_members (crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,1,0,NOW() FROM characters WHERE charId=110 OR handle='{PeerCaptain}';
            """ },
        readyMessages: new[] { "CSR_Tester captains QACrew. Invite nearby QAFriend to the crew and QAOther's crew to a faction. "
            + "Run the chat, rank and paired-emote checks in docs/GAMEPLAY-BREADTH-PEERS.md." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Social QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Social QA requires a live server.");
        foreach (string name in new[] { Member, PeerCaptain })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, player);
            await player.EnterWorldAsync();
            player.SkipReceived();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var member = context.Helpers[Member];
        var captain = context.Helpers[PeerCaptain];
        await member.WaitForViewNamedAsync(PeerCaptain);
        await captain.WaitForViewNamedAsync(Member);
        Check.Equal(2L, await database.LongAsync($"""
            SELECT COUNT(*) FROM characters c JOIN crew_members m ON m.char_id=c.charId AND m.crew_id=c.crewId
              JOIN crews r ON r.id=c.crewId
            WHERE m.is_captain=1 AND r.org=1 AND r.faction_id=0 AND r.master_player_handle=c.handle
              AND ((c.charId=110 AND r.id={CaptainCrew}) OR (c.charId={captain.CharId} AND r.id={PeerCrew}));
            """), "Both starting crew captains have the expected authority");
        Check.Equal(1L, await database.LongAsync($"""
            SELECT COUNT(*) FROM characters WHERE charId={member.CharId} AND crewId=0 AND factionId=0;
            """), "QAFriend is available for a crew invitation");
        return Array.Empty<byte>();
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
