using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using hds.shared;

namespace hds
{
    /// <summary>A phase's mission object, spawned for one player.</summary>
    public class MissionObjectState
    {
        public MissionObjectDef def;
        public StaticWorldObject spot;  // the furniture or spawner it's bound to
        public UInt16 goid;
        public UInt64 entity;
        public MissionOpenFloorPlacement OpenFloor; // null for untouched legacy objects
        public bool lootOpen;           // a Search window is showing
        public bool putOpen;            // the empty Put container window is showing
        public UInt32 shownState;       // a computer's CurrentState as the client last had it
        public bool unlockedOnce;       // a locked object's lock was picked (it spawns again unlocked)
    }

    /// <summary>
    /// Sets up a generated phase: picks a site from the world's mission markup (MissionSites), places the phase's NPCs on its
    /// NPC spawners, spawns its hostiles and objects, and sets its waypoint to the floor's elevator door. Also walks
    /// escorted NPCs after the player, and offers the elevator ride retail's operator offered (ID_OPERATOR_MISSION_ELEVATOR).
    /// </summary>
    public static partial class MissionStaging
    {
        /// <summary>"I've hacked into the elevator control system. Do you want me to send you to the right floor?"</summary>
        public const UInt32 StringElevatorOffer = 0x600000E2;
        public const UInt32 StringYes = 0x02000007, StringNo = 0x02000000;

        private const UInt64 EntityBase = 987000000000UL;

        /// <summary>The MissionKey element retail's mission containers carried.</summary>
        public const byte ContainerElement = 0xff;
        private const double FollowDistance = 200, FollowStep = 400, FollowSnap = 3000;

        private static readonly Dictionary<string, (string handle, string[] rsi, UInt32 weapon)> Enemies =
            new Dictionary<string, (string, string[], UInt32)>(StringComparer.OrdinalIgnoreCase)
            {
                // Appearances and weapons from the captures (The Internuncio) and the world's mob data
                { "Merovingian", ("Exile Soldier", new[] { "c6020058", "c5020058", "ca020058" }, 2568) },
                { "Machines", ("Machine Program", new[] { "8f080058" }, 2568) },
                { "Zion", ("Zion Soldier", new[] { "60010058" }, 2564) }
            };

        private static readonly Dictionary<string, UInt16> ObjectGoids = new Dictionary<string, UInt16>(StringComparer.OrdinalIgnoreCase)
        {
            { "Computer", 3 }, { "Safe", 1416 }, { "Mainframe", 357 }, { "Box", 548 }
        };

        /// <summary>Titles for an object's loot window.</summary>
        public static UInt32 TitleFor(string kind)
        {
            switch ((kind ?? "").ToLowerInvariant())
            {
                case "safe":
                    return 0x0100032A; // ID_SAFE
                case "mainframe":
                    return 0x76000285; // ID_MAINFRAME_INFO
                default:
                    return 0x01000071; // ID_CONTAINER
            }
        }

        /// <summary>
        /// The first phase's site, picked when the player looks at a generated mission so the info window can show its
        /// distance; Stage uses it if they accept. Null for missions placed by their data.
        /// </summary>
        public static MissionSite Preview(WorldClient client, Mission mission)
        {
            ClientData data = client.playerData;
            if (mission.phases.Count == 0 || mission.phases[0].site == null)
            {
                return null;
            }

            if (data.missionPreview != null && data.missionPreviewFor == mission)
            {
                return data.missionPreview;
            }

            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            MissionPhase phase = mission.phases[0];
            int npcs = mission.npcs.Count(n => n.at == 1) + SpotsForWaves(phase);
            int objects = PlacesNeeded(phase);
            int furniture = FurnitureNeeded(phase);
            Random rng = LayoutRandom(mission.id,0,unchecked((int)data.getCharID() * 7919 + mission.id * 31 + Environment.TickCount));
            List<MissionSite> sites = MissionSites.For(mission.district);
            List<string> hoods = phase.neighbourhoods.Count > 0 ? phase.neighbourhoods : mission.neighbourhoods;
            data.missionPreview = MissionSites.Choose(sites, phase.site, px, pz, npcs, objects, rng, neighbourhoods: hoods, requiredFurniture: furniture) ??
                                  MissionSites.Choose(sites, "Any", px, pz, npcs, objects, rng, neighbourhoods: hoods, requiredFurniture: furniture);
            data.missionPreviewFor = mission;
            return data.missionPreview;
        }

        /// <summary>
        /// How many furniture pieces or object spawners a phase's site needs: one per object. A furniture kind takes furniture, a
        /// spawned kind (Safe, Computer) a spawner or else furniture. Counting only the furniture kinds let a floor with neither be
        /// picked, and its safe was never placed ("Nowhere to put object"), so the mission couldn't finish.
        /// </summary>
        public static int PlacesNeeded(MissionPhase phase) => phase.objects.Count;

        // EXACT PlaceObjects classifier: these kinds have no spawnable replacement and require actual furniture.
        public static int FurnitureNeeded(MissionPhase phase) => phase.objects.Count(d=>!ObjectGoids.ContainsKey(d.kind));

        public static int? QaSiteSeed(string dev,string database,string raw,int mission,int phase)
        {
            if(dev!="1" || database==null || !database.StartsWith("mxotest_",StringComparison.Ordinal) || database.Length<=8 ||
                !database.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_') || !int.TryParse(raw,out int seed) || seed<0) return null;
            return unchecked(seed ^ mission*31 ^ phase*104729);
        }
        internal static Random LayoutRandom(int mission,int phase,int legacySeed) => new Random(
            QaSiteSeed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable("MXO_MISSION_SITE_QA_SEED"),mission,phase) ?? legacySeed);


        /// <summary>How many of the site's NPC spots a phase's hostiles take: those that don't appear beside the player or an object.</summary>
        public static int SpotsForWaves(MissionPhase phase) => phase.Waves().Where(w => w.arrival.at == null).Sum(w => w.count);

        /// <summary>Picks and fills the site of the phase that has just started. False when the phase isn't generated.</summary>
        public static bool Stage(WorldClient client, MissionProgress progress) => StageCore(client,progress,null);
        internal static bool StagePrepared(WorldClient client,MissionProgress progress,MissionOpenFloorAdmission.Plan plan) => StageCore(client,progress,plan);
        private static bool StageCore(WorldClient client,MissionProgress progress,MissionOpenFloorAdmission.Plan prepared)
        {
            MissionPhase phase = progress.Phase;
            Mission mission = progress.mission;
            if (phase?.site == null)
            {
                return false;
            }

            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);

            int at = progress.phaseIndex + 1;
            List<MissionNpc> placed = mission.npcs.Where(n => n.at == at).ToList();
            int objects = PlacesNeeded(phase);
            int furniture = FurnitureNeeded(phase);
            Random rng=null;MissionSite site;
            if(prepared==null)
            {
                rng = LayoutRandom(mission.id,progress.phaseIndex,unchecked((int)client.playerData.getCharID() * 7919 + mission.id * 31 + at + (int)progress.startedAt));
                List<MissionSite> sites = MissionSites.For(mission.district);
                site = progress.phaseIndex == 0 && client.playerData.missionPreviewFor == mission ? client.playerData.missionPreview : null;
                client.playerData.missionPreview = null;
                client.playerData.missionPreviewFor = null;
                List<string> hoods = phase.neighbourhoods.Count > 0 ? phase.neighbourhoods : mission.neighbourhoods;
                int npcs = placed.Count + SpotsForWaves(phase);
                site = site ?? MissionSites.Choose(sites, phase.site, px, pz, npcs, objects, rng, progress.sites, hoods, requiredFurniture: furniture)
                            ?? MissionSites.Choose(sites, "Any", px, pz, npcs, objects, rng, progress.sites, hoods, requiredFurniture: furniture);
                if (site == null)
                {
                    Output.WriteLine("[MISSION] No site for phase " + at + " of '" + mission.title + "'");
                    return false;
                }
            }
            else
            {
                prepared.RequireCurrent(client,progress);
                site=prepared.Site;
                if(prepared.Adopted)progress.openFloorResolvedPhase=progress.phaseIndex;
                client.playerData.missionPreview=null;client.playerData.missionPreviewFor=null;
            }

            progress.site = site;
            progress.sites.Add(site);
            progress.waypoints = new List<MissionWaypoint>
            {
                new MissionWaypoint
                {
                    x = site.elevatorDoor.pos_x, y = site.elevatorDoor.pos_y, z = site.elevatorDoor.pos_z,
                    doorHex = MissionSites.DoorHex(site.elevatorDoor)
                }
            };
            progress.exits.Clear();
            progress.exits.Add(new[] { site.lobbyDoor.pos_x, site.lobbyDoor.pos_y, site.lobbyDoor.pos_z });
            Output.WriteDebugLog("[MISSION] Phase " + at + " at a " + site.style + " floor in " + site.neighbourhood + " (" + site.CentreX + "," + site.floorY + "," +
                                 site.CentreZ + "), elevator " + MissionSites.DoorHex(site.elevatorDoor) + " at " + site.elevatorDoor.pos_x + "," + site.elevatorDoor.pos_y + "," + site.elevatorDoor.pos_z + ", " + site.npcSpots.Count + " NPC spots; street door at " + site.lobbyDoor.pos_x + "," + site.lobbyDoor.pos_y + "," + site.lobbyDoor.pos_z);

            // NPC spots: the mission's own NPCs deepest in, away from the elevator; the hostiles between
            List<double[]> spots = site.npcSpots
                .OrderByDescending(s => Distance2(s[0], s[2], site.elevatorDoor.pos_x, site.elevatorDoor.pos_z))
                .ToList();
            int next = 0;
            double[] NextSpot()
            {
                double[] spot = spots[next % spots.Count];
                int lap = next / spots.Count;
                next++;
                // Out of spots: stand beside one already taken
                return lap == 0 ? spot : new[] { spot[0] + 120 * lap, spot[1], spot[2] + 80 * lap, spot[3] };
            }

            ushort level = PlayerLevel(client);
            List<MissionNpc> besideObjects = new List<MissionNpc>();
            foreach (MissionNpc npc in placed)
            {
                double[] spot = NextSpot();
                if (npc.arrival.Late)
                {
                    // Arrives later: its spot is kept for it
                    progress.reservedSpots[npc.id] = spot;
                    continue;
                }

                if (npc.arrival.AtObject != 0)
                {
                    besideObjects.Add(npc); // once the objects are placed
                    continue;
                }

                Mob mob = MissionRuntime.SpawnNpc(client, mission, npc, spot[0], spot[1], spot[2],
                    level: npc.level > 1 ? npc.level : level, friendly: progress.IsFriendly(npc));
                if (mob == null)
                {
                    progress.pendingNpcPlacements.Add(npc.id);
                    progress.reservedSpots[npc.id] = spot;
                    progress.justArrived.Add(npc);
                }
                MissionRuntime.AddMob(client, mob);
            }

            // Hostiles on the site's spots: the phase's own, then its waves; those that come later have their spots kept
            List<MissionWave> placeLater = new List<MissionWave>();
            foreach (MissionWave wave in phase.Waves())
            {
                if (wave.count <= 0 || !Enemies.ContainsKey(wave.enemy ?? ""))
                {
                    continue;
                }

                if (wave.arrival.at != null)
                {
                    if (!wave.arrival.Late)
                    {
                        placeLater.Add(wave); // beside the player or an object, from the start
                    }

                    continue;
                }

                List<double[]> enemySpots = new List<double[]>();
                for (int i = 0; i < wave.count; i++)
                {
                    enemySpots.Add(NextSpot());
                }

                if (wave.arrival.Late)
                {
                    progress.SpotsFor(wave.id).AddRange(enemySpots);
                }
                else
                {
                    if (!SpawnWave(client, progress, wave, enemySpots))
                    {
                        progress.SpotsFor(wave.id).AddRange(enemySpots);
                        progress.wavesJustArrived.Add(wave);
                    }
                }
            }

            if(prepared==null)PlaceObjects(client, progress, rng);
            else PlacePreparedObjects(client,progress,prepared);

            for (int i = 0; i < besideObjects.Count; i++)
            {
                double[] spot = MissionRuntime.ArrivalSpot(client, progress, besideObjects[i].arrival, i, px, py, pz);
                Mob mob = MissionRuntime.SpawnNpc(client, mission, besideObjects[i], spot[0], spot[1], spot[2],
                    level: besideObjects[i].level > 1 ? besideObjects[i].level : level, friendly: progress.IsFriendly(besideObjects[i]));
                if (mob == null)
                {
                    progress.pendingNpcPlacements.Add(besideObjects[i].id);
                    progress.justArrived.Add(besideObjects[i]);
                }
                MissionRuntime.AddMob(client, mob);
            }

            foreach (MissionWave wave in placeLater)
            {
                List<double[]> beside = new List<double[]>();
                for (int i = 0; i < wave.count; i++)
                {
                    beside.Add(MissionRuntime.ArrivalSpot(client, progress, wave.arrival, besideObjects.Count + i, px, py, pz));
                }

                if (!SpawnWave(client, progress, wave, beside)) progress.wavesJustArrived.Add(wave);
            }

            return true;
        }

        /// <summary>
        /// A wave of the phase's generated hostiles, at these spots: when the phase starts, or when the wave arrives. The
        /// phase's own (enemies=) are wave 0.
        /// </summary>
        public static bool SpawnWave(WorldClient client, MissionProgress progress, MissionWave wave, List<double[]> spots)
        {
            MissionPhase phase = progress.Phase;
            if (phase == null || string.IsNullOrEmpty(wave.enemy) || !Enemies.TryGetValue(wave.enemy, out var template))
            {
                return true;
            }

            // Plan the entire wave first, so a failed placement can retry without duplicating half a wave.
            List<NavPoint> floor = new List<NavPoint>();
            foreach (double[] spot in spots)
            {
                if (!MissionPlacement.ForSpawn(client, spot[0], spot[1], spot[2], wave.arrival.AtPlayer, out NavPoint placed, reserved: floor))
                {
                    progress.pendingWavePlacements.Add(wave.id);
                    return false;
                }
                floor.Add(placed);
            }
            int at = progress.phaseIndex + 1;
            ushort level = PlayerLevel(client);
            for (int i = 0; i < spots.Count; i++)
            {
                // Numbered on through the phase's waves, so no two of its hostiles share an id
                int n = progress.hostiles.Count;
                double[] spot = spots[i];
                MissionNpc enemy = new MissionNpc
                {
                    id = (ushort)(1000 + at * 50 + n),
                    handle = template.handle,
                    friendly = false,
                    attacksOnSight = true,
                    rsiHex = template.rsi[n % template.rsi.Length],
                    level = level,
                    maxHealth = (ushort)(150 + 40 * level),
                    weapon = template.weapon,
                    arrival = wave.arrival
                };
                Mob mob = MissionRuntime.SpawnNpc(client, progress.mission, enemy, spot[0], spot[1], spot[2], placement: floor[i]);
                MissionRuntime.AddMob(client, mob);
                progress.hostiles.Add(mob);
                progress.hostileWave[mob] = wave.id;
            }
            progress.pendingWavePlacements.Remove(wave.id);
            return true;
        }

        internal sealed class ObjectChoice
        {
            internal readonly MissionObjectDef Definition;
            internal readonly StaticWorldObject Spot;
            internal readonly ushort Goid;
            internal ObjectChoice(MissionObjectDef definition,StaticWorldObject spot,ushort goid)
            {Definition=definition;Spot=spot;Goid=goid;}
        }
        // Shared exact chooser. The legacy iterator still yields between each spawn;
        // only opt-in open-floor preflight materializes it before mutation.
        internal static IEnumerable<ObjectChoice> SelectObjects(MissionPhase phase,MissionSite site,Random rng)
        {
            List<StaticWorldObject> furniture = site.containers.OrderBy(_ => rng.Next()).ToList();
            List<StaticWorldObject> spawners = site.objectSpots.OrderBy(_ => rng.Next()).ToList();
            HashSet<StaticWorldObject> taken = new HashSet<StaticWorldObject>();
            foreach (MissionObjectDef def in phase.objects)
            {
                StaticWorldObject spot;
                UInt16 goid;
                StaticWorldObject real = site.realObjects.FirstOrDefault(o => !taken.Contains(o) &&
                    string.Equals(MissionSites.RealObjects[MissionSites.Goid(o)], def.kind, StringComparison.OrdinalIgnoreCase));
                if (real != null)
                {
                    // One already standing there, bound in place as retail bound its filing cabinets
                    spot = real;
                    goid = MissionSites.Goid(real);
                    taken.Add(real);
                }
                else if (ObjectGoids.TryGetValue(def.kind, out goid))
                {
                    // Spawned at a spawner meant for it, or any spawner, or failing that in place of some furniture
                    spot = spawners.FirstOrDefault(s => string.Equals(MissionSites.ObjectSpawners[MissionSites.Goid(s)], def.kind,
                               StringComparison.OrdinalIgnoreCase)) ?? spawners.FirstOrDefault() ?? furniture.FirstOrDefault();
                    if (spot != null && spot != furniture.FirstOrDefault())
                    {
                        spawners.Remove(spot);
                    }
                    else
                    {
                        furniture.Remove(spot);
                    }
                }
                else
                {
                    bool phone = string.Equals(def.kind, "Phone", StringComparison.OrdinalIgnoreCase);
                    spot = furniture.FirstOrDefault(f => (MissionSites.Goid(f) == MissionSites.PhoneGoId) == phone) ?? furniture.FirstOrDefault();
                    furniture.Remove(spot);
                    goid = spot == null ? (UInt16)0 : MissionSites.Goid(spot);
                }

                yield return new ObjectChoice(def,spot,GeneratedConcrete(spot,goid));
            }
        }

        private static void PlaceObjects(WorldClient client, MissionProgress progress, Random rng)
        {
            foreach (ObjectChoice choice in SelectObjects(progress.Phase,progress.site,rng))
            {
                MissionObjectDef def=choice.Definition;StaticWorldObject spot=choice.Spot;ushort goid=choice.Goid;
                if (spot == null)
                {
                    Output.WriteLine("[MISSION] Nowhere to put object " + def.id + " (" + def.kind + ")");
                    continue;
                }

                MissionObjectState state = new MissionObjectState
                {
                    def = def,
                    spot = spot,
                    goid = goid,
                    entity = EntityBase + (UInt64)client.playerData.missionInstanceCounter * 1000UL + (UInt64)(progress.phaseIndex * 50 + def.id)
                };
                progress.objects[def.id] = state;
                SpawnObject(client, progress, state);
                var pose = MissionObjectPoses.For(state);
                Output.WriteDebugLog("[MISSION] Object " + def.id + " (" + def.kind + ") is a " + goid + " at " + pose.X + "," +
                                     pose.Y + "," + pose.Z);
            }
        }

        /// <summary>
        /// Retail spawned a mission's containers as dynamic objects bound to the static they stand in for, with the mission's
        /// key (The Internuncio's filing cabinet; the tutorial's wardrobe), and the client can only use them then.
        /// </summary>
        /// <summary>
        /// A computer's states, from its record in the client's gameobjects.gob (0x960 to 0x965, default 0x960). Tried in the
        /// client: 0x960 is idle; 0x961 offers Take (80 c7 use 5); 0x962 offers Put (use 8) with a matching mission key; 0x963 to 0x965 offer Hack (use
        /// 4). The earlier no-item probe did not satisfy the Put key rule. Containers (Wall Safe, Filing Cabinet) default to 0x835, which offers Open (use 3).
        /// </summary>
        public const UInt32 ComputerIdle = 0x960, ComputerTake = 0x961, ComputerPut = 0x962, ComputerHack = 0x963;

        /// <summary>Computer and MainFrame_Computer (AttributeClass20): the client's state machine decides what they offer.</summary>
        public static bool IsComputer(UInt16 goid)
        {
            return typeof(GameObjectDefinitions).GetField("Object" + goid)?.FieldType == typeof(AttributeClass20);
        }

        /// <summary>
        /// Objects the player can click: computers, and containers (AttributeClass46: safes, cabinets, boxes, desks, dressers;
        /// 369: the wardrobe), whose default state offers Open. The phone isn't known to be.
        /// </summary>
        public static bool IsClickable(UInt16 goid)
        {
            Type type = typeof(GameObjectDefinitions).GetField("Object" + goid)?.FieldType;
            return type == typeof(AttributeClass20) || type == typeof(AttributeClass46) || type == typeof(AttributeClass369);
        }

        /// <summary>What a computer offers for its open goal: Take for one that gives an item, Hack for the rest.</summary>
        public static UInt32 ComputerStateFor(MissionProgress progress, MissionObjectState state)
        {
            foreach (MissionObjective objective in progress.OpenObjectives())
            {
                if (objective.objectId == state.def.id)
                {
                    return objective.type == MissionGoalType.Put ? ComputerPut :
                        objective.type == MissionGoalType.Search && objective.item != 0 ? ComputerTake : ComputerHack;
                }
            }

            return ComputerIdle;
        }

        /// <summary>After goals change: each computer offers what its next goal needs, or nothing (CurrentState, update mask 01 08).</summary>
        public static void RefreshObjects(WorldClient client, MissionProgress progress)
        {
            foreach (MissionObjectState state in progress.objects.Values)
            {
                if (!IsComputer(state.goid))
                {
                    continue;
                }

                UInt32 wanted = ComputerStateFor(progress, state);
                ClientView view = client.viewMan.FindViewForEntity(state.entity);
                if (wanted == state.shownState || view == null)
                {
                    continue;
                }

                state.shownState = wanted;
                byte[] value = NumericalUtils.uint32ToByteArray(wanted, 1);
                client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID, new byte[] { 0x01, 0x08 }.Concat(value).ToArray()), false);
            }
        }

        internal static void RestoreObjects(WorldClient client, MissionProgress progress)
        {
            foreach(var state in progress.objects.Values)
            {
                state.entity=EntityBase+(ulong)client.playerData.missionInstanceCounter*1000UL+(ulong)(progress.phaseIndex*50+state.def.id);
                state.lootOpen=false;state.putOpen=false;
                if(client.viewMan.FindViewForEntity(state.entity)==null)SpawnObject(client,progress,state);
            }
        }

        // A generated proxy is independent of its spawner; unrelated same-prototype furniture keeps its existing binding.
        internal static void ConfigureSourceBinding(GameObject obj,MissionObjectState state)
        {
            if(MissionSites.Goid(state.spot)==state.goid)obj.SetRelatedStaticObjId(state.spot.mxoStaticId);
            else obj.UnsetRelatedStaticObjId();
        }

        private static void SpawnObject(WorldClient client, MissionProgress progress, MissionObjectState state)
        {
            FieldInfo field = typeof(GameObjectDefinitions).GetField("Object" + state.goid);
            if (field == null)
            {
                return;
            }

            GameObject obj = (GameObject)Activator.CreateInstance(field.FieldType, field.Name, state.goid);
            // Bound to the static only when it's the same object, as retail bound its filing cabinets and dressers (08 <goid>
            // <static>). At a spawner marker retail spawned it free (0c, no static): the client's definitions give the
            // spawners no Info or Usable part, so a mission object bound to one couldn't be selected.
            ConfigureSourceBinding(obj,state);
            obj.DisableAllAttributes();
            ushort instance = client.playerData.missionInstanceCounter;
            var pose = MissionObjectPoses.For(state);
            SetAttribute(obj, "Position", NumericalUtils.doublesToLtVector3d(pose.X, pose.Y, pose.Z));
            SetAttribute(obj, "Orientation", pose.Orientation);
            // Retail's mission containers (The Internuncio's filing cabinets and wall safes, 04 c6 01 <quaternion> <position> 04
            // ff 98 02 02): use count 1 and the mission's key with element 0xff
            SetAttribute(obj, "MissionKey", new byte[] { ContainerElement, (byte)instance, (byte)(instance >> 8), 2 });
            SetAttribute(obj, "UseCount", new byte[] { 1 });
            if (IsComputer(state.goid))
            {
                // In its default state a computer can't be selected
                state.shownState = ComputerStateFor(progress, state);
                SetAttribute(obj, "CurrentState", NumericalUtils.uint32ToByteArray(state.shownState, 1));
            }
            else if (state.def.locked && UtilityAbilities.IsLockable(state.goid) &&
                     !state.unlockedOnce)
            {
                // Its LockedState offers Pick Lock and Unlock, not Open (UtilityAbilities: rule 4000 needs Open Locks)
                SetAttribute(obj, "CurrentState", NumericalUtils.uint32ToByteArray(UtilityAbilities.LockedState, 1));
                UtilityAbilities.RegisterLocked(client, state.entity, state.goid, () =>
                {
                    lock(client.playerData.missionSync)
                    {
                        if(client.playerData.missionProgress!=progress)return;
                        state.unlockedOnce=true;
                        MissionPersistence.Save(client);
                    }
                });
            }
            new ServerPackets().SendSpawnGameObject(client, obj, state.entity);
        }

        private static void SetAttribute(GameObject obj, string name, byte[] value)
        {
            FieldInfo field = obj.GetType().GetField(name);
            if (field?.GetValue(obj) is Attribute attribute)
            {
                attribute.enable();
                attribute.setValue(value);
            }
        }

        /// <summary>Dev (?missionobject): spawns a mission object again at the player's feet, free-standing and unrotated.</summary>
        public static bool MoveObjectToPlayer(WorldClient client, int id)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null || !progress.objects.TryGetValue((ushort)id, out MissionObjectState state))
            {
                return false;
            }

            // Fixed admitted poses are immutable; this explicit legacy custom-move tool refuses before touching a view or source.
            if(state.OpenFloor!=null)return false;

            ClientView view = client.viewMan.FindViewForEntity(state.entity);
            if (view != null)
            {
                new ServerPackets().sendDeleteViewPacket(client, view.ViewID);
                client.viewMan.removeViewByViewId(view.ViewID);
            }

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            state.spot = new StaticWorldObject
            {
                pos_x = x, pos_y = y, pos_z = z, quat = "000000000000000000000000" + "0000803f",
                type = new byte[] { 0, 0, 0, 0 }
            };
            SpawnObject(client, progress, state);
            return true;
        }

        public static void DespawnObjects(WorldClient client, MissionProgress progress)
        {
            PosterBoxFloorProbe.Clear(client);
            foreach (MissionObjectState state in progress.objects.Values)
            {
                ClientView view = client.viewMan.FindViewForEntity(state.entity);
                if (view != null)
                {
                    new ServerPackets().sendDeleteViewPacket(client, view.ViewID);
                    client.viewMan.removeViewByViewId(view.ViewID);
                }
            }

            progress.objects.Clear();
        }

        /// <summary>Walks following NPCs after the player. Called from the mission tick.</summary>
        public static void Follow(WorldClient client, MissionProgress progress, double px, double py, double pz)
        {
            foreach (ushort id in progress.following.ToList())
            {
                Mob mob = MissionRuntime.FindNpc(client.playerData, id);
                if (mob == null || mob.getIsDead())
                {
                    continue;
                }

                double dx = px - mob.getXPos(), dz = pz - mob.getZPos(), dy = py - mob.getYPos();
                double flat = Math.Sqrt(dx * dx + dz * dz);
                if (flat <= FollowDistance + 50 && Math.Abs(dy) < 200)
                {
                    continue;
                }

                if (flat > FollowSnap || Math.Abs(dy) >= 200)
                {
                    // The player took the elevator or ran off: catch up behind them
                    PlaceBeside(mob, px, py, pz);
                    continue;
                }

                double step = Math.Min(FollowStep, flat - FollowDistance);
                double nx = mob.getXPos() + dx / flat * step, nz = mob.getZPos() + dz / flat * step;
                mob.setXPos(nx);
                mob.setZPos(nz);
                mob.setYPos(py);
                mob.setRotation(MobAggro.YawTowards(nx, nz, px, pz));
                mob.updateAnimation((byte)(flat - step > FollowDistance + 60 ? Mob.Animations.WALK : Mob.Animations.STAND));
            }
        }

        public static void PlaceBeside(Mob mob, double px, double py, double pz)
        {
            if (!NavMesh.Project((ushort)mob.getDistrict(), px + 150, py, pz + 100, out NavPoint placed,
                reachableFrom: new[] { px, py, pz }, requireDirect: true)) return;
            mob.setXPos(placed.x);
            mob.setYPos(placed.y);
            mob.setZPos(placed.z);
            mob.setRotation(MobAggro.YawTowards(placed.x, placed.z, px, pz));
            mob.updateAnimation((byte)Mob.Animations.STAND);
        }

        /// <summary>
        /// The player used an elevator door or panel at (x, z). In a mission's building the operator takes them to the mission
        /// floor, or back down to the street once that floor is done. Returns true if a ride was set off.
        ///
        /// Retail's operator asked first ("I've hacked into the elevator control system. Do you want me to send you to the
        /// right floor?", ID_OPERATOR_MISSION_ELEVATOR) in a modal. Here the client showed that modal from a chat command, but
        /// not when it followed a door's use, and after that it showed none until relogging; so the ride is taken without
        /// asking, and the elevator panel's own floor list isn't used (BuildPanel: its region id is only known for two shafts).
        /// </summary>
        public static bool OnElevator(WorldClient client, StaticWorldObject used)
        {
            return used != null && OnElevator(client, used.pos_x, used.pos_z, used);
        }

        public static bool OnElevator(WorldClient client, double x, double z, StaticWorldObject used = null)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return false;
            }

            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);

            MissionSite site = progress.sites.LastOrDefault(s => used != null ? MissionSites.InShaft(s, used) : MissionSites.InShaft(s, x, z));
            if (site == null)
            {
                return false;
            }

            bool current = site == progress.site && !progress.waitingForExit;
            StaticWorldObject target = current ? site.elevatorDoor : site.lobbyDoor;
            if (Math.Abs(py - target.pos_y) < 200)
            {
                return false; // already on that floor
            }

            client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(0x28000a70, 0x26000014, 0, 1, 0, "",
                current ? ElevatorUpLine : ElevatorDownLine));
            client.FlushQueue();
            Output.WriteDebugLog("[MISSION] Elevator: to " + target.pos_y);
            var rideRequested = System.Diagnostics.Stopwatch.StartNew();
            var stamp=new MissionRideStamp(client,progress);
            DelayedActions.AfterFor(client, 2, () =>
            {
                Output.WriteDebugLog("[MISSION] Elevator callback elapsed ms " + rideRequested.ElapsedMilliseconds);
                MissionTransportState.Run(client, () => client.TransportFence.TryRun(stamp.Transport, () =>
                {
                    if(!stamp.Current)return;
                    Ride(client, progress, target, site);
                }));
            });
            return true;
        }

        public const string ElevatorUpLine = "I've hacked into the elevator control system. Sending you to the right floor.";
        public const string ElevatorDownLine = "I've hacked into the elevator control system. Taking you back down to the street.";

        private static void Ride(WorldClient client, MissionProgress progress, StaticWorldObject target, MissionSite site)
        {
            Output.WriteDebugLog("[MISSION] Elevator callback for " + progress.mission.id + "; current " +
                (client.playerData.missionProgress?.mission.id.ToString() ?? "none"));
            if (client.playerData.missionProgress != progress)
            {
                Output.WriteDebugLog("[MISSION] Elevator ride cancelled: mission changed");
                return;
            }

            // Into the car at the other floor, as the street door's car is: a couple of metres from its door towards the
            // middle of the building. Retail stood players 50 units below a floor after a ride (y 2495 on floor 2545).
            double dx = site.CentreX - target.pos_x, dz = site.CentreZ - target.pos_z, length = Math.Max(1, Math.Sqrt(dx * dx + dz * dz));
            double x = target.pos_x + dx / length * 200, y = target.pos_y - 50, z = target.pos_z + dz / length * 200;
            // That spot can be in the gap between the elevator core and the floor, where the client has no nav polygon and
            // lets the player through walls: stand on the nearest polygon to the door instead
            Output.WriteDebugLog("[MISSION] Elevator navigation start");
            if (PacketsUtils.PlaceNear((UInt16)client.playerData.getDistrictId(), target.pos_x, y, target.pos_z, out NavLocation car) &&
                !NavMesh.Locate((UInt16)client.playerData.getDistrictId(), x, y, z, out _))
            {
                x = NearestOnMesh(client, target.pos_x, y, target.pos_z, out z);
            }

            Output.WriteDebugLog("[MISSION] Elevator navigation finished");
            Teleport(client, x, y, z);
            progress.CommitGeneratedFloor(progress.phaseIndex, site, target);
            foreach (ushort id in progress.following)
            {
                Mob mob = MissionRuntime.FindNpc(client.playerData, id);
                if (mob != null && !mob.getIsDead())
                {
                    PlaceBeside(mob, x, y, z);
                }
            }

            client.FlushQueue();
        }

        /// <summary>The nearest spot to (x, z) with a nav polygon at this height, searched outward to 4 m; returns x, sets z.</summary>
        private static double NearestOnMesh(WorldClient client, double x, double y, double z, out double outZ)
        {
            UInt16 metr = (UInt16)client.playerData.getDistrictId();
            for (int radius = 50; radius <= 400; radius += 50)
            {
                for (int step = 0; step < 16; step++)
                {
                    double angle = step * Math.PI / 8, px = x + radius * Math.Cos(angle), pz = z + radius * Math.Sin(angle);
                    if (NavMesh.Locate(metr, px, y, pz, out _))
                    {
                        outZ = pz;
                        return px;
                    }
                }
            }

            outZ = z;
            return x;
        }

        /// <summary>The player took an elevator to another floor: following NPCs come too.</summary>
        public static void OnFloorReached(WorldClient client, double x, double y, double z)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return;
            }

            foreach (ushort id in progress.following)
            {
                Mob mob = MissionRuntime.FindNpc(client.playerData, id);
                if (mob != null && !mob.getIsDead())
                {
                    PlaceBeside(mob, x, y, z);
                }
            }
        }

        public static void Teleport(WorldClient client, double x, double y, double z)
        {
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
            client.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket(client, (float)x, (float)y, (float)z), false);
            Output.WriteDebugLog("[MISSION] Teleported to " + x + "," + y + "," + z);
        }

        public static ushort PlayerLevel(WorldClient client)
        {
            byte[] level = client.playerInstance.Level.getValue();
            return (ushort)Math.Max(1, level != null && level.Length > 0 ? level[0] : 1);
        }

        private static double Distance2(double ax, double az, double bx, double bz) => (ax - bx) * (ax - bx) + (az - bz) * (az - bz);
    }
}
