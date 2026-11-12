using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using hds.databases;
using hds.shared;

namespace hds
{
    /// <summary>Data-node locator and persistent, shared light-luggable ground objects. Evidence/choices: DATA-MINING.md.</summary>
    public static class DataMining
    {
        public sealed class Definition
        {
            public ushort Goid;
            public int PickupUse, Level, Size;
            public bool Singleton;
            public double Reach = 100;
            public string HalfExtents;
            public uint[] Exclusive = Array.Empty<uint>();
        }
        public sealed class Ground
        {
            public ulong Id;
            public ushort Goid, District;
            public double X, Y, Z;
            public ulong Entity => 982000000000UL + Id;
            public bool Node => Goid == 606;
        }
        private sealed class Search
        {
            public uint Tool;
            public double Next;
            public ushort District;
            public readonly HashSet<ushort> Markers = new HashSet<ushort>();
        }
        private static readonly object sync = new object();
        private static List<Ground> ground;
        private static readonly ConditionalWeakTable<WorldClient, Search> searches = new ConditionalWeakTable<WorldClient, Search>();
        private static readonly ConditionalWeakTable<WorldClient, StrongBox<double>> mapNext = new ConditionalWeakTable<WorldClient, StrongBox<double>>();
        private static readonly Lazy<Dictionary<ushort, Definition>> definitions = new Lazy<Dictionary<ushort, Definition>>(() =>
        {
            using var json = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("utility_objects.json")));
            return json.RootElement.GetProperty("objects").EnumerateArray().Select(row =>
            {
                var f = row.GetProperty("fields");
                int Number(string key, int fallback = 0) => f.TryGetProperty(key, out var v) ? v.GetInt32() : fallback;
                string String(string key) => f.TryGetProperty(key, out var v) ? v.GetString() : null;
                return new Definition { Goid = row.GetProperty("goid").GetUInt16(), PickupUse = row.GetProperty("pickup_use").GetInt32(),
                    Level = Number("CharStatsPrerequisite/ReqCharLevel"), Size = Number("Luggable/Size"),
                    Singleton = Number("InvItem/Singleton") != 0, HalfExtents = String("Model/HalfExtents") ?? String("Normal/HalfExtents"),
                    Reach = f.TryGetProperty("Usable/MaxDistance", out var reach) ? reach.GetDouble() : 100,
                    Exclusive = f.EnumerateObject().Where(p => p.Name.StartsWith("InvItem/PickupExclusive", StringComparison.Ordinal)).Select(p => p.Value.GetUInt32()).ToArray() };
            }).ToDictionary(d => d.Goid);
        });
        public static Definition Find(ushort goid) => definitions.Value.TryGetValue(goid, out var d) ? d : null;
        public static bool IsLight(ushort goid) => goid != 606 && Find(goid)?.Size == 0;
        public static bool InRange(Ground g, ushort district, double x, double y, double z, double range) =>
            g.District == district && Math.Abs(g.Y - y) <= 250 &&
            (g.X - x) * (g.X - x) + (g.Z - z) * (g.Z - z) <= range * range;
        public static bool CanPickup(Ground g, ushort district, double x, double y, double z, double reach) =>
            InRange(g,district,x,y,z,reach) && (g.X-x)*(g.X-x)+(g.Y-y)*(g.Y-y)+(g.Z-z)*(g.Z-z)<=reach*reach;
        public static string PickupRefusal(Definition d, int level, IEnumerable<Crafting.InventoryItem> inventory)
        {
            if (d == null || d.Goid == 606 || d.Size != 0) return "Heavy luggable carrying is not implemented.";
            if (level < d.Level) return "Your level is too low to pick up this luggable.";
            if (inventory.Any(i => d.Singleton && i.Goid == d.Goid || d.Exclusive.Contains(i.Goid))) return "You already carry an exclusive luggable.";
            return null;
        }
        private static void Load()
        {
            if (ground == null) ground = WorldLuggablesDb.Load().Where(g => Find(g.Goid) != null).ToList();
        }
        private static void Position(WorldClient c, out double x, out double y, out double z)
        {
            x = y = z = 0; NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(), ref x, ref y, ref z);
        }
        public static void Start(WorldClient c, uint tool)
        {
            lock (sync)
            {
                Stop(c); var s = searches.GetOrCreateValue(c); s.Tool = tool; s.District = (ushort)c.playerData.getDistrictId();
            }
        }
        public static void Stop(WorldClient c)
        {
            lock (sync)
            {
                if (!searches.TryGetValue(c, out var s)) return;
                foreach (ushort id in s.Markers) c.messageQueue.addRpcMessage(DataMiningPackets.Remove(id));
                searches.Remove(c);
            }
        }
        public static bool Searching(WorldClient c) { lock (sync) return searches.TryGetValue(c, out _); }

        /// <summary>Visibility-loop entry; nearest nodes and shared light luggables in the recipient's district/floor.</summary>
        public static void Check(WorldClient c)
        {
            if (!c.playerData.getOnWorld() || c.playerData.waitForRPCShutDown) return;
            lock (sync)
            {
                Load(); Position(c, out double x, out double y, out double z);
                ushort district = (ushort)c.playerData.getDistrictId();
                var visible = ground.Where(g => !g.Node && InRange(g, district, x, y, z, 5000)).ToList();
                foreach (Ground g in visible) Spawn(c, g);
                foreach (ClientView v in c.viewMan.Snapshot().Where(v => v.entityId >= 982000000000UL && v.entityId < 983000000000UL).ToList())
                    if (!visible.Any(g => g.Entity == v.entityId)) Delete(c, v);
                if (!searches.TryGetValue(c, out var s)) return;
                if (c.playerData.isDead || district != s.District || UtilityAbilities.Level(c, 621) <= 0 ||
                    !Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()).Any(i => i.Slot >= 1 && i.Slot <= 96 && i.Goid == s.Tool))
                { Stop(c); c.FlushQueue(); return; }
                double now = TimeUtils.GetSimTimeSeconds(); if (now < s.Next) return;
                var (metres, type) = UtilityAbilities.Locators[s.Tool]; s.Next = now + (type == 0 ? 10 : 1);
                var nodes = ground.Where(g => g.Node && InRange(g, district, x, y, z, metres * 100)).OrderBy(g => (g.X-x)*(g.X-x)+(g.Z-z)*(g.Z-z)).Take(64).ToList();
                var desired = new Dictionary<ushort, (uint fx, double x, double y, double z)>();
                // ID zero is what the native 80 cb consumer adjusts. Other IDs are stable for this search pass.
                for (int i = 0; i < nodes.Count; i++) desired[(ushort)i] = (DataMiningPackets.NodeFx, nodes[i].X, nodes[i].Y, nodes[i].Z);
                if (type == 1 && UtilityAbilities.Level(c, 540) > 0)
                {
                    int id = 128;
                    foreach (Ground g in visible.Where(g => InRange(g, district, x, y, z, 500)).Take(32)) desired[(ushort)id++] = (0x280004f9, g.X, g.Y, g.Z);
                    foreach (WorldClient carrier in SocialHandler.OnlineWhere(p => p != c && p.playerData.getOnWorld() && p.playerData.getDistrictId() == district && !p.playerData.isDead))
                    {
                        Position(carrier, out double cx, out double cy, out double cz);
                        if (Math.Abs(cy-y) > 250 || (cx-x)*(cx-x)+(cz-z)*(cz-z) > 500*500) continue;
                        if (Store.dbManager.WorldDbHandler.GetInventoryItems(carrier.playerData.getCharID()).Any(i => i.Slot >= 1 && i.Slot <= 96 && i.Goid <= ushort.MaxValue && IsLight((ushort)i.Goid)))
                            desired[(ushort)id++] = (0x280004f9, cx, cy, cz);
                    }
                }
                foreach (ushort old in s.Markers) c.messageQueue.addRpcMessage(DataMiningPackets.Remove(old));
                s.Markers.Clear();
                foreach (var kv in desired)
                {
                    var v = kv.Value; c.messageQueue.addRpcMessage(DataMiningPackets.Marker(kv.Key, v.fx, v.x, v.y, v.z)); s.Markers.Add(kv.Key);
                }
                if (type == 0 && nodes.Count > 0)
                {
                    var n = nodes[0]; double distance = Math.Sqrt((n.X-x)*(n.X-x)+(n.Z-z)*(n.Z-z));
                    byte yaw = c.playerInstance.YawInterval.getValue()[0];
                    double angle = Stealth.DegreesOff(x, z, yaw, n.X, n.Z);
                    c.messageQueue.addRpcMessage(DataMiningPackets.Indicator((byte)Math.Clamp(angle, 0, 180), (byte)Math.Clamp(distance / metres, 0, 100)));
                }
                if (type == 0 && UtilityAbilities.Level(c, 541) > 0)
                {
                    Ground nearest = ground.Where(g => !g.Node && g.District == district && Math.Abs(g.Y-y) <= 250).OrderBy(g => (g.X-x)*(g.X-x)+(g.Z-z)*(g.Z-z)).FirstOrDefault();
                    if (nearest != null && DataMiningPackets.Relative(x,z,nearest.X,nearest.Z,0) is DataMiningPackets.Ping ping)
                        c.messageQueue.addRpcMessage(DataMiningPackets.Map(new[] { ping }, 10));
                }
                c.FlushQueue();
            }
        }
        private static void Spawn(WorldClient c, Ground g)
        {
            if (c.viewMan.FindViewForEntity(g.Entity)?.viewCreated == true) return;
            GameObject obj = BuildObject(g);
            if (obj != null) new ServerPackets().SendSpawnGameObject(c, obj, g.Entity);
        }
        public static GameObject BuildObject(Ground g)
        {
            var d = Find(g.Goid); FieldInfo field = typeof(GameObjectDefinitions).GetField("Object" + g.Goid);
            if (d == null || field == null) return null;
            var obj = (GameObject)Activator.CreateInstance(field.FieldType, field.Name, g.Goid);
            obj.DisableAllAttributes(); obj.UnsetRelatedStaticObjId();
            void Set(string name, byte[] value)
            { if (obj.GetType().GetField(name)?.GetValue(obj) is Attribute a) { a.enable(); a.setValue(value); } }
            Set("Position", NumericalUtils.doublesToLtVector3d(g.X, g.Y, g.Z));
            Set("HalfExtents", StringUtils.hexStringToBytes(d.HalfExtents));
            Set("Orientation", new byte[] {0,0,0,0,0,0,0,0,0,0,0,0,0,0,128,63});
            if (!g.Node) Set("CurrentState", BitConverter.GetBytes(2111u));
            return obj;
        }
        private static void Delete(WorldClient c, ClientView v)
        { new ServerPackets().sendDeleteViewPacket(c, v.ViewID); c.viewMan.removeViewByViewId(v.ViewID); }
        private static void Say(WorldClient c, string s) => new ServerPackets().sendSystemChatMessage(c, s, "SYSTEM");
        public static bool Use(WorldClient c, ClientView v, byte action)
        {
            lock (sync)
            {
                Load(); Ground g = ground.Find(g => g.Entity == v.entityId);
                if (g == null) return v.entityId >= 982000000000UL && v.entityId < 983000000000UL;
                Definition d = Find(g.Goid);
                if (g.Node || action != d.PickupUse || c.playerData.isDead) return true;
                Position(c, out double x, out double y, out double z);
                if (!CanPickup(g, (ushort)c.playerData.getDistrictId(), x,y,z,d.Reach)) { Say(c,"Move closer to pick up the luggable."); return true; }
                string refusal = PickupRefusal(d, MissionStaging.PlayerLevel(c), Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()));
                if (refusal != null) { Say(c, refusal); return true; }
                ushort slot = WorldLuggablesDb.Pickup(c.playerData.getCharID(),g);
                if (slot == 0) { Say(c,"Your inventory is full."); return true; }
                ground.Remove(g);
                new ServerPackets().sendInventoryItemAdd(slot,g.Goid,0,0,c);
                foreach (WorldClient other in SocialHandler.OnlineWhere(p => p.playerData.getOnWorld()))
                { ClientView view = other.viewMan.FindViewForEntity(g.Entity); if (view != null) Delete(other,view); }
                c.FlushQueue(); return true;
            }
        }
        /// <summary>5c slot u16 + personal-inventory flag. Reject equipment/storage, stacks, arbitrary items and bad floor.</summary>
        public static void Drop(WorldClient c, byte[] body)
        {
            if (body == null || body.Length != 3 || body[2] != 1 || c.playerData.isDead || !c.playerData.getOnWorld()) return;
            ushort slot = BitConverter.ToUInt16(body,0); if (slot < 1 || slot > 96) return;
            lock (sync)
            {
                Load(); var item = Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()).FirstOrDefault(i => i.Slot == slot);
                if (item.Goid > ushort.MaxValue || !IsLight((ushort)item.Goid) || item.Count != 1) return;
                Position(c,out double x,out double y,out double z); ushort district = (ushort)c.playerData.getDistrictId();
                if (!NavMesh.Project(district,x,y,z,out NavPoint floor,150) || Math.Abs(floor.y-y)>150) { Say(c,"No nearby floor for the luggable."); return; }
                var g = new Ground {Goid=(ushort)item.Goid,District=district,X=floor.x,Y=floor.y,Z=floor.z};
                if (!WorldLuggablesDb.Drop(c.playerData.getCharID(),slot,g)) return;
                ground.Add(g); new ServerPackets().sendInventoryItemDelete(slot,c); Spawn(c,g); c.FlushQueue();
            }
        }
        /// <summary>Dev-only explicit fixture coordinates are ours, never presented as retail ambient placements.</summary>
        public static void Dev(WorldClient c, string[] args)
        {
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1") return;
            ushort goid = 606;
            if (args.Length > 1 && (!ushort.TryParse(args[1],out goid) || goid != 606 && !IsLight(goid))) { Say(c,"?utilityobject [606|light-luggable-goid]"); return; }
            Position(c,out double x,out double y,out double z);
            if (!NavMesh.Project((ushort)c.playerData.getDistrictId(),x,y,z,out NavPoint p,150) || Math.Abs(p.y-y)>150) { Say(c,"No nearby floor for the fixture."); return; }
            lock (sync)
            {
                Load(); var g = new Ground {Goid=goid,District=(ushort)c.playerData.getDistrictId(),X=p.x,Y=p.y,Z=p.z};
                g.Id=WorldLuggablesDb.Insert(g); ground.Add(g); if (!g.Node) Spawn(c,g);
                Say(c,"Utility fixture " + goid + " placed."); c.FlushQueue();
            }
        }
        public static void DevLocator(WorldClient c, string[] args)
        {
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1" ||
                !Store.config.dbParams.DatabaseName.StartsWith("mxotest_",StringComparison.Ordinal)) return;
            uint tool=0;
            if (args.Length!=2 || args[1]!="none" && (!uint.TryParse(args[1],out tool) || !UtilityAbilities.Locators.ContainsKey(tool)))
            { Say(c,"?utilitylocator 422|423|38208|38209|none (throwaway DB only)"); return; }
            Stop(c);
            foreach (var i in Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()).Where(i=>i.Slot>=1 && i.Slot<=96 && UtilityAbilities.Locators.ContainsKey(i.Goid)))
            { Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(i.Slot,c.playerData.getCharID()); new ServerPackets().sendInventoryItemDelete(i.Slot,c); }
            if (tool!=0) InventoryHandler.AddItem(c,tool,0x10);
            Say(c,"Locator fixture changed."); c.FlushQueue();
        }
        /// <summary>Called under MissionRuntime's mission lock. Ping categories/refresh are choices; roll is retail.</summary>
        public static void RevealMission(WorldClient c, MissionProgress progress)
        {
            int flags = UtilityAbilities.MissionMapAccess(c);
            if (flags == 0 || UtilityAbilities.Level(c,44) <= 0 || c.playerData.isDead) return;
            double now = TimeUtils.GetSimTimeSeconds(); var next = mapNext.GetOrCreateValue(c);
            if (now < next.Value) return; next.Value = now + 2;
            Position(c,out double x,out double y,out double z); var pings = new List<DataMiningPackets.Ping>();
            void Add(double px,double py,double pz,byte relationship)
            { if (Math.Abs(py-y)<=250 && DataMiningPackets.Relative(x,z,px,pz,relationship) is DataMiningPackets.Ping p) pings.Add(p); }
            if ((flags & 1)!=0) foreach (var o in progress.objects.Values) if (o.spot!=null) Add(o.spot.pos_x,o.spot.pos_y,o.spot.pos_z,0);
            if ((flags & 2)!=0) foreach (Mob m in progress.hostiles) if (!m.getIsDead()) Add(m.getXPos(),m.getYPos(),m.getZPos(),2);
            if ((flags & 4)!=0 && progress.site!=null)
                foreach (ClientView view in c.viewMan.Snapshot().Where(v=>v.staticObject!=null))
                {
                    var o=view.staticObject;
                    if (o.pos_x<progress.site.minX || o.pos_x>progress.site.maxX || o.pos_z<progress.site.minZ || o.pos_z>progress.site.maxZ) continue;
                    if (ObjectStates.TrackedState(c,view.entityId)==UtilityAbilities.LockedState) Add(o.pos_x,o.pos_y,o.pos_z,0);
                }
            c.messageQueue.addRpcMessage(DataMiningPackets.Map(pings.Distinct(),3)); c.FlushQueue();
        }
    }
}
