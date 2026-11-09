using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Building elevators. Each floor of a shaft has its own ElevatorPanel static object, stacked at the same x and z, so
    /// a shaft's floors are its panels from the bottom up.
    ///
    /// Retail (luizoe Recursion, 07/05/09): using a panel (80 c8 &lt;panel&gt; 00 00) got 81 3f &lt;sector u16&gt; &lt;region u16&gt;,
    /// the client showed the floor list, and the button picked came back as 81 40 &lt;button u16&gt;, numbered from the bottom
    /// floor up. The server then moved the player to that floor: button 7 of a shaft whose bottom floor was at 145 put them
    /// at y 2495, and button 1 at y 95.
    /// </summary>
    public static class Elevators
    {
        public const UInt16 PanelGoId = 6952;

        /// <summary>Panels in a shaft are this close in x and z (they're stacked exactly).</summary>
        private const double ShaftTolerance = 50;

        private static readonly object cacheLock = new object();
        private static Dictionary<UInt32, List<StaticWorldObject>> panelsBySector;

        public static bool IsPanel(StaticWorldObject obj)
        {
            return obj?.type != null && NumericalUtils.ByteArrayToUint16(obj.type, 1) == PanelGoId;
        }

        /// <summary>The panels of the shaft this panel is in, bottom floor first.</summary>
        public static List<StaticWorldObject> Shaft(StaticWorldObject panel)
        {
            List<StaticWorldObject> shaft = new List<StaticWorldObject>();
            if (!PanelsBySector().TryGetValue(SectorKey(panel), out List<StaticWorldObject> sector))
            {
                return shaft;
            }

            foreach (StaticWorldObject other in sector)
            {
                if (Math.Abs(other.pos_x - panel.pos_x) <= ShaftTolerance && Math.Abs(other.pos_z - panel.pos_z) <= ShaftTolerance)
                {
                    shaft.Add(other);
                }
            }

            shaft.Sort((a, b) => a.pos_y.CompareTo(b.pos_y));
            return shaft;
        }

        /// <summary>The player used an elevator panel: show its floor list.</summary>
        public static void OnPanelUsed(WorldClient client, StaticWorldObject panel)
        {
            List<StaticWorldObject> shaft = Shaft(panel);
            int floor = shaft.IndexOf(panel);
            client.playerData.elevatorPanel = panel;
            UInt16 region = client.playerData.elevatorRegionOverride ?? RegionFor(panel, floor);
            Output.WriteDebugLog("[ELEVATOR] Panel " + panel.mxoStaticId.ToString("x8") + " in sector " + panel.sectorID +
                                 ": floor " + (floor + 1) + " of " + shaft.Count + ", region " + region);
            client.messageQueue.addRpcMessage(BuildPanel(panel.sectorID, region));
            client.FlushQueue();
        }

        /// <summary>81 40 &lt;button u16&gt;: take the player to that floor of the shaft they used.</summary>
        public static void OnFloorChosen(WorldClient client, UInt16 button)
        {
            StaticWorldObject panel = client.playerData.elevatorPanel;
            if (panel == null)
            {
                return;
            }

            List<StaticWorldObject> shaft = Shaft(panel);
            if (button < 1 || button > shaft.Count)
            {
                Output.WriteDebugLog("[ELEVATOR] Button " + button + " isn't a floor of this shaft (" + shaft.Count + ")");
                return;
            }

            StaticWorldObject destination = shaft[button - 1];
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);

            // Same place in the car, on the other floor.
            y += destination.pos_y - panel.pos_y;
            client.playerData.elevatorPanel = destination;
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
            client.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket((float)x, (float)y, (float)z), false);
            Output.WriteDebugLog("[ELEVATOR] Button " + button + ": to " + x + "," + y + "," + z);
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

        /// <summary>
        /// Retail's second field. It fell by one per floor up the shaft (0x1f at the bottom of sector 405's shaft at 60300,
        /// 33390, 0x19 six floors up; 0x60 at the bottom of sector 1334's shaft at 46700, 22010, 0x5c four floors up), and the
        /// teleport after the ride carried the destination's value in the same place, so it looks like the client's id for the
        /// floor's interior. How the base is assigned isn't known; these two shafts use retail's.
        /// </summary>
        public static UInt16 RegionFor(StaticWorldObject panel, int floor)
        {
            UInt16 bottom;
            if (panel.sectorID == 405 && Math.Abs(panel.pos_x - 60300) < 1 && Math.Abs(panel.pos_z - 33390) < 1)
            {
                bottom = 0x1f;
            }
            else if (panel.sectorID == 1334 && Math.Abs(panel.pos_x - 46700) < 1 && Math.Abs(panel.pos_z - 22010) < 1)
            {
                bottom = 0x60;
            }
            else
            {
                bottom = 0x1f;
            }

            return (UInt16)Math.Max(0, bottom - Math.Max(0, floor));
        }

        private static UInt32 SectorKey(StaticWorldObject obj) => ((UInt32)obj.metrId << 16) | obj.sectorID;

        private static Dictionary<UInt32, List<StaticWorldObject>> PanelsBySector()
        {
            lock (cacheLock)
            {
                if (panelsBySector != null)
                {
                    return panelsBySector;
                }

                Dictionary<UInt32, List<StaticWorldObject>> built = new Dictionary<UInt32, List<StaticWorldObject>>();
                HashSet<UInt64> seen = new HashSet<UInt64>();
                foreach (StaticWorldObject obj in DataLoader.getInstance().WorldObjectsDB)
                {
                    if (!IsPanel(obj) || !seen.Add(((UInt64)obj.metrId << 32) | obj.mxoStaticId))
                    {
                        continue;
                    }

                    UInt32 key = SectorKey(obj);
                    if (!built.TryGetValue(key, out List<StaticWorldObject> list))
                    {
                        list = new List<StaticWorldObject>();
                        built[key] = list;
                    }

                    list.Add(obj);
                }

                panelsBySector = built;
                return built;
            }
        }
    }
}
