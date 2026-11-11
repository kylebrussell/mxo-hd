using System;
using System.Text;
using hds;
using hds.world.Structures;

namespace hds.Tests;

// Chat rooms: the rules are ours (no retail capture has a room); the record layouts are client.dll's
// (docs/CLIENT-ACTIONS.md, "Chat rooms in detail").
public class ChatRoomTests
{
    private static string H(byte[] bytes) => Convert.ToHexString(bytes);
    private static uint Ok => ChatRooms.Status.Success;

    private static ushort U16(byte[] b, int at) => (ushort)(b[at] | (b[at + 1] << 8));
    private static uint U32(byte[] b, int at) => BitConverter.ToUInt32(b, at);

    /// <summary>The sized string a u16 offset field points at; null for offset 0.</summary>
    private static string? Str(byte[] record, int field)
    {
        int offset = U16(record, field);
        if (offset == 0)
        {
            return null;
        }
        int size = U16(record, offset);
        Assert.Equal(0, record[offset + 2 + size - 1]);
        return Encoding.UTF8.GetString(record, offset + 2, size - 1);
    }

    [Fact]
    public void CreateEnterLeaveAndTheLastOneOutClosesTheRoom()
    {
        ChatRooms rooms = new ChatRooms();
        Assert.Equal(Ok, rooms.Create("Alice", "Lounge", "", "Talk here"));
        Assert.Equal(ChatRooms.Status.AlreadyExists, rooms.Create("Bob", "lounge", "", ""));
        Assert.Equal(ChatRooms.Status.InvalidName, rooms.Create("Bob", " ", "", ""));

        Assert.Equal(Ok, rooms.Enter("Alice", "LOUNGE", "", out ChatRoom? entered));
        Assert.Equal("Lounge", entered!.Name);
        Assert.Equal(ChatRooms.Status.AlreadyInRoom, rooms.Enter("alice", "Lounge", "", out _));
        Assert.Equal(Ok, rooms.Enter("Bob", "Lounge", "", out _));
        Assert.Equal(new[] { "Alice", "Bob" }, rooms.OccupantsOf("lounge"));

        Assert.Equal(Ok, rooms.CanSpeak("bob", "Lounge"));
        Assert.Equal(ChatRooms.Status.NotInRoom, rooms.CanSpeak("Carol", "Lounge"));
        Assert.Equal(ChatRooms.Status.RoomDoesntExist, rooms.CanSpeak("Bob", "Nowhere"));

        Assert.Equal(ChatRooms.Status.NotInRoom, rooms.Leave("Carol", "Lounge"));
        Assert.Equal(Ok, rooms.Leave("Alice", "Lounge"));
        Assert.Equal(Ok, rooms.Leave("Bob", "Lounge"));
        Assert.Null(rooms.Find("Lounge"));
        Assert.Equal(ChatRooms.Status.RoomDoesntExist, rooms.Enter("Bob", "Lounge", "", out _));
    }

    [Fact]
    public void PrivateRoomsTakeThePasswordAnInviteOrAModeratorsAllow()
    {
        ChatRooms rooms = new ChatRooms();
        Assert.Equal(Ok, rooms.Create("Alice", "Den", "secret", ""));
        Assert.True(rooms.Find("Den")!.IsPrivate);
        Assert.Equal(Ok, rooms.Enter("Alice", "Den", "", out _));          // the owner needs no password
        Assert.Equal(Ok, rooms.Enter("Bob", "Den", "secret", out _));

        Assert.Equal(ChatRooms.Status.WaitingForEntry, rooms.Enter("Carol", "Den", "wrong", out _));
        Assert.Equal(ChatRooms.Status.NoPrivileges, rooms.Allow("Bob", "Den", "Carol", out _));
        Assert.Equal(ChatRooms.Status.NotEntering, rooms.Allow("Alice", "Den", "Dave", out _));
        Assert.Equal(Ok, rooms.Allow("Alice", "Den", "carol", out ChatRoom? den));
        Assert.True(den!.Has("Carol"));

        Assert.Equal(Ok, rooms.Apply(ChatRooms.Change.Invite, "Alice", "Den", "Dave", true));
        Assert.Equal(ChatRooms.Status.DuplicateInvite, rooms.Apply(ChatRooms.Change.Invite, "Alice", "Den", "dave", true));
        Assert.Equal(Ok, rooms.Enter("Dave", "Den", "", out _));
        Assert.Equal(ChatRooms.Status.NotInvited, rooms.Apply(ChatRooms.Change.Uninvite, "Alice", "Den", "Erin", true));
    }

    [Fact]
    public void ModeratorsKickAndBanOnlyTheOwnerHandsOutModeratorsAndTheRoom()
    {
        ChatRooms rooms = new ChatRooms();
        rooms.Create("Alice", "Hall", "", "");
        foreach (string who in new[] { "Alice", "Bob", "Carol" })
        {
            Assert.Equal(Ok, rooms.Enter(who, "Hall", "", out _));
        }

        Assert.Equal(ChatRooms.Status.NoPrivileges, rooms.Apply(ChatRooms.Change.Kick, "Bob", "Hall", "Carol", true));
        Assert.Equal(ChatRooms.Status.AvatarDoesntExist, rooms.Apply(ChatRooms.Change.Kick, "Alice", "Hall", "Nobody", false));
        Assert.Equal(Ok, rooms.Apply(ChatRooms.Change.Moderator, "Alice", "Hall", "Bob", true, temporary: true));
        Assert.True(rooms.Find("Hall")!.Moderators["bob"]);
        Assert.Equal(ChatRooms.Status.DuplicateModerator, rooms.Apply(ChatRooms.Change.Moderator, "Alice", "Hall", "Bob", true));
        Assert.Equal(ChatRooms.Status.NoPrivileges, rooms.Apply(ChatRooms.Change.Moderator, "Bob", "Hall", "Carol", true));

        Assert.Equal(Ok, rooms.Apply(ChatRooms.Change.Kick, "Bob", "Hall", "Carol", true));
        Assert.Equal(ChatRooms.Status.DestNotInRoom, rooms.Apply(ChatRooms.Change.Kick, "Bob", "Hall", "Carol", true));
        Assert.Equal(Ok, rooms.Apply(ChatRooms.Change.Ban, "Bob", "Hall", "Carol", true));
        Assert.Equal(ChatRooms.Status.DuplicateBan, rooms.Apply(ChatRooms.Change.Ban, "Bob", "Hall", "Carol", true));
        Assert.Equal(ChatRooms.Status.Banned, rooms.Enter("Carol", "Hall", "", out _));
        Assert.Equal(Ok, rooms.Apply(ChatRooms.Change.Unban, "Alice", "Hall", "Carol", true));
        Assert.Equal(ChatRooms.Status.NotBanned, rooms.Apply(ChatRooms.Change.Unban, "Alice", "Hall", "Carol", true));
        Assert.Equal(Ok, rooms.Enter("Carol", "Hall", "", out _));

        Assert.Equal(ChatRooms.Status.NoPrivileges, rooms.Destroy("Bob", "Hall", out _));
        Assert.Equal(Ok, rooms.Apply(ChatRooms.Change.Owner, "Alice", "Hall", "Bob", true));
        Assert.Equal(ChatRooms.Status.NoPrivileges, rooms.Destroy("Alice", "Hall", out _));
        Assert.Equal(Ok, rooms.Destroy("Bob", "Hall", out ChatRoom? destroyed));
        Assert.Equal(3, destroyed!.Occupants.Count);
        Assert.Null(rooms.Find("Hall"));
    }

    [Fact]
    public void LeavingTheWorldEmptiesYourRoomsAndForgetsRoomsYouNeverEntered()
    {
        ChatRooms rooms = new ChatRooms();
        rooms.Create("Alice", "Kept", "", "");
        rooms.Create("Alice", "Unused", "", "");
        rooms.Create("Bob", "Bobs", "", "");
        rooms.Enter("Alice", "Kept", "", out _);
        rooms.Enter("Bob", "Kept", "", out _);
        rooms.Enter("Alice", "Bobs", "", out _);

        Assert.Equal(new[] { "Bobs", "Kept" }, rooms.LeaveAll("alice").OrderBy(r => r));
        Assert.Equal(new[] { "Bob" }, rooms.OccupantsOf("Kept"));
        Assert.Null(rooms.Find("Unused"));
        Assert.Null(rooms.Find("Bobs")); // empty now
    }

    [Fact]
    public void SearchMatchesInsideTheName()
    {
        ChatRooms rooms = new ChatRooms();
        rooms.Create("A", "Zion Talk", "", "");
        rooms.Create("A", "machine+talk", "", "");
        rooms.Create("A", "Trading", "", "");
        Assert.Equal(new[] { "machine+talk", "Zion Talk" }, rooms.Search("TALK").Select(r => r.Name));
        Assert.Equal(3, rooms.Search("").Count);
    }

    [Fact]
    public void RequestsAreReadAsTheClientWritesThem()
    {
        // 81 c2 as client.dll's 0x622462e0 builds it for "/roomenter Den:secret": RoomName at +2, Password at +4,
        // strings after the 6-byte fixed part, offsets from the record's first byte. rpcData starts after 81 c2.
        string room = "SOE+MXO+Recursion+Den";
        byte[] record = new byte[] { 0x81, 0xc2, 0x06, 0x00, 0x00, 0x00 }
            .Concat(Sized(room)).ToArray();
        int password = record.Length;
        record = record.Concat(Sized("secret")).ToArray();
        record[4] = (byte)password;
        byte[] rpcData = record[2..];
        Assert.Equal(room, ChatRoomHandler.StringAt(rpcData, 2));
        Assert.Equal("secret", ChatRoomHandler.StringAt(rpcData, 4));
        Assert.Equal("Den", ChatRoomHandler.Bare(room));
        Assert.Equal("my+room", ChatRoomHandler.Bare("SOE+MXO+Recursion+my+room"));
        Assert.Equal("Den", ChatRoomHandler.Bare("Den"));
        Assert.Equal("", ChatRoomHandler.StringAt(new byte[] { 0, 0 }, 2));
    }

    private static byte[] Sized(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return new[] { (byte)(bytes.Length + 1), (byte)0 }.Concat(bytes).Concat(new byte[] { 0 }).ToArray();
    }

    [Fact]
    public void RecordsFollowTheClientsLayouts()
    {
        string before = ChatAddress.WorldName;
        ChatAddress.WorldName = "Recursion";
        try
        {
            CheckLayouts();
        }
        finally
        {
            ChatAddress.WorldName = before;
        }
    }

    private static void CheckLayouts()
    {

        byte[] created = ChatRoomPackets.Created("Den", Ok);
        Assert.Equal("81C1", H(created[..2]));
        Assert.Equal("SOE+MXO+Recursion+Den", Str(created, 2));
        Assert.Equal(Ok, U32(created, 4));

        // 81 c3: no player = "You entered chat room %s."; the status at +6
        byte[] mine = ChatRoomPackets.Entered(null, "Den", Ok);
        Assert.Null(Str(mine, 2));
        Assert.Equal("SOE+MXO+Recursion+Den", Str(mine, 4));
        Assert.Equal(Ok, U32(mine, 6));
        byte[] theirs = ChatRoomPackets.Entered("Bob", "Den", Ok);
        Assert.Equal("SOE+MXO+Recursion+Bob", Str(theirs, 2));

        // 81 d2 kick: target +2, actor +4, room +6, status +8
        byte[] kick = ChatRoomPackets.TwoPlayer(0xd2, "Bob", null, "Den", ChatRooms.Status.NoPrivileges);
        Assert.Equal("SOE+MXO+Recursion+Bob", Str(kick, 2));
        Assert.Null(Str(kick, 4));
        Assert.Equal("SOE+MXO+Recursion+Den", Str(kick, 6));
        Assert.Equal(ChatRooms.Status.NoPrivileges, U32(kick, 8));

        // 81 d8: IsTemporaryModerator +8, status +9 (the handler reads [+9])
        byte[] mod = ChatRoomPackets.Moderator(0xd8, null, "Alice", "Den", true, Ok);
        Assert.Equal(1, mod[8]);
        Assert.Equal(Ok, U32(mod, 9));
        Assert.Equal("SOE+MXO+Recursion+Alice", Str(mod, 4));

        // 81 c5: PlayerName, RoomName, Allow
        byte[] knock = ChatRoomPackets.RequestingEntry("Carol", "Den");
        Assert.Equal("SOE+MXO+Recursion+Carol", Str(knock, 2));
        Assert.Equal(0, knock[6]);

        // 81 e2 / 81 e3
        Assert.Equal("81E2" + "03000000" + "8400008E", H(ChatRoomPackets.SearchResult(3, Ok)));
        ChatRoom room = new ChatRoom { Name = "Den", Topic = "Hi", Password = "x", Owner = "Alice" };
        byte[] found = ChatRoomPackets.RoomFound(room, 2, 7);
        Assert.Equal("SOE+MXO+Recursion+Den", Str(found, 2));
        Assert.Equal("Hi", Str(found, 4));
        Assert.Equal("0100000000", H(found[6..11]));
        Assert.Equal(2u, U32(found, 0x0b));
        Assert.Equal(7u, U32(found, 0x13));
        Assert.Equal(0x17, U16(found, 2) - 0); // strings start right after the 0x17-byte fixed part

        // 81 bf ChatHandle u8 at +2, Status at +3
        Assert.Equal("81BF05" + "4C00000E", H(ChatRoomPackets.MessageFailed(5, ChatRooms.Status.NotInRoom)));

        // 2e room line: ChatType 18, SenderId +3, SenderName +7, RoomName +9, Text +b, strings from 0x24
        byte[] line = ChatRoomPackets.RoomLine(0x1234, "Alice", "Den", "hello");
        Assert.Equal(0x2e, line[0]);
        Assert.Equal(0x18, line[1]);
        Assert.Equal(0x1234u, U32(line, 3));
        Assert.Equal(0x24, U16(line, 7));
        Assert.Equal("Alice", Str(line, 7));
        Assert.Equal("SOE+MXO+Recursion+Den", Str(line, 9));
        Assert.Equal("hello", Str(line, 0x0b));
        Assert.Equal(0u, U32(line, 0x0d));
    }
}
