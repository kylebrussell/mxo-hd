using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    /// <summary>
    /// The client's chat room requests, all from slash commands (client.dll's command handler 0x62256760; no window sends
    /// them). Who gets which record, and the rules, are ours: docs/CLIENT-ACTIONS.md, "Chat rooms in detail".
    /// <c>rpcData</c> is the record after its two opcode bytes, so a field at record offset N is rpcData[N - 2], and the
    /// string offsets it holds count from the record's first byte.
    /// </summary>
    public static class ChatRoomHandler
    {
        public const int Create = 0x1c0;      // 81 c0 RoomName, Topic, Password, IsPrivate .. IsGameLocal, MaxOccupancy
        public const int Enter = 0x1c2;       // 81 c2 RoomName, Password
        public const int Allow = 0x1c5;       // 81 c5 PlayerName, RoomName, Allow
        public const int Leave = 0x1c7;       // 81 c7 RoomName
        public const int Destroy = 0x1c9;     // 81 c9 RoomName
        public const int Invite = 0x1cd;      // 81 cd .. 81 df: PlayerName, RoomName (81 d7 adds IsTemporaryModerator,
        public const int Uninvite = 0x1cf;    //   81 d9 has IsTemporaryModerator before RoomName, 81 db adds a status)
        public const int Kick = 0x1d1;
        public const int Ban = 0x1d3;
        public const int Unban = 0x1d5;
        public const int AddModerator = 0x1d7;
        public const int RemoveModerator = 0x1d9;
        public const int SetOwner = 0x1db;
        public const int GiveVoice = 0x1dd;
        public const int RemoveVoice = 0x1df;
        public const int Find = 0x1e1;        // 81 e1 StartingNodeName, SearchFilter

        public static bool Handles(int header) =>
            header == Create || header == Enter || header == Allow || header == Leave || header == Destroy ||
            (header >= Invite && header <= RemoveVoice && (header & 1) == 1) || header == Find;

        private static ChatRooms Rooms => ChatRooms.Instance;

        // ---- reading the requests --------------------------------------------------------------------------------

        /// <summary>The string whose record offset is the u16 at record offset <paramref name="field"/>; "" for offset 0.</summary>
        internal static string StringAt(byte[] rpcData, int field)
        {
            int index = field - 2;
            if (rpcData == null || index < 0 || index + 2 > rpcData.Length)
            {
                return "";
            }

            int offset = rpcData[index] | (rpcData[index + 1] << 8);
            if (offset == 0)
            {
                return "";
            }

            string value;
            return SocialRequests.TryReadSized(rpcData, offset - 2, out value) ? value : "";
        }

        internal static byte ByteAt(byte[] rpcData, int field)
        {
            int index = field - 2;
            return rpcData != null && index >= 0 && index < rpcData.Length ? rpcData[index] : (byte) 0;
        }

        /// <summary>The name inside a chat address: the client sends <c>SOE+MXO+&lt;world&gt;+name</c> (its own world for
        /// players and rooms both, 0x6224ff40/0x6224f880), turning dots in a room name into '+'.</summary>
        internal static string Bare(string address)
        {
            address = (address ?? "").Trim();
            string[] parts = address.Split('+');
            if (parts.Length >= 4 && parts[0].Equals("SOE", StringComparison.OrdinalIgnoreCase) &&
                parts[1].Equals("MXO", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("+", parts.Skip(3));
            }

            return address;
        }

        // ---- sending -----------------------------------------------------------------------------------------------

        private static string HandleOf(WorldClient client) => SocialHandler.HandleOf(client);

        private static void Send(string handle, byte[] record) => SocialHandler.Send(SocialHandler.FindOnlineByHandle(handle), record);

        private static void SendToOccupants(IEnumerable<string> occupants, string except, byte[] record)
        {
            foreach (string handle in occupants.Where(h => !ChatRoom.Same(h, except)))
            {
                Send(handle, record);
            }
        }

        // ---- the requests ----------------------------------------------------------------------------------------

        public static void Handle(WorldClient client, int header, byte[] rpcData)
        {
            string me = HandleOf(client);
            switch (header)
            {
                case Create:
                {
                    string room = Bare(StringAt(rpcData, 2));
                    uint status = Rooms.Create(me, room, StringAt(rpcData, 6), StringAt(rpcData, 4));
                    SocialHandler.Send(client, ChatRoomPackets.Created(room, status));
                    Log(me + " creates room '" + room + "'", status);
                    break;
                }

                case Enter:
                {
                    string room = Bare(StringAt(rpcData, 2));
                    ChatRoom entered;
                    uint status = Rooms.Enter(me, room, StringAt(rpcData, 4), out entered);
                    Log(me + " enters room '" + room + "'", status);
                    if (status == ChatRooms.Status.WaitingForEntry)
                    {
                        // A private room: the owner and its moderators hear that someone is at the door.
                        ChatRoom target = Rooms.Find(room);
                        if (target != null)
                        {
                            foreach (string moderator in new[] {target.Owner}.Concat(target.Moderators.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
                            {
                                Send(moderator, ChatRoomPackets.RequestingEntry(me, target.Name));
                            }
                        }
                    }

                    if (status != ChatRooms.Status.Success)
                    {
                        SocialHandler.Send(client, ChatRoomPackets.Entered(null, room, status));
                        break;
                    }

                    AnnounceEntry(me, entered.Name);
                    break;
                }

                case Allow:
                {
                    string player = Bare(StringAt(rpcData, 2));
                    string room = Bare(StringAt(rpcData, 4));
                    ChatRoom entered;
                    uint status = Rooms.Allow(me, room, player, out entered);
                    Log(me + " allows " + player + " into room '" + room + "'", status);
                    SocialHandler.Send(client, ChatRoomPackets.Allowed(null, room, status));
                    if (status == ChatRooms.Status.Success)
                    {
                        AnnounceEntry(player, entered.Name);
                    }

                    break;
                }

                case Leave:
                {
                    string room = Bare(StringAt(rpcData, 2));
                    List<string> before = Rooms.OccupantsOf(room);
                    uint status = Rooms.Leave(me, room);
                    Log(me + " leaves room '" + room + "'", status);
                    SocialHandler.Send(client, ChatRoomPackets.Left(null, room, status));
                    if (status == ChatRooms.Status.Success)
                    {
                        SendToOccupants(before, me, ChatRoomPackets.Left(me, room, status));
                    }

                    break;
                }

                case Destroy:
                {
                    string room = Bare(StringAt(rpcData, 2));
                    ChatRoom destroyed;
                    uint status = Rooms.Destroy(me, room, out destroyed);
                    Log(me + " destroys room '" + room + "'", status);
                    SocialHandler.Send(client, ChatRoomPackets.Destroyed(null, room, status));
                    if (destroyed != null)
                    {
                        SendToOccupants(destroyed.Occupants, me, ChatRoomPackets.Destroyed(me, destroyed.Name, status));
                    }

                    break;
                }

                case Find:
                {
                    // /roomfind [node] [filter]: both words as typed. A filter matches inside the room's name; with one
                    // word, that word is the filter.
                    string node = StringAt(rpcData, 2).Trim();
                    string filter = StringAt(rpcData, 4).Trim();
                    List<ChatRoom> found = Rooms.Search(filter.Length > 0 ? filter : node);
                    SocialHandler.Send(client, ChatRoomPackets.SearchResult((uint) found.Count, ChatRooms.Status.Success));
                    uint index = 0;
                    foreach (ChatRoom room in found)
                    {
                        SocialHandler.Send(client, ChatRoomPackets.RoomFound(room, Rooms.OccupantsOf(room.Name).Count, index++));
                    }

                    Log(me + " finds rooms '" + (filter.Length > 0 ? filter : node) + "': " + found.Count, ChatRooms.Status.Success);
                    break;
                }

                default:
                    HandleChange(client, me, header, rpcData);
                    break;
            }
        }

        /// <summary>The newcomer hears "You entered ...", everyone already inside "%s entered ...".</summary>
        private static void AnnounceEntry(string handle, string room)
        {
            Send(handle, ChatRoomPackets.Entered(null, room, ChatRooms.Status.Success));
            SendToOccupants(Rooms.OccupantsOf(room), handle, ChatRoomPackets.Entered(handle, room, ChatRooms.Status.Success));
        }

        private static void HandleChange(WorldClient client, string me, int header, byte[] rpcData)
        {
            string target = Bare(StringAt(rpcData, 2));
            string room = Bare(StringAt(rpcData, header == RemoveModerator ? 5 : 4));
            bool temporary = header == AddModerator ? ByteAt(rpcData, 6) != 0 : header == RemoveModerator && ByteAt(rpcData, 4) != 0;
            ChatRooms.Change change;
            byte answer;
            switch (header)
            {
                case Invite: change = ChatRooms.Change.Invite; answer = 0xce; break;
                case Uninvite: change = ChatRooms.Change.Uninvite; answer = 0xd0; break;
                case Kick: change = ChatRooms.Change.Kick; answer = 0xd2; break;
                case Ban: change = ChatRooms.Change.Ban; answer = 0xd4; break;
                case Unban: change = ChatRooms.Change.Unban; answer = 0xd6; break;
                case AddModerator: change = ChatRooms.Change.Moderator; answer = 0xd8; break;
                case RemoveModerator: change = ChatRooms.Change.Unmoderator; answer = 0xda; break;
                case SetOwner: change = ChatRooms.Change.Owner; answer = 0xdc; break;
                case GiveVoice: change = ChatRooms.Change.Voice; answer = 0xde; break;
                case RemoveVoice: change = ChatRooms.Change.Unvoice; answer = 0xe0; break;
                default: return;
            }

            // Spelt as the player spells it, when they are online; otherwise as typed, if anyone has that handle.
            WorldClient targetClient = SocialHandler.FindOnlineByHandle(target);
            bool exists = targetClient != null || (target.Length > 0 && Store.dbManager?.WorldDbHandler?.GetCharIdByHandle(target) > 0);
            if (targetClient != null)
            {
                target = HandleOf(targetClient);
            }

            List<string> before = Rooms.OccupantsOf(room);
            ChatRoom chatRoom = Rooms.Find(room);
            string roomName = chatRoom != null ? chatRoom.Name : room;
            uint status = Rooms.Apply(change, me, roomName, target, exists, temporary);
            Log(me + " " + change + " " + target + " in room '" + roomName + "'", status);

            if (change == ChatRooms.Change.Owner)
            {
                // 81 dc has one name: "%s gains ownership of chat room %s.", to the old owner, the new one and the room.
                byte[] owner = ChatRoomPackets.OwnerChanged(target, roomName, status);
                SocialHandler.Send(client, owner);
                if (status == ChatRooms.Status.Success)
                {
                    SendToOccupants(before.Concat(new[] {target}).Distinct(StringComparer.OrdinalIgnoreCase), me, owner);
                }

                return;
            }

            bool moderatorRecord = change == ChatRooms.Change.Moderator || change == ChatRooms.Change.Unmoderator;
            Func<string, string, byte[]> build = (t, a) => moderatorRecord
                ? ChatRoomPackets.Moderator(answer, t, a, roomName, temporary, status)
                : ChatRoomPackets.TwoPlayer(answer, t, a, roomName, status);

            SocialHandler.Send(client, build(target, null));
            if (status != ChatRooms.Status.Success)
            {
                return;
            }

            // The player it was done to hears it ("%s kicks you ..."), and so does everyone in the room.
            if (!ChatRoom.Same(target, me))
            {
                Send(target, build(null, me));
            }

            foreach (string occupant in before.Where(o => !ChatRoom.Same(o, me) && !ChatRoom.Same(o, target)))
            {
                Send(occupant, build(target, me));
            }
        }

        // ---- room lines ----------------------------------------------------------------------------------------

        /// <summary>
        /// Client 28 with ChatType 18: <c>/room &lt;room&gt; &lt;text&gt;</c> (sent from 0x6216aff0: +3 Text, +5 RoomName,
        /// +7 ChatHandle). Goes to everyone in the room, the speaker included: the client doesn't print its own line.
        /// </summary>
        public static bool TrySendRoomLine(WorldClient client, byte[] rpcData, string text)
        {
            if (rpcData == null || rpcData.Length < 1 || (rpcData[0] & 0x0f) != (ChatRoomPackets.ChatRoomType & 0x0f))
            {
                return false;
            }

            string me = HandleOf(client);
            string room = "";
            if (rpcData.Length >= 6)
            {
                int offset = rpcData[4] | (rpcData[5] << 8);
                string address;
                if (offset > 0 && SocialRequests.TryReadSized(rpcData, offset - 1, out address))
                {
                    room = Bare(address);
                }
            }

            byte chatHandle = rpcData.Length >= 7 ? rpcData[6] : (byte) 0;
            uint status = Rooms.CanSpeak(me, room);
            if (status != ChatRooms.Status.Success)
            {
                SocialHandler.Send(client, ChatRoomPackets.MessageFailed(chatHandle, status));
                Log(me + " speaks in room '" + room + "'", status);
                return true;
            }

            ChatRoom chatRoom = Rooms.Find(room);
            byte[] line = ChatRoomPackets.RoomLine(client.playerData.getCharID(), me, chatRoom.Name, text);
            foreach (string occupant in Rooms.OccupantsOf(chatRoom.Name))
            {
                Send(occupant, line);
            }

            return true;
        }

        /// <summary>A player left the world: they leave every room they were in, and the others there are told.</summary>
        public static void PlayerLeftWorld(WorldClient client)
        {
            string me = HandleOf(client);
            foreach (string room in Rooms.LeaveAll(me))
            {
                SendToOccupants(Rooms.OccupantsOf(room), me, ChatRoomPackets.Left(me, room, ChatRooms.Status.Success));
            }
        }

        private static void Log(string what, uint status)
        {
            Output.WriteDebugLog("[ROOM] " + what + ": " + (status == ChatRooms.Status.Success ? "ok" : "0x" + status.ToString("x8")));
        }
    }
}
