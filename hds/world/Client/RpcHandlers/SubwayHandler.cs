using System;
using System.Collections.Generic;
using System.IO;
using hds.shared;

namespace hds
{
    /// <summary>
    /// The subway map's records, from client.dll (no retail capture has a subway ride):
    /// <list type="bullet">
    /// <item>81 3a &lt;Position: 3 × f64&gt; (class 0x621429c0): opens the map (window 0x47, 0x620fd9e0). The client looks for
    /// the line and connector within 2000 units of the position in its own .metr data; without one the map stays shut.</item>
    /// <item>81 3d, no body (0x620fc150): the client's answer once the map is open.</item>
    /// <item>81 3e &lt;Seconds u8&gt; (0x620fccd0): the departure countdown, shown as m:ss. The client starts at 60 and only
    /// takes a lower value; at 0 it says "The subway is now leaving the station... sorry, but you missed it!" and
    /// sends 81 3b.</item>
    /// <item>81 3b, no body (0x620fc490): Cancel, or the countdown ran out. The map closes.</item>
    /// <item>81 3c &lt;WorldID u16&gt; &lt;NetworkID u16&gt; &lt;ConnectorID u16&gt; (0x620fc520): the station picked. WorldID is
    /// the universe id of the district whose map is shown (12, 7, 169: a link connector switches the map to another
    /// district when Client_AllowCrossWorldSubwayTeleport is on, its default); NetworkID and ConnectorID index that
    /// district's NETS chunk (on another district's map the line with the same name). The map closes.</item>
    /// </list>
    /// What happens next is ours: the player is moved to the picked connector and the world reloads (81 07, as a hardline
    /// teleport does), which is the loading screen the client's subway strings ("Loading World...") are for.
    /// </summary>
    public static class SubwayPackets
    {
        public static byte[] BuildOpenMap(double x, double y, double z)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x81);
                writer.Write((byte)0x3a);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                return stream.ToArray();
            }
        }

        public static byte[] BuildCountdown(byte seconds) => new byte[] { 0x81, 0x3e, seconds };

        /// <summary>81 3c after its two opcode bytes.</summary>
        public static bool TryParsePick(byte[] rpcData, out UInt16 world, out UInt16 network, out UInt16 connector)
        {
            world = network = connector = 0;
            if (rpcData == null || rpcData.Length < 6)
            {
                return false;
            }
            world = BitConverter.ToUInt16(rpcData, 0);
            network = BitConverter.ToUInt16(rpcData, 2);
            connector = BitConverter.ToUInt16(rpcData, 4);
            return true;
        }
    }

    public static class SubwayHandler
    {
        /// <summary>The client's own starting value for the countdown.</summary>
        public const byte DepartureSeconds = 60;

        /// <summary>How far (world units) a player using a subway car may be from its station's connector. Cars stand about
        /// 400 from their connector; this leaves room for where the player stands.</summary>
        public const double MaxStationDistance = 4000;

        private class Ride
        {
            public SubwayStation From;
            public double OpenedAt;
            public bool Counting;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<WorldClient, Ride> Rides = new Dictionary<WorldClient, Ride>();

        private static double Now() => TimeUtils.GetSimTimeSeconds();

        private static void Send(WorldClient client, byte[] record)
        {
            client.messageQueue.addRpcMessage(record);
            client.FlushQueue();
        }

        private static void Position(WorldClient client, out double x, out double y, out double z)
        {
            x = y = z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
        }

        /// <summary>
        /// The player used a subway car (80 c7 02 on a 6568 view): open the map at the nearest station. With
        /// <paramref name="anyDistance"/> (dev command ?subway) the station may be anywhere in the district.
        /// </summary>
        public static bool OpenMap(WorldClient client, bool anyDistance = false)
        {
            Position(client, out double x, out double y, out double z);
            UInt16 district = (UInt16)client.playerData.getDistrictId();
            SubwayStation station = SubwayNetwork.Nearest(district, x, y, z, out double distance);
            if (station == null || (!anyDistance && distance > MaxStationDistance))
            {
                Output.WriteDebugLog("[SUBWAY] No station near " + x + "," + y + "," + z + " in district " + district +
                                     (station != null ? " (nearest " + station.name + " at " + (int)distance + ")" : ""));
                return false;
            }
            lock (Sync)
            {
                Rides[client] = new Ride { From = station, OpenedAt = Now() };
            }
            Output.WriteDebugLog("[SUBWAY] Map at " + station.name);
            Send(client, SubwayPackets.BuildOpenMap(station.x, station.y, station.z));
            return true;
        }

        /// <summary>Client 81 3d: the map is open. Count down to the departure.</summary>
        public static void OnMapOpen(WorldClient client)
        {
            lock (Sync)
            {
                if (!Rides.TryGetValue(client, out Ride ride) || ride.Counting)
                {
                    return;
                }
                ride.Counting = true;
                Tick(client, ride, DepartureSeconds);
            }
        }

        private static void Tick(WorldClient client, Ride ride, int seconds)
        {
            Send(client, SubwayPackets.BuildCountdown((byte)seconds));
            if (seconds <= 0)
            {
                return;
            }
            DelayedActions.After(1, () =>
            {
                lock (Sync)
                {
                    if (Rides.TryGetValue(client, out Ride current) && current == ride)
                    {
                        Tick(client, ride, seconds - 1);
                    }
                }
            });
        }

        /// <summary>Client 81 3b: cancelled, or the train left without the player.</summary>
        public static void OnCancel(WorldClient client)
        {
            lock (Sync)
            {
                Rides.Remove(client);
            }
        }

        /// <summary>Why a pick can't be ridden, or null. Pure, for tests.</summary>
        public static string CheckPick(SubwayStation from, SubwayStation to, bool mapOpen, double secondsOpen)
        {
            if (!mapOpen || from == null) return "no subway map open";
            if (secondsOpen > DepartureSeconds + 5) return "the train has left";
            if (to == null) return "no such station";
            if (!to.IsStation) return "not a station";
            if (to.worldId == from.worldId && to.network == from.network && to.connector == from.connector) return "already there";
            return null;
        }

        /// <summary>Client 81 3c: ride to the picked station.</summary>
        public static void OnPick(WorldClient client, byte[] rpcData)
        {
            if (!SubwayPackets.TryParsePick(rpcData, out UInt16 world, out UInt16 network, out UInt16 connector))
            {
                return;
            }
            Ride ride;
            lock (Sync)
            {
                Rides.TryGetValue(client, out ride);
                Rides.Remove(client);
            }
            SubwayStation to = SubwayNetwork.Get(world, network, connector);
            string refusal = CheckPick(ride?.From, to, ride != null, ride != null ? Now() - ride.OpenedAt : 0);
            if (refusal != null)
            {
                Output.WriteDebugLog("[SUBWAY] Pick " + world + "/" + network + "/" + connector + " refused: " + refusal);
                return;
            }
            Output.WriteDebugLog("[SUBWAY] " + SocialHandler.HandleOf(client) + " rides from " + ride.From.name + " to " + to.name);
            MoveTo(client, to);

            // As a hardline teleport: the client unloads the world (81 07), answers 81 08, and loads the district again
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            Send(client, pak.ReturnFinalPacket());
        }

        /// <summary>
        /// Where a rider stands on arrival: on the connector, at standing height. Every station connector lies on the
        /// district's nav mesh floor (SubwayTests), so this is the floor under it plus the 95 retail positions stand above
        /// the floor; without nav data, the connector plus 95.
        /// </summary>
        public static double[] Arrival(SubwayStation to)
        {
            if (NavMesh.Project(to.metrId, to.x, to.y + 95, to.z, out NavPoint point, 400))
            {
                return new[] { to.x, point.y, to.z };
            }
            return new[] { to.x, to.y + 95, to.z };
        }

        /// <summary>Puts the player on the station's connector in its district and saves it.</summary>
        public static void MoveTo(WorldClient client, SubwayStation to)
        {
            double[] at = Arrival(to);
            client.playerData.setDistrict(to.district);
            client.playerData.setDistrictId(to.metrId);
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(at[0], at[1], at[2]));
            Store.dbManager.WorldDbHandler.SavePlayer(client);
        }

        /// <summary>The player left or the world changed: drop any open map.</summary>
        public static void Forget(WorldClient client) => OnCancel(client);
    }
}
