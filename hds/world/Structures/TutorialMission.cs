using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>
    /// The retail tutorial (mission 986). TutorialScript.g.cs is retail's own script, converted; this is the mission engine it
    /// assumes: goals close in order, an early completion is held and replayed when its turn comes, and timers run on the server.
    /// The goals the script leaves to "mission data" are completed here, from what the player does.
    /// research/missions/tutorial-wire-spec.md has the wire evidence for all of it.
    /// </summary>
    public partial class TutorialMission
    {
        public const int MissionId = 986;

        // Where the player leaves the tutorial, from all complete runs
        public const double ExitX = 12300, ExitY = -705, ExitZ = 59700;
        public const UInt16 RichlandHardline = 153;

        private const ushort AmmitId = 1, ScarlettId = 2, BobId = 3, AlexId = 4;
        private const UInt32 StaticWardrobe = 0x6a, StaticDoor = 0x51, StaticBooth = 0x4d;
        private const UInt32 DoorKey = 568, Letter = 6793, Revolver = 2565;
        private const byte UseOpen = 0x03, UseExit = 0x05, UseUpload = 0x06;
        private const double DojoDoorZ = -12600;
        private const UInt32 StateOpen = 0x834, StateReady = 0x835, StateClosed = 0x836;
        private const UInt64 EntityBase = 986000000000UL;
        private const UInt16 DojoDoorType = 6994;
        private const UInt32 WardrobeTitle = 0x76000287; // ID_WARDROBE

        // Retail spawned a room's objects about a second after the teleport, not with it
        private const long ObjectSpawnDelay = 2, SpawnWardrobeEvent = 9001, SpawnDoorEvent = 9002, SpawnBoothEvent = 9003;
        private const long DoorClosingEvent = 9005, DoorClosedEvent = 9006;

        private static readonly Dictionary<string, double[]> Elements = new Dictionary<string, double[]>
        {
            // x, y, z, then the quaternion
            ["Interrogation"] = new[] { 7600, 3920, -16150, 0, -0.3987, 0, 0.9171 },
            ["Gallery"] = new[] { 5100, 420, -16700, 0, -0.7071, 0, 0.7071 },
            ["Dojo Ante"] = new double[] { 5800, -1180, -13700, 0, 0, 0, 1 },
            ["Firearm Training"] = new double[] { 6700, 1520, -12300, 0, 0, 0, 1 },
            ["Exit"] = new double[] { 6950, 220, -11100, 0, 0, 0, 1 }
        };

        // The panels a goal's script would have shown, for skipping it with MXO_TUTORIAL_WARP
        private static readonly Dictionary<long, UInt16[]> WarpPanels = new Dictionary<long, UInt16[]>
        {
            [1] = new UInt16[] { 0x0e },
            [4] = new UInt16[] { 0x15 },
            [8] = new UInt16[] { 0x01 },
            [12] = new UInt16[] { 0x02 },
            [13] = new UInt16[] { 0x0f, 0x27 },
            [15] = new UInt16[] { 0x19 },
            [16] = new UInt16[] { 0x18 },
            [17] = new UInt16[] { 0x1b },
            [18] = new UInt16[] { 0x1a },
            [19] = new UInt16[] { 0x1c },
            [28] = new UInt16[] { 0x03 }
        };

        private static readonly List<TutorialMission> running = new List<TutorialMission>();
        private static Timer timer;

        private readonly WorldClient client;
        private readonly object sync = new object();
        private readonly List<KeyValuePair<double, long>> timers = new List<KeyValuePair<double, long>>();
        private readonly HashSet<long> held = new HashSet<long>();
        private readonly HashSet<long> done = new HashSet<long>();
        private readonly Dictionary<UInt32, UInt16> itemSlots = new Dictionary<UInt32, UInt16>();
        private readonly List<Mob> npcs = new List<Mob>();
        private readonly double startAt;
        private readonly ushort instance;
        private long current;
        private bool started;
        private bool finished;
        private bool wardrobeOpen;
        private double lastX, lastZ;
        private byte lastYaw;
        private bool haveLast;

        private TutorialMission(WorldClient client)
        {
            this.client = client;
            client.playerData.missionInstanceCounter++;
            instance = client.playerData.missionInstanceCounter;
            startAt = TimeUtils.GetSimTimeSeconds() + 2; // retail began once the client reported the world loaded
        }

        public static TutorialMission Of(WorldClient client) => client?.playerData?.tutorial;

        /// <summary>A character spawned in the tutorial district.</summary>
        public static void Begin(WorldClient client)
        {
            if (client.playerData.tutorial != null)
            {
                return;
            }

            TutorialMission mission = new TutorialMission(client);
            client.playerData.tutorial = mission;
            lock (running)
            {
                running.Add(mission);
                if (timer == null)
                {
                    timer = new Timer(_ => TickAll(), null, 500, 500);
                }
            }

            Output.WriteDebugLog("[TUTORIAL] Begins for charId " + client.playerData.getCharID());
        }

        private static void TickAll()
        {
            List<TutorialMission> missions;
            lock (running)
            {
                missions = new List<TutorialMission>(running);
            }

            foreach (TutorialMission mission in missions)
            {
                try
                {
                    mission.Tick(TimeUtils.GetSimTimeSeconds());
                }
                catch (Exception ex)
                {
                    Output.WriteLine("[TUTORIAL] Tick failed: " + ex.Message);
                }
            }
        }

        private void Tick(double now)
        {
            lock (sync)
            {
                if (finished)
                {
                    return;
                }

                if (!started)
                {
                    if (now >= startAt)
                    {
                        Start();
                    }

                    return;
                }

                CheckMovement();

                List<long> due = new List<long>();
                timers.RemoveAll(t =>
                {
                    if (t.Key > now)
                    {
                        return false;
                    }

                    due.Add(t.Value);
                    return true;
                });
                foreach (long eventId in due)
                {
                    switch (eventId)
                    {
                        case SpawnWardrobeEvent:
                            SpawnWardrobe();
                            break;
                        case SpawnDoorEvent:
                            SpawnDoor();
                            break;
                        case SpawnBoothEvent:
                            SpawnBooth();
                            break;
                        case DoorClosingEvent:
                            SetObjectState(EntityBase + StaticDoor, new byte[] { 0x01, 0x80, 0x02 }, DoorLifecycle.StateClosing);
                            break;
                        case DoorClosedEvent:
                            SetObjectState(EntityBase + StaticDoor, new byte[] { 0x01, 0x80, 0x02 }, DoorLifecycle.StateClosed);
                            break;
                        default:
                            OnMissionEvent(eventId);
                            break;
                    }
                }

                client.FlushQueue();
            }
        }

        // Retail's order (spec 3.2)
        private void Start()
        {
            started = true;
            client.messageQueue.addObjectMessage(ServerPackets.BuildMissionKeyUpdate(instance), false);
            OnStartOfTutorialMission();
            client.messageQueue.addRpcMessage(ServerPackets.BuildTutorialSetName(instance));
            client.messageQueue.addRpcMessage(ServerPackets.BuildMissionBriefing("", "300022030000000000000000" + "00003c00",
                new float[3], new float[3]));
            OpenGoal(0);
            PlayNarration(CAMERA_DIALOG);
            SendMissionEvent(1);
            ScheduleEvent(26, CAMERA_GOAL + DELAY_OFFSET);
            Spawn(AmmitId, "Ammit", 9011, 295, -17000, "86010058", 50, 2600, true, false);
            Warp();
            client.FlushQueue();
        }

        /// <summary>For testing, MXO_TUTORIAL_WARP=n starts at goal n, as retail's TutorialWarpFactor console variable did:
        /// the goals before it close without their scripts running.</summary>
        private void Warp()
        {
            if (!long.TryParse(Environment.GetEnvironmentVariable("MXO_TUTORIAL_WARP"), out long goal) || goal <= 0 || goal >= GoalTexts.Length)
            {
                return;
            }

            for (long g = 0; g < goal; g++)
            {
                new ServerPackets().SendSetMissionObjective((ushort)g, MissionRuntime.ObjectiveDone, GoalTexts[g], client);
                done.Add(g);
                foreach (KeyValuePair<long, UInt16[]> panels in WarpPanels)
                {
                    if (panels.Key == g)
                    {
                        foreach (UInt16 panel in panels.Value)
                        {
                            ToggleView(panel, 1);
                        }
                    }
                }
            }

            current = goal;
            if (goal >= 28)
            {
                ScheduleEvent(ObjectSpawnDelay, SpawnBoothEvent);
            }

            if (goal >= 12 && goal < 22)
            {
                ScheduleEvent(ObjectSpawnDelay, SpawnDoorEvent);
                Spawn(BobId, "Bob", 5800, -1105, -10300, "37050058", 2, 100, false, false);
            }

            CompleteGoal(goal);
        }

        // ---- What the script calls ----

        private void PlayNarration(long id)
        {
            if (id != 0)
            {
                client.messageQueue.addRpcMessage(ServerPackets.BuildNarration((UInt32)id));
            }
        }

        private void BroadcastMessage(long stringId, long image)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildDialog((UInt32)image, 0, (UInt32)stringId, 1, 0, "", ""));
        }

        private void ScheduleEvent(long seconds, long eventId)
        {
            timers.Add(new KeyValuePair<double, long>(TimeUtils.GetSimTimeSeconds() + seconds, eventId));
        }

        private void SendModalMessage(long message, long stringId, long image, long left, long right)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildModalMessage((UInt32)image, (UInt32)stringId, (UInt32)message, (UInt32)left, (UInt32)right));
        }

        private bool IsGoalComplete(long goal) => done.Contains(goal);

        private void ToggleView(long view, long show)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildToggleView((UInt16)view, (byte)show));
        }

        private void SendMissionEvent(long eventId)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildMissionEvent((UInt32)eventId));
        }

        private void PlayLocalFX(UInt32 fx, long token = 0)
        {
            // Retail's fade to black and white transitions (81 42) leave this client's interface hidden for good, with no
            // effect to see, and with a token it never answers 81 91, so the teleport waits forever. Cause unknown. Without
            // them the tutorial works: a token's event runs after the 1 s or so the client took in retail.
            if (token != 0)
            {
                ScheduleEvent(1, token);
            }
        }

        private void TeleportToMissionElement(string name)
        {
            double[] element = Elements[name];
            // Retail's 81 99 came with a position update on the player's own view, 75 above the element (the player's feet)
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(element[0], element[1] + 75, element[2]));
            client.messageQueue.addRpcMessage(ServerPackets.BuildMissionTeleport(element));
            client.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket(client, (float)element[0], (float)element[1] + 75, (float)element[2]), false);
            haveLast = false;

            // Who's waiting in the room just reached
            switch (name)
            {
                case "Gallery":
                    Spawn(ScarlettId, "Scarlett", 4100, 495, -16800, "89090058", 1, 75, true, false);
                    ScheduleEvent(ObjectSpawnDelay, SpawnWardrobeEvent);
                    break;
                case "Dojo Ante":
                    Spawn(BobId, "Bob", 5800, -1105, -10300, "37050058", 2, 100, false, false);
                    ScheduleEvent(ObjectSpawnDelay, SpawnDoorEvent);
                    break;
                case "Firearm Training":
                    Spawn(AlexId, "Alex", 7480, 1595, -11900, "ec020058", 1, 75, false, false);
                    break;
                case "Exit":
                    ScheduleEvent(ObjectSpawnDelay, SpawnBoothEvent);
                    break;
            }
        }

        /// <summary>The blue pill. Nobody captured it; the script boots the player, so they're sent back out of the game.</summary>
        private void BootPlayer()
        {
            Finish();
            new ServerPackets().sendExitGame(client);
        }

        private void RespawnNPC(string name)
        {
            RemoveNpc(BobId);
            Spawn(BobId, "Bob", 5800, -1105, -10300, "37050058", 2, 100, false, false);
        }

        /// <summary>Retail reopened goal 23 with empty text (80 a0 17 06 00 00 01 00 00).</summary>
        private void UnkillNPC(string name)
        {
            done.Remove(ABILITIES_USE_GOAL);
            new ServerPackets().SendSetMissionObjective((ushort)ABILITIES_USE_GOAL, MissionRuntime.ObjectiveOpen, "", client);
        }

        private void GrantExperience(long amount)
        {
            PlayerRewards.GrantExperience(client, (UInt32)amount, 0x03);
        }

        // ---- The engine ----

        private void OpenGoal(long goal)
        {
            current = goal;
            if (goal < GoalTexts.Length)
            {
                new ServerPackets().SendSetMissionObjective((ushort)goal, MissionRuntime.ObjectiveOpen, GoalTexts[goal], client);
            }
        }

        /// <summary>A completion for a later goal is held silently and replayed when its turn comes (spec 4.1).</summary>
        private void CompleteGoal(long goal)
        {
            if (done.Contains(goal))
            {
                return;
            }

            if (goal != current)
            {
                held.Add(goal);
                return;
            }

            Output.WriteDebugLog("[TUTORIAL] Goal " + goal + " done: " + GoalTexts[goal]);
            new ServerPackets().SendSetMissionObjective((ushort)goal, MissionRuntime.ObjectiveDone, GoalTexts[goal], client);
            done.Add(goal);
            if (goal == WITHDRAWL_GOAL)
            {
                PrepareForDojo();
            }

            OnGoalComplete(goal);

            // Goals can go out of step with OnGoalComplete only when the respawn loop reopens goal 23
            if (goal + 1 < GoalTexts.Length && current == goal && !done.Contains(goal + 1))
            {
                long next = goal + 1;
                if (held.Remove(next))
                {
                    current = next;
                    CompleteGoal(next);
                }
                else
                {
                    OpenGoal(next);
                }
            }
        }

        private void CheckMovement()
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            byte yaw = client.playerInstance.YawInterval.getValue()[0];

            if (!haveLast)
            {
                lastX = x;
                lastZ = z;
                lastYaw = yaw;
                haveLast = true;
                return;
            }

            bool moved = x != lastX || z != lastZ;
            if (yaw != lastYaw || moved)
            {
                CompleteGoal(CAMERA_GOAL);
            }

            if (moved)
            {
                CompleteGoal(MOVEMENT_GOAL);
            }

            // Retail's zone data isn't readable; every run closed this just past the door's plane.
            if (done.Contains(WITHDRAWL_GOAL) && z >= DojoDoorZ)
            {
                CompleteGoal(ENTERDOJO_GOAL);
            }

            lastX = x;
            lastZ = z;
            lastYaw = yaw;
        }

        private Mob Spawn(ushort id, string name, double x, double y, double z, string rsi, ushort level, ushort hp, bool friendly, bool attacksOnSight)
        {
            Mob mob = new Mob();
            mob.setEntityId(WorldServer.entityIdCounter++);
            mob.setDistrict((ushort)MxOLocations.TUTORIAL);
            mob.setDistrictName(client.playerData.getDistrict());
            mob.setName(name);
            mob.setLevel(level);
            mob.setHealthM(hp);
            mob.setHealthC(hp);
            mob.setMobId(id);
            mob.setRsiHex(rsi);
            mob.setXPos(x);
            mob.setYPos(y);
            mob.setZPos(z);
            mob.xBase = x;
            mob.yBase = y;
            mob.zBase = z;
            mob.setRotation(0);
            mob.setIsDead(false);
            mob.setIsLootable(false);
            mob.isFriendly = friendly;
            mob.aggroKind = attacksOnSight ? MobAggroKind.Unfiltered : MobAggroKind.Passive;
            mob.missionNpcId = id;
            mob.missionPhase = id == ScarlettId ? (byte)1 : id == BobId ? (byte)2 : (byte)0;
            mob.hostileMissionKey = !friendly;
            mob.missionTaker = id == ScarlettId;
            mob.missionInstance = instance;

            lock (WorldServer.mobs.SyncRoot)
            {
                WorldServer.mobs.Add(mob);
            }

            lock (WorldServer.gameServerEntities.SyncRoot)
            {
                WorldServer.gameServerEntities.Add(mob);
            }

            npcs.Add(mob);
            return mob;
        }

        // The objects retail spawned, bound to their static ids, from its creation records (spec 5.4). The client can't
        // use them until they're spawned.
        private void SpawnWardrobe()
        {
            AttributeClass369 wardrobe = new AttributeClass369("Wardrobe", 7538);
            wardrobe.SetRelatedStaticObjId(StaticWardrobe);
            wardrobe.DisableAllAttributes();
            wardrobe.UseCount.enable();
            wardrobe.UseCount.setValue(3);
            wardrobe.Orientation.enable();
            wardrobe.Orientation.setValue(StringUtils.hexStringToBytes("00000000f20435bf00000000f304353f"));
            wardrobe.Position.enable();
            wardrobe.Position.setValue(NumericalUtils.doublesToLtVector3d(5450, 522.5, -16500));
            wardrobe.MissionKey.enable();
            wardrobe.MissionKey.setValue(new byte[] { 5, (byte)instance, (byte)(instance >> 8), 2 });
            wardrobe.CurrentState.enable();
            wardrobe.CurrentState.setValue(NumericalUtils.uint32ToByteArray(StateClosed, 1));
            new ServerPackets().SendSpawnGameObject(client, wardrobe, EntityBase + StaticWardrobe);
        }

        // Retail: 08 52 1b 51 00 00 00 <spawn> cd ab 03 80 a2 <position> 06 <instance u16> <key> 01 36 08 00 00
        private void SpawnDoor()
        {
            AttributeClass363 door = new AttributeClass363("Door DT 2Push Knob darkwood01", DojoDoorType);
            door.SetRelatedStaticObjId(StaticDoor);
            door.DisableAllAttributes();
            door.Position.enable();
            door.Position.setValue(NumericalUtils.doublesToLtVector3d(5800, -1055, -12600));
            door.MissionKey.enable();
            door.MissionKey.setValue(new byte[] { 6, (byte)instance, (byte)(instance >> 8), 2 });
            door.CurrentState.enable();
            door.CurrentState.setValue(NumericalUtils.uint32ToByteArray(StateClosed, 1));
            new ServerPackets().SendSpawnGameObject(client, door, EntityBase + StaticDoor);
        }

        // Retail, as goal 20 closed: 03 <door view> 01 84 00 <instance u16> <key> 02 35 08 00 00, its MissionKey with a zero part
        // and state 0x835, which turns the door's Pick Lock into Open.
        private void PrepareForDojo()
        {
            ClientView view = client.viewMan.FindViewForEntity(EntityBase + StaticDoor);
            if (view != null)
            {
                client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID, new byte[]
                {
                    0x01, 0x84, 0x00, (byte)instance, (byte)(instance >> 8), 0x02, 0x02, 0x35, 0x08, 0x00, 0x00
                }), false);
            }
        }

        private void SpawnBooth()
        {
            AttributeClass3632 booth = new AttributeClass3632("TutorialExitPoint_PhoneBooth", 2412);
            booth.SetRelatedStaticObjId(StaticBooth);
            booth.DisableAllAttributes();
            booth.Position.enable();
            booth.Position.setValue(NumericalUtils.doublesToLtVector3d(6950, 331, -8550));
            new ServerPackets().SendSpawnGameObject(client, booth, EntityBase + StaticBooth);
        }

        private void SetObjectState(UInt64 entity, byte[] updateMask, UInt32 state)
        {
            ClientView view = client.viewMan.FindViewForEntity(entity);
            if (view != null)
            {
                byte[] value = NumericalUtils.uint32ToByteArray(state, 1);
                byte[] data = new byte[updateMask.Length + value.Length];
                updateMask.CopyTo(data, 0);
                value.CopyTo(data, updateMask.Length);
                client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID, data), false);
            }
        }

        /// <summary>A spawned object used (80 c7). Returns true if it's one of the tutorial's.</summary>
        public bool OnViewUse(UInt64 entity, byte use)
        {
            lock (sync)
            {
                if (entity == EntityBase + StaticWardrobe)
                {
                    SetObjectState(entity, new byte[] { 0x01, 0x20 }, StateOpen);
                    OpenWardrobe();
                }
                else if (entity == EntityBase + StaticDoor)
                {
                    // Retail: 01 80 02 34 08 00 00, then closing after 20 s and closed 1 s later. It stays spawned.
                    SetObjectState(entity, new byte[] { 0x01, 0x80, 0x02 }, StateOpen);
                    ScheduleEvent(20, DoorClosingEvent);
                    ScheduleEvent(21, DoorClosedEvent);
                    client.FlushQueue();
                }
                else if (entity == EntityBase + StaticBooth)
                {
                    UseBooth(use);
                }
                else
                {
                    return false;
                }

                return true;
            }
        }

        private void RemoveNpc(ushort id)
        {
            foreach (Mob mob in npcs.FindAll(n => n.missionNpcId == id))
            {
                npcs.Remove(mob);
                lock (client.playerData.loot)
                {
                    client.playerData.loot.Forget(mob);
                }
                lock (WorldServer.mobs.SyncRoot)
                {
                    WorldServer.mobs.Remove(mob);
                }

                lock (WorldServer.gameServerEntities.SyncRoot)
                {
                    WorldServer.gameServerEntities.Remove(mob);
                }

                Store.world?.DeleteMobViews(mob);
            }
        }

        private void Say(string name, string text)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildNpcDialog(name, text));
        }

        private void GiveItem(UInt32 goId, bool keyed)
        {
            UInt16 slot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            Store.dbManager.WorldDbHandler.AddItemToInventory(slot, goId);
            client.messageQueue.addRpcMessage(keyed
                ? ServerPackets.BuildMissionItemAdd(slot, goId, 1, instance)
                : ServerPackets.BuildInventoryItemAdd(slot, goId, 0, 0x14));
            itemSlots[goId] = slot;
        }

        public void OnInventoryItemMoved(UInt16 fromSlot, UInt16 toSlot)
        {
            foreach (var item in itemSlots.ToList())
            {
                if (item.Value == fromSlot)
                {
                    itemSlots[item.Key] = toSlot;
                }
            }
        }

        private void TakeItem(UInt32 goId)
        {
            if (itemSlots.TryGetValue(goId, out UInt16 slot))
            {
                UInt32 charId = client.playerData.getCharID();
                if (Store.dbManager.WorldDbHandler.GetInventoryItems(charId).Any(item => item.Slot == slot && item.Goid == goId))
                {
                    Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, charId);
                    new ServerPackets().sendInventoryItemDelete(slot, client);
                }
                itemSlots.Remove(goId);
            }
        }

        private void Finish()
        {
            finished = true;
            foreach (Mob mob in new List<Mob>(npcs))
            {
                RemoveNpc((ushort)mob.missionNpcId);
            }

            lock (running)
            {
                running.Remove(this);
            }

            client.playerData.tutorial = null;
        }

        // ---- What the player does ----

        public bool OnTalk(Mob npc)
        {
            lock (sync)
            {
                if (npc == null || !npcs.Contains(npc))
                {
                    return false;
                }

                switch (npc.missionNpcId)
                {
                    case AmmitId:
                        Say("Ammit", "Listen to what the Captain has to say. And be honest.");
                        CompleteGoal(TALKING_GOAL);
                        break;
                    case ScarlettId:
                        if (done.Contains(GIVE_GOAL))
                        {
                            Say("Scarlett", "Thanks! You may pass.");
                        }
                        else if (done.Contains(TALK2_GOAL))
                        {
                            Say("Scarlett", "Where {i}is{/i} that key?");
                        }
                        else
                        {
                            Say("Scarlett", "Hello there. I've hidden a key, but apparently too well. Can you help me?");
                            CompleteGoal(TALK2_GOAL);
                            // Retail: the wardrobe can only be used from here on (01 20 35 08 00 00)
                            SetObjectState(EntityBase + StaticWardrobe, new byte[] { 0x01, 0x20 }, StateReady);
                        }

                        break;
                }

                client.FlushQueue();
                return true;
            }
        }

        public bool OnGive(Mob npc)
        {
            lock (sync)
            {
                if (npc == null || npc.missionNpcId != ScarlettId || !npcs.Contains(npc) || !itemSlots.ContainsKey(DoorKey))
                {
                    return false;
                }

                TakeItem(DoorKey);
                CompleteGoal(GIVE_GOAL);
                Say("Scarlett", "Thanks! You may pass.");
                client.FlushQueue();
                return true;
            }
        }

        /// <summary>A static object used (80 c8). Returns true if the tutorial handled it.</summary>
        public bool OnStaticUse(UInt32 staticId, byte use)
        {
            lock (sync)
            {
                switch (staticId)
                {
                    case StaticWardrobe:
                        OpenWardrobe();
                        return true;
                    case StaticBooth:
                        UseBooth(use);
                        return true;
                    default:
                        return false;
                }
            }
        }

        private void OpenWardrobe()
        {
            if (wardrobeOpen || done.Contains(LOOT_GOAL))
            {
                return;
            }

            wardrobeOpen = true;
            lock (client.playerData.loot)
            {
                client.playerData.loot.Close();
            }
            CompleteGoal(CONTAINER_GOAL);
            client.messageQueue.addRpcMessage(ServerPackets.BuildContainerLoot(WardrobeTitle, 5, instance, 2, new[]
            {
                (new byte[] { 1, (byte)instance, (byte)(instance >> 8), 0x82 }, DoorKey),
                (new byte[] { 0, 0, 0, 0x14 }, (UInt32)FEMALE_SHOES),
                (new byte[] { 0, 0, 0, 0x14 }, (UInt32)MALE_SHOES)
            }));
            client.FlushQueue();
        }

        private void UseBooth(byte use)
        {
            // The final goal's own way out is Jack In (05). Any use of the booth counts then, so nobody can be stuck here.
            if (use == UseExit || current == EXIT_GOAL)
            {
                CompleteGoal(EXIT_GOAL);
                LeaveTutorial();
            }
            else if (use == UseUpload)
            {
                TakeItem(Letter);
                client.messageQueue.addRpcMessage(HardlineBooths.BuildUploadDone());
                CompleteGoal(UPLOAD_GOAL);
            }

            client.FlushQueue();
        }

        // Retail: 81 01, the client asks to leave with 80 fc 01, 80 fd, 80 fe 01, then the Loading Area. (Spec 3.3)
        private void LeaveTutorial()
        {
            Output.WriteDebugLog("[TUTORIAL] Leaving");
            Store.dbManager.WorldDbHandler.SaveMissionCompleted(client.playerData.getCharID(), MissionId);
            client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x01 });
            new ServerPackets().SendMissionAbort(client);
            Finish();
            MoveOutOfTutorial();
        }

        private void MoveOutOfTutorial()
        {
            client.playerData.leavingTutorial = true;
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(ExitX, ExitY, ExitZ));
            client.playerData.setDistrictId((uint)MxOLocations.SLUMS);
            client.playerData.setDistrict("slums");
        }

        /// <summary>
        /// Retail didn't show the hardline picker after the tutorial: in the Loading Area its server sent 80 fa 01000000
        /// 99000000 01 (Richland, hardline 153, make active) and 81 07, and the client went on to the Slums. This client waits
        /// at "Negotiating with Servers" without it.
        /// </summary>
        public static bool JackIntoRichland(WorldClient client)
        {
            if (!client.playerData.leavingTutorial)
            {
                return false;
            }

            client.playerData.leavingTutorial = false;
            TeleportHandler.MoveToHardline(client, (UInt16)MxOLocations.SLUMS, RichlandHardline);
            client.messageQueue.addRpcMessage(new byte[] { 0x80, 0xfa, 0x01, 0x00, 0x00, 0x00, (byte)RichlandHardline, 0x00, 0x00, 0x00, 0x01 });
            client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x07 });
            client.FlushQueue();
            Output.WriteDebugLog("[TUTORIAL] Jacking into Richland at hardline " + RichlandHardline);
            return true;
        }

        /// <summary>The client's Skip Tutorial button (80 a6, then its own 80 fc 01). Retail still gave a level 1 player the 501 XP.</summary>
        public void OnSkip()
        {
            lock (sync)
            {
                Output.WriteDebugLog("[TUTORIAL] Skipped");
                Store.dbManager.WorldDbHandler.SaveMissionCompleted(client.playerData.getCharID(), MissionId);
                if (client.playerData.getExperience() < 501)
                {
                    GrantExperience(501);
                }

                Finish();
                MoveOutOfTutorial();
                new ServerPackets().SendMissionAbort(client);
            }
        }

        /// <summary>Closing the native container invalidates its selection without granting or discarding loot.</summary>
        public void CloseContainer()
        {
            lock (sync) wardrobeOpen = false;
        }

        /// <summary>Take All (81 1f). Returns true if this was the wardrobe's loot.</summary>
        public bool OnTakeAll()
        {
            lock (sync)
            {
                if (wardrobeOpen && !done.Contains(LOOT_GOAL))
                {
                    GiveItem(1258, false);
                    GiveItem(905, false);
                    GiveItem(DoorKey, true);
                    wardrobeOpen = false;
                    CompleteGoal(LOOT_GOAL);
                    new ServerPackets().SendLootAccepted(client);
                    client.FlushQueue();
                    return true;
                }

                return false;
            }
        }

        /// <summary>Bob's combat-loot goal closes after every item fits in the bag.</summary>
        public void OnCorpseLooted(Mob mob)
        {
            lock (sync)
            {
                if (mob.missionNpcId == BobId && npcs.Contains(mob) &&
                    done.Contains(ABILITIES_USE_GOAL) && !done.Contains(COMBAT_LOOT_GOAL))
                {
                    CompleteGoal(COMBAT_LOOT_GOAL);
                    client.FlushQueue();
                }
            }
        }

        /// <summary>What Bob drops: all runs had the gun and the letter.</summary>
        public List<UInt32> DropsFor(Mob mob)
        {
            lock (sync)
            {
                return mob != null && mob.missionNpcId == BobId ? new List<UInt32> { Revolver, Letter } : null;
            }
        }

        public void OnCombatStarted(Mob mob)
        {
            lock (sync)
            {
                if (mob != null && mob.missionNpcId == BobId && npcs.Contains(mob))
                {
                    CompleteGoal(COMBAT_GOAL);
                    client.FlushQueue();
                }
            }
        }

        public void OnMobDied(Mob mob)
        {
            lock (sync)
            {
                if (mob == null || !npcs.Contains(mob))
                {
                    return;
                }

                if (mob.missionNpcId == BobId)
                {
                    if (current > ABILITIES_USE_GOAL)
                    {
                        // The respawn loop: goal 25 stays open while goal 23 closes again (spec 4.4)
                        new ServerPackets().SendSetMissionObjective((ushort)ABILITIES_USE_GOAL, MissionRuntime.ObjectiveDone, GoalTexts[ABILITIES_USE_GOAL], client);
                        done.Add(ABILITIES_USE_GOAL);
                        OnGoalComplete(ABILITIES_USE_GOAL);
                    }
                    else
                    {
                        CompleteGoal(ABILITIES_USE_GOAL);
                    }
                }
                else if (mob.missionNpcId == AlexId)
                {
                    CompleteGoal(FIREARM_COMBAT_GOAL);
                }

                client.FlushQueue();
            }
        }

        public void OnViewToggled(UInt16 view)
        {
            lock (sync)
            {
                OnToggledViewActivated(view);
                client.FlushQueue();
            }
        }

        public void OnModalAnswer(UInt32 message, bool left)
        {
            lock (sync)
            {
                OnModalDialogueAccepted(message, left);
                client.FlushQueue();
            }
        }

        /// <summary>81 91: a token from 81 42 coming back when the FX ends, which runs the teleport.</summary>
        public void OnClientEvent(UInt32 eventId)
        {
            lock (sync)
            {
                OnMissionEvent(eventId);
                client.FlushQueue();
            }
        }

        public void OnItemEquipped(UInt32 goId)
        {
            lock (sync)
            {
                if (done.Contains(INVENTORY_GOAL) && (goId == MALE_SHOES || goId == FEMALE_SHOES))
                {
                    CompleteGoal(EQUIP_GOAL);
                    client.FlushQueue();
                }
            }
        }

        public void OnShortcutAdded(UInt32 type, UInt32 classData)
        {
            lock (sync)
            {
                // 198 is Cheap Shot; five type 7 shortcuts arrive by themselves after event 2
                if (type == 0 && classData == 198 && done.Contains(HOTBAR_GOAL))
                {
                    CompleteGoal(CHEAPSHOT_GOAL);
                    client.FlushQueue();
                }
            }
        }

        // Retail's level-up goals close on panel clicks (81 04 0c, 1d to 22) that this client doesn't send for them, though the
        // real upgrades do arrive. So an upgrade closes all the steps leading to it, in order.
        public void OnAbilityUpgraded()
        {
            lock (sync)
            {
                for (long goal = OPENLEVEL_GOAL; goal <= APPLY_LEVELUP_GOAL; goal++)
                {
                    CompleteGoal(goal);
                }

                client.FlushQueue();
            }
        }

        public void OnAttributeRaised()
        {
            lock (sync)
            {
                for (long goal = OPEN_ATTRIBUTE_GOAL; goal <= CONFIRM_ATTRIBUTE_GOAL; goal++)
                {
                    CompleteGoal(goal);
                }

                client.FlushQueue();
            }
        }

        /// <summary>The player disconnected.</summary>
        public void Abandon()
        {
            lock (sync)
            {
                Finish();
            }
        }
    }
}
