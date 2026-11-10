using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using hds.shared;

namespace hds
{
    /// <summary>Where a neighbourhood contact stands: one of the world's NPC_MissionContact statics (data/mission_contact_npcs.csv).</summary>
    public class MissionContactSpot
    {
        public UInt32 contactId;
        public UInt16 metrId, sector;
        public UInt32 staticId;
        public double x, y, z;
        public string quatHex;
        public string rsiHex;
        public string neighbourhood;

        public MissionContact Contact => MissionContacts.ById(contactId);
    }

    /// <summary>
    /// The neighbourhood contacts in the world. The client's world data has an NPC_MissionContact static (goid 6520) for each
    /// of 51 contacts, and retail spawned the contact bound to it:
    /// <c>08 78 19 0f23b004 32 cd ab 05 3d &lt;position&gt; "The Newsie" (32 bytes) 7e0b0058 2f000000 &lt;orientation&gt;</c>
    /// (Position, ContactName, DescriptionID = the RSI, ContactID, Orientation). Using the contact (retail:
    /// <c>80 c7 &lt;view&gt; &lt;spawn&gt; 02 00</c>) got the contact's mission list straight back, request number 0.
    /// </summary>
    public static class MissionContactNpcs
    {
        public const UInt16 ContactGoid = 6520; // NPC_MissionContact
        private const UInt64 EntityBase = 988000000000UL;
        /// <summary>Retail's Newsie stood 50 units below the static (1295 against 1345), as NPCs stand below their spawners.</summary>
        private const double StandOffset = 50;
        private const double SpawnRange = 10000, DespawnRange = 11000;

        private static readonly object loadLock = new object();
        private static List<MissionContactSpot> spots;

        public static IReadOnlyList<MissionContactSpot> All
        {
            get
            {
                Load();
                return spots;
            }
        }

        public static void Load(string path = null)
        {
            lock (loadLock)
            {
                if (spots != null && path == null)
                {
                    return;
                }

                List<MissionContactSpot> loaded = new List<MissionContactSpot>();
                path = path ?? DataLoader.DataPath("mission_contact_npcs.csv");
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadLines(path).Skip(1))
                    {
                        string[] f = line.Split(',');
                        if (f.Length < 11 || !UInt32.TryParse(f[0], out UInt32 id))
                        {
                            continue;
                        }

                        loaded.Add(new MissionContactSpot
                        {
                            contactId = id,
                            metrId = UInt16.Parse(f[2]),
                            sector = UInt16.Parse(f[3]),
                            // The id as the data files write it: the bytes in order
                            staticId = BitConverter.ToUInt32(StringUtils.hexStringToBytes(f[4]), 0),
                            x = double.Parse(f[5], CultureInfo.InvariantCulture),
                            y = double.Parse(f[6], CultureInfo.InvariantCulture),
                            z = double.Parse(f[7], CultureInfo.InvariantCulture),
                            quatHex = f[8],
                            rsiHex = f[9],
                            neighbourhood = f[10]
                        });
                    }
                }
                else
                {
                    Output.WriteLine("[MISSION] No contact placements at " + path);
                }

                spots = loaded;
            }
        }

        public static MissionContactSpot SpotOf(UInt32 contactId) => All.FirstOrDefault(s => s.contactId == contactId);

        /// <summary>The nearest contact in the district, or null.</summary>
        public static MissionContactSpot Nearest(UInt16 metrId, double x, double z)
        {
            return All.Where(s => s.metrId == metrId).OrderBy(s => (s.x - x) * (s.x - x) + (s.z - z) * (s.z - z)).FirstOrDefault();
        }

        public static UInt64 EntityId(MissionContactSpot spot) => EntityBase + spot.contactId;

        /// <summary>The contact as retail spawned it: bound to its static, with its name, look and contact id.</summary>
        public static GameObject BuildContactObject(MissionContactSpot spot)
        {
            MissionContact contact = spot.Contact;
            AttributeClass3833 obj = new AttributeClass3833("NPC_MissionContact", ContactGoid);
            obj.SetRelatedStaticObjId(spot.staticId);
            obj.DisableAllAttributes();
            obj.Position.setValue(NumericalUtils.doublesToLtVector3d(spot.x, spot.y - StandOffset, spot.z));
            obj.ContactName.setValue(StringUtils.stringToBytes(contact?.name ?? ""));
            obj.DescriptionID.setValue(StringUtils.hexStringToBytes(spot.rsiHex));
            obj.ContactID.setValue(spot.contactId);
            obj.Orientation.setValue(StringUtils.hexStringToBytes(spot.quatHex));
            return obj;
        }

        /// <summary>Spawns the contacts near each player and takes away those out of range. Called by the view thread.</summary>
        public static void CheckViews(List<WorldClient> clients)
        {
            foreach (WorldClient client in clients)
            {
                if (client == null || !client.Alive || !client.playerData.getOnWorld() || client.playerData.waitForRPCShutDown)
                {
                    continue;
                }

                double px = 0, py = 0, pz = 0;
                NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
                UInt16 district = (UInt16)client.playerData.getDistrictId();
                HashSet<UInt64> keep = new HashSet<UInt64>();
                foreach (MissionContactSpot spot in All)
                {
                    if (spot.metrId != district || spot.Contact == null)
                    {
                        continue;
                    }

                    double dx = spot.x - px, dz = spot.z - pz, distance = Math.Sqrt(dx * dx + dz * dz);
                    UInt64 entity = EntityId(spot);
                    ClientView existing = client.viewMan.FindViewForEntity(entity);
                    if (distance <= DespawnRange && existing != null && existing.viewCreated)
                    {
                        keep.Add(entity);
                    }
                    else if (distance <= SpawnRange)
                    {
                        keep.Add(entity);
                        new ServerPackets().SendSpawnGameObject(client, BuildContactObject(spot), entity);
                    }
                }

                foreach (ClientView view in client.viewMan.Snapshot())
                {
                    if (view.viewCreated && view.GoID == ContactGoid && view.entityId >= EntityBase &&
                        view.entityId < EntityBase + 100000 && !keep.Contains(view.entityId))
                    {
                        new ServerPackets().sendDeleteViewPacket(client, view.ViewID);
                        client.viewMan.removeViewByViewId(view.ViewID);
                    }
                }
            }
        }

        /// <summary>A contact's view was used (retail: 80 c7 &lt;view&gt; &lt;spawn&gt; 02 00): their mission list. False if it isn't one.</summary>
        public static bool OnUse(WorldClient client, ClientView view, byte use)
        {
            if (view == null || view.GoID != ContactGoid || view.entityId < EntityBase)
            {
                return false;
            }

            MissionContact contact = MissionContacts.ById((UInt32)(view.entityId - EntityBase));
            if (contact == null)
            {
                return false;
            }

            SendList(client, contact, 0);
            Output.WriteDebugLog("[MISSION] " + contact.name + " used (" + use.ToString("x2") + ")");
            return true;
        }

        /// <summary>A contact's list, as the mission list request answers it.</summary>
        public static void SendList(WorldClient client, MissionContact contact, UInt16 txn)
        {
            ClientData data = client.playerData;
            SocialHandler.EnsureTeam(client);
            if (data.completedMissions == null)
            {
                data.completedMissions = Store.dbManager.WorldDbHandler.GetCompletedMissions(data.getCharID());
            }

            List<Mission> list = contact.IsNeighbourhood
                ? NeighbourhoodMissions.For(contact, data.completedMissions)
                : DataLoader.getInstance().FindMissions(contact, data.completedMissions);
            data.currentMissionList = list;
            data.selectedMissionIndex = -1;
            new ServerPackets().sendMissionList(txn, contact, list, client);
            client.FlushQueue();
            Output.WriteDebugLog("[MISSION] " + contact.name + " listed " + list.Count + " missions");
        }

        /// <summary>Dev (?contact): a spot just in front of a contact, to stand at.</summary>
        public static (int x, int y, int z) InFrontOf(MissionContactSpot spot)
        {
            // Orientation is a quaternion (x, y, z, w) about the y axis; the contact faces +z rotated by it
            byte[] q = StringUtils.hexStringToBytes(spot.quatHex);
            double qy = BitConverter.ToSingle(q, 4), qw = BitConverter.ToSingle(q, 12);
            double angle = 2 * Math.Atan2(qy, qw);
            return ((int)(spot.x + 200 * Math.Sin(angle)), (int)(spot.y - StandOffset), (int)(spot.z + 200 * Math.Cos(angle)));
        }
    }
}
