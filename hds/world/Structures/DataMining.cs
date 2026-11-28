using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using hds.databases;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    /// <summary>Data-node locator and persistent, shared light-luggable ground objects. Evidence/choices: DATA-MINING.md.</summary>
    public static class DataMining
    {
        public sealed class Definition
        {
            public ushort Goid;
            public int PickupUse, Level, Size;
            public bool Singleton, DropOnConstructChange, NonTradeable;
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
            public ushort Ability;
            public double Next, Expires;
            public ushort District;
            public readonly HashSet<ushort> Markers = new HashSet<ushort>();
        }
        private static readonly object sync = new object();
        private static List<Ground> ground;
        private static readonly List<Ground> visibleScratch = new List<Ground>();
        private static readonly List<ClientView> viewScratch = new List<ClientView>();
        private static readonly ConditionalWeakTable<WorldClient, Search> searches = new ConditionalWeakTable<WorldClient, Search>();
        private sealed class MissionReveal
        {
            public double Next;
            public readonly HashSet<string> Objectives = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<uint> TeamRecipients = new HashSet<uint>();
        }
        private static readonly ConditionalWeakTable<WorldClient, MissionReveal> missionReveals =
            new ConditionalWeakTable<WorldClient, MissionReveal>();
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
                    DropOnConstructChange = Number("Luggable/DropOnInterConstructTeleport") != 0,
                    Singleton = Number("InvItem/Singleton") != 0, NonTradeable = Number("InvItem/NonTradeable") != 0,
                    HalfExtents = String("Model/HalfExtents") ?? String("Normal/HalfExtents"),
                    Reach = f.TryGetProperty("Usable/MaxDistance", out var reach) ? reach.GetDouble() : 100,
                    Exclusive = f.EnumerateObject().Where(p => p.Name.StartsWith("Usable/PickupExclusive", StringComparison.Ordinal)).Select(p => p.Value.GetUInt32()).ToArray() };
            }).ToDictionary(d => d.Goid);
        });
        public static Definition Find(ushort goid) => definitions.Value.TryGetValue(goid, out var d) ? d : null;
        public static bool IsLight(ushort goid) => goid != 606 && Find(goid)?.Size == 0;
        internal static bool IsNonTradeable(uint goid) => goid <= ushort.MaxValue && Find((ushort)goid)?.NonTradeable == true;
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
        public static void Start(WorldClient c, uint tool, ushort ability = UtilityAbilities.FindDataNode)
        {
            if (!UtilityAbilities.IsDataNodeSearch(ability)) throw new ArgumentOutOfRangeException(nameof(ability));
            lock (sync)
            {
                Stop(c); var s = searches.GetOrCreateValue(c); s.Tool = tool; s.District = (ushort)c.playerData.getDistrictId();
                s.Ability = ability; s.Expires = UtilityAbilities.Locators[tool].type == 0 ? TimeUtils.GetSimTimeSeconds() + 10 : double.PositiveInfinity;
                c.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(ability, true));
                Output.WriteDebugLog("[DATA-MINING] search char="+c.playerData.getCharID()+" ability="+ability+" tool="+tool+" outcome=on");
            }
        }
        public static bool Stop(WorldClient c) => Stop(c,out _);
        public static bool Stop(WorldClient c, out ushort ability)
        {
            lock (sync)
            {
                ability = 0;
                if (!searches.TryGetValue(c, out var s)) return false;
                ability = s.Ability;
                foreach (ushort id in s.Markers) c.messageQueue.addRpcMessage(DataMiningPackets.Remove(id));
                searches.Remove(c);
                c.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(ability, false));
                Output.WriteDebugLog("[DATA-MINING] search char="+c.playerData.getCharID()+" ability="+ability+" tool="+s.Tool+" outcome=off");
                return true;
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
                visibleScratch.Clear();
                for (int i = 0; i < ground.Count; i++)
                {
                    Ground g = ground[i];
                    if (!g.Node && InRange(g, district, x, y, z, 5000))
                    {
                        visibleScratch.Add(g);
                    }
                }
                for (int i = 0; i < visibleScratch.Count; i++)
                {
                    Spawn(c, visibleScratch[i]);
                }
                c.viewMan.CollectNonPlayerViewsInEntityRange(982000000000UL, 983000000000UL, viewScratch);
                for (int i = 0; i < viewScratch.Count; i++)
                {
                    ClientView v = viewScratch[i];
                    bool stillVisible = false;
                    for (int j = 0; j < visibleScratch.Count; j++)
                    {
                        if (visibleScratch[j].Entity == v.entityId)
                        {
                            stillVisible = true;
                            break;
                        }
                    }
                    if (!stillVisible) Delete(c, v);
                }
                if (!searches.TryGetValue(c, out var s)) return;
                var visible = visibleScratch;
                if (c.playerData.isDead || district != s.District || UtilityAbilities.Level(c, s.Ability) <= 0 ||
                    UtilityAbilities.Level(c, UtilityAbilities.LocatorRequirement(s.Tool)) <= 0 ||
                    !Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()).Any(i => i.Slot >= 1 && i.Slot <= 96 && i.Goid == s.Tool))
                { Stop(c); c.FlushQueue(); return; }
                double now = TimeUtils.GetSimTimeSeconds();
                if (now >= s.Expires) { UtilityAbilities.StopFindDataNode(c); c.FlushQueue(); return; }
                if (now < s.Next) return;
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
        public static bool IsEntity(ulong entity) => entity >= 982000000000UL && entity < 983000000000UL;
        public static bool Use(WorldClient c, ClientView v, byte action, ushort? receivedSpawn = null)
        {
            if (!IsEntity(v.entityId)) return false;
            string context = "[DATA-MINING] pickup char="+c.playerData.getCharID()+" view="+v.ViewID+" spawn="+(receivedSpawn ?? v.spawnId)+
                " expectedSpawn="+v.spawnId+" entity="+v.entityId+" goid="+v.GoID+" use="+action;
            void Log(string outcome) => Output.WriteDebugLog(context+" "+outcome);
            void RefuseTransfer(string reason)
            {
                new ServerPackets().sendSystemChatMessage(c, reason, "BROADCAST");
                c.FlushQueue();
            }
            if (receivedSpawn.HasValue && receivedSpawn.Value != v.spawnId) { Log("outcome=denied_handle db=unchanged"); return true; }
            ushort pickedGoid;
            lock (sync)
            {
                Load(); Ground g = ground.Find(g => g.Entity == v.entityId);
                if (g == null) { Log("outcome=missing_ground db=unchanged"); return true; }
                Definition d = Find(g.Goid);
                if (g.Node || action != d.PickupUse || c.playerData.isDead) { Log("outcome=denied_action expectedUse="+d.PickupUse+" dead="+c.playerData.isDead+" node="+g.Node+" db=unchanged"); return true; }
                Position(c, out double x, out double y, out double z);
                if (!CanPickup(g, (ushort)c.playerData.getDistrictId(), x,y,z,d.Reach)) { Log("outcome=denied_reach db=unchanged"); Say(c,"Move closer to pick up the luggable."); return true; }
                string refusal = PickupRefusal(d, MissionStaging.PlayerLevel(c), Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID()));
                if (refusal != null) { Log("outcome=denied_requirement reason="+refusal+" db=unchanged"); Say(c, refusal); return true; }
                ushort slot;
                try { slot = WorldLuggablesDb.Pickup(c.playerData.getCharID(),g); }
                catch (DbException ex)
                {
                    Log("outcome=pickup_rolled_back db=not_committed type=" + ex.GetType().Name);
                    RefuseTransfer("The pickup could not be saved. The luggable is still on the ground.");
                    return true;
                }
                if (slot == 0)
                {
                    Log("outcome=no_bag_slot_or_missing_ground db=not_committed");
                    RefuseTransfer("Your inventory is full.");
                    return true;
                }
                Log("outcome=success db=committed ground="+g.Id+" bagSlot="+slot);
                pickedGoid = g.Goid;
                ground.Remove(g);
                new ServerPackets().sendInventoryItemAdd(slot,g.Goid,0,0,c);
                foreach (WorldClient other in SocialHandler.OnlineWhere(p => p.playerData.getOnWorld()))
                { ClientView view = other.viewMan.FindViewForEntity(g.Entity); if (view != null) Delete(other,view); }
                c.FlushQueue();
            }
            // serverrules.py TestLightLuggablePickup -> BreakExclusiveAbility. Keep callbacks outside
            // the shared ground-item lock: ending stealth may update views and acquire other state locks.
            Stealth.BreakForAction(c, "picked up a luggable");
            UtilityAbilities.StopFindDataNode(c);
            DatamineTapEffects.OnPickup(c, pickedGoid);
            return true;
        }
        /// <summary>5c slot u16 + personal-inventory flag. Reject equipment/storage, stacks, arbitrary items and bad floor.</summary>
        public static void Drop(WorldClient c, byte[] body)
        {
            if (body == null || body.Length != 3 || body[2] != 1 || c.playerData.isDead || !c.playerData.getOnWorld()) return;
            ushort slot = BitConverter.ToUInt16(body,0); if (slot < 1 || slot > 96) return;
            var publication = new AbilityAcquisition.Publication(c);
            lock (sync)
            {
                var item = Store.dbManager.WorldDbHandler.GetInventoryItems(publication.Character).FirstOrDefault(i => i.Slot == slot);
                if (item.Goid > ushort.MaxValue || !IsLight((ushort)item.Goid) || item.Count != 1)
                {
                    InventoryHandler.RefuseSlotMove(c, publication, slot, "Only a single light luggable can be dropped.");
                    return;
                }
                Position(c,out double x,out double y,out double z); ushort district = (ushort)c.playerData.getDistrictId();
                if (!NavMesh.Project(district, x, y, z, out NavPoint floor, 150) || Math.Abs(floor.y - y) > 150)
                {
                    InventoryHandler.RefuseSlotMove(c, publication, slot, "No nearby floor for the luggable.");
                    return;
                }
                var g = new Ground {Goid=(ushort)item.Goid,District=district,X=floor.x,Y=floor.y,Z=floor.z};
                bool committed;
                try
                {
                    Load();
                    committed = WorldLuggablesDb.Drop(publication.Character, slot, g);
                }
                catch (DbException ex)
                {
                    Output.WriteDebugLog($"[DATA-MINING] drop char={publication.Character} bagSlot={slot} goid={g.Goid} " +
                        $"outcome=drop_rolled_back db=not_committed type={ex.GetType().Name}");
                    committed = false;
                }
                if (!committed)
                {
                    InventoryHandler.RefuseSlotMove(c, publication, slot, "The drop could not be saved. Your item was kept.");
                    return;
                }
                Output.WriteDebugLog("[DATA-MINING] drop char="+c.playerData.getCharID()+" bagSlot="+slot+" goid="+g.Goid+" ground="+g.Id+" outcome=success db=committed");
                ground.Add(g); new ServerPackets().sendInventoryItemDelete(slot,c); Spawn(c,g); c.FlushQueue();
            }
        }
        /// <summary>Release the flagged light luggables to the departure floor before crossing an Archive boundary.</summary>
        internal static bool DropForArchiveTravel(WorldClient client)
        {
            uint character = client.playerData.getCharID();
            lock (sync)
            {
                var items = Store.dbManager.WorldDbHandler.GetInventoryItems(character).Where(item =>
                    item.Slot >= 1 && item.Slot <= 96 && item.Goid <= ushort.MaxValue &&
                    IsLight((ushort)item.Goid) && Find((ushort)item.Goid).DropOnConstructChange).ToArray();
                if (items.Length == 0) return true;

                Position(client, out double x, out double y, out double z);
                ushort district = (ushort)client.playerData.getDistrictId();
                if (!NavMesh.Project(district, x, y, z, out NavPoint floor, 150) || Math.Abs(floor.y - y) > 150)
                {
                    Say(client, "Move to a clear floor before travelling with a luggable.");
                    return false;
                }

                var drops = items.Select(item => (item.Slot, Ground: new Ground
                {
                    Goid = (ushort)item.Goid, District = district, X = floor.x, Y = floor.y, Z = floor.z
                })).ToArray();
                try
                {
                    Load();
                    if (!WorldLuggablesDb.Drop(character, drops))
                    {
                        Say(client, "Your luggables could not be dropped. Archive travel was cancelled.");
                        return false;
                    }
                }
                catch (DbException ex)
                {
                    Output.WriteDebugLog($"[DATA-MINING] archive drop rollback char={character} type={ex.GetType().Name}");
                    Say(client, "Your luggables could not be dropped. Archive travel was cancelled.");
                    return false;
                }

                // Nearby players receive the normal ground-object view on the next visibility pass.
                var packets = new ServerPackets();
                foreach (var (slot, dropped) in drops)
                {
                    ground.Add(dropped);
                    packets.sendInventoryItemDelete(slot, client);
                    Output.WriteDebugLog($"[DATA-MINING] archive drop char={character} bagSlot={slot} goid={dropped.Goid} district={district} ground={dropped.Id}");
                }
                return true;
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
        public static void DevStatus(WorldClient c)
        {
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1" || !Store.config.dbParams.DatabaseName.StartsWith("mxotest_",StringComparison.Ordinal)) return;
            lock (sync)
            {
                Load(); ushort active = searches.TryGetValue(c,out var s) ? s.Ability : (ushort)0;
                var bag=Store.dbManager.WorldDbHandler.GetInventoryItems(c.playerData.getCharID());
                Say(c,"Utility status: search="+active+", loaded215="+UtilityAbilities.Level(c,215)+", loaded621="+UtilityAbilities.Level(c,621)+
                    ", carried="+string.Join(",",bag.Where(i=>i.Slot>=1 && i.Slot<=96 && i.Goid<=ushort.MaxValue && IsLight((ushort)i.Goid)).Select(i=>i.Goid+"@"+i.Slot)));
                c.FlushQueue();
            }
        }
        private const byte MissionPingSeconds = 3;

        // Called with the loadout lock held, so unloading cannot leave a queued refresh behind the clear.
        internal static void EndMissionReveal(WorldClient client)
        {
            if (!missionReveals.TryGetValue(client, out MissionReveal reveal)) return;
            missionReveals.Remove(client);
            foreach (string objective in reveal.Objectives.OrderBy(name => name, StringComparer.Ordinal))
                LogMissionPingCleared(objective);
            foreach (uint recipient in reveal.TeamRecipients)
                LogTeamPingsCleared(client, recipient);
        }

        private static void LogMissionPingCleared(string objective) =>
            Output.WriteDebugLog($"[UTILITY] Download Mission Map: ping cleared {objective} (no refresh, expires within {MissionPingSeconds}s)");

        /// <summary>Refreshes the current floor's mission markers under MissionRuntime's mission lock.</summary>
        public static void RevealMission(WorldClient c, MissionProgress progress)
        {
            List<WorldClient> notified;
            lock (c.playerData.passives)
            {
                int flags = UtilityAbilities.MissionMapAccess(c);
                if (flags == 0 || UtilityAbilities.Level(c, UtilityAbilities.DownloadMissionMap) <= 0 || c.playerData.isDead) return;
                double now = TimeUtils.GetSimTimeSeconds();
                var reveal = missionReveals.GetOrCreateValue(c);
                if (now < reveal.Next) return;
                reveal.Next = now + 2;

                Position(c, out double x, out double y, out double z);
                var pings = new List<DataMiningPackets.Ping>();
                var objectives = new Dictionary<string, DataMiningPackets.Ping>(StringComparer.Ordinal);
                if ((flags & 1) != 0)
                    foreach (var obj in progress.objects.Values.Where(obj => obj.spot != null))
                    {
                        var pose = MissionObjectPoses.For(obj);
                        AddNamed($"{obj.def.kind} {obj.def.id}", pose.X, pose.Y, pose.Z, 0);
                    }
                if ((flags & 2) != 0)
                    foreach (Mob mob in progress.hostiles.Where(mob => !mob.getIsDead()))
                        Add(mob.getXPos(), mob.getYPos(), mob.getZPos(), 2);
                if ((flags & 4) != 0 && progress.site != null)
                    foreach (ClientView view in c.viewMan.Snapshot().Where(view => view.staticObject != null))
                    {
                        var obj = view.staticObject;
                        if (obj.pos_x < progress.site.minX || obj.pos_x > progress.site.maxX ||
                            obj.pos_z < progress.site.minZ || obj.pos_z > progress.site.maxZ) continue;
                        if (ObjectStates.TrackedState(c, view.entityId) == UtilityAbilities.LockedState)
                            Add(obj.pos_x, obj.pos_y, obj.pos_z, 0);
                    }
                if ((flags & 8) != 0)
                    lock (c.playerData.missionMobs)
                        foreach (var npc in c.playerData.missionMobs.Where(npc => npc.missionNpcId != 0 &&
                                     npc.isFriendly && npc.getIsSpawned() && !npc.getIsDead()))
                            AddNamed($"Friendly {npc.getName()}", npc.getXPos(), npc.getYPos(), npc.getZPos(), 1);
                if ((flags & 16) != 0)
                    foreach (var waypoint in progress.waypoints)
                        AddNamed("Mission entrance", waypoint.x, waypoint.y, waypoint.z, 0);
                if ((flags & 32) != 0)
                    foreach (var exit in progress.exits)
                        AddNamed("Mission exit", exit[0], exit[1], exit[2], 0);

                var sent = pings.Distinct().Take(128).ToArray();
                c.messageQueue.addRpcMessage(DataMiningPackets.Map(sent, MissionPingSeconds));
                foreach (string objective in reveal.Objectives.Where(name => !objectives.ContainsKey(name)).ToArray())
                {
                    reveal.Objectives.Remove(objective);
                    LogMissionPingCleared(objective);
                }
                foreach (var objective in objectives.Where(entry => sent.Contains(entry.Value)))
                    if (reveal.Objectives.Add(objective.Key))
                        Output.WriteDebugLog($"[UTILITY] Download Mission Map: ping sent {objective.Key}");

                notified = ShareMissionPings(c, progress, flags, reveal);

                void AddNamed(string label, double px, double py, double pz, byte relationship)
                {
                    if (Add(px, py, pz, relationship) is DataMiningPackets.Ping ping)
                        objectives[label] = ping;
                }

                DataMiningPackets.Ping? Add(double px, double py, double pz, byte relationship)
                {
                    if (!(Math.Abs(py - y) <= 250)) return null;
                    var ping = DataMiningPackets.Relative(x, z, px, pz, relationship);
                    if (ping.HasValue) pings.Add(ping.Value);
                    return ping;
                }
            }
            c.FlushQueue();
            foreach (var member in notified) member.FlushQueue();
        }

        // The shipped deactivate callback addresses MissionTeam. Share objective and hostile pings from the
        // team leader's active mission, retaining each recipient's own map origin and normal ping lifetime.
        private static List<WorldClient> ShareMissionPings(WorldClient owner, MissionProgress progress, int flags, MissionReveal reveal)
        {
            var notified = new List<WorldClient>();
            var active = new HashSet<uint>();
            foreach (uint memberId in SocialGroups.Instance.TeammatesOfLeader(owner.playerData.getCharID()))
            {
                var member = SocialHandler.FindOnlineByCharId(memberId);
                var data = member?.playerData;
                if (data == null || !member.Alive || !data.getOnWorld() || data.worldChangePending || data.waitForRPCShutDown ||
                    data.isDead || data.activeMission != null || data.getDistrictId() != owner.playerData.getDistrictId()) continue;

                Position(member, out double x, out double y, out double z);
                var pings = new List<DataMiningPackets.Ping>();
                if ((flags & 1) != 0)
                    foreach (var obj in progress.objects.Values.Where(obj => obj.spot != null))
                    {
                        var pose = MissionObjectPoses.For(obj);
                        Add(pose.X, pose.Y, pose.Z, 0);
                    }
                if ((flags & 2) != 0)
                    foreach (Mob mob in progress.hostiles.Where(mob => !mob.getIsDead()))
                        Add(mob.getXPos(), mob.getYPos(), mob.getZPos(), 2);

                var sent = pings.Distinct().Take(128).ToArray();
                if (sent.Length == 0 || !SocialGroups.Instance.ShareTeam(owner.playerData.getCharID(), memberId)) continue;
                member.messageQueue.addRpcMessage(DataMiningPackets.Map(sent, MissionPingSeconds));
                notified.Add(member);
                active.Add(memberId);
                if (reveal.TeamRecipients.Add(memberId))
                    Output.WriteDebugLog($"[UTILITY] Download Mission Map: team pings sent owner={owner.playerData.getCharID()} recipient={memberId} count={sent.Length}");

                void Add(double px, double py, double pz, byte relationship)
                {
                    if (!(Math.Abs(py - y) <= 250)) return;
                    var ping = DataMiningPackets.Relative(x, z, px, pz, relationship);
                    if (ping.HasValue) pings.Add(ping.Value);
                }
            }
            foreach (uint gone in reveal.TeamRecipients.Except(active).ToArray())
            {
                reveal.TeamRecipients.Remove(gone);
                LogTeamPingsCleared(owner, gone);
            }
            return notified;
        }

        private static void LogTeamPingsCleared(WorldClient owner, uint recipient) =>
            Output.WriteDebugLog($"[UTILITY] Download Mission Map: team pings cleared owner={owner.playerData.getCharID()} recipient={recipient} (expires within {MissionPingSeconds}s)");
    }
}
