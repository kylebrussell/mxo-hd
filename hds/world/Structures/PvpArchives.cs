using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using hds.shared;

namespace hds
{
    /// <summary>Archive books and transporters. GOB Ticket/Archivist fields and rshooks.LookupCID/LookupConstructData.
    /// Map IDs used by the server are not retail construct IDs (base 11). See docs/CLIENT-COMBAT-ACTIONS.md.</summary>
    public static class PvpArchives
    {
        public const byte BookSlot = 106;
        public sealed class Ticket
        {
            public uint Goid, Organization;
            public int Id, MinLevel, MaxLevel;
        }
        private static readonly ushort[] maps = { 0, 6, 8, 10, 11, 4, 5, 7, 9 };
        private static readonly string[] keys = { "", "", "", "", "archive01", "archive02", "archive_ashencourte", "archive_datamine", "archive_sakura", "archive_sati", "archive_widowsmoor", "archive_yuki" };
        private static readonly uint[] returners = { 0, 38271, 38272, 38273, 38275, 38274, 38276 };
        private static readonly Lazy<Dictionary<uint, Ticket>> tickets = new Lazy<Dictionary<uint, Ticket>>(() =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("pvp_archives.json")));
            var result = new Dictionary<uint, Ticket>();
            foreach (var row in doc.RootElement.GetProperty("objects").EnumerateArray())
            {
                var f = row.GetProperty("fields");
                if (!f.TryGetProperty("Ticket/PvPID", out var id)) continue;
                var t = new Ticket { Goid = row.GetProperty("goid").GetUInt32(), Id = id.GetInt32(),
                    Organization = f.GetProperty("Ticket/OrganizationID").GetUInt32(),
                    MinLevel = f.GetProperty("Ticket/MinLevel").GetInt32(), MaxLevel = f.GetProperty("Ticket/MaxLevel").GetInt32() };
                result[t.Goid] = t;
            }
            return result;
        });
        private sealed class Session
        {
            public uint OriginMap;
            public string OriginKey;
            public byte[] OriginPosition;
            public long LastCycle = -1;
            public readonly Dictionary<ulong, byte> Views = new Dictionary<ulong, byte>();
        }
        private static readonly ConditionalWeakTable<WorldClient, Session> sessions = new ConditionalWeakTable<WorldClient, Session>();
        private static readonly Lazy<List<StaticWorldObject>> senders = new Lazy<List<StaticWorldObject>>(() =>
            DataLoader.getInstance().WorldObjectsDB.Where(s => s.metrId == 1 &&
                (MissionSites.Goid(s) == 38278 || MissionSites.Goid(s) == 38279 || MissionSites.Goid(s) == 38282)).ToList());
        private static readonly Dictionary<(ushort, uint), NavPoint> arrivals = new Dictionary<(ushort, uint), NavPoint>();
        public static Ticket Find(uint goid) => tickets.Value.TryGetValue(goid, out var t) ? t : null;
        public static ushort MapFor(int id) => id > 0 && id < maps.Length ? maps[id] : (ushort)0;
        public static uint ConstructId(uint map)
        {
            if (!IsArchive(map)) return map;
            int id = Array.IndexOf(maps, (ushort)map, 1);
            return id > 0 ? (uint)(10 + id) : map;
        }
        public static bool IsArchive(uint map) => map >= 4 && map <= 11;
        public static string Refusal(Ticket ticket, uint organization, int level, uint senderOrganization)
        {
            if (ticket == null) return "Equip an archive book in the program launcher slot.";
            if (organization != senderOrganization || organization != ticket.Organization)
                return "This archivist and book belong to a different organization.";
            if (level < ticket.MinLevel || level > ticket.MaxLevel)
                return "This archive book requires level " + ticket.MinLevel + " through " + ticket.MaxLevel + ".";
            if (MapFor(ticket.Id) == 0) return "This archive is not available.";
            return null;
        }
        // The six state durations total 180 s. These are the actual GOB sender events, not a new timer protocol.
        public static uint Stage(double seconds)
        {
            double t = ((seconds % 180) + 180) % 180;
            return t < 118 ? 0xc1fu : t < 163 ? 0xc21u : t < 174 ? 0xc1du : t < 176 ? 0xc20u : t < 178 ? 0xc22u : 0xc1eu;
        }
        public static uint Organization(WorldClient c) => c.playerInstance.OrganizationID.getValue()[0];
        private static uint SenderOrg(uint goid) => goid == 38282 ? 1u : goid == 38278 ? 2u : 3u;
        private static bool Available(WorldClient c) => !c.playerData.isDead && !c.playerData.duelDeath && c.playerData.duelId == 0 &&
            !CombatManager.GetHandler(c).isCombatRunning && ControlActions.CanUseAbilities(c);
        private static void Say(WorldClient c, string text) => new ServerPackets().sendSystemChatMessage(c, text, "SYSTEM");

        /// <summary>Mount/unmount only the archive book slot. No stack manipulation or clothing changes.</summary>
        public static bool Equipment(WorldClient c, byte source, byte dest)
        {
            if (source != BookSlot && dest != BookSlot) return false;
            if (!Available(c)) return true;
            var db = Store.dbManager.WorldDbHandler;
            var inventory = db.GetInventoryItems(c.playerData.getCharID());
            var item = inventory.FirstOrDefault(i => i.Slot == source);
            if (item.Goid == 0 || Find(item.Goid) == null || (source != BookSlot && (source < 1 || source > 96)) ||
                (dest != BookSlot && (dest < 1 || dest > 96))) return true;
            if (inventory.Any(i => i.Slot == dest)) { Say(c, "Empty the destination slot before moving the archive book."); return true; }
            db.UpdateInventorySlot(source, dest, c.playerData.getCharID());
            new ServerPackets().sendInventoryItemMove(source, dest, c);
            c.FlushQueue();
            return true;
        }

        public static bool Arrival(ushort map, uint org, out NavPoint point)
        {
            point = default;
            if (!IsArchive(map) || org < 1 || org > 6) return false;
            lock (arrivals)
            {
                if (arrivals.TryGetValue((map, org), out point)) return true;
                // rshooks.py uses organization 5 = PluribusNeo, 6 = Cypherites.
                double[][] source = map == 4 ? new[] {
                        new double[] {-4000,95,-46000},
                        new double[] {23000,95,5000},
                        new double[] {-43000,1095,5000},
                        new double[] {23000,1095,-17000},
                        new double[] {-43000,95,-17000},
                        new double[] {-16000,95,33000} } :
                    map == 5 ? new[] {
                        new double[] {-3000,-105,12000},
                        new double[] {-3000,-105,40000},
                        new double[] {16000,-105,28000},
                        new double[] {11000,-105,14000},
                        new double[] {-12000,-105,26000},
                        new double[] {11000,-105,40000} } :
                    map == 8 || map == 11 ? new[] {
                        new double[] {15500,295,15500},
                        new double[] {-25500,295,35500},
                        new double[] {-24500,295,-5500},
                        new double[] {4500,295,35500},
                        new double[] {5500,295,-5500},
                        new double[] {-35500,295,14500} } :
                    new[] {
                        new double[] {-18500,95,-7500},
                        new double[] {18000,95,-6000},
                        new double[] {5000,95,15000},
                        new double[] {-18000,95,6000},
                        new double[] {-5000,95,-15000},
                        new double[] {18500,95,7500} };
                bool found;
                if (map == 7 || map == 9)
                    found = NavMesh.NearestEntry(map, -500, 2100, 500, out point);
                else
                {
                    double[] p = source[org - 1];
                    found = NavMesh.Project(map, p[0], p[1], p[2], out point, 1000);
                }
                if (found)
                {
                    arrivals[(map, org)] = point;
                    if (map == 7 || map == 9)
                        for (uint other = 1; other <= 6; other++) arrivals[(map, other)] = point;
                }
                return found;
            }
        }

        public static bool Enter(WorldClient c, uint senderOrg)
        {
            if (!Available(c) || c.playerData.getDistrictId() != 1) return false;
            uint goid = Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()).FirstOrDefault(i => i.Slot == BookSlot).Goid;
            Ticket t = Find(goid);
            string refusal = Refusal(t, Organization(c), PvpFlag.LevelOf(c), senderOrg);
            if (refusal != null) { Say(c, refusal); return false; }
            ushort map = MapFor(t.Id);
            if (!Arrival(map, Organization(c), out NavPoint p)) { Say(c, "No safe arrival floor was found for this archive."); return false; }
            Session s = sessions.GetOrCreateValue(c);
            s.OriginMap = c.playerData.getDistrictId(); s.OriginKey = c.playerData.getDistrict();
            s.OriginPosition = (byte[])c.playerInstance.Position.getValue().Clone();
            Move(c, map, keys[map], NumericalUtils.doublesToLtVector3d(p.x, p.y, p.z));
            return true;
        }
        private static void Move(WorldClient c, uint map, string key, byte[] position)
        {
            DuelHandler.OnUnavailable(c);
            CombatManager.GetHandler(c).AbandonCombat();
            c.playerData.setOnWorld(false);
            c.playerData.setDistrictId(map); c.playerData.setDistrict(key);
            c.playerInstance.Position.setValue(position);
            Store.dbManager.WorldDbHandler.SavePlayer(c);
            c.messageQueue.addRpcMessage(new byte[] { 0x81, 0x07 });
            c.FlushQueue();
        }
        public static bool OnUse(WorldClient c, ClientView view, byte action)
        {
            if (view?.GoID == DataMineReturners.GoId)
            {
                double[] genericAt = Position(c);
                if (action == 2 && Available(c) && !c.playerData.waitForRPCShutDown &&
                    DataMineReturners.CanUseView(view,c.viewMan.getViewById(view.ViewID),DataMineReturners.Bindings,
                        c.playerData.getDistrictId(),c.playerData.getOnWorld(),genericAt[0],genericAt[1],genericAt[2])) ReturnToOrigin(c);
                return true;
            }
            int org = Array.IndexOf(returners, view?.GoID ?? 0, 1);
            if (org < 1 || !IsArchive(c.playerData.getDistrictId())) return false;
            if (action != 2 || !Available(c)) return true;
            if (Organization(c) != org) { Say(c, "This teleporter belongs to a different organization."); return true; }
            if (!Arrival((ushort)c.playerData.getDistrictId(), (uint)org, out NavPoint p)) return true;
            double[] at = Position(c);
            if (Math.Sqrt(Math.Pow(at[0]-p.x,2)+Math.Pow(at[1]-p.y,2)+Math.Pow(at[2]-p.z,2)) > 1000) return true;
            ReturnToOrigin(c);
            return true;
        }
        private static void ReturnToOrigin(WorldClient c)
        {
            Session s = sessions.GetOrCreateValue(c);
            // A reconnect has no in-memory origin. Mara is the safe server fallback, rather than tutorial world 0.
            Move(c, s.OriginMap == 0 ? 1u : s.OriginMap, s.OriginKey ?? "slums",
                s.OriginPosition ?? NumericalUtils.doublesToLtVector3d(23828,495,13984));
        }
        private static double[] Position(WorldClient c)
        {
            double x=0,y=0,z=0;
            NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(),ref x,ref y,ref z);
            return new[] {x,y,z};
        }
        public static void Check(WorldClient c, double now)
        {
            if (!c.playerData.getOnWorld() || c.playerData.waitForRPCShutDown) return;
            Session s = sessions.GetOrCreateValue(c);
            lock (s)
            {
                double[] at = Position(c);
                uint map = c.playerData.getDistrictId();
                var wanted = new HashSet<ulong>();
                void View(ulong entity, ushort goid, double x, double y, double z, StaticWorldObject spot, uint state)
                {
                    if (Math.Abs(at[1]-y) > 3000 || Math.Pow(at[0]-x,2)+Math.Pow(at[2]-z,2) > 5000*5000) return;
                    wanted.Add(entity);
                    ClientView v = c.viewMan.FindViewForEntity(entity);
                    if (v == null || !v.viewCreated)
                    {
                        FieldInfo field = typeof(GameObjectDefinitions).GetField("Object" + goid);
                        if (field == null) return;
                        var obj = (GameObject)Activator.CreateInstance(field.FieldType, field.Name, goid);
                        obj.DisableAllAttributes();
                        if (spot != null) obj.SetRelatedStaticObjId(spot.mxoStaticId); else obj.UnsetRelatedStaticObjId();
                        void Attr(string name, byte[] value) { if (obj.GetType().GetField(name)?.GetValue(obj) is Attribute a) { a.enable(); a.setValue(value); } }
                        Attr("Position", NumericalUtils.doublesToLtVector3d(x,y,z));
                        Attr("Orientation", spot == null ? StringUtils.hexStringToBytes("0000000000000000000000000000803f") : StringUtils.hexStringToBytes(spot.quat));
                        if (state != 0) Attr("CurrentState", BitConverter.GetBytes(state));
                        new ServerPackets().SendSpawnGameObject(c, obj, entity);
                        v = c.viewMan.FindViewForEntity(entity);
                        if (v != null) v.staticObject = spot;
                        s.Views[entity] = 0;
                        if (state != 0) ObjectStates.Track(c, entity, goid, state);
                    }
                    else if (state != 0 && ObjectStates.TrackedState(c, entity) != state) ObjectStates.SetState(c, entity, state);
                }
                if (map == 1)
                    foreach (var spot in senders.Value)
                    {
                        ushort goid = MissionSites.Goid(spot);
                        View(0xa100000000000000UL | spot.mxoStaticId, goid, spot.pos_x, spot.pos_y, spot.pos_z, spot, Stage(now));
                        long cycle = (long)Math.Floor(now/180);
                        if (Stage(now) == 0xc1e && s.LastCycle != cycle && Available(c) &&
                            Math.Pow(at[0]-spot.pos_x,2)+Math.Pow(at[1]-spot.pos_y,2)+Math.Pow(at[2]-spot.pos_z,2) <= 500*500)
                        {
                            s.LastCycle = cycle;
                            if (Enter(c, SenderOrg(goid))) break;
                        }
                    }
                else if (IsArchive(map))
                    for (uint org = 1; org <= 6; org++)
                        if ((map != 7 && map != 9 || org == Organization(c)) && Arrival((ushort)map, org, out NavPoint p))
                            View(0xa200000000000000UL | map << 8 | org, (ushort)returners[org], p.x,p.y,p.z,null,0);
                // These actual statics are NPC/model views, not automatically visible merely because CSV exists.
                // Keep the existing generated map7 fallback ALWAYS: exact retail arrival/route remains unknown.
                if (map == 7)
                    foreach (var spot in DataMineReturners.Bindings)
                        View(DataMineReturners.Entity(spot),DataMineReturners.GoId,spot.pos_x,spot.pos_y,spot.pos_z,spot,0);
                foreach (ulong entity in s.Views.Keys.Where(e => !wanted.Contains(e)).ToArray())
                {
                    var v = c.viewMan.FindViewForEntity(entity);
                    if (v != null) { new ServerPackets().sendDeleteViewPacket(c,v.ViewID); c.viewMan.removeViewByViewId(v.ViewID); }
                    s.Views.Remove(entity);
                }
                c.FlushQueue();
            }
        }
    }
}
