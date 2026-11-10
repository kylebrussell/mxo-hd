using System;
using System.Collections.Generic;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>A door view the server spawned for one client, and how far it is through closing.</summary>
    public class OpenDoor
    {
        public UInt16 ViewId;
        public UInt64 EntityId;
        /// <summary>The door's goid, for its timing in the client's data (0: retail's 20 s + 1 s).</summary>
        public UInt16 GoId;
        public double OpenedAt;
        public DoorStage Stage;
    }

    public enum DoorStage
    {
        Open,
        Closing,
        Closed
    }

    /// <summary>
    /// Door open/close timing from a retail capture (afterwhoruneo second_pc_005, 2009-07-30 09:55):
    /// the door spawns open (CurrentState 0x0834), turns to closing (0x0838) after 20 s and closed (0x0835)
    /// a second later, then at 26 s its state attributes are reset and its view deleted.
    /// AttributeClass363, 364 and 365 all put CurrentState at update index 8, so one layout fits every door.
    /// The client's data (data/object_states.csv) has the same timing per door: OpenedState goes to ClosingState after 20 s,
    /// ClosingState to ClosedState after 1 s, or 5 s for AttributeClass365 doors.
    /// </summary>
    public static class DoorLifecycle
    {
        public const UInt32 StateOpen = 0x0834;
        public const UInt32 StateClosing = 0x0838;
        public const UInt32 StateClosed = 0x0835;

        public const double ClosingAfterSeconds = 20;
        public const double ClosedAfterSeconds = 21;
        public const double RemoveAfterSeconds = 26;

        private static Timer timer;
        private static readonly object timerLock = new object();

        /// <summary>Part 01, groups 80 02 (update index 8 = CurrentState), then the state.</summary>
        public static byte[] BuildStateUpdate(UInt32 state)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("018002");
            pak.AddUint32(state, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Retail sends this just before the delete: update indices 0-8 all zero apart from CurrentState,
        /// which stays closed.
        /// </summary>
        public static byte[] BuildResetUpdate()
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("01ff" + "00000000000000000000000000" + "03" + "00000000");
            pak.AddUint32(StateClosed, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>Records that the client sees this door open, re-opening it if it was already closing.</summary>
        public static void Opened(WorldClient client, UInt16 viewId, UInt64 entityId, UInt16 goid = 0)
        {
            double now = TimeUtils.GetSimTimeSeconds();
            bool reopen = false;
            lock (client.playerData.openDoors)
            {
                OpenDoor door = client.playerData.openDoors.Find(d => d.ViewId == viewId);
                if (door == null)
                {
                    client.playerData.openDoors.Add(new OpenDoor
                        { ViewId = viewId, EntityId = entityId, GoId = goid, OpenedAt = now, Stage = DoorStage.Open });
                }
                else
                {
                    reopen = door.Stage != DoorStage.Open;
                    door.OpenedAt = now;
                    door.Stage = DoorStage.Open;
                }
            }

            if (reopen)
            {
                new ServerPackets().SendUpdateViewStatePacket(client, viewId, BuildStateUpdate(StateOpen));
                client.FlushQueue();
            }

            EnsureTimer();
        }

        /// <summary>What a door should do now, given when it opened; null when nothing changes.</summary>
        public static DoorStage? NextStage(OpenDoor door, double now)
        {
            double age = now - door.OpenedAt;
            (double closing, double closed, double _) = Timing(door);
            switch (door.Stage)
            {
                case DoorStage.Open when age >= closing:
                    return DoorStage.Closing;
                case DoorStage.Closing when age >= closed:
                    return DoorStage.Closed;
                default:
                    return null;
            }
        }

        public static bool ShouldRemove(OpenDoor door, double now)
        {
            return door.Stage == DoorStage.Closed && now - door.OpenedAt >= Timing(door).remove;
        }

        /// <summary>Seconds after opening that the door starts closing, is closed, and is removed (5 s after closing, as retail).</summary>
        public static (double closing, double closed, double remove) Timing(OpenDoor door)
        {
            (UInt32 open, UInt32 closing)? data = door.GoId == 0 ? null : ObjectStates.DoorTiming(door.GoId);
            if (data == null)
            {
                return (ClosingAfterSeconds, ClosedAfterSeconds, RemoveAfterSeconds);
            }

            double closed = data.Value.open + data.Value.closing;
            return (data.Value.open, closed, closed + (RemoveAfterSeconds - ClosedAfterSeconds));
        }

        private static void EnsureTimer()
        {
            lock (timerLock)
            {
                if (timer == null)
                {
                    timer = new Timer(_ => TickAll(), null, 500, 500);
                }
            }
        }

        private static void TickAll()
        {
            List<WorldClient> clients;
            lock (WorldServer.Clients)
            {
                clients = new List<WorldClient>(WorldServer.Clients.Values);
            }

            foreach (WorldClient client in clients)
            {
                try
                {
                    if (client != null)
                    {
                        Tick(client, TimeUtils.GetSimTimeSeconds());
                    }
                }
                catch (Exception ex)
                {
                    Output.WriteLine("[DOOR] Tick failed: " + ex.Message);
                }
            }
        }

        public static void Tick(WorldClient client, double now)
        {
            List<OpenDoor> remove = new List<OpenDoor>();
            List<KeyValuePair<UInt16, UInt32>> stateChanges = new List<KeyValuePair<UInt16, UInt32>>();

            lock (client.playerData.openDoors)
            {
                foreach (OpenDoor door in client.playerData.openDoors)
                {
                    DoorStage? next = NextStage(door, now);
                    if (next.HasValue)
                    {
                        door.Stage = next.Value;
                        stateChanges.Add(new KeyValuePair<UInt16, UInt32>(door.ViewId,
                            next.Value == DoorStage.Closing ? StateClosing : StateClosed));
                    }
                    else if (ShouldRemove(door, now))
                    {
                        remove.Add(door);
                    }
                }

                foreach (OpenDoor door in remove)
                {
                    client.playerData.openDoors.Remove(door);
                }
            }

            if (stateChanges.Count == 0 && remove.Count == 0)
            {
                return;
            }

            ServerPackets packets = new ServerPackets();
            foreach (KeyValuePair<UInt16, UInt32> change in stateChanges)
            {
                packets.SendUpdateViewStatePacket(client, change.Key, BuildStateUpdate(change.Value));
            }

            foreach (OpenDoor door in remove)
            {
                packets.SendUpdateViewStatePacket(client, door.ViewId, BuildResetUpdate());
                client.FlushQueue();
                packets.sendDeleteViewPacket(client, door.ViewId);
                // Dropping the view lets the next use spawn the door again.
                client.viewMan.removeViewByViewId(door.ViewId);
            }

            client.FlushQueue();
        }
    }
}
