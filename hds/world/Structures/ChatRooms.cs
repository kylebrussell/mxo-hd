using System;
using System.Collections.Generic;
using System.Linq;

namespace hds.world.Structures
{
    /// <summary>
    /// Chat rooms (the client's /roomcreate .. /roomfind, docs/CLIENT-ACTIONS.md "Chat rooms in detail"). The client keeps
    /// no state about rooms: it prints whatever the server says. So the rules here are ours, written to the status codes
    /// client.dll registers for SOE chat rooms (<see cref="Status"/>). Players are named by their handle, compared without
    /// case. Rooms are not stored: they last until destroyed, or until the last occupant leaves.
    /// </summary>
    public sealed class ChatRoom
    {
        /// <summary>The name as first typed, without the SOE+MXO+world prefix (dots arrive as '+').</summary>
        public string Name;
        public string Topic = "";
        public string Password = "";
        public string Owner;

        public bool IsPrivate => Password.Length > 0;

        /// <summary>Occupants, in the order they entered.</summary>
        public readonly List<string> Occupants = new List<string>();

        /// <summary>Moderators; the value says whether the status is temporary.</summary>
        public readonly Dictionary<string, bool> Moderators = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Voiced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Invited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Players refused at the door of a private room, waiting for /roomallow.</summary>
        public readonly HashSet<string> WaitingForEntry = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool Has(string handle) => Occupants.Any(o => Same(o, handle));
        public bool IsOwner(string handle) => Same(Owner, handle);
        public bool IsModerator(string handle) => IsOwner(handle) || Moderators.ContainsKey(handle);

        internal static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class ChatRooms
    {
        public static readonly ChatRooms Instance = new ChatRooms();

        /// <summary>LTOBJSRV_SOECHAT_* codes, as client.dll registers them (0x622a6480, category 0x0e).</summary>
        public static class Status
        {
            public const uint Success = 0x8e000084;
            public const uint AvatarDoesntExist = 0x0e000041;   // DESTAVATARDOESNTEXIST
            public const uint RoomDoesntExist = 0x0e000042;     // ADDRESSDOESNTEXIST
            public const uint Banned = 0x0e000049;              // ROOM_BANNEDAVATAR
            public const uint PrivateRoom = 0x0e00004a;         // ROOM_PRIVATEROOM
            public const uint ModeratedRoom = 0x0e00004b;       // ROOM_MODERATEDROOM
            public const uint NotInRoom = 0x0e00004c;           // ROOM_NOTINROOM
            public const uint NoPrivileges = 0x0e00004d;        // ROOM_NOPRIVILEGES
            public const uint DestNotInRoom = 0x0e000048;       // ROOM_DESTNOTINROOM
            public const uint RoomFull = 0x0e000053;            // ROOMMAXAVATARSREACHED
            public const uint AlreadyExists = 0x0e000055;       // ROOM_ALREADYEXISTS
            public const uint NotModerator = 0x0e00005b;        // ROOM_DESTAVATARNOTMODERATOR
            public const uint NotInvited = 0x0e00005c;          // ROOM_DESTAVATARNOTINVITED
            public const uint NotBanned = 0x0e00005d;           // ROOM_DESTAVATARNOTBANNED
            public const uint DuplicateBan = 0x0e00005e;        // ROOM_DUPLICATEBAN
            public const uint DuplicateModerator = 0x0e00005f;  // ROOM_DUPLICATEMODERATOR
            public const uint DuplicateInvite = 0x0e000060;     // ROOM_DUPLICATEINVITE
            public const uint AlreadyInRoom = 0x0e000061;       // ROOM_ALREADYINROOM
            public const uint InvalidName = 0x0e00006e;         // INVALIDNODENAME
            public const uint NotEntering = 0x0e00007e;         // ROOM_DESTAVATARNOTENTERING
            public const uint WaitingForEntry = 0x0e000080;     // ROOM_WAITINGFORENTRY
        }

        /// <summary>Longest room name or topic we take (ours; the client sets no limit).</summary>
        public const int MaxNameLength = 64;
        public const int MaxTopicLength = 255;

        private readonly object sync = new object();
        private readonly Dictionary<string, ChatRoom> rooms = new Dictionary<string, ChatRoom>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Everything a test or a fresh server needs gone.</summary>
        public void Clear()
        {
            lock (sync)
            {
                rooms.Clear();
            }
        }

        public ChatRoom Find(string name)
        {
            lock (sync)
            {
                return rooms.TryGetValue(name ?? "", out ChatRoom room) ? room : null;
            }
        }

        /// <summary>A copy of the room's occupants, safe to use outside the lock.</summary>
        public List<string> OccupantsOf(string name)
        {
            lock (sync)
            {
                return rooms.TryGetValue(name ?? "", out ChatRoom room) ? room.Occupants.ToList() : new List<string>();
            }
        }

        public static bool ValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength && !name.Any(char.IsControl);

        public uint Create(string handle, string name, string password, string topic)
        {
            if (!ValidName(name))
            {
                return Status.InvalidName;
            }

            lock (sync)
            {
                if (rooms.ContainsKey(name))
                {
                    return Status.AlreadyExists;
                }

                topic = topic ?? "";
                rooms[name] = new ChatRoom
                {
                    Name = name, Owner = handle, Password = password ?? "",
                    Topic = topic.Length > MaxTopicLength ? topic.Substring(0, MaxTopicLength) : topic
                };
                return Status.Success;
            }
        }

        /// <summary>The handle's entry: a public room, the right password, an invite, or being its owner or a moderator
        /// lets them in. A wrong or missing password puts them on the waiting list for /roomallow.</summary>
        public uint Enter(string handle, string name, string password, out ChatRoom entered)
        {
            lock (sync)
            {
                entered = null;
                if (!rooms.TryGetValue(name ?? "", out ChatRoom room))
                {
                    return Status.RoomDoesntExist;
                }

                if (room.Has(handle))
                {
                    return Status.AlreadyInRoom;
                }

                if (room.Banned.Contains(handle))
                {
                    return Status.Banned;
                }

                bool admitted = !room.IsPrivate || room.IsModerator(handle) || room.Invited.Contains(handle) ||
                                string.Equals(room.Password, password ?? "", StringComparison.Ordinal);
                if (!admitted)
                {
                    room.WaitingForEntry.Add(handle);
                    return Status.WaitingForEntry;
                }

                room.WaitingForEntry.Remove(handle);
                room.Occupants.Add(handle);
                entered = room;
                return Status.Success;
            }
        }

        /// <summary>/roomallow: a moderator lets a waiting player in.</summary>
        public uint Allow(string handle, string name, string player, out ChatRoom entered)
        {
            lock (sync)
            {
                entered = null;
                if (!rooms.TryGetValue(name ?? "", out ChatRoom room))
                {
                    return Status.RoomDoesntExist;
                }

                if (!room.IsModerator(handle))
                {
                    return Status.NoPrivileges;
                }

                if (!room.WaitingForEntry.Remove(player))
                {
                    return Status.NotEntering;
                }

                if (!room.Has(player))
                {
                    room.Occupants.Add(player);
                }

                entered = room;
                return Status.Success;
            }
        }

        /// <summary>Takes the handle out of the room; an empty room goes.</summary>
        public uint Leave(string handle, string name)
        {
            lock (sync)
            {
                if (!rooms.TryGetValue(name ?? "", out ChatRoom room))
                {
                    return Status.RoomDoesntExist;
                }

                if (!room.Has(handle))
                {
                    return Status.NotInRoom;
                }

                room.Occupants.RemoveAll(o => ChatRoom.Same(o, handle));
                DropIfEmpty(room);
                return Status.Success;
            }
        }

        public uint Destroy(string handle, string name, out ChatRoom destroyed)
        {
            lock (sync)
            {
                destroyed = null;
                if (!rooms.TryGetValue(name ?? "", out ChatRoom room))
                {
                    return Status.RoomDoesntExist;
                }

                if (!room.IsOwner(handle))
                {
                    return Status.NoPrivileges;
                }

                rooms.Remove(room.Name);
                destroyed = room;
                return Status.Success;
            }
        }

        public enum Change
        {
            Invite, Uninvite, Kick, Ban, Unban, Moderator, Unmoderator, Owner, Voice, Unvoice
        }

        /// <summary>
        /// A moderator's action on another player. Owner-only: making and unmaking moderators, giving the room away.
        /// <paramref name="targetExists"/> says whether anyone has that handle. Kick and ban take the player out.
        /// </summary>
        public uint Apply(Change change, string handle, string name, string target, bool targetExists, bool temporary = false)
        {
            lock (sync)
            {
                if (!rooms.TryGetValue(name ?? "", out ChatRoom room))
                {
                    return Status.RoomDoesntExist;
                }

                bool ownerOnly = change == Change.Moderator || change == Change.Unmoderator || change == Change.Owner;
                if (ownerOnly ? !room.IsOwner(handle) : !room.IsModerator(handle))
                {
                    return Status.NoPrivileges;
                }

                if (!targetExists)
                {
                    return Status.AvatarDoesntExist;
                }

                switch (change)
                {
                    case Change.Invite:
                        return room.Invited.Add(target) ? Status.Success : Status.DuplicateInvite;
                    case Change.Uninvite:
                        return room.Invited.Remove(target) ? Status.Success : Status.NotInvited;
                    case Change.Kick:
                        if (!room.Has(target))
                        {
                            return Status.DestNotInRoom;
                        }

                        room.Occupants.RemoveAll(o => ChatRoom.Same(o, target));
                        return Status.Success;
                    case Change.Ban:
                        if (!room.Banned.Add(target))
                        {
                            return Status.DuplicateBan;
                        }

                        room.Occupants.RemoveAll(o => ChatRoom.Same(o, target));
                        room.Invited.Remove(target);
                        return Status.Success;
                    case Change.Unban:
                        return room.Banned.Remove(target) ? Status.Success : Status.NotBanned;
                    case Change.Moderator:
                        if (room.Moderators.ContainsKey(target) || room.IsOwner(target))
                        {
                            return Status.DuplicateModerator;
                        }

                        room.Moderators[target] = temporary;
                        return Status.Success;
                    case Change.Unmoderator:
                        return room.Moderators.Remove(target) ? Status.Success : Status.NotModerator;
                    case Change.Owner:
                        room.Owner = target;
                        return Status.Success;
                    case Change.Voice:
                        room.Voiced.Add(target);
                        return Status.Success;
                    case Change.Unvoice:
                        room.Voiced.Remove(target);
                        return Status.Success;
                    default:
                        return Status.NoPrivileges;
                }
            }
        }

        /// <summary>Whether the handle may speak in the room: it must be in it.</summary>
        public uint CanSpeak(string handle, string name)
        {
            lock (sync)
            {
                if (!rooms.TryGetValue(name ?? "", out ChatRoom room))
                {
                    return Status.RoomDoesntExist;
                }

                return room.Has(handle) ? Status.Success : Status.NotInRoom;
            }
        }

        /// <summary>Rooms whose name holds the filter (all rooms for an empty one), by name.</summary>
        public List<ChatRoom> Search(string filter)
        {
            lock (sync)
            {
                filter = filter ?? "";
                return rooms.Values.Where(r => r.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        /// <summary>A player left the world: out of every room, off every waiting list. Returns the rooms they were in.
        /// A room left empty goes, unless someone else owns it and it has never been entered.</summary>
        public List<string> LeaveAll(string handle)
        {
            lock (sync)
            {
                List<string> left = new List<string>();
                foreach (ChatRoom room in rooms.Values.ToList())
                {
                    room.WaitingForEntry.Remove(handle);
                    if (room.Has(handle))
                    {
                        room.Occupants.RemoveAll(o => ChatRoom.Same(o, handle));
                        left.Add(room.Name);
                        DropIfEmpty(room);
                    }
                    else if (room.IsOwner(handle) && room.Occupants.Count == 0)
                    {
                        // Made and never entered: nobody can reach it once its owner is gone.
                        rooms.Remove(room.Name);
                    }
                }

                return left;
            }
        }

        private void DropIfEmpty(ChatRoom room)
        {
            if (room.Occupants.Count == 0)
            {
                rooms.Remove(room.Name);
            }
        }
    }
}
