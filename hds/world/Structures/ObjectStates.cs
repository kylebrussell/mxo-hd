using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>
    /// What an object's state machine does, from the client's gameobjects.gob (data/object_states.csv and
    /// data/object_usables.csv; docs/OBJECT-STATES.md has how they were read).
    ///
    /// An object's CurrentState is an event id. When the client sees a state (at spawn or in an update) it runs that state's
    /// script, which turns usables on and off: a computer in 0x961 (ST_Giver) turns Take on and Hack off. The use number
    /// the client sends (80 c7 &lt;view&gt; &lt;spawn&gt; &lt;use&gt; 00) is the usable's place in the object's component list:
    /// a computer's Hack is 4, Take 5, Search 6, Put 8; a container's Open is 3, Pick Lock 8, Unlock 9.
    /// What a use does next is the server's job. The data has the retail answer: the script run for the usable's rule moves
    /// the object to another state (a container's Open to 0x834) or to the state's "next" (a computer's Take back to 0x960).
    /// </summary>
    public static class ObjectStates
    {
        /// <summary>A use result that goes to the state's next state rather than a fixed one.</summary>
        public const string ToNext = "next";

        public class State
        {
            public UInt16 goid;
            public UInt32 id;
            public string name;
            public bool isDefault;
            /// <summary>Where the state goes on its own after <see cref="seconds"/>, or where a "next" use result goes. 0 if none.</summary>
            public UInt32 next;
            public UInt32 seconds;
            /// <summary>Use numbers the state's script turns on and off.</summary>
            public byte[] enables = new byte[0];
            public byte[] disables = new byte[0];
        }

        public class Usable
        {
            public UInt16 goid;
            public byte use;
            public string component;
            /// <summary>The menu text, from the client's string table (Take, Hack, Put, Open, Pick Lock, ...).</summary>
            public string label;
            public string cursor;
            /// <summary>In world units (100 = 1 m); NaN when the object doesn't store one.</summary>
            public float maxDistance = float.NaN;
            /// <summary>Can be the double-click action.</summary>
            public bool defaultable;
            /// <summary>The client's check before it offers the usable (a clientrules.py rule; 0 = none).</summary>
            public UInt32 clientRule;
            public string clientRuleName = "";
            public UInt32 executeRule;
            public string executeRuleName = "";
            /// <summary>Rule result (1 = success) to the state it moves to: "0x834" or <see cref="ToNext"/>.</summary>
            public Dictionary<byte, string> results = new Dictionary<byte, string>();
            /// <summary>
            /// The attribute the server sends to turn the usable on (its ActiveTracker, e.g. TextActiveTracker), or "" when
            /// the class doesn't send one. Retail sent TalkActiveTracker 1 on talkable NPCs.
            /// </summary>
            public string tracker = "";
            /// <summary>Whether it starts on, from the object's stored ActiveTracker; null when the object doesn't store it.</summary>
            public bool? startsOn;
        }

        private static readonly object loadLock = new object();
        private static Dictionary<UInt16, List<State>> states;
        private static Dictionary<UInt16, List<Usable>> usables;

        /// <summary>Loads both files from a data folder (default: the server's data folder). Missing files leave the tables empty.</summary>
        public static void Load(string dataDir = null)
        {
            lock (loadLock)
            {
                if (states != null && dataDir == null)
                {
                    return;
                }

                Dictionary<UInt16, List<State>> s = new Dictionary<UInt16, List<State>>();
                Dictionary<UInt16, List<Usable>> u = new Dictionary<UInt16, List<Usable>>();
                string statesPath = dataDir == null ? DataLoader.DataPath("object_states.csv") : Path.Combine(dataDir, "object_states.csv");
                string usablesPath = dataDir == null ? DataLoader.DataPath("object_usables.csv") : Path.Combine(dataDir, "object_usables.csv");

                foreach (string[] f in Rows(statesPath, 10))
                {
                    State state = new State
                    {
                        goid = UInt16.Parse(f[0]),
                        id = Hex(f[3]),
                        name = f[4],
                        isDefault = f[5] == "1",
                        next = f[6].Length > 0 ? Hex(f[6]) : 0,
                        seconds = f[7].Length > 0 ? UInt32.Parse(f[7]) : 0,
                        enables = Bytes(f[8]),
                        disables = Bytes(f[9])
                    };
                    if (!s.TryGetValue(state.goid, out List<State> list))
                    {
                        s[state.goid] = list = new List<State>();
                    }
                    list.Add(state);
                }

                foreach (string[] f in Rows(usablesPath, 14))
                {
                    Usable usable = new Usable
                    {
                        goid = UInt16.Parse(f[0]),
                        use = byte.Parse(f[3]),
                        component = f[4],
                        label = f[5],
                        cursor = f[6],
                        maxDistance = f[7].Length > 0 ? float.Parse(f[7], CultureInfo.InvariantCulture) : float.NaN,
                        defaultable = f[8] == "1",
                        tracker = f[12],
                        startsOn = f[13].Length > 0 ? f[13] == "1" : (bool?)null
                    };
                    (usable.clientRule, usable.clientRuleName) = Rule(f[9]);
                    (usable.executeRule, usable.executeRuleName) = Rule(f[10]);
                    foreach (string result in f[11].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] kv = result.Split('>');
                        usable.results[byte.Parse(kv[0])] = kv[1];
                    }
                    if (!u.TryGetValue(usable.goid, out List<Usable> list))
                    {
                        u[usable.goid] = list = new List<Usable>();
                    }
                    list.Add(usable);
                }

                states = s;
                usables = u;
            }
        }

        private static IEnumerable<string[]> Rows(string path, int columns)
        {
            if (!File.Exists(path))
            {
                Output.WriteLine("[OBJECT] No object state data at " + path);
                yield break;
            }

            foreach (string line in File.ReadLines(path))
            {
                string[] f = line.Split(',');
                if (f.Length >= columns && UInt16.TryParse(f[0], out _))
                {
                    yield return f;
                }
            }
        }

        private static UInt32 Hex(string s)
        {
            return Convert.ToUInt32(s.StartsWith("0x") ? s.Substring(2) : s, 16);
        }

        private static byte[] Bytes(string s)
        {
            return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(byte.Parse).ToArray();
        }

        private static (UInt32, string) Rule(string s)
        {
            if (s.Length == 0)
            {
                return (0, "");
            }

            int space = s.IndexOf(' ');
            return space < 0 ? (UInt32.Parse(s), "") : (UInt32.Parse(s.Substring(0, space)), s.Substring(space + 1));
        }

        public static IReadOnlyList<State> States(UInt16 goid)
        {
            Load();
            return states.TryGetValue(goid, out List<State> list) ? list : new List<State>();
        }

        public static IReadOnlyList<Usable> Usables(UInt16 goid)
        {
            Load();
            return usables.TryGetValue(goid, out List<Usable> list) ? list : new List<Usable>();
        }

        public static State Find(UInt16 goid, UInt32 state)
        {
            return States(goid).FirstOrDefault(s => s.id == state);
        }

        /// <summary>A state by its name in the client's data (ClosedState, ST_Giver, ...), or null.</summary>
        public static State Named(UInt16 goid, string name)
        {
            return States(goid).FirstOrDefault(s => s.name == name);
        }

        /// <summary>The object's default CurrentState, or 0 when it has no state machine.</summary>
        public static UInt32 DefaultState(UInt16 goid)
        {
            return States(goid).FirstOrDefault(s => s.isDefault)?.id ?? 0;
        }

        public static Usable FindUsable(UInt16 goid, byte use)
        {
            return Usables(goid).FirstOrDefault(u => u.use == use);
        }

        /// <summary>The usable with this menu text (Take, Hack, Put, Open, Pick Lock, Unlock, ...), or null.</summary>
        public static Usable UsableLabelled(UInt16 goid, string label)
        {
            return Usables(goid).FirstOrDefault(u => string.Equals(u.label, label, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The use number the client sends for this menu text, or null.</summary>
        public static byte? UseNumber(UInt16 goid, string label)
        {
            return UsableLabelled(goid, label)?.use;
        }

        /// <summary>
        /// The usables an object spawned in this state offers: the ones its script turns on. Objects without a state machine
        /// (hardline booths, the desk phone) aren't covered: their usables don't depend on a state. Whether a usable the
        /// script doesn't mention starts on is a guess (off fits every computer state tried in the client).
        /// </summary>
        public static ISet<byte> Offered(UInt16 goid, UInt32 state)
        {
            return Enter(goid, Enumerable.Empty<byte>(), state);
        }

        /// <summary>The usables on after the client enters a state, given the ones on before it (scripts only change what they name).</summary>
        public static ISet<byte> Enter(UInt16 goid, IEnumerable<byte> before, UInt32 state)
        {
            HashSet<byte> on = new HashSet<byte>(before);
            State s = Find(goid, state);
            if (s != null)
            {
                on.ExceptWith(s.disables);
                on.UnionWith(s.enables);
            }

            return on;
        }

        /// <summary>
        /// The state to put an object in so that it offers this usable (by menu text), or 0 if none does. With
        /// <paramref name="then"/>, the state whose next state offers that one: a computer's Hack then Take is 0x964
        /// (ST_UnhackedGiver), Hack then Put 0x965. Prefers states offering nothing else, then the default state, then a
        /// state whose next is the default (a computer's plain Hack is 0x963, which goes back to 0x960).
        /// </summary>
        public static UInt32 StateOffering(UInt16 goid, string label, string then = null)
        {
            Usable usable = UsableLabelled(goid, label);
            Usable after = then == null ? null : UsableLabelled(goid, then);
            if (usable == null || (then != null && after == null))
            {
                return 0;
            }

            UInt32 def = DefaultState(goid);
            return States(goid)
                .Where(s => Offered(goid, s.id).Contains(usable.use))
                .Where(s => after == null || (s.next != 0 && Offered(goid, s.next).Contains(after.use)))
                .OrderBy(s => Offered(goid, s.id).Count)
                .ThenBy(s => s.isDefault ? 0 : 1)
                .ThenBy(s => s.next == def ? 0 : 1)
                .ThenBy(s => s.id)
                .Select(s => s.id)
                .FirstOrDefault();
        }

        /// <summary>
        /// Where a use moves the object, per the retail script of the usable's rule: a container's Open (result 1) to 0x834
        /// (OpenedState), a computer's Take to its state's next (0x961 to 0x960). 0 when the data has no move.
        /// </summary>
        public static UInt32 StateAfterUse(UInt16 goid, UInt32 state, byte use, byte result = 1)
        {
            Usable usable = FindUsable(goid, use);
            if (usable == null || !usable.results.TryGetValue(result, out string to))
            {
                return 0;
            }

            return to == ToNext ? Find(goid, state)?.next ?? 0 : Hex(to);
        }

        /// <summary>How long a door stays open (OpenedState) and closing (ClosingState), or null if the data has no timing.</summary>
        public static (UInt32 open, UInt32 closing)? DoorTiming(UInt16 goid)
        {
            State opened = Named(goid, "OpenedState");
            State closing = Named(goid, "ClosingState");
            if (opened == null || closing == null || opened.seconds == 0 || closing.seconds == 0)
            {
                return null;
            }

            return (opened.seconds, closing.seconds);
        }

        /// <summary>
        /// The view update that sets an object's CurrentState: the class's update mask and the state (a computer's 01 08,
        /// the wardrobe's 01 20, a door's 01 80 02). Null if the repo has no class with a CurrentState for the goid.
        /// </summary>
        public static byte[] BuildStateUpdate(UInt16 goid, UInt32 state)
        {
            FieldInfo field = typeof(GameObjectDefinitions).GetField("Object" + goid);
            if (field == null)
            {
                return null;
            }

            GameObject obj = (GameObject)Activator.CreateInstance(field.FieldType, field.Name, goid);
            if (!(obj.GetType().GetField("CurrentState")?.GetValue(obj) is Attribute current))
            {
                return null;
            }

            current.enable();
            current.setValue(NumericalUtils.uint32ToByteArray(state, 1));
            return obj.GetUpdateAttributes(new List<Attribute> { current });
        }

        // Objects whose state the server follows for a client: entity -> (goid, state)
        private static readonly ConditionalWeakTable<WorldClient, Dictionary<UInt64, (UInt16 goid, UInt32 state)>> tracked =
            new ConditionalWeakTable<WorldClient, Dictionary<UInt64, (UInt16, UInt32)>>();

        /// <summary>Follow an object the client was sent in this state, so <see cref="OnUse"/> can move it as retail did.</summary>
        public static void Track(WorldClient client, UInt64 entity, UInt16 goid, UInt32 state)
        {
            Dictionary<UInt64, (UInt16, UInt32)> map = tracked.GetOrCreateValue(client);
            lock (map)
            {
                map[entity] = (goid, state == 0 ? DefaultState(goid) : state);
            }
        }

        /// <summary>The state the server last sent for a followed object, or 0.</summary>
        public static UInt32 TrackedState(WorldClient client, UInt64 entity)
        {
            if (!tracked.TryGetValue(client, out Dictionary<UInt64, (UInt16, UInt32)> map))
            {
                return 0;
            }

            lock (map)
            {
                return map.TryGetValue(entity, out (UInt16 goid, UInt32 state) o) ? o.state : 0;
            }
        }

        /// <summary>Sends a followed object's new state (CurrentState view update) and records it. False if not followed or no view.</summary>
        public static bool SetState(WorldClient client, UInt64 entity, UInt32 state)
        {
            if (!tracked.TryGetValue(client, out Dictionary<UInt64, (UInt16, UInt32)> map))
            {
                return false;
            }

            UInt16 goid;
            lock (map)
            {
                if (!map.TryGetValue(entity, out (UInt16 goid, UInt32 state) o))
                {
                    return false;
                }
                goid = o.goid;
                map[entity] = (goid, state);
            }

            ClientView view = client.viewMan.FindViewForEntity(entity);
            byte[] update = BuildStateUpdate(goid, state);
            if (view == null || update == null)
            {
                return false;
            }

            client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID, update), false);
            return true;
        }

        /// <summary>
        /// A use (80 c7) of a followed object: logs what the data says it was and moves the object to the state retail's
        /// script moves it to on success. Does nothing for objects nobody follows. Returns the new state, or 0.
        /// </summary>
        public static UInt32 OnUse(WorldClient client, UInt64 entity, byte use)
        {
            if (!tracked.TryGetValue(client, out Dictionary<UInt64, (UInt16, UInt32)> map))
            {
                return 0;
            }

            (UInt16 goid, UInt32 state) o;
            lock (map)
            {
                if (!map.TryGetValue(entity, out o))
                {
                    return 0;
                }
            }

            Usable usable = FindUsable(o.goid, use);
            UInt32 to = StateAfterUse(o.goid, o.state, use);
            Output.WriteDebugLog("[OBJECT] Use " + use + " (" + (usable?.label ?? "not in the data") + ") of " + o.goid + " in state 0x" +
                                 o.state.ToString("x") + (to != 0 ? ", to 0x" + to.ToString("x") : ""));
            if (to != 0 && to != o.state)
            {
                SetState(client, entity, to);
                return to;
            }

            return 0;
        }

        /// <summary>The state a successful Open moves this object to (a container's OpenedState, 0x834), or 0.</summary>
        public static UInt32 OpenedState(UInt16 goid)
        {
            Usable open = UsableLabelled(goid, "Open");
            return open == null ? 0 : StateAfterUse(goid, DefaultState(goid), open.use);
        }

        /// <summary>
        /// Sends a container its opened state (see <see cref="OpenedState"/>). Retail's script went there as soon as Open
        /// succeeded; here it's sent once the loot is taken, so a player who closes the loot window can open it again.
        /// </summary>
        public static bool ShowOpened(WorldClient client, UInt64 entity, UInt16 goid)
        {
            UInt32 opened = OpenedState(goid);
            ClientView view = opened == 0 ? null : client.viewMan.FindViewForEntity(entity);
            byte[] update = view == null ? null : BuildStateUpdate(goid, opened);
            if (update == null)
            {
                return false;
            }

            client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID, update), false);
            return true;
        }

        /// <summary>One line on what a state offers, e.g. "0x961 ST_Giver: 5 Take".</summary>
        public static string Describe(UInt16 goid, UInt32 state)
        {
            State s = Find(goid, state);
            IEnumerable<string> offered = Offered(goid, state).OrderBy(b => b)
                .Select(b => b + " " + (FindUsable(goid, b)?.label ?? "?"));
            return "0x" + state.ToString("x") + " " + (s?.name ?? "(not a state of " + goid + ")") + ": " +
                   (offered.Any() ? string.Join(", ", offered) : "nothing");
        }

        private static readonly ConditionalWeakTable<WorldClient, StrongBox<UInt64>> devObject =
            new ConditionalWeakTable<WorldClient, StrongBox<UInt64>>();

        /// <summary>
        /// Dev (MXO_DEV_COMMANDS=1), for trying the data in the client:
        ///   ?objstate &lt;goid&gt; [state hex|-] [Attribute=hex ...]   spawn the object at the player's feet with the mission's
        ///                                  key (element ff), use count 1, the state and any attributes of its class (e.g.
        ///                                  TextActiveTracker=01); the server then follows it (uses move it as retail did)
        ///   ?objstate &lt;state hex&gt;          send the last one a new state
        ///   ?objstate item &lt;goid&gt; [element hex]  a mission item with the mission's key (element default 00), client only
        /// Each prints what the data says the state offers.
        /// </summary>
        public static void DevCommand(WorldClient client, string[] args)
        {
            ServerPackets packets = new ServerPackets();
            ushort instance = client.playerData.missionInstanceCounter;
            if (args.Length >= 3 && args[1] == "item")
            {
                UInt32 item = UInt32.Parse(args[2]);
                byte element = args.Length > 3 ? Convert.ToByte(args[3], 16) : (byte)0;
                UInt16 slot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
                client.messageQueue.addObjectMessage(ServerPackets.BuildMissionKeyUpdate(instance), false);
                client.messageQueue.addRpcMessage(ServerPackets.BuildMissionItemAdd(slot, item, element, instance));
                packets.sendSystemChatMessage(client, "Mission item " + item + " in slot " + slot + ", key " + element.ToString("x2") +
                                                      " " + instance + " 02 (not saved)", "BROADCAST");
                client.FlushQueue();
                return;
            }

            StrongBox<UInt64> last = devObject.GetOrCreateValue(client);
            if (args.Length == 2 && args[1].StartsWith("0x"))
            {
                UInt32 state = Hex(args[1]);
                bool sent = SetState(client, last.Value, state);
                (UInt16 goid, UInt32 _) = TrackedGoid(client, last.Value);
                packets.sendSystemChatMessage(client, sent ? Describe(goid, state) : "No ?objstate object in view", "BROADCAST");
                client.FlushQueue();
                return;
            }

            if (args.Length < 2 || !UInt16.TryParse(args[1], out UInt16 objGoid))
            {
                packets.sendSystemChatMessage(client, "?objstate <goid> [state hex] | ?objstate 0x<state> | ?objstate item <goid> [element hex]", "BROADCAST");
                return;
            }

            FieldInfo field = typeof(GameObjectDefinitions).GetField("Object" + objGoid);
            if (field == null)
            {
                packets.sendSystemChatMessage(client, "No class for " + objGoid, "BROADCAST");
                return;
            }

            UInt32 spawnState = args.Length > 2 && args[2] != "-" ? Hex(args[2]) : DefaultState(objGoid);
            GameObject obj = (GameObject)Activator.CreateInstance(field.FieldType, field.Name, objGoid);
            obj.UnsetRelatedStaticObjId();
            obj.DisableAllAttributes();
            void Set(string name, byte[] value)
            {
                if (obj.GetType().GetField(name)?.GetValue(obj) is Attribute attribute)
                {
                    attribute.enable();
                    attribute.setValue(value);
                }
            }

            Set("Position", client.playerInstance.Position.getValue());
            Set("Orientation", new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x80, 0x3f });
            Set("MissionKey", new byte[] { 0xff, (byte)instance, (byte)(instance >> 8), 2 });
            Set("UseCount", new byte[] { 1 });
            if (spawnState != 0)
            {
                Set("CurrentState", NumericalUtils.uint32ToByteArray(spawnState, 1));
            }

            foreach (string arg in args.Skip(3).Where(a => a.Contains("=")))
            {
                string[] kv = arg.Split('=');
                Set(kv[0], StringUtils.hexStringToBytes(kv[1]));
            }

            client.messageQueue.addObjectMessage(ServerPackets.BuildMissionKeyUpdate(instance), false);
            UInt64 entity = WorldServer.entityIdCounter++;
            new ServerPackets().SendSpawnGameObject(client, obj, entity);
            Track(client, entity, objGoid, spawnState);
            last.Value = entity;
            packets.sendSystemChatMessage(client, "Object " + objGoid + ", instance " + instance + ", " + Describe(objGoid, spawnState),
                "BROADCAST");
        }

        private static (UInt16, UInt32) TrackedGoid(WorldClient client, UInt64 entity)
        {
            if (!tracked.TryGetValue(client, out Dictionary<UInt64, (UInt16, UInt32)> map))
            {
                return (0, 0);
            }

            lock (map)
            {
                return map.TryGetValue(entity, out (UInt16, UInt32) o) ? o : ((UInt16)0, 0u);
            }
        }
    }
}
