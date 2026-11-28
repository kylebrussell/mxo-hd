namespace HeadlessClient;

public static partial class Scenarios
{
    private const string AtomicFactionName = "QARetryFaction";

    public static string FactionFoundingFaultSql(uint crew) => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT qa_faction_founder_update
            CHECK (crewId<>{crew} OR cash<>1000 OR factionId=0);
        """;

    private static byte[] FactionFoundRequest(string name, string invitee)
    {
        byte[] factionName = Sized(name);
        List<byte> found = new() { 0x7a };
        found.AddRange(BitConverter.GetBytes((ushort)(5 + factionName.Length)));
        found.AddRange(BitConverter.GetBytes((ushort)5));
        found.AddRange(factionName);
        found.AddRange(Sized(invitee));
        return found.ToArray();
    }

    private static async Task AtomicFactionFounding(ScenarioContext context)
    {
        const int founderCrew = 9101, peerCrew = 9102;
        var db = context.Database;
        await db.ExecuteAsync($"""
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({founderCrew},'Founding Crew','FoundingCaptain',0,1,0,0),
                ({peerCrew},'Peer Crew','PeerCaptain',0,1,0,0);
            """);
        var founder = await context.AddPlayerAsync(new CharacterSeed("FoundingCaptain") { CrewId = founderCrew });
        var peer = await context.AddPlayerAsync(new CharacterSeed("PeerCaptain") { X = 17220, CrewId = peerCrew });
        var member = await context.AddPlayerAsync(new CharacterSeed("CrewMember") { X = 17120, CrewId = founderCrew, Info = 500 });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
            VALUES ({founderCrew},{founder.CharId},1,0,NOW()),({founderCrew},{member.CharId},0,0,NOW()),
                ({peerCrew},{peer.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(FactionFoundingFaultSql(founderCrew));
        await founder.EnterWorldAsync();
        await peer.EnterWorldAsync();
        await member.EnterWorldAsync();

        member.SkipReceived();
        member.SendRpc(FactionFoundRequest(AtomicFactionName, peer.Handle));
        await WaitForFoundingRefusal(member, "Only the captain of an unaffiliated crew can found a faction.");
        uint faction = await ProveAtomicFactionFoundingAsync(founder, peer, db, founderCrew, member);

        peer.SendRpc(FactionFoundRequest(AtomicFactionName, founder.Handle));
        await WaitForFoundingRefusal(peer, "That faction name is invalid or already in use.");
        peer.SendRpc(InviteAnswer(1, 0, founder.Handle));
        await peer.WaitForRpcAsync($"7c {Le(peer.CharId)} {Le(faction)} 02");
        Check.Equal(5L, await db.LongAsync($"SELECT faction_rank FROM crews WHERE id={peerCrew} AND faction_id={faction}"),
            "The invite still brings in a second crew at rank 5");
        founder.SkipReceived();
        founder.SendRpc(FactionFoundRequest("AnotherFaction", peer.Handle));
        await WaitForFoundingRefusal(founder, "Only the captain of an unaffiliated crew can found a faction.", "AnotherFaction");
        Check.Equal((long)faction, await db.LongAsync($"SELECT factionId FROM characters WHERE charId={founder.CharId}"),
            "Refusing a second faction leaves the existing affiliation in place");
    }

    public static async Task<uint> ProveAtomicFactionFoundingAsync(HeadlessPlayer founder, HeadlessPlayer peer,
        ThrowawayDatabase db, uint crew, HeadlessPlayer? member = null)
    {
        await founder.WaitUntilAsync(
            () => founder.Received($"7c {Le(founder.CharId)} 00 00 00 00 01").Count > 0,
            "the unaffiliated faction panel initializer", HeadlessPlayer.DefaultTimeout);
        founder.SkipReceived();
        peer.SkipReceived();
        member?.SkipReceived();

        byte[] request = FactionFoundRequest(AtomicFactionName, peer.Handle);
        founder.SendRpc(request);
        await WaitForFoundingRefusal(founder, "The faction could not be created. Your crew was kept.");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM factions WHERE name='{AtomicFactionName}'"),
            "A failure updating character membership rolls back the inserted faction and the name can be reused");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={crew} AND faction_id=0 AND faction_rank=0"),
            "The founder's crew still has no faction or rank");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE crewId={crew} AND factionId<>0"),
            "All original crew members remain unaffiliated");
        Check.Equal(0, peer.Received("6e 01").Count, "The peer receives no invite for a failed founding");

        // A stock bank action releases the disposable wallet predicate without removing the CHECK.
        founder.SendRpc("78 02 01 00 00 00 01");
        await founder.WaitForRpcAsync($"79 02 01 00 00 00 {Le(founder.CharId)} 01");
        await founder.WaitForRpcAsync($"80 e4 {Le(999u)}");
        founder.SendRpc(request);
        var joined = await founder.WaitForRpcAsync($"7c {Le(founder.CharId)} ?? ?? ?? ?? 01");
        uint faction = BitConverter.ToUInt32(joined.Bytes, 5);
        Check.That(faction != 0, "The exact retry gives the founder a faction");
        await founder.WaitForRpcAsync($"7c {Le(founder.CharId)} {Le(faction)} 02");
        if (member != null) await member.WaitForRpcAsync($"7c {Le(member.CharId)} {Le(faction)} 02");
        await peer.WaitForRpcAsync("6e 01 06 00");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM factions WHERE id={faction} AND name='{AtomicFactionName}'"),
            "One named faction exists after retry");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={crew} AND faction_id={faction} AND faction_rank=1"),
            "The founding crew has rank 1");
        Check.Equal(member == null ? 1L : 2L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE crewId={crew} AND factionId={faction}"),
            "The entire founding crew entered the faction");
        return faction;
    }

    private static async Task WaitForFoundingRefusal(HeadlessPlayer player, string notice, string name = AtomicFactionName)
    {
        var status = await player.WaitForRpcAsync("7b 07 00 01 00 00 00");
        Check.That(status.Bytes.Skip(7).SequenceEqual(Sized(name)), "The creation error identifies the rejected faction name");
        await player.WaitForTextAsync(notice);
        Check.Equal(0, player.Received("6f 01").Count, "A founding refusal is not a declined invitation");
    }
}
