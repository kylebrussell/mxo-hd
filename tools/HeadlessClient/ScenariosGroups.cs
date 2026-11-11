namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>72/75 as client.dll's 0x621c0c40 / 0x621c0700 send them: &lt;type&gt; &lt;charId u32&gt;.</summary>
    private static byte[] GroupMemberRecord(byte opcode, byte groupType, uint charId) =>
        new byte[] { opcode, groupType }.Concat(BitConverter.GetBytes(charId)).ToArray();

    /// <summary>The leader hands the team over (75 03), the new leader boots the old one (72 03).</summary>
    private static async Task TeamNewLeaderAndBoot(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        HeadlessPlayer alice = players[0], bob = players[1];

        alice.SendRpc(GroupInvite(Team, "Bob", ""));
        await bob.WaitForRpcAsync("80 8f 0c 00");
        bob.SendRpc(InviteAnswer(Team, 0, "Alice"));
        await bob.WaitForRpcAsync("80 8e");
        await alice.WaitForRpcAsync($"70 03 {Le(bob.CharId)} 09 00");

        // Bob isn't the leader: he can't boot anyone
        bob.SendRpc(GroupMemberRecord(0x72, Team, alice.CharId));
        await bob.WaitForRpcAsync($"6f 03 08 00 {Le(0x0d000039u)}");

        // Alice makes Bob the leader: both hear it
        alice.SendRpc(GroupMemberRecord(0x75, Team, bob.CharId));
        await alice.WaitForRpcAsync($"75 03 {Le(bob.CharId)}");
        await bob.WaitForRpcAsync($"75 03 {Le(bob.CharId)}");

        // Bob boots Alice: both get 72 03 with her id
        bob.SendRpc(GroupMemberRecord(0x72, Team, alice.CharId));
        await alice.WaitForRpcAsync($"72 03 {Le(alice.CharId)}");
        await bob.WaitForRpcAsync($"72 03 {Le(alice.CharId)}");

        // She is off the team: team chat says so, and Bob's team chat reaches nobody
        alice.SendChat("still here?", ChatTeam);
        await alice.WaitForTextAsync("not on a mission team");
    }

    /// <summary>SocialBot joins a team when invited, takes the lead and gives it back, and can be booted.</summary>
    private static async Task SocialBotOnATeam(ScenarioContext context)
    {
        HeadlessPlayer alice = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")))[0];
        alice.SendChat("?socialbot");
        await alice.WaitForTextAsync("SocialBot is next to you.");
        ViewCreation bot = await alice.WaitForViewNamedAsync("SocialBot");
        Check.That(bot.ViewId != 0, "The bot has a view");

        alice.SendRpc(GroupInvite(Team, "SocialBot", ""));
        RpcRecord joined = await alice.WaitForRpcAsync("70 03 ?? ?? ?? ?? 09 00");
        uint botId = BitConverter.ToUInt32(joined.Bytes, 2);

        alice.SendRpc(GroupMemberRecord(0x75, Team, botId));
        await alice.WaitForRpcAsync($"75 03 {Le(botId)}");
        alice.SendChat("?socialbot leader");
        await alice.WaitForRpcAsync($"75 03 {Le(alice.CharId)}");

        alice.SendRpc(GroupMemberRecord(0x72, Team, botId));
        await alice.WaitForRpcAsync($"72 03 {Le(botId)}");
    }

    /// <summary>80 84 as the crew creation window builds it: invitee offset, crew name offset (7), org, then the crew
    /// name and the invitee.</summary>
    private static byte[] CrewCreate(string crewName, string invitee, byte org)
    {
        byte[] name = Sized(crewName);
        List<byte> record = new() { 0x80, 0x84 };
        record.AddRange(BitConverter.GetBytes((ushort)(7 + name.Length)));
        record.AddRange(BitConverter.GetBytes((ushort)7));
        record.Add(org);
        record.AddRange(name);
        record.AddRange(Sized(invitee));
        return record.ToArray();
    }

    /// <summary>The crew creation window (80 84 / 80 85), then first mate (80 87), captain (75 02) and removal (72 02).</summary>
    private static async Task CrewCreateFirstMateCaptainAndRemove(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 },
            new CharacterSeed("Carol") { X = 17220 });
        HeadlessPlayer alice = players[0], bob = players[1], carol = players[2];

        // A declined creation invite: "Crew could not be created.  Invitation was declined."
        alice.SendRpc(CrewCreate("Alpha", "Bob", 1));
        await bob.WaitForRpcAsync("80 88 07 00");
        bob.SendRpc(InviteAnswer(Crew, 0x0d00001e, "Alice"));
        await alice.WaitForRpcAsync($"80 85 00 00 00 00 0d 00 01 {Le(0x0d00001eu)}");

        // Accepted: Alice gets 80 85 with the new crew and status 0, Bob the member list
        alice.SendRpc(CrewCreate("Alpha", "Bob", 1));
        await bob.WaitForRpcAsync("80 88 07 00");
        bob.SendRpc(InviteAnswer(Crew, 0, "Alice"));
        RpcRecord created = await alice.WaitForRpcAsync("80 85 ?? ?? ?? ?? 0d 00 01 00 00 00 00");
        uint crewId = BitConverter.ToUInt32(created.Bytes, 2);
        Check.Equal((long)crewId, await context.Database.LongAsync("SELECT id FROM crews WHERE crew_name='Alpha'"), "80 85's crew id");
        await bob.WaitForRpcAsync("80 86");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT org FROM crews WHERE id={crewId}"), "The organisation the window picked");

        // A taken name, and a creator already in a crew
        carol.SendRpc(CrewCreate("alpha", "Alice", 1));
        await carol.WaitForRpcAsync($"80 85 00 00 00 00 0d 00 01 {Le(0x0d000021u)}");
        alice.SendRpc(CrewCreate("Beta", "Carol", 1));
        await alice.WaitForRpcAsync($"80 85 00 00 00 00 0d 00 01 {Le(0x0d000020u)}");

        // Carol joins the usual way
        alice.SendRpc(GroupInvite(Crew, "Carol", ""));
        await carol.WaitForRpcAsync("80 88 07 00");
        carol.SendRpc(InviteAnswer(Crew, 0, "Alice"));
        await carol.WaitForRpcAsync("80 86");

        // First mate: every member hears 80 87
        alice.SendRpc(new byte[] { 0x80, 0x87 }.Concat(BitConverter.GetBytes(bob.CharId)).ToArray());
        await alice.WaitForRpcAsync($"80 87 {Le(bob.CharId)}");
        await bob.WaitForRpcAsync($"80 87 {Le(bob.CharId)}");
        await carol.WaitForRpcAsync($"80 87 {Le(bob.CharId)}");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT is_first_mate FROM crew_members WHERE char_id={bob.CharId}"), "Bob is first mate");

        // A member can't name one
        carol.SendRpc(new byte[] { 0x80, 0x87, 0, 0, 0, 0 });
        await Task.Delay(300);
        Check.Equal(1L, await context.Database.LongAsync($"SELECT is_first_mate FROM crew_members WHERE char_id={bob.CharId}"), "Still first mate");

        // Bob becomes captain (and stops being first mate)
        alice.SendRpc(GroupMemberRecord(0x75, Crew, bob.CharId));
        await alice.WaitForRpcAsync($"75 02 {Le(bob.CharId)}");
        await carol.WaitForRpcAsync($"75 02 {Le(bob.CharId)}");
        Check.Equal("Bob", await context.Database.ScalarAsync($"SELECT master_player_handle FROM crews WHERE id={crewId}"), "The captain in the database");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT is_first_mate FROM crew_members WHERE char_id={bob.CharId}"), "The captain isn't first mate");

        // The old captain can't remove anyone now; the new one removes Alice and everyone hears it
        alice.SendRpc(GroupMemberRecord(0x72, Crew, carol.CharId));
        bob.SendRpc(GroupMemberRecord(0x72, Crew, alice.CharId));
        await alice.WaitForRpcAsync($"72 02 {Le(alice.CharId)}");
        await bob.WaitForRpcAsync($"72 02 {Le(alice.CharId)}");
        await carol.WaitForRpcAsync($"72 02 {Le(alice.CharId)}");
        Check.Equal(0, carol.Received($"72 02 {Le(carol.CharId)}").Count, "Carol was not removed by Alice");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT crewId FROM characters WHERE charId={alice.CharId}"), "Alice's crew in the database");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={crewId}"), "Members left");
    }

    /// <summary>Two crews in a faction: the leading crew's captain promotes the other (80 80 / 7f), the other captain
    /// can't (80 81), and the leader removes the crew from the faction (73).</summary>
    private static async Task FactionRanksAndRemovingACrew(ScenarioContext context)
    {
        // The crews exist before the captains log in, as they would
        HeadlessPlayer alice = await context.AddPlayerAsync(new CharacterSeed("Alice") { CrewId = 9001 });
        HeadlessPlayer bob = await context.AddPlayerAsync(new CharacterSeed("Bob") { X = 17120, CrewId = 9002 });
        await context.Database.ExecuteAsync($@"
INSERT INTO crews (id, crew_name, master_player_handle, money, org, faction_id, faction_rank) VALUES
  (9001, 'Alpha', 'Alice', 0, 1, 0, 0), (9002, 'Beta', 'Bob', 0, 1, 0, 0);
INSERT INTO crew_members (crew_id, char_id, is_captain, is_first_mate, created_at) VALUES
  (9001, {alice.CharId}, 1, 0, NOW()), (9002, {bob.CharId}, 1, 0, NOW());");
        await alice.EnterWorldAsync();
        await bob.EnterWorldAsync();

        // Alice founds a faction with her crew and invites Bob's (7a, then 6e 01 / 6f 01)
        byte[] factionName = Sized("Union");
        List<byte> found = new() { 0x7a };
        found.AddRange(BitConverter.GetBytes((ushort)(5 + factionName.Length)));
        found.AddRange(BitConverter.GetBytes((ushort)5));
        found.AddRange(factionName);
        found.AddRange(Sized("Bob"));
        alice.SendRpc(found.ToArray());
        await bob.WaitForRpcAsync("6e 01 06 00");
        bob.SendRpc(InviteAnswer(1, 0, "Alice"));
        await bob.WaitForRpcAsync("7c ?? ?? ?? ?? ?? ?? ?? ?? 02");
        long factionId = await context.Database.LongAsync("SELECT id FROM factions WHERE name='Union'");
        Check.Equal(1L, await context.Database.LongAsync("SELECT faction_rank FROM crews WHERE id=9001"), "The founder's crew leads");
        Check.Equal(5L, await context.Database.LongAsync("SELECT faction_rank FROM crews WHERE id=9002"), "The newcomer starts at the bottom");

        // Bob's crew ranks below Alice's: he can't promote hers
        bob.SendRpc(new byte[] { 0x80, 0x80 }.Concat(BitConverter.GetBytes(9001u)).Append((byte)1).ToArray());
        await bob.WaitForRpcAsync($"80 81 {Le(9001u)} 01 {Le(0x0d000014u)}");

        // Alice promotes Bob's crew: 7f to every member of the faction
        alice.SendRpc(new byte[] { 0x80, 0x80 }.Concat(BitConverter.GetBytes(9002u)).Append((byte)1).ToArray());
        await alice.WaitForRpcAsync($"7f {Le(9002u)} 04");
        await bob.WaitForRpcAsync($"7f {Le(9002u)} 04");
        Check.Equal(4L, await context.Database.LongAsync("SELECT faction_rank FROM crews WHERE id=9002"), "Promoted in the database");

        // Bob can't remove a crew (his isn't leading); Alice removes his
        bob.SendRpc(new byte[] { 0x73 }.Concat(BitConverter.GetBytes(9001u)).ToArray());
        await bob.WaitForRpcAsync($"74 {Le(9001u)} {Le(0x0d000014u)}");
        alice.SendRpc(new byte[] { 0x73 }.Concat(BitConverter.GetBytes(9002u)).ToArray());
        await alice.WaitForRpcAsync($"73 {Le(9002u)}");
        await bob.WaitForRpcAsync($"73 {Le(9002u)}");
        Check.Equal(0L, await context.Database.LongAsync("SELECT faction_id FROM crews WHERE id=9002"), "Beta out of the faction");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT factionId FROM characters WHERE charId={bob.CharId}"), "Bob out of the faction");
        Check.Equal(factionId, await context.Database.LongAsync("SELECT faction_id FROM crews WHERE id=9001"), "Alpha still in it");
    }

    /// <summary>80 92 as the Find Team Members window sends it: min, max, my faction only, then two empty arrays.</summary>
    private static byte[] FindSeekers(byte min, byte max, bool myFaction = false, byte[]? orgs = null)
    {
        List<byte> record = new() { 0x80, 0x92, min, max, (byte)(myFaction ? 1 : 0), 9, 0, 11, 0, 0, 0 };
        orgs ??= Array.Empty<byte>();
        record.AddRange(BitConverter.GetBytes((ushort)orgs.Length));
        record.AddRange(orgs);
        return record.ToArray();
    }

    /// <summary>The team window's Seek (80 91) and the Find Team Members search (80 92 / 80 93).</summary>
    private static async Task TeamSeekersList(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice") { Level = 20 },
            new CharacterSeed("Bob") { X = 17120, Level = 12, Organization = 1 });
        HeadlessPlayer alice = players[0], bob = players[1];

        alice.SendRpc(FindSeekers(1, 50));
        await alice.WaitForRpcAsync("80 93 04 00 00 00");

        // Bob flags himself (80 91 00, org list with his own organisation, as the client fills it)
        bob.SendRpc("80 91 00 00 00 07 00 01 00 01");
        await Task.Delay(300);
        alice.SendRpc(FindSeekers(5, 15));
        RpcRecord found = await alice.WaitForRpcAsync("80 93 04 00 01 00");
        Check.Equal(bob.CharId, BitConverter.ToUInt32(found.Bytes, 7), "The seeker's id");
        Check.Equal("Bob", System.Text.Encoding.ASCII.GetString(found.Bytes, 11, 3), "The seeker's handle");
        Check.Equal((byte)12, found.Bytes[6 + 0x25], "The seeker's level");

        // Out of the level range, another organisation, or only my faction: nobody
        alice.SendRpc(FindSeekers(20, 30));
        await alice.WaitForRpcAsync("80 93 04 00 00 00");
        alice.SendRpc(FindSeekers(1, 50, orgs: new byte[] { 2 }));
        await alice.WaitForRpcAsync("80 93 04 00 00 00");
        alice.SendRpc(FindSeekers(1, 50, myFaction: true));
        await alice.WaitForRpcAsync("80 93 04 00 00 00");
        alice.SendRpc(FindSeekers(1, 50, orgs: new byte[] { 1 }));
        await alice.WaitForRpcAsync("80 93 04 00 01 00");

        // Bob takes himself off the list
        bob.SendRpc("80 91 01 00 00 00 00");
        await Task.Delay(300);
        alice.SendRpc(FindSeekers(1, 50));
        await alice.WaitForRpcAsync("80 93 04 00 00 00");
    }
}
