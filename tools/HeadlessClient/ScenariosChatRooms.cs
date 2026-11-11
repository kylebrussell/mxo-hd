using System.Text;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// A client record with string fields: the fixed part as given (opcode bytes included), then each string as
    /// <c>&lt;u16 length with the NUL&gt; text 00</c>, its offset (from the record's first byte) written at its field.
    /// That is how client.dll's room senders lay them out (docs/CLIENT-ACTIONS.md, "Chat rooms in detail").
    /// </summary>
    private static byte[] WithStrings(byte[] fixedPart, params (int Field, string Text)[] strings)
    {
        List<byte> record = fixedPart.ToList();
        foreach ((int field, string text) in strings)
        {
            record[field] = (byte)record.Count;
            record[field + 1] = (byte)(record.Count >> 8);
            record.AddRange(Sized(text));
        }
        return record.ToArray();
    }

    private static string Address(string world, string name) => "SOE+MXO+" + world + "+" + name;

    /// <summary>81 c0, as /roomcreate builds it (size 0x11): RoomName, Topic, Password, five flags, MaxOccupancy.</summary>
    private static byte[] RoomCreate(string world, string room, string password = "", string topic = "")
    {
        byte[] fixedPart = new byte[0x11];
        fixedPart[0] = 0x81;
        fixedPart[1] = 0xc0;
        fixedPart[8] = (byte)(password.Length > 0 ? 1 : 0);
        List<(int, string)> strings = new() { (2, Address(world, room)) };
        if (password.Length > 0) strings.Add((6, password));
        if (topic.Length > 0) strings.Add((4, topic));
        return WithStrings(fixedPart, strings.ToArray());
    }

    private static byte[] RoomEnter(string world, string room, string password = "") =>
        password.Length > 0
            ? WithStrings(new byte[] { 0x81, 0xc2, 0, 0, 0, 0 }, (2, Address(world, room)), (4, password))
            : WithStrings(new byte[] { 0x81, 0xc2, 0, 0, 0, 0 }, (2, Address(world, room)));

    private static byte[] RoomOnly(byte opcode, string world, string room) =>
        WithStrings(new byte[] { 0x81, opcode, 0, 0 }, (2, Address(world, room)));

    /// <summary>81 cd .. 81 df: PlayerName at +2, RoomName at +4 (81 d7 adds IsTemporaryModerator at +6).</summary>
    private static byte[] RoomPlayer(byte opcode, string world, string player, string room)
    {
        byte[] fixedPart = opcode == 0xd7 ? new byte[7] : new byte[6];
        fixedPart[0] = 0x81;
        fixedPart[1] = opcode;
        return WithStrings(fixedPart, (2, Address(world, player)), (4, Address(world, room)));
    }

    /// <summary>28 18 as /room sends it (0x6216aff0): +3 Text, +5 RoomName, +7 ChatHandle; size 8.</summary>
    private static byte[] RoomLine(string world, string room, string text) =>
        WithStrings(new byte[] { 0x28, 0x18, 0x00, 0, 0, 0, 0, 0x00 }, (3, text), (5, Address(world, room)));

    private static string? OptionalString(byte[] record, int field) =>
        BitConverter.ToUInt16(record, field) == 0 ? null : StringAtOffset(record, field);

    /// <summary>Two players and a room: create, enter, talk, private entry, kick, ban, find, leave, destroy.</summary>
    private static async Task ChatRoomsCreateTalkModerateAndFind(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        HeadlessPlayer alice = players[0], bob = players[1];
        string world = await context.Database.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1");
        const string Ok = "84 00 00 8e";

        // Create: "Created chat room %s."; the same name again fails
        alice.SendRpc(RoomCreate(world, "Lounge", topic: "Hello all"));
        RpcRecord created = await alice.WaitForRpcAsync($"81 c1 ?? ?? {Ok}");
        Check.Equal(Address(world, "Lounge"), StringAtOffset(created.Bytes, 2), "Created room's address");
        bob.SendRpc(RoomCreate(world, "lounge"));
        await bob.WaitForRpcAsync("81 c1 ?? ?? 55 00 00 0e");

        // Speaking in a room you are not in: 81 bf with ROOM_NOTINROOM, nothing to anyone
        bob.SendRpc(RoomLine(world, "Lounge", "early"));
        await bob.WaitForRpcAsync("81 bf 00 4c 00 00 0e");

        // Enter: "You entered" to the newcomer (no name), "%s entered" to those inside
        alice.SendRpc(RoomEnter(world, "Lounge"));
        RpcRecord aliceIn = await alice.WaitForRpcAsync($"81 c3 00 00 ?? ?? {Ok}");
        Check.Equal(Address(world, "Lounge"), StringAtOffset(aliceIn.Bytes, 4), "Entered room's address");
        bob.SendRpc(RoomEnter(world, "LOUNGE"));
        await bob.WaitForRpcAsync($"81 c3 00 00 ?? ?? {Ok}");
        RpcRecord bobIn = await alice.WaitForRpcAsync($"81 c3 ?? ?? ?? ?? {Ok}");
        Check.Equal(Address(world, "Bob"), StringAtOffset(bobIn.Bytes, 2), "Alice hears Bob entered");

        // A room line reaches both, the speaker included, as 2e type 18 with the room's address
        alice.SendRpc(RoomLine(world, "lounge", "room hello"));
        RpcRecord heard = await bob.WaitForTextAsync("room hello");
        Check.Equal((byte)0x2e, heard.Bytes[0], "Room line record");
        Check.Equal((byte)0x18, heard.Bytes[1], "Room line type");
        Check.Equal(alice.CharId, BitConverter.ToUInt32(heard.Bytes, 3), "Room line sender id");
        Check.Equal("Alice", StringAtOffset(heard.Bytes, 7), "Room line sender");
        Check.Equal(Address(world, "Lounge"), StringAtOffset(heard.Bytes, 9), "Room line room");
        await alice.WaitForTextAsync("room hello");

        // Bob can't kick (not a moderator); Alice kicks him: three forms of 81 d2
        bob.SendRpc(RoomPlayer(0xd1, world, "Alice", "Lounge"));
        await bob.WaitForRpcAsync("81 d2 ?? ?? 00 00 ?? ?? 4d 00 00 0e");
        alice.SendRpc(RoomPlayer(0xd1, world, "bob", "Lounge"));
        RpcRecord kicked = await alice.WaitForRpcAsync($"81 d2 ?? ?? 00 00 ?? ?? {Ok}");
        Check.Equal(Address(world, "Bob"), StringAtOffset(kicked.Bytes, 2), "Kick names Bob, spelt as his handle is");
        RpcRecord kickedMe = await bob.WaitForRpcAsync($"81 d2 00 00 ?? ?? ?? ?? {Ok}");
        Check.Equal(Address(world, "Alice"), StringAtOffset(kickedMe.Bytes, 4), "Bob hears who kicked him");
        bob.SendRpc(RoomLine(world, "Lounge", "kicked?"));
        await bob.WaitForRpcAsync("81 bf 00 4c 00 00 0e");

        // Ban keeps him out until unbanned
        alice.SendRpc(RoomPlayer(0xd3, world, "Bob", "Lounge"));
        await alice.WaitForRpcAsync($"81 d4 ?? ?? 00 00 ?? ?? {Ok}");
        await bob.WaitForRpcAsync($"81 d4 00 00");
        bob.SendRpc(RoomEnter(world, "Lounge"));
        await bob.WaitForRpcAsync("81 c3 00 00 ?? ?? 49 00 00 0e");
        alice.SendRpc(RoomPlayer(0xd5, world, "Bob", "Lounge"));
        await alice.WaitForRpcAsync($"81 d6 ?? ?? 00 00 ?? ?? {Ok}");

        // A private room: a wrong password waits at the door and the owner hears the knock; /roomallow lets him in
        alice.SendRpc(RoomCreate(world, "Den", password: "secret"));
        await alice.WaitForRpcAsync($"81 c1 ?? ?? {Ok}");
        alice.SendRpc(RoomEnter(world, "Den"));
        await alice.WaitForRpcAsync($"81 c3 00 00 ?? ?? {Ok}");
        bob.SendRpc(RoomEnter(world, "Den", "guess"));
        await bob.WaitForRpcAsync("81 c3 00 00 ?? ?? 80 00 00 0e");
        RpcRecord knock = await alice.WaitForRpcAsync("81 c5");
        Check.Equal(Address(world, "Bob"), StringAtOffset(knock.Bytes, 2), "The knock names Bob");
        byte[] allow = WithStrings(new byte[] { 0x81, 0xc5, 0, 0, 0, 0, 0 }, (2, Address(world, "Bob")), (4, Address(world, "Den")));
        alice.SendRpc(allow);
        await alice.WaitForRpcAsync($"81 c6 00 00 ?? ?? {Ok}");
        await bob.WaitForRpcAsync($"81 c3 00 00 ?? ?? {Ok}");
        await alice.WaitForRpcAsync($"81 c3 ?? ?? ?? ?? {Ok}");

        // Find: the count, then one 81 e3 per room, flags and occupants filled in
        bob.SendRpc(WithStrings(new byte[] { 0x81, 0xe1, 0, 0, 0, 0 }, (2, "e")));
        await bob.WaitForRpcAsync($"81 e2 02 00 00 00 {Ok}");
        RpcRecord den = await bob.WaitForRpcAsync("81 e3");
        Check.Equal(Address(world, "Den"), StringAtOffset(den.Bytes, 2), "First room found");
        Check.Equal((byte)1, den.Bytes[6], "Den is private");
        Check.Equal(2u, BitConverter.ToUInt32(den.Bytes, 0x0b), "Den's occupants");
        RpcRecord lounge = await bob.WaitForRpcAsync("81 e3");
        Check.Equal("Hello all", StringAtOffset(lounge.Bytes, 4), "Lounge's topic");

        // Leave and destroy
        bob.SendRpc(RoomOnly(0xc7, world, "Den"));
        await bob.WaitForRpcAsync($"81 c8 00 00 ?? ?? {Ok}");
        await alice.WaitForRpcAsync($"81 c8 ?? ?? ?? ?? {Ok}");
        bob.SendRpc(RoomOnly(0xc9, world, "Lounge"));
        await bob.WaitForRpcAsync("81 ca 00 00 ?? ?? 4d 00 00 0e");
        alice.SendRpc(RoomOnly(0xc9, world, "Lounge"));
        await alice.WaitForRpcAsync($"81 ca 00 00 ?? ?? {Ok}");
        alice.SendRpc(RoomEnter(world, "Lounge"));
        await alice.WaitForRpcAsync("81 c3 00 00 ?? ?? 42 00 00 0e");
    }

    /// <summary>?socialbot room: one player's partner enters the room and speaks; it can be kicked like anyone.</summary>
    private static async Task SocialBotInAChatRoom(ScenarioContext context)
    {
        HeadlessPlayer alice = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")))[0];
        string world = await context.Database.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1");
        const string Ok = "84 00 00 8e";

        alice.SendRpc(RoomCreate(world, "Lounge"));
        await alice.WaitForRpcAsync($"81 c1 ?? ?? {Ok}");
        alice.SendRpc(RoomEnter(world, "Lounge"));
        await alice.WaitForRpcAsync($"81 c3 00 00 ?? ?? {Ok}");

        alice.SendChat("?socialbot room Lounge");
        RpcRecord botIn = await alice.WaitForRpcAsync($"81 c3 ?? ?? ?? ?? {Ok}");
        Check.Equal(Address(world, "SocialBot"), StringAtOffset(botIn.Bytes, 2), "The bot entered");
        RpcRecord line = await alice.WaitForTextAsync("Hello from SocialBot.");
        Check.Equal((byte)0x18, line.Bytes[1], "The bot's line is a room line");

        alice.SendRpc(RoomPlayer(0xd1, world, "SocialBot", "Lounge"));
        await alice.WaitForRpcAsync($"81 d2 ?? ?? 00 00 ?? ?? {Ok}");
        alice.SendChat("?socialbot say Lounge still here?");
        await alice.WaitForTextAsync("SocialBot is not in");
    }
}
