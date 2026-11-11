using System;
using hds.world.Structures;

namespace hds
{
    /// <summary>
    /// The chat room records, server to client. Layouts are client.dll's (each class's printer names its fields; strings
    /// are u16 offsets from the record's first byte to <c>&lt;u16 length with the NUL&gt; text 00</c>); no retail capture
    /// has a room. Every handler only prints a System chat line, and treats a status without the success bit as a
    /// failure ("Failed to ... Reason: %s."). A player name left out (offset 0) means "you": 81 c3 with no name prints
    /// "You entered chat room %s.", with one "%s entered chat room %s.". Names and rooms are full chat addresses
    /// (<c>SOE+MXO+world+name</c>); the client strips its own world's prefix for display.
    /// </summary>
    internal static class ChatRoomPackets
    {
        /// <summary>Server 2e ChatType for a room line, as the client's /room sends it (low nibble 8 = room).</summary>
        public const byte ChatRoomType = 0x18;

        public static string RoomAddress(string room) => ChatAddress.Prefix + room;

        private static string Player(string handle) => string.IsNullOrEmpty(handle) ? null : ChatAddress.Of(handle);

        private static void OptionalString(MailRecord r, int field, string value)
        {
            if (value != null)
            {
                r.String(field, value);
            }
        }

        /// <summary>81 c1 &lt;RoomName&gt; &lt;Status&gt;: "Created chat room %s." (0x6217a744).</summary>
        public static byte[] Created(string room, uint status)
        {
            MailRecord r = new MailRecord(0xc1);
            int name = r.Offset();
            r.U32(status).String(name, RoomAddress(room));
            return r.ToArray();
        }

        /// <summary>81 c3 &lt;PlayerNameEnteringRoom&gt; &lt;RoomName&gt; &lt;Status&gt;; no player = "You entered ...".</summary>
        public static byte[] Entered(string player, string room, uint status) => PlayerRoomStatus(0xc3, player, room, status);

        /// <summary>81 c8: "You left chat room %s." / "%s left chat room %s.".</summary>
        public static byte[] Left(string player, string room, uint status) => PlayerRoomStatus(0xc8, player, room, status);

        /// <summary>81 ca: "Destroy chat room successful." (no player) / "%s destroyed chat room %s.".</summary>
        public static byte[] Destroyed(string player, string room, uint status) => PlayerRoomStatus(0xca, player, room, status);

        /// <summary>81 c6: "You allow entry into chat room %s." to the moderator who let someone in.</summary>
        public static byte[] Allowed(string player, string room, uint status) => PlayerRoomStatus(0xc6, player, room, status);

        private static byte[] PlayerRoomStatus(byte opcode, string player, string room, uint status)
        {
            MailRecord r = new MailRecord(opcode);
            int playerField = r.Offset();
            int roomField = r.Offset();
            r.U32(status);
            OptionalString(r, playerField, Player(player));
            r.String(roomField, RoomAddress(room));
            return r.ToArray();
        }

        /// <summary>81 c5 &lt;PlayerName&gt; &lt;RoomName&gt; &lt;Allow u8&gt;: "%s is requesting entry into chat room %s.",
        /// to the room's owner and moderators.</summary>
        public static byte[] RequestingEntry(string player, string room)
        {
            MailRecord r = new MailRecord(0xc5);
            int playerField = r.Offset();
            int roomField = r.Offset();
            r.U8(0);
            r.String(playerField, Player(player));
            r.String(roomField, RoomAddress(room));
            return r.ToArray();
        }

        /// <summary>
        /// The two-player records, 81 ce .. 81 e0 (all but 81 d8/81 da/81 dc): &lt;Target&gt; &lt;Actor&gt; &lt;RoomName&gt;
        /// &lt;Status&gt;. To the target leave the target out ("%s kicks you from chat room %s."), to the actor the actor
        /// ("You kick %s ..."), to anyone else neither ("%s kicks %s ...").
        /// </summary>
        public static byte[] TwoPlayer(byte opcode, string target, string actor, string room, uint status)
        {
            MailRecord r = new MailRecord(opcode);
            int targetField = r.Offset();
            int actorField = r.Offset();
            int roomField = r.Offset();
            r.U32(status);
            OptionalString(r, targetField, Player(target));
            OptionalString(r, actorField, Player(actor));
            r.String(roomField, RoomAddress(room));
            return r.ToArray();
        }

        /// <summary>81 d8 (grant) / 81 da (revoke) moderator: &lt;Target&gt; &lt;Actor&gt; &lt;RoomName&gt;
        /// &lt;IsTemporaryModerator u8&gt; &lt;Status&gt;; the handler checks the status at +9.</summary>
        public static byte[] Moderator(byte opcode, string target, string actor, string room, bool temporary, uint status)
        {
            MailRecord r = new MailRecord(opcode);
            int targetField = r.Offset();
            int actorField = r.Offset();
            int roomField = r.Offset();
            r.U8(temporary ? 1 : 0).U32(status);
            OptionalString(r, targetField, Player(target));
            OptionalString(r, actorField, Player(actor));
            r.String(roomField, RoomAddress(room));
            return r.ToArray();
        }

        /// <summary>81 dc &lt;PlayerNameGainingOwnership&gt; &lt;RoomName&gt; &lt;Status&gt;: "%s gains ownership of chat room %s.".</summary>
        public static byte[] OwnerChanged(string newOwner, string room, uint status) => PlayerRoomStatus(0xdc, newOwner, room, status);

        /// <summary>81 e2 &lt;NumRoomsFound u32&gt; &lt;Status&gt;: "Found %i matching rooms".</summary>
        public static byte[] SearchResult(uint count, uint status) => new MailRecord(0xe2).U32(count).U32(status).ToArray();

        /// <summary>81 e3, one room found (size 0x17): RoomName, RoomTopic, private, moderated, persistent, world-local,
        /// game-local, occupants u32, max occupants u32, index u32. Printed as "  Room %s (%i occupants)" with the topic
        /// and the flags set.</summary>
        public static byte[] RoomFound(ChatRoom room, int occupants, uint index)
        {
            MailRecord r = new MailRecord(0xe3);
            int nameField = r.Offset();
            int topicField = r.Offset();
            r.U8(room.IsPrivate ? 1 : 0).U8(0).U8(0).U8(0).U8(0);
            r.U32((uint) occupants).U32(0).U32(index);
            r.String(nameField, RoomAddress(room.Name));
            if (room.Topic.Length > 0)
            {
                r.String(topicField, room.Topic);
            }

            return r.ToArray();
        }

        /// <summary>81 bf &lt;ChatHandle u8&gt; &lt;Status&gt;: "Failed to send room message.  Reason: %s." (0x6217a664).</summary>
        public static byte[] MessageFailed(byte chatHandle, uint status) => new MailRecord(0xbf).U8(chatHandle).U32(status).ToArray();

        /// <summary>A room line: 2e with ChatType 18 and the RoomName field (+9) set; shown as "[room] name: text".</summary>
        public static byte[] RoomLine(UInt32 charId, string handle, string room, string text)
        {
            PacketContent pak = new PacketContent();
            string address = RoomAddress(room);
            UInt16 nameOffset = 0x24;
            UInt16 roomOffset = (UInt16) (nameOffset + 2 + handle.Length + 1);
            UInt16 textOffset = (UInt16) (roomOffset + 2 + address.Length + 1);
            pak.AddByte((byte) RPCResponseHeaders.SERVER_CHAT_MESSAGE_RESPONSE);
            pak.AddByte(ChatRoomType);
            pak.AddByte(0x00);
            pak.AddUint32(charId, 1);
            pak.AddUint16(nameOffset, 1);
            pak.AddUint16(roomOffset, 1);
            pak.AddUint16(textOffset, 1);
            pak.AddByteArray(new byte[0x24 - 0x0d]);
            pak.AddSizedTerminatedString(handle);
            pak.AddSizedTerminatedString(address);
            pak.AddSizedTerminatedString(text);
            return pak.ReturnFinalPacket();
        }
    }
}
