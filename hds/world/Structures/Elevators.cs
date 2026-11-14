using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Building elevators. Each floor of a shaft has its own ElevatorPanel static object.
    ///
    /// Retail (luizoe Recursion, 07/05/09): using a panel (80 c8 &lt;panel&gt; 00 00) got 81 3f &lt;sector u16&gt; &lt;region u16&gt;,
    /// the client showed the floor list, and the button picked came back as 81 40 &lt;button u16&gt;, numbered from the bottom
    /// floor up. The server then moved the player to that floor.
    ///
    /// The region is the floor's id in the client's own world file (slums_barrens_full.cooked_cod and the other districts'
    /// equivalents: one TransportGroup per shaft, one TransportSpace per floor, its TSPI the region, which the client calls
    /// PrefabID). A sector's k-th ElevatorPanel by static id is its k-th TransportSpace. data/elevator_panels.csv has that
    /// mapping for every panel; it gives retail's answer for all 37 of the 81 3f records in the captures. With any other
    /// region the client says "Unable to properly establish the elevator panel UI!".
    /// </summary>
    public static class Elevators
    {
        public const UInt16 PanelGoId = 6952;

        /// <summary>One panel's floor, from data/elevator_panels.csv.</summary>
        public class PanelFloor
        {
            public UInt16 metrId, sector, region, shaft, floorFromBottom, floors;
            public UInt32 mxoStaticId;
            public double x, y, z;
        }

        private static readonly object loadLock = new object();
        private static Dictionary<(UInt16, UInt16, UInt32), PanelFloor> panels;
        private static Dictionary<(UInt16, UInt16, UInt16, UInt16), PanelFloor> floors;

        public static bool IsPanel(StaticWorldObject obj)
        {
            return obj?.type != null && NumericalUtils.ByteArrayToUint16(obj.type, 1) == PanelGoId;
        }

        /// <summary>The panel's floor in the client's data, or null.</summary>
        public static PanelFloor Find(StaticWorldObject panel)
        {
            Load();
            return panel != null && panels.TryGetValue((panel.metrId, panel.sectorID, panel.mxoStaticId), out PanelFloor floor) ? floor : null;
        }

        /// <summary>The panel of the same shaft for this button (81 40 numbers floors from 1 at the bottom).</summary>
        public static PanelFloor FloorOf(PanelFloor from, UInt16 button)
        {
            Load();
            return floors.TryGetValue((from.metrId, from.sector, from.shaft, (UInt16)(button - 1)), out PanelFloor floor) ? floor : null;
        }

        // Bind a door to the uniquely nearest panel in the shipped sector data. No player-distance
        // threshold is involved; the floor-pair delta is checked again before authorizing departure.
        internal static PanelFloor PanelForDoor(StaticWorldObject door)
        {
            Load();
            if (door == null) return null;
            double Distance(PanelFloor p) => (p.x-door.pos_x)*(p.x-door.pos_x) +
                (p.y-door.pos_y)*(p.y-door.pos_y) + (p.z-door.pos_z)*(p.z-door.pos_z);
            var candidates=panels.Values.Where(p=>p.metrId==door.metrId && p.sector==door.sectorID)
                .OrderBy(Distance).Take(2).ToArray();
            return candidates.Length==0 || (candidates.Length==2 && Distance(candidates[0])==Distance(candidates[1]))
                ? null : candidates[0];
        }

        /// <summary>The player used an elevator panel: show its floor list.</summary>
        public static void OnPanelUsed(WorldClient client, StaticWorldObject panel)
        {
            PanelFloor floor = Find(panel);
            client.playerData.elevatorPanel = panel;
            UInt16 region = client.playerData.elevatorRegionOverride ?? floor?.region ?? 0;
            Output.WriteDebugLog("[ELEVATOR] Panel " + MissionSites.DoorHex(panel) + " in sector " + panel.sectorID + ": " +
                                 (floor == null ? "not in the client's data" :
                                     "floor " + (floor.floorFromBottom + 1) + " of " + floor.floors + ", region " + floor.region));
            client.messageQueue.addRpcMessage(BuildPanel(panel.sectorID, region));
            client.FlushQueue();
        }

        /// <summary>81 40 &lt;button u16&gt;: take the player to that floor of the shaft they used.</summary>
        public static void OnFloorChosen(WorldClient client, UInt16 button)
        {
            if(client.playerData.missionProgress==null)ChooseFloor(client,button); // ordinary nonmission behavior
            else MissionTransportState.Run(client, () => {ChooseFloor(client, button);return true;});
        }

        private static void ChooseFloor(WorldClient client, UInt16 button)
        {
            StaticWorldObject panel = client.playerData.elevatorPanel;
            PanelFloor from = Find(panel);
            PanelFloor to = from == null ? null : FloorOf(from, button);
            if (to == null)
            {
                Output.WriteDebugLog("[ELEVATOR] Button " + button + " isn't a floor of this shaft");
                return;
            }

            // Same place in the car, on the other floor (panel ids repeat across copies of a building, so the floor's own
            // panel comes from the client's data, not a lookup by id)
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            y += to.y - from.y;
            client.playerData.elevatorPanel = new StaticWorldObject
            {
                metrId = to.metrId, sectorID = to.sector, mxoStaticId = to.mxoStaticId, pos_x = to.x, pos_y = to.y, pos_z = to.z,
                type = BitConverter.GetBytes(PanelGoId)
            };
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
            client.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket(client, (float)x, (float)y, (float)z), false);
            Output.WriteDebugLog("[ELEVATOR] Button " + button + ": to " + x + "," + y + "," + z);
            client.messageQueue.addRpcMessage(PacketsUtils.BuildElevatorDing(to.region, to.sector));
            client.playerData.missionProgress?.CommitGeneratedPanelFloor(from, to);
            MissionStaging.OnFloorReached(client, x, y, z);
            client.FlushQueue();
        }

        /// <summary>81 3f &lt;sector u16&gt; &lt;region u16&gt;.</summary>
        public static byte[] BuildPanel(UInt16 sector, UInt16 region)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_ELEVATOR_PANEL, 0);
            pak.AddUint16(sector, 1);
            pak.AddUint16(region, 1);
            return pak.ReturnFinalPacket();
        }

        public static void Load(string path = null)
        {
            lock (loadLock)
            {
                if (panels != null && path == null)
                {
                    return;
                }

                panels = new Dictionary<(UInt16, UInt16, UInt32), PanelFloor>();
                floors = new Dictionary<(UInt16, UInt16, UInt16, UInt16), PanelFloor>();
                path = path ?? DataLoader.DataPath("elevator_panels.csv");
                if (!File.Exists(path))
                {
                    Output.WriteLine("[ELEVATOR] No panel data at " + path);
                    return;
                }

                foreach (string line in File.ReadLines(path))
                {
                    string[] f = line.Split(',');
                    if (f.Length < 10 || !UInt16.TryParse(f[0], out UInt16 metrId))
                    {
                        continue;
                    }

                    PanelFloor floor = new PanelFloor
                    {
                        metrId = metrId,
                        sector = UInt16.Parse(f[1]),
                        // The ids as the data files and the loader write them: the bytes in order
                        mxoStaticId = BitConverter.ToUInt32(StringUtils.hexStringToBytes(f[2]), 0),
                        region = UInt16.Parse(f[3]),
                        shaft = UInt16.Parse(f[4]),
                        floorFromBottom = UInt16.Parse(f[5]),
                        floors = UInt16.Parse(f[6]),
                        x = double.Parse(f[7], System.Globalization.CultureInfo.InvariantCulture),
                        y = double.Parse(f[8], System.Globalization.CultureInfo.InvariantCulture),
                        z = double.Parse(f[9], System.Globalization.CultureInfo.InvariantCulture)
                    };
                    panels[(floor.metrId, floor.sector, floor.mxoStaticId)] = floor;
                    floors[(floor.metrId, floor.sector, floor.shaft, floor.floorFromBottom)] = floor;
                }
            }
        }
    }
}
