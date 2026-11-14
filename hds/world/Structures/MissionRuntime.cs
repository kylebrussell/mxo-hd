using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Runs an accepted mission from its data: spawns its NPCs, sends each phase's briefing, waypoints and objectives, plays
    /// its lines, and moves on when the player leaves through a mission door.
    ///
    /// Retail behaviour this follows (Welcome to the Matrix, Exile Extermination and The Internuncio, luizoe Recursion log):
    /// objectives go out with 80 a0 (flag 01 = done); talking arrives as 80 c7 &lt;view&gt; &lt;spawn&gt; 09, Get as 0d, Give as 0e. When a
    /// phase's goals are all done the operator sends client string 0x2600000c ("Goals for this area are complete. Find the
    /// nearest exit quickly!"), and the next phase or the mission's end comes once the player has left. When the player
    /// needn't leave first (an upload at a hardline, NPCs that follow into the next phase), retail's operator sent
    /// 0x26000004 ("Goals for this mission phase are complete!") instead, and the next briefing came in the same second.
    /// </summary>
    public static class MissionRuntime
    {
        /// <summary>Retail's 80 e4 after a completed mission carried reason 2 (80 e4 ... 02 00 00 00).</summary>
        public const UInt32 MissionRewardInfoReason = 0x02;

        public const byte ObjectiveOpen = 0;
        public const byte ObjectiveDone = 1;

        public const UInt32 StringGoalsCompleteLeave = 0x2600000c; // ID_MISSION_GOALSCOMPLETELEAVE
        public const UInt32 StringGoalsComplete = 0x26000004;      // ID_MISSION_GOALSCOMPLETE

        private const UInt32 OperatorImage = 0x28000a70;
        private const UInt32 OperatorName = 0x26000014;
        private const UInt32 ContactImage = 0x28000cc0;
        private const UInt32 ContactName = 0xb4000031;
        private const UInt32 FriendlyNpcImage = 0x2800121f;
        private const UInt32 HostileNpcImage = 0x2800121e;

        private static readonly object timerLock = new object();
        private static Timer timer;

        public static bool Start(WorldClient client, Mission mission, ushort transaction = 0) => MissionPersistence.Mutate(client,()=>{StartCore(client,mission,transaction);return true;},true);

        internal static bool StartOpenFloor(WorldClient client,MissionProgress progress,MissionOpenFloorAdmission.Plan plan,ushort transaction) =>
            MissionPersistence.Mutate(client,()=>
            {
                if(!plan.BeforeCurrent(client)||!plan.BindOutput(client))return false;
                StartCore(client,progress.mission,transaction,progress,plan);return true;
            },true);

        private static void StartCore(WorldClient client, Mission mission, ushort transaction,MissionProgress preparedProgress=null,MissionOpenFloorAdmission.Plan prepared=null)
        {
            lock (client.playerData.missionSync)
            {
                EndCore(client,false);

                ClientData data = client.playerData;
                MissionProgress progress = preparedProgress ?? new MissionProgress(mission);
                data.activeMission = mission;
                data.missionProgress = progress;
                new ServerPackets().sendMissionAccept(client,transaction,data.missionInstanceCounter,mission);

                foreach (MissionNpc npc in mission.npcs)
                {
                    // Site NPCs spawn with their phase; late arrivals wait for their trigger goal.
                    if (npc.at == 0 && !npc.arrival.Late)
                    {
                        Mob mob = SpawnNpc(client, mission, npc);
                        if (mob == null)
                        {
                            progress.pendingNpcPlacements.Add(npc.id);
                            progress.justArrived.Add(npc);
                        }
                        AddMob(client, mob);
                    }
                }

                if(prepared==null)MissionStaging.Stage(client, progress);
                else {if(prepared.Adopted)progress.openFloorCheckpoint=true;MissionStaging.StagePrepared(client,progress,prepared);}
                SendPhase(client, progress);
                EnsureTimer();
                MissionPersistence.Save(client);
            }
        }

        public static void AddMob(WorldClient client, Mob mob)
        {
            if (mob == null) return;
            lock (client.playerData.missionMobs)
            {
                client.playerData.missionMobs.Add(mob);
            }
        }

        /// <summary>The mission's spawned NPC with this id (a living one if there are several).</summary>
        public static Mob FindNpc(ClientData data, ushort npcId)
        {
            lock (data.missionMobs)
            {
                return data.missionMobs.Find(m => m.missionNpcId == npcId && !m.getIsDead()) ??
                       data.missionMobs.Find(m => m.missionNpcId == npcId);
            }
        }

        /// <summary>Removes the mission's NPCs and deletes their views from the clients that have them.</summary>
        public static bool End(WorldClient client) => MissionPersistence.Mutate(client,()=>{EndCore(client);return true;},abortBlocked:true);

        private static void EndCore(WorldClient client, bool clearPersistent = true)
        {
            lock (client.playerData.missionSync)
            {
                ClientData data = client.playerData;
                List<Mob> mobs;
                lock (data.missionMobs)
                {
                    mobs = new List<Mob>(data.missionMobs);
                    data.missionMobs.Clear();
                }

                foreach (Mob mob in mobs)
                {
                    Despawn(data, mob);
                }

                MissionProgress progress = data.missionProgress;
                if (progress != null)
                {
                    MissionStaging.DespawnObjects(client, progress);
                    foreach(var slot in progress.itemSlots.Values)new ServerPackets().sendInventoryItemDelete(slot,client);
                }
                data.activeMission = null;
                data.missionProgress = null;
                if(clearPersistent)MissionPersistence.Save(client,true);
            }

        }

        internal static void Detach(WorldClient client)
        {
            lock(client.playerData.missionSync)
            {
                var data=client.playerData;
                lock(data.missionMobs)
                {
                    foreach(var mob in data.missionMobs.ToArray()) Despawn(data,mob);
                    data.missionMobs.Clear();
                }
                if(data.missionProgress!=null)MissionStaging.DespawnObjects(client,data.missionProgress);
                data.activeMission=null;data.missionProgress=null;
            }
        }
        internal static void Reannounce(WorldClient client)
        {
            var progress=client.playerData.missionProgress;
            if(progress==null)return;
            if(progress.restoreRefusal!=null)
            {
                new ServerPackets().sendMissionAccept(client,0,client.playerData.missionInstanceCounter,progress.mission);
                MissionObjectiveReplay.Arm(client,progress);
                foreach(var item in progress.itemSlots)
                    client.messageQueue.addRpcMessage(ServerPackets.BuildMissionItemAdd(item.Value,item.Key,ItemElement(progress,item.Key),client.playerData.missionInstanceCounter));
                new ServerPackets().sendSystemChatMessage(client,"Your saved mission area is no longer safe to restore. Abort this mission from Mission Status before accepting another.","BROADCAST");
                return; // Never publish unsafe objects, actors, waypoints or automatic goal/item grants.
            }
            EnsureTimer();
            if(client.playerData.getDistrictId()!=progress.mission.district)return;
            new ServerPackets().sendMissionAccept(client,0,client.playerData.missionInstanceCounter,progress.mission);
            MissionStaging.RestoreObjects(client,progress);
            SendPhase(client,progress,false,true);
            foreach(var item in progress.itemSlots)
                client.messageQueue.addRpcMessage(ServerPackets.BuildMissionItemAdd(item.Value,item.Key,ItemElement(progress,item.Key),client.playerData.missionInstanceCounter));
        }

        /// <summary>Takes a mission NPC out of the world and off the clients that have it.</summary>
        private static void Despawn(ClientData data, Mob mob)
        {
            lock (data.loot)
            {
                data.loot.Forget(mob);
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

        public static Mob FindMissionMob(ClientData data, ulong entityId)
        {
            lock (data.missionMobs)
            {
                return data.missionMobs.Find(m => m.getEntityId() == entityId);
            }
        }

        /// <summary>The player double-clicked a mission NPC (80 c7 ... 09). Returns false if it isn't one.</summary>
        public static bool OnTalk(WorldClient client, Mob npc)
        {
            return MissionPersistence.Mutate(client,()=>OnTalkCore(client, npc));
        }

        private static bool OnTalkCore(WorldClient client, Mob npc)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (npc == null || npc.missionNpcId == 0 || progress == null)
            {
                return false;
            }

            MissionObjective objective = progress.FindOpenFor(npc.missionNpcId);
            if (objective != null && objective.talkLines.Count > 0)
            {
                Say(client, objective.talkLines, interactive:true);
            }

            if (objective != null && objective.type == MissionGoalType.Talk)
            {
                Complete(client, progress, objective);
            }

            if (progress.FindOpen(MissionGoalType.Escort, npc.missionNpcId) != null)
            {
                progress.following.Add(npc.missionNpcId);
            }

            if (objective == null)
            {
                MissionLine line = LatestLine(progress, npc.missionNpcId);
                if (line != null)
                {
                    Say(client, new List<MissionLine> { line }, interactive:true);
                }
            }

            client.FlushQueue();
            return true;
        }

        /// <summary>Right-click Get on an NPC (80 c7 ... 0d): it hands over the objective's item.</summary>
        public static bool OnGet(WorldClient client, Mob npc)
        {
            return MissionPersistence.Mutate(client,()=>OnGetCore(client, npc));
        }

        private static bool OnGetCore(WorldClient client, Mob npc)
        {
            MissionProgress progress = client.playerData.missionProgress;
            MissionObjective objective = npc == null ? null : progress?.FindOpen(MissionGoalType.Get, npc.missionNpcId);
            if (objective == null)
            {
                return false;
            }

            if (!GiveItem(client, progress, objective.item))
            {
                return true;
            }
            Complete(client, progress, objective);
            client.FlushQueue();
            return true;
        }

        /// <summary>Right-click Give on an NPC (80 c7 ... 0e): the player hands over the objective's item.</summary>
        public static bool OnGive(WorldClient client, Mob npc)
        {
            return MissionPersistence.Mutate(client,()=>OnGiveCore(client, npc));
        }

        private static bool OnGiveCore(WorldClient client, Mob npc)
        {
            MissionProgress progress = client.playerData.missionProgress;
            MissionObjective objective = npc == null ? null : progress?.FindOpen(MissionGoalType.Give, npc.missionNpcId);
            if (objective == null)
            {
                return false;
            }

            if (objective.item != 0 && !TakeItem(client, progress, objective.item))
            {
                return true;
            }
            Complete(client, progress, objective);
            client.FlushQueue();
            return true;
        }

        /// <summary>A mob the player fought died. Completes a Defeat objective for it.</summary>
        public static void OnMobDefeated(WorldClient client, Mob mob)
        {
            MissionPersistence.Mutate(client,()=>OnMobDefeatedCore(client, mob));
        }

        private static void OnMobDefeatedCore(WorldClient client, Mob mob)
        {
            TutorialMission.Of(client)?.OnMobDied(mob);
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null || mob == null)
            {
                return;
            }

            if (mob.isFriendly)
            {
                // An NPC the player was to protect or escort
                if (progress.following.Contains(mob.missionNpcId) || progress.FindOpen(MissionGoalType.Protect, mob.missionNpcId) != null ||
                    progress.FindOpen(MissionGoalType.Escort, mob.missionNpcId) != null)
                {
                    Fail(client, progress, mob.getName() + " is dead.");
                }

                return;
            }

            progress.kills++;
            if (mob.missionNpcId != 0)
            {
                // Remembered for a kill goal that opens later
                progress.killed.Add(mob.missionNpcId);
            }

            // "Find Burdette": a hostile is found by reaching it (TickEscorts), or by killing it before that
            foreach (MissionObjective found in new List<MissionObjective>(progress.OpenObjectives()))
            {
                if (found.type == MissionGoalType.Talk && found.Has(mob.missionNpcId))
                {
                    Complete(client, progress, found);
                }
            }

            MissionObjective objective = mob.missionNpcId == 0
                ? null
                : progress.FindOpen(MissionGoalType.Defeat, mob.missionNpcId);
            if (objective != null)
            {
                Complete(client, progress, objective);
            }

            CompleteDefeatAlls(client, progress);
            client.FlushQueue();
        }

        /// <summary>Open DefeatAll goals whose hostiles are all dead. Returns whether any completed.</summary>
        private static bool CompleteDefeatAlls(WorldClient client, MissionProgress progress)
        {
            bool any = false;
            foreach (MissionObjective all in new List<MissionObjective>(progress.OpenObjectives()))
            {
                if (all.type == MissionGoalType.DefeatAll && progress.IsOpen(all) && AllHostilesDown(client, progress, all))
                {
                    Complete(client, progress, all);
                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// Every hostile of the current phase that the DefeatAll counts is dead: its generated ones, and the mission's own
        /// placed there; with wave="n", only those of the waves it names (an NPC's wave is 0 unless it says).
        /// </summary>
        private static bool AllHostilesDown(WorldClient client, MissionProgress progress, MissionObjective defeatAll)
        {
            if (progress.HostilesStillToArrive(defeatAll))
            {
                return false;
            }

            int at = progress.phaseIndex + 1;
            lock (client.playerData.missionMobs)
            {
                foreach (Mob mob in client.playerData.missionMobs)
                {
                    bool ofPhase;
                    if (progress.hostileWave.TryGetValue(mob, out int wave))
                    {
                        ofPhase = true;
                    }
                    else
                    {
                        MissionNpc npc = progress.hostiles.Contains(mob) ? null : progress.mission.npcs.Find(n => n.id == mob.missionNpcId);
                        ofPhase = progress.hostiles.Contains(mob) ||
                                  (npc != null && (npc.at == at || (npc.at == 0 && !progress.mission.generated)));
                        wave = npc?.wave ?? 0;
                    }

                    if (ofPhase && MissionProgress.Counts(defeatAll, wave) && !mob.isFriendly && !mob.getIsDead())
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>A protected NPC died: the mission is over.</summary>
        public static void Fail(WorldClient client, MissionProgress progress, string reason)
        {
            MissionPersistence.Mutate(client,()=>FailCore(client, progress, reason));
        }

        private static void FailCore(WorldClient client, MissionProgress progress, string reason)
        {
            if (progress.failed)
            {
                return;
            }

            progress.failed = true;
            client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(OperatorImage, OperatorName, 0, 1, 0, "", reason));
            new ServerPackets().SendMissionFailed(client);
            Output.WriteDebugLog("[MISSION] Failed '" + progress.mission.title + "': " + reason);
            End(client);
            client.FlushQueue();
        }

        /// <summary>A mission object was used (80 c7 on its view). Returns true if it's one of the mission's.</summary>
        public static bool OnViewUse(WorldClient client, UInt64 entity)
        {
            return MissionPersistence.Mutate(client,()=>OnViewUseCore(client, entity));
        }

        private static bool OnViewUseCore(WorldClient client, UInt64 entity)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return false;
            }

            MissionObjectState state = null;
            foreach (MissionObjectState candidate in progress.objects.Values)
            {
                if (candidate.entity == entity)
                {
                    state = candidate;
                }
            }

            if (state == null)
            {
                return false;
            }

            foreach (MissionObjective objective in new List<MissionObjective>(progress.OpenObjectives()))
            {
                if (objective.objectId != state.def.id)
                {
                    continue;
                }

                if (objective.type == MissionGoalType.Search && MissionStaging.IsComputer(state.goid))
                {
                    // A computer's Take or Hack: no loot window, the item goes straight to the inventory
                    if (objective.item != 0 && !GiveItem(client, progress, objective.item))
                    {
                        break;
                    }

                    Complete(client, progress, objective);
                    break;
                }

                if (objective.type == MissionGoalType.Search && objective.item == 0)
                {
                    // Searching that turns nothing up ("Search the computer", then "Get the data disk from the computer")
                    Complete(client, progress, objective);
                    break;
                }

                if (objective.type == MissionGoalType.Search)
                {
                    lock (client.playerData.loot)
                    {
                        client.playerData.loot.Close();
                    }
                    foreach (MissionObjectState container in progress.objects.Values)
                    {
                        container.lootOpen = false;
                        container.putOpen = false;
                    }
                    state.lootOpen = true;
                    ushort instance = client.playerData.missionInstanceCounter;
                    client.messageQueue.addRpcMessage(ServerPackets.BuildContainerLoot(MissionStaging.TitleFor(state.def.kind),
                        MissionStaging.ContainerElement, instance, 2, new[]
                        {
                            (new byte[] { 0, (byte)instance, (byte)(instance >> 8), 0x82 }, objective.item)
                        }));
                    Output.WriteDebugLog("[MISSION] Searching object " + state.def.id + " for item " + objective.item);
                    break;
                }

                if (objective.type == MissionGoalType.Put)
                {
                    lock (client.playerData.loot) client.playerData.loot.Close();
                    foreach (MissionObjectState container in progress.objects.Values)
                        container.lootOpen = container.putOpen = false;
                    state.putOpen = true;
                    client.messageQueue.addRpcMessage(ServerPackets.BuildContainerLoot(MissionStaging.TitleFor(state.def.kind),
                        MissionStaging.ContainerElement, client.playerData.missionInstanceCounter, 2,
                        Array.Empty<(byte[], UInt32)>()));
                    break;
                }

                if (objective.type == MissionGoalType.Use)
                {
                    Complete(client, progress, objective);
                    break;
                }
            }

            client.FlushQueue();
            return true;
        }

        /// <summary>81 21: only the mission item expected by the currently open, nearby Put container is accepted.</summary>
        public static bool OnPutItem(WorldClient client, byte slot)
        {
            return MissionPersistence.Mutate(client,()=>OnPutItemCore(client, slot));
        }

        private static bool OnPutItemCore(WorldClient client, byte slot)
        {
            lock (client.playerData.missionSync)
            {
                MissionProgress progress = client.playerData.missionProgress;
                if (progress == null || progress.failed || client.playerData.loot.WindowOpen) return false;
                MissionObjectState state = progress.objects.Values.FirstOrDefault(o => o.putOpen);
                if (state?.spot == null) return false;
                var pose = MissionObjectPoses.For(state);
                if (!ContainerActions.InReach(client.playerInstance.Position.getValue(), pose.X, pose.Y, pose.Z)) return false;
                MissionObjective objective = progress.OpenObjectives().FirstOrDefault(o => o.type == MissionGoalType.Put &&
                    o.objectId == state.def.id && progress.itemSlots.TryGetValue(o.item, out ushort ownedSlot) && ownedSlot == slot);
                if (objective == null || !TakeItem(client, progress, objective.item)) return false;
                state.putOpen = false;
                ushort instance = client.playerData.missionInstanceCounter;
                client.messageQueue.addRpcMessage(ContainerActions.BuildReply(objective.item,
                    ((uint)instance << 8) | 0x82000000u, 0));
                ObjectStates.ShowOpened(client, state.entity, state.goid);
                Complete(client, progress, objective);
                return true;
            }
        }

        /// <summary>Take All (81 1f) on a mission object's loot window. Returns true if it was one.</summary>
        public static bool OnTakeAll(WorldClient client)
        {
            return MissionPersistence.Mutate(client,()=>OnTakeAllCore(client));
        }

        private static bool OnTakeAllCore(WorldClient client)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return false;
            }

            foreach (MissionObjectState state in progress.objects.Values)
            {
                if (!state.lootOpen)
                {
                    continue;
                }

                foreach (MissionObjective objective in new List<MissionObjective>(progress.OpenObjectives()))
                {
                    if (objective.type == MissionGoalType.Search && objective.objectId == state.def.id)
                    {
                        if (objective.item != 0 && !GiveItem(client, progress, objective.item))
                        {
                            ushort instance = client.playerData.missionInstanceCounter;
                            new ServerPackets().SendLootAccepted(client, new[]
                            {
                                (new byte[] { 0, (byte)instance, (byte)(instance >> 8), 0x82 }, objective.item)
                            });
                            return true;
                        }

                        Complete(client, progress, objective);
                    }
                }

                state.lootOpen = false;
                // Emptied: the container goes to the state retail's Open leads to (OpenedState, 0x834), which plays its
                // opening and takes Open off its menu
                ObjectStates.ShowOpened(client, state.entity, state.goid);
                new ServerPackets().SendLootAccepted(client);
                client.FlushQueue();
                return true;
            }

            return false;
        }

        /// <summary>The player used a hardline booth: Upload goals for what they carry, and NPCs escorted to a hardline.</summary>
        public static void OnHardline(WorldClient client, bool upload)
        {
            MissionPersistence.Mutate(client,()=>OnHardlineCore(client, upload));
        }

        private static void OnHardlineCore(WorldClient client, bool upload)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return;
            }

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            foreach (MissionObjective objective in new List<MissionObjective>(progress.OpenObjectives()))
            {
                if (objective.type == MissionGoalType.Upload && upload &&
                    (objective.item == 0 || progress.itemSlots.ContainsKey(objective.item)))
                {
                    if (objective.item != 0)
                    {
                        if (!TakeItem(client, progress, objective.item))
                        {
                            continue;
                        }
                    }

                    Complete(client, progress, objective);
                }
                else if (objective.type == MissionGoalType.Escort && objective.escortTo == "hardline" &&
                         objective.Npcs.TrueForAll(id => Near(client, id, x, y, z, 1500)))
                {
                    Complete(client, progress, objective);
                    objective.Npcs.ForEach(id => progress.following.Remove(id));
                }
            }

            client.FlushQueue();
        }

        /// <summary>Whether the player at (x, y, z) is within radius of a place: "object:1", "npc:2" (living) or "x,y,z".</summary>
        private static bool Near(WorldClient client, MissionProgress progress, string place, double radius, double x, double y, double z)
        {
            if (place.StartsWith("object:"))
            {
                if (!ushort.TryParse(place.Substring(7), out ushort objectId) || !progress.objects.TryGetValue(objectId, out MissionObjectState state) || state.spot == null) return false;
                var pose = MissionObjectPoses.For(state);
                return Distance2(pose.X, pose.Y, pose.Z, x, y, z) <= radius * radius;
            }

            if (place.StartsWith("npc:"))
            {
                return ushort.TryParse(place.Substring(4), out ushort npcId) && Near(client, npcId, x, y, z, radius);
            }

            string[] parts = place.Split(',');
            return parts.Length == 3 &&
                   double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double px) &&
                   double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double py) &&
                   double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pz) &&
                   Distance2(px, py, pz, x, y, z) <= radius * radius;
        }

        private static bool Near(WorldClient client, ushort npcId, double x, double y, double z, double radius)
        {
            Mob mob = FindNpc(client.playerData, npcId);
            if (mob == null || mob.getIsDead())
            {
                return false;
            }

            double dx = mob.getXPos() - x, dy = mob.getYPos() - y, dz = mob.getZPos() - z;
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }

        /// <summary>The item a Loot objective puts in the dead mob's loot window, or 0.</summary>
        public static UInt32 LootItemFor(ClientData data, Mob mob)
        {
            MissionProgress progress = data.missionProgress;
            if (progress == null || mob == null || mob.missionNpcId == 0)
            {
                return 0;
            }

            MissionObjective objective = progress.FindOpen(MissionGoalType.Loot, mob.missionNpcId);
            return objective?.item ?? 0;
        }

        /// <summary>Returns false when the mission item needs more bag space, so the loot can be retried.</summary>
        public static bool OnMissionItemLooted(WorldClient client, UInt32 itemGoId)
        {
            return MissionPersistence.Mutate(client,()=>OnMissionItemLootedCore(client, itemGoId));
        }

        private static bool OnMissionItemLootedCore(WorldClient client, UInt32 itemGoId)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return true;
            }

            foreach (MissionObjective objective in progress.OpenObjectives())
            {
                if (objective.type == MissionGoalType.Loot && objective.item == itemGoId)
                {
                    if (!GiveItem(client, progress, itemGoId))
                    {
                        return false;
                    }
                    Complete(client, progress, objective);
                    client.FlushQueue();
                    return true;
                }
            }
            return true;
        }

        private static MissionLine LatestLine(MissionProgress progress, ushort npcId)
        {
            MissionNpc npc = progress.mission.npcs.Find(n => n.id == npcId);
            MissionLine chosen = null;
            if (npc != null)
            {
                foreach (MissionLine line in npc.lines)
                {
                    if (line.afterGoal < 0 || progress.WasDoneInAnyPhase(line.afterGoal))
                    {
                        chosen = line;
                    }
                }
            }

            return chosen;
        }

        private static bool GiveItem(WorldClient client, MissionProgress progress, UInt32 goId)
        {
            if(progress.itemSlots.ContainsKey(goId))return true;
            UInt16 slot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            if (slot == 0)
            {
                if (!progress.inventoryFullNotice)
                {
                    new ServerPackets().sendSystemChatMessage(client, "Make room in your inventory for the mission item.", "BROADCAST");
                    progress.inventoryFullNotice = true;
                    client.FlushQueue();
                }
                return false;
            }

            progress.itemSlots[goId] = slot;
            progress.inventoryFullNotice = false;
            try { MissionPersistence.Save(client); }
            catch { progress.itemSlots.Remove(goId); throw; }
            client.messageQueue.addRpcMessage(ServerPackets.BuildMissionItemAdd(slot, goId, ItemElement(progress, goId),
                client.playerData.missionInstanceCounter));
            return true;
        }

        /// <summary>The first byte of the key of the NPC the item is to be given to.</summary>
        private static byte ItemElement(MissionProgress progress, UInt32 goId)
        {
            foreach (MissionPhase phase in progress.mission.phases)
            {
                foreach (MissionObjective objective in phase.missionObjectives)
                {
                    if (objective.type == MissionGoalType.Give && objective.item == goId)
                    {
                        return progress.mission.npcs.Find(n => n.id == objective.npcId)?.phase ?? 0;
                    }
                }
            }

            return 0;
        }

        public static void OnInventoryItemMoved(WorldClient client, UInt16 fromSlot, UInt16 toSlot)
        {
            MissionPersistence.Mutate(client,()=>OnInventoryItemMovedCore(client, fromSlot, toSlot));
        }

        private static void OnInventoryItemMovedCore(WorldClient client, UInt16 fromSlot, UInt16 toSlot)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return;
            }
            foreach (var item in progress.itemSlots.ToList())
            {
                if (item.Value == fromSlot)
                {
                    progress.itemSlots[item.Key] = toSlot;
                }
            }
            MissionPersistence.Save(client);
        }

        private static bool TakeItem(WorldClient client, MissionProgress progress, UInt32 goId)
        {
            if (progress.itemSlots.TryGetValue(goId, out UInt16 slot))
            {
                UInt32 charId = client.playerData.getCharID();
                if (Store.dbManager.WorldDbHandler.GetInventoryItems(charId).Any(item => item.Slot == slot && item.Goid == goId))
                {

                    new ServerPackets().sendInventoryItemDelete(slot, client);
                    progress.itemSlots.Remove(goId);
                    return true;
                }
            }
            return false;
        }

        private static void Complete(WorldClient client, MissionProgress progress, MissionObjective objective)
        {
            lock (client.playerData.missionSync)
            {
                if (client.playerData.missionProgress == progress)
                {
                    CompleteGoal(client, progress, objective);
                    MissionPersistence.Save(client);
                }
            }
        }

        private static void CompleteGoal(WorldClient client, MissionProgress progress, MissionObjective objective)
        {
            if (progress.IsDone(objective.goal))
            {
                return;
            }

            ServerPackets packets = new ServerPackets();
            packets.SendSetMissionObjective((ushort)objective.goal, ObjectiveDone, Fill(client, objective.description), client);
            Output.WriteDebugLog("[MISSION] Goal " + objective.goal + " done: " + objective.description);

            List<MissionObjective> opened = progress.Complete(objective);
            foreach (MissionObjective next in opened)
            {
                packets.SendSetMissionObjective((ushort)next.goal, ObjectiveOpen, Fill(client, next.description), client);
            }

            SpawnArrivals(client, progress);
            SwitchSides(client, progress);
            MissionStaging.RefreshObjects(client, progress);

            Say(client, objective.doneLines);

            // A kill goal that opens after its kill was made is done at once
            foreach (MissionObjective next in opened)
            {
                if (!progress.IsDone(next.goal) && AlreadyKilled(client, progress, next))
                {
                    Complete(client, progress, next);
                }
            }

            if (progress.AllDone && !progress.goalsCompleteSent)
            {
                progress.goalsCompleteSent = true;

                // A Protect goal is kept by finishing the rest
                foreach (MissionObjective kept in new List<MissionObjective>(progress.OpenObjectives()))
                {
                    if (kept.type == MissionGoalType.Protect)
                    {
                        progress.Complete(kept);
                        packets.SendSetMissionObjective((ushort)kept.goal, ObjectiveDone, Fill(client, kept.description), client);
                    }
                }

                Say(client, progress.Phase.completeLines);

                if (NeedsToLeave(client, progress, objective))
                {
                    // Retail: the operator's "Goals for this area are complete. Find the nearest exit quickly!" (client string).
                    client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(OperatorImage, OperatorName, StringGoalsCompleteLeave, 1, 1, "", ""));
                    Output.WriteDebugLog("[MISSION] Phase " + progress.phaseIndex + " goals done; waiting for the player to leave");
                }
                else
                {
                    // Retail: "Goals for this mission phase are complete!", then the next briefing (or the closing) at once
                    progress.leaveNotNeeded = true;
                    client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(OperatorImage, OperatorName, StringGoalsComplete, 1, 1, "", ""));
                    Output.WriteDebugLog("[MISSION] Phase " + progress.phaseIndex + " goals done; moving on without leaving");
                }
            }
        }

        /// <summary>
        /// Whether a finished phase waits for the player to leave its area. The phase's leave attribute says so; otherwise they
        /// must, unless the last goal was done on the way out (an upload at a hardline, an escort to the exit or a hardline)
        /// or they are far from the area's ways out already.
        /// </summary>
        public static bool NeedsToLeave(WorldClient client, MissionProgress progress, MissionObjective last)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return NeedsToLeave(progress, last, x, y, z);
        }

        public static bool NeedsToLeave(MissionProgress progress, MissionObjective last, double x, double y, double z)
        {
            MissionPhase phase = progress.Phase;
            if (phase?.leave != null)
            {
                return phase.leave.Value;
            }

            if (last != null && (last.type == MissionGoalType.Upload ||
                                 (last.type == MissionGoalType.Escort && (last.escortTo == "exit" || last.escortTo == "hardline"))))
            {
                return false;
            }

            return !MissionExit.IsOutside(progress.waypoints, progress.exits, x, y, z);
        }

        /// <summary>A Defeat goal whose NPC is already dead, or a DefeatAll with nobody left: the kill came before the goal opened.</summary>
        private static bool AlreadyKilled(WorldClient client, MissionProgress progress, MissionObjective objective)
        {
            switch (objective.type)
            {
                case MissionGoalType.Defeat:
                    return objective.Npcs.Exists(id => progress.killed.Contains(id));
                case MissionGoalType.DefeatAll:
                    return AllHostilesDown(client, progress, objective);
                default:
                    return false;
            }
        }

        /// <summary>
        /// NPCs and waves whose time has come (spawnAfter, spawnDelay, spawnNear; enemiesAfter; &lt;wave&gt;): at the spot kept
        /// for them in a generated phase's site, at their own x, y, z, beside the player (spawnAt="player", or when they
        /// have no place) or beside one of the phase's objects (spawnAt="object:1"). A wave's lines are said as it comes.
        /// Returns whether anything arrived.
        /// </summary>
        private static bool SpawnArrivals(WorldClient client, MissionProgress progress)
        {
            if (progress.justArrived.Count == 0 && progress.wavesJustArrived.Count == 0)
            {
                return false;
            }

            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            Mission mission = progress.mission;
            List<MissionNpc> arriving = new List<MissionNpc>(progress.justArrived);
            progress.justArrived.Clear();
            int index = 0;
            foreach (MissionNpc npc in arriving)
            {
                ushort level = npc.at > 0 || mission.generated ? (npc.level > 1 ? npc.level : MissionStaging.PlayerLevel(client)) : (ushort)0;
                bool placedByData = npc.at == 0 && (npc.x != 0 || npc.y != 0 || npc.z != 0);
                bool friendly = progress.IsFriendly(npc);
                Mob mob;
                if (npc.arrival.at == null && progress.reservedSpots.TryGetValue(npc.id, out double[] spot))
                {
                    mob = SpawnNpc(client, mission, npc, spot[0], spot[1], spot[2], level, friendly);
                }
                else if (npc.arrival.at == null && placedByData)
                {
                    mob = SpawnNpc(client, mission, npc, friendly: friendly);
                }
                else
                {
                    double[] beside = ArrivalSpot(client, progress, npc.arrival, index++, px, py, pz);
                    mob = SpawnNpc(client, mission, npc, beside[0], beside[1], beside[2], level, friendly);
                }

                if (mob == null)
                {
                    progress.pendingNpcPlacements.Add(npc.id);
                    progress.justArrived.Add(npc); // Retry rather than place it inside a wall or on another floor.
                    continue;
                }
                progress.pendingNpcPlacements.Remove(npc.id);
                AddMob(client, mob);
                Output.WriteDebugLog("[MISSION] " + npc.handle + " arrives");
            }

            List<MissionWave> waves = new List<MissionWave>(progress.wavesJustArrived);
            progress.wavesJustArrived.Clear();
            foreach (MissionWave wave in waves)
            {
                List<double[]> spots = wave.arrival.at == null ? new List<double[]>(progress.SpotsFor(wave.id)) : new List<double[]>();
                while (spots.Count < wave.count)
                {
                    spots.Add(ArrivalSpot(client, progress, wave.arrival, index++, px, py, pz));
                }

                if (!MissionStaging.SpawnWave(client, progress, wave, spots))
                {
                    progress.wavesJustArrived.Add(wave);
                    continue;
                }
                Say(client, wave.lines);
                Output.WriteDebugLog("[MISSION] " + spots.Count + " hostiles arrive" + (wave.id != 0 ? " (wave " + wave.id + ")" : ""));
            }

            return true;
        }

        /// <summary>
        /// Where an arrival with no spot of its own appears: beside the phase object it names (spawnAt="object:1"), else
        /// beside the player. index spreads several of them out.
        /// </summary>
        public static double[] ArrivalSpot(WorldClient client, MissionProgress progress, MissionArrival arrival, int index,
            double px, double py, double pz)
        {
            ushort objectId = arrival.AtObject;
            if (objectId != 0 && progress.objects.TryGetValue(objectId, out MissionObjectState state) && state.spot != null)
            {
                // Object origins may be on a desk or cabinet. NPC height belongs to the floor, as site NPC spots do.
                var pose = MissionObjectPoses.For(state);
                return Beside(pose.X, state.OpenFloor!=null ? state.OpenFloor.Standing[1] : progress.site == null ? pose.Y : progress.site.floorY - 50, pose.Z, index);
            }

            return BesidePlayer(px, py, pz, index);
        }

        /// <summary>A few metres from the player, each arrival at a different angle so they don't stand in one another.</summary>
        public static double[] BesidePlayer(double px, double py, double pz, int index) => Beside(px, py, pz, index);

        /// <summary>3 m from a point, at an angle that differs with index.</summary>
        public static double[] Beside(double x, double y, double z, int index)
        {
            double angle = 0.6 + index * 1.3;
            return new[] { x + 300 * Math.Cos(angle), y, z + 300 * Math.Sin(angle) };
        }

        /// <summary>
        /// NPCs whose side a goal has just changed (hostileAfter, friendlyAfter). The client is told an NPC's side only in its
        /// creation record (IsEnemy for a hostile; DontAttack, TalkDefaultable and TalkActiveTracker for a friendly, as Mob
        /// builds it), and no capture shows retail changing those on a living NPC, so the NPC is taken off the clients and
        /// spawned again where it stands, with its health, on its new side. One that hasn't arrived comes on its new side.
        /// </summary>
        private static void SwitchSides(WorldClient client, MissionProgress progress)
        {
            if (progress.justSwitched.Count == 0)
            {
                return;
            }

            List<ushort> switching = new List<ushort>(progress.justSwitched);
            progress.justSwitched.Clear();
            foreach (ushort id in switching)
            {
                MissionNpc npc = progress.mission.npcs.Find(n => n.id == id);
                Mob old = FindNpc(client.playerData, id);
                if (npc == null || old == null || old.getIsDead())
                {
                    continue;
                }

                bool friendly = progress.IsFriendly(npc);
                if (!friendly)
                {
                    progress.following.Remove(id); // it no longer walks with the player
                }

                if (old.isFriendly == friendly)
                {
                    continue; // it arrived on its new side
                }

                Mob mob = SpawnNpc(client, progress.mission, npc, old.getXPos(), old.getYPos(), old.getZPos(), old.getLevel(), friendly, sideChange: true);
                if (mob == null)
                {
                    progress.pendingNpcPlacements.Add(id);
                    progress.justSwitched.Add(id);
                    continue;
                }
                progress.pendingNpcPlacements.Remove(id);
                mob.setHealthM(old.getHealthM());
                mob.setHealthC(old.getHealthC());
                mob.setRotation(old.getRotation());
                lock (client.playerData.missionMobs)
                {
                    client.playerData.missionMobs.Remove(old);
                    client.playerData.missionMobs.Add(mob);
                }

                Despawn(client.playerData, old);
                Output.WriteDebugLog("[MISSION] " + npc.handle + " turns " + (friendly ? "friendly" : "hostile"));
            }
        }

        private static void SendPhase(WorldClient client, MissionProgress progress, bool provideItems = true, bool restoring = false)
        {
            MissionPhase phase = progress.Phase;
            ServerPackets packets = new ServerPackets();
            if (!string.IsNullOrEmpty(phase.briefing))
            {
                string header = phase.briefingHeaderHex;
                float[] areaA = phase.areaA, areaB = phase.areaB;
                MissionSite site = progress.site;
                if (site != null)
                {
                    // Retail's header carried the site's sector after 30 00 01 00 (Welcome: 405; The Internuncio: 1349, then 35)
                    UInt16 sector = site.elevatorDoor.sectorID;
                    header = header ?? "30000100" + ((byte)sector).ToString("x2") + ((byte)(sector >> 8)).ToString("x2") +
                             "01000000000000003c00";
                    // The area points are its street entrance, as retail's were
                    areaA = new[] { (float)site.lobbyDoor.pos_x, (float)site.lobbyDoor.pos_y, (float)site.lobbyDoor.pos_z };
                    areaB = areaA;
                }

                client.messageQueue.addRpcMessage(ServerPackets.BuildMissionBriefing(Fill(client, phase.briefing),
                    header ?? "30000100000000000000000000003c00", areaA, areaB));
            }

            for (int i = 0; i < progress.waypoints.Count; i++)
            {
                MissionWaypoint door = progress.waypoints[i];
                client.messageQueue.addRpcMessage(ServerPackets.BuildMissionWaypoint(door.x, door.y, door.z, (byte)i, door.doorHex));
            }

            SendObjectives(client, progress, restoring);

            if(provideItems) GiveProvidedItems(client, progress);
            client.FlushQueue();
        }

        /// <summary>A new native HUD needs each row created before its completed-state update.
        /// Replay only this phase's completed or currently open goals; a locked goal stays hidden.</summary>
        internal static void SendObjectives(WorldClient client, MissionProgress progress, bool restoring)
        {
            if(restoring){MissionObjectiveReplay.Arm(client,progress);return;}
            SendObjectiveRows(client,progress,false);
        }
        internal static void SendObjectiveRows(WorldClient client, MissionProgress progress, bool restoring)
        {
            ServerPackets packets = new ServerPackets();
            foreach (MissionObjective objective in progress.Phase.missionObjectives)
                if (progress.IsOpen(objective) || restoring && progress.IsDone(objective.goal))
                    packets.SendSetMissionObjective((ushort)objective.goal, ObjectiveOpen, Fill(client, objective.description), client);
        }
        internal static void SendObjectiveCompletions(WorldClient client, MissionProgress progress)
        {
            ServerPackets packets = new ServerPackets();
            foreach (MissionObjective objective in progress.Phase.missionObjectives.Where(o => progress.IsDone(o.goal)))
                packets.SendSetMissionObjective((ushort)objective.goal, ObjectiveDone, Fill(client, objective.description), client);
        }

        // Items the briefing provides, including a retry when the bag was full at the start of this phase.
        private static bool GiveProvidedItems(WorldClient client, MissionProgress progress)
        {
            bool sent = false;
            foreach (MissionObjective objective in progress.Phase.missionObjectives)
            {
                bool handsOver = objective.type == MissionGoalType.Give || objective.type == MissionGoalType.Put ||
                                 objective.type == MissionGoalType.Upload;
                if (handsOver && !progress.IsDone(objective.goal) && objective.item != 0 && !progress.itemSlots.ContainsKey(objective.item) &&
                    !Provided(progress.mission, progress.phaseIndex, objective.item))
                {
                    if (GiveItem(client, progress, objective.item))
                    {
                        Output.WriteDebugLog("[MISSION] Gave item " + objective.item + " for goal " + objective.goal);
                        sent = true;
                    }
                }
            }
            return sent;
        }

        private static void Finish(WorldClient client, MissionProgress progress)
        {
            MissionPersistence.Save(client);
            // Quote both rewards once, before transactional XP can change the level.
            var payout=MissionRewards.Calculate(progress.mission,client.playerInstance.Level.getValue()[0]);
            uint experience=payout.Experience, info=payout.Info;
            MissionCompletion committed;
            try
            {
                committed=Store.dbManager.WorldDbHandler.CompleteActiveMission(client.playerData.getCharID(),
                    client.playerData.missionCheckpoint,experience,info);
            }
            catch(Exception ex)
            {
                // The business transaction rolled back. Keep the phase, owned items and unpaid receipt for a retry.
                Output.WriteLine("[MISSION] Completion transaction deferred: "+ex.Message);
                return;
            }
            if(committed==null)
            {
                if(!progress.completionNotice)
                {
                    new ServerPackets().sendSystemChatMessage(client,"Keep the mission ticket in your backpack and make room for the completion reward.","BROADCAST");
                    progress.completionNotice=true;
                }
                return;
            }
            client.playerData.missionCheckpoint=null;
            Mission mission = progress.mission;
            ServerPackets packets = new ServerPackets();
            if (!string.IsNullOrEmpty(mission.finishLine))
            {
                var contact = MissionContacts.For(mission.contact);
                client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(contact.image, contact.name, 0, 1, 0, "",
                    Fill(client, mission.finishLine)));
            }

            // Retail order: the mission-end message (status 00 = complete), then the rewards.
            UInt32 seconds = (UInt32)Math.Max(0, TimeUtils.getUnixTimeUint32() - progress.startedAt);
            packets.SendMissionComplete(client, committed.ExperienceCredited, committed.InfoCredited,
                (UInt16)Math.Min(progress.kills, UInt16.MaxValue), seconds);
            if(!committed.AlreadyCommitted)
            {
                if(committed.Exchange!=null)CollectorHandler.NotifyExchange(client,committed.Exchange,mission.completionItem);
                client.playerData.setExperience(committed.ExperienceTotal);client.playerData.setInfo(committed.InfoTotal);
                if(experience>0)
                {
                    packets.SendEXPGain(client,committed.ExperienceCredited,ServerPackets.ExpGainMission);
                    client.messageQueue.addRpcMessage(ServerPackets.BuildEXPTotal((uint)committed.ExperienceTotal,ServerPackets.ExpGainMission));
                    PlayerProgression.ApplyLevelUps(client);
                }
                if(info>0)packets.SendInfoCurrent(client,(uint)committed.InfoTotal,MissionRewardInfoReason);
            }
            client.playerData.completedMissions?.Add(mission.id);
            Output.WriteDebugLog("[MISSION] Completed '" + mission.title + "'");
            End(client);
            client.FlushQueue();
        }

        /// <summary>Strict disposable native fixture: no arbitrary NPC, text or mission mutation.</summary>
        public static bool DevPresentation(WorldClient client,string[] words)
        {
            if(words.Length==0 || words[0]!="?missionpresentation") return false;
            bool allowed=ReferenceEquals(client,Store.currentClient) && client.Alive &&
                MissionPresentation.QaAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                    Store.config?.dbParams?.DatabaseName,client.playerData.getCharID(),client.playerData.getOnWorld(),
                    client.playerData.isDead || client.playerData.duelDeath || client.playerData.worldChangePending || client.playerData.DistrictIsLA());
            lock(client.playerData.missionSync)
            {
                var data=client.playerData;
                Mob source=allowed && words.Length==2 && words[1]=="speech" && data.activeMission?.id==1203 && data.missionProgress!=null
                    ? data.missionMobs.FirstOrDefault(m=>m.ScriptedCorpse && m.getIsDead() && !m.isFriendly &&
                        m.missionInstance==data.missionInstanceCounter && client.viewMan.FindViewForEntity(m.getEntityId())?.viewCreated==true) : null;
                if(source==null) new ServerPackets().sendSystemChatMessage(client,"Mission presentation QA refused: requires CSR110, dev1, mxotest_, live Internuncio and a visible owned hostile body.","SYSTEM");
                else Say(client,new List<MissionLine>{new MissionLine {speaker=MissionSpeaker.Npc,npcId=source.missionNpcId,
                    display=5,text="[QA] Hostile mission presentation diagnostic."}});
            }
            client.FlushQueue();return true;
        }

        private static void Say(WorldClient client, List<MissionLine> lines, bool interactive=false)
        {
            foreach (MissionLine line in lines)
            {
                string text = Fill(client, line.text);
                switch (line.speaker)
                {
                    case MissionSpeaker.Npc:
                        MissionNpc npc = client.playerData.activeMission?.npcs.Find(n => n.id == line.npcId);
                        bool friendly = npc == null || (client.playerData.missionProgress?.IsFriendly(npc) ?? npc.friendly);
                        client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(
                            npc != null && !friendly ? HostileNpcImage : FriendlyNpcImage, 0, 0, line.display, 0,
                            npc?.handle ?? "", text));
                        string echo=MissionPresentation.ChatFallback(!friendly,interactive,npc?.handle ?? "",text);
                        if(echo!=null) new ServerPackets().sendSystemChatMessage(client,echo,"SYSTEM");
                        break;
                    case MissionSpeaker.Contact:
                        var contact = MissionContacts.For(client.playerData.activeMission?.contact);
                        client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(contact.image, contact.name, 0, line.display, 0, "", text));
                        break;
                    default:
                        client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(OperatorImage, OperatorName, 0, line.display, 0, "", text));
                        break;
                }
            }
        }

        /// <summary>Retail's text named the player; the files say {player}.</summary>
        private static string Fill(WorldClient client, string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("{player}"))
            {
                return text;
            }

            return text.Replace("{player}", StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue()));
        }

        private static void EnsureTimer()
        {
            lock (timerLock)
            {
                if (timer == null)
                {
                    timer = new Timer(_ => TickAll(), null, 500, 500);
                }
            }
        }

        private static void TickAll()
        {
            List<WorldClient> clients;
            lock (WorldServer.Clients)
            {
                clients = new List<WorldClient>(WorldServer.Clients.Values);
            }

            foreach (WorldClient client in clients)
            {
                // Store.currentClient is per thread: the inventory and database code a tick reaches (a searched item going into
                // the bag) reads it, so it's this client for the tick, as DelayedActions.AfterFor does
                WorldClient previous = Store.currentClient;
                try
                {
                    if (client?.playerData?.missionProgress != null)
                    {
                        Store.currentClient = client;
                        Tick(client);
                    }
                }
                catch (Exception ex)
                {
                    Output.WriteLine("[MISSION] Tick failed: " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault());
                }
                finally
                {
                    Store.currentClient = previous;
                }
            }
        }

        /// <summary>Where the player walked to: lines set off by a place, and leaving a finished phase.</summary>
        private static void Tick(WorldClient client)
        {
            lock (client.playerData.missionSync)
            {
                if(!client.Alive || !client.playerData.getOnWorld() || client.playerData.worldChangePending || client.playerData.getDistrictId()!=client.playerData.activeMission?.district)return;
                MissionPersistence.Mutate(client,()=>TickMission(client));
            }
        }

        private static void TickMission(WorldClient client)
        {
            MissionProgress progress = client.playerData.missionProgress;
            MissionPhase phase = progress?.Phase;
            if (phase == null)
            {
                return;
            }

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);

            bool sent = GiveProvidedItems(client, progress);
            SwitchSides(client, progress); // Retry a side change that was waiting for a safe placement.
            MissionStaging.Follow(client, progress, x, y, z);
            sent |= TickObjects(client, progress, x, y, z);
            sent |= TickEscorts(client, progress, x, y, z);
            if (client.playerData.missionProgress != progress)
            {
                return;
            }

            DataMining.RevealMission(client, progress);
            MissionSite site = progress.site;
            if (site != null && !progress.entered && x >= site.minX && x <= site.maxX && z >= site.minZ && z <= site.maxZ &&
                Math.Abs(y - site.floorY) < 250)
            {
                progress.entered = true;
                Say(client, phase.enterLines);
                sent = true;
            }

            // Arrivals on the area, a place or a timer; hold-outs and time limits; a kill-all whose last hostile never came
            progress.UpdateArrivals((place, radius) => Near(client, progress, place, radius, x, y, z));
            if (SpawnArrivals(client, progress))
            {
                MissionStaging.RefreshObjects(client, progress);
                sent = true;
            }

            foreach (MissionObjective survived in progress.DueSurvivals())
            {
                Complete(client, progress, survived);
                sent = true;
            }

            MissionObjective overdue = progress.Overdue();
            if (overdue != null)
            {
                Fail(client, progress, "You ran out of time.");
                return;
            }

            sent |= CompleteDefeatAlls(client, progress);

            foreach (MissionTrigger trigger in phase.triggers)
            {
                double tx = trigger.x, ty = trigger.y, tz = trigger.z;
                if (trigger.npcId != 0)
                {
                    if (!progress.HasArrived(trigger.npcId))
                    {
                        continue; // its NPC hasn't arrived yet
                    }

                    Mob placed = FindNpc(client.playerData, trigger.npcId);
                    MissionNpc npc = progress.mission.npcs.Find(n => n.id == trigger.npcId);
                    if (placed != null)
                    {
                        tx = placed.getXPos();
                        ty = placed.getYPos();
                        tz = placed.getZPos();
                    }
                    else if (npc != null)
                    {
                        tx = npc.x;
                        ty = npc.y;
                        tz = npc.z;
                    }
                    else
                    {
                        continue;
                    }
                }

                double dx = tx - x, dy = ty - y, dz = tz - z;
                if (dx * dx + dy * dy + dz * dz <= trigger.radius * trigger.radius && progress.Fire(trigger))
                {
                    Say(client, trigger.lines);
                    sent = true;
                }
            }

            if (progress.IsExiting(x, y, z))
            {
                if (progress.phaseIndex == progress.mission.phases.Count - 1)
                {
                    Finish(client, progress);
                    return;
                }
                MissionOpenFloorAdmission.Plan openFloorPlan=null;
                if(MissionOpenFloorAdmission.Requires(progress.mission.phases[progress.phaseIndex+1]))
                {
                    if(!MissionOpenFloorAdmission.TryPrepare(client,progress,progress.phaseIndex+1,progress,out openFloorPlan,out var selected,out string refusal))
                    {
                        if(selected!=null&&openFloorPlan?.BeforeCurrent(client)==true)
                        {
                            bool changed=progress.pendingOpenFloor==null||progress.pendingOpenFloor.Refusal!=refusal;
                            progress.pendingOpenFloor=selected.WithRefusal(refusal);MissionOpenFloorAdmission.MarkPendingCheckpoint(progress);
                            if(changed) {Output.WriteDebugLog("[MISSION] Box next-phase admission deferred: "+refusal);
                                client.messageQueue.addRpcMessage(ServerPackets.BuildSystemChatMessage(MissionOpenFloorAdmission.UnavailableMessage,"BROADCAST"));}
                        }
                        return;
                    }
                    if(!openFloorPlan.BeforeCurrent(client))return;
                    // Admit the exact choice durably before tearing down the old phase.
                    // A later staging/SQL rollback restores this same pending choice.
                    if(openFloorPlan.Adopted)
                    {
                        progress.pendingOpenFloor=openFloorPlan.Selection.WithRefusal(null);MissionOpenFloorAdmission.MarkPendingCheckpoint(progress);
                        MissionPersistence.Save(client);
                        if(!MissionOpenFloorAdmission.PendingDurable(client.playerData,progress.pendingOpenFloor)||!openFloorPlan.BeforeCurrent(client))return;
                    }
                }
                if(openFloorPlan!=null&&!openFloorPlan.BindOutput(client))return;
                MissionStaging.DespawnObjects(client, progress);
                progress.NextPhase();
                progress.pendingOpenFloor=null;
                Output.WriteDebugLog("[MISSION] Left the area: phase " + progress.phaseIndex);
                if(openFloorPlan==null)MissionStaging.Stage(client, progress);
                else {if(openFloorPlan.Adopted)progress.openFloorCheckpoint=true;MissionStaging.StagePrepared(client,progress,openFloorPlan);}
                SendPhase(client, progress);

                return;
            }

            if (sent)
            {
                client.FlushQueue();
            }
        }

        /// <summary>
        /// Escorts: an NPC starts following when its Escort goal is open and the player reaches it (or talks to it); the goal
        /// is done when the two of them reach the way out (to="exit") or the NPC named (to="npc:3"). Hardlines: OnHardline.
        /// </summary>
        /// <summary>
        /// Standing at a mission object does its goals, for objects the player can't click (the phone): Search puts the item
        /// straight in the inventory. Computers and containers are clicked (OnViewUse).
        /// </summary>
        private static bool TickObjects(WorldClient client, MissionProgress progress, double x, double y, double z)
        {
            bool sent = false;
            foreach (MissionObjectState state in progress.objects.Values)
            {
                if (MissionStaging.IsClickable(state.goid))
                {
                    continue;
                }

                var pose = MissionObjectPoses.For(state);
                double dx = pose.X - x, dz = pose.Z - z;
                if (dx * dx + dz * dz > ObjectReachUnits * ObjectReachUnits || Math.Abs(pose.Y - y) > 300)
                {
                    continue;
                }

                foreach (MissionObjective objective in new List<MissionObjective>(progress.OpenObjectives()))
                {
                    if (objective.objectId != state.def.id)
                    {
                        continue;
                    }

                    if (objective.type == MissionGoalType.Search)
                    {
                        if (objective.item != 0 && !GiveItem(client, progress, objective.item))
                        {
                            break;
                        }

                        Complete(client, progress, objective);
                        sent = true;
                    }
                    else if (objective.type == MissionGoalType.Use ||
                             (objective.type == MissionGoalType.Put && progress.itemSlots.ContainsKey(objective.item)))
                    {
                        if (objective.type == MissionGoalType.Put)
                        {
                            if (!TakeItem(client, progress, objective.item))
                            {
                                break;
                            }
                        }

                        Complete(client, progress, objective);
                        sent = true;
                    }

                    // One goal per object per tick: "Search the computer" then "Get the disk from the computer"
                    break;
                }
            }

            return sent;
        }

        /// <summary>How near a mission object the player does its goals: arm's length and a step.</summary>
        public const double ObjectReachUnits = 250;

        /// <summary>Whether a goal up to this phase gives the player the item (Get, Search or Loot).</summary>
        public static bool Provided(Mission mission, int upToPhase, UInt32 item)
        {
            for (int p = 0; p <= upToPhase && p < mission.phases.Count; p++)
            {
                foreach (MissionObjective objective in mission.phases[p].missionObjectives)
                {
                    if (objective.item == item && (objective.type == MissionGoalType.Get || objective.type == MissionGoalType.Search ||
                                                   objective.type == MissionGoalType.Loot))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TickEscorts(WorldClient client, MissionProgress progress, double x, double y, double z)
        {
            bool sent = false;

            // "Find Arcturus": the client offers no Talk on a hostile NPC, so finding one is reaching it
            foreach (MissionObjective objective in new List<MissionObjective>(progress.OpenObjectives()))
            {
                Mob npc = objective.type == MissionGoalType.Talk ? FindNpc(client.playerData, objective.npcId) : null;
                if (npc != null && !npc.isFriendly && !npc.getIsDead() && Near(client, objective.npcId, x, y, z, 500))
                {
                    Complete(client, progress, objective);
                    sent = true;
                }
            }

            foreach (MissionObjective objective in new List<MissionObjective>(progress.OpenObjectives()))
            {
                if (objective.type != MissionGoalType.Escort)
                {
                    continue;
                }

                // "Lead both hostages out of the area": every NPC the goal names has to come along
                bool allWithPlayer = true;
                foreach (ushort id in objective.Npcs)
                {
                    if (!progress.following.Contains(id) && Near(client, id, x, y, z, 600))
                    {
                        progress.following.Add(id);
                        Output.WriteDebugLog("[MISSION] NPC " + id + " follows the player");
                    }

                    allWithPlayer &= progress.following.Contains(id) && Near(client, id, x, y, z, 1500);
                }

                if (!allWithPlayer)
                {
                    continue;
                }

                if (objective.escortTo == "exit" && progress.AtExit(x, y, z))
                {
                    Complete(client, progress, objective);
                    sent = true;
                }
                else if (objective.escortTo.StartsWith("object:") && ushort.TryParse(objective.escortTo.Substring(7), out ushort objectId))
                {
                    // Led to one of the phase's mission objects: the player and each NPC beside it
                    if (progress.objects.TryGetValue(objectId, out MissionObjectState state) &&
                        AtObject(client, progress, objective, state, x, y, z))
                    {
                        foreach (ushort id in objective.Npcs)
                        {
                            progress.following.Remove(id);
                        }

                        Complete(client, progress, objective);
                        sent = true;
                    }
                }
                else if (objective.escortTo.StartsWith("npc:") && ushort.TryParse(objective.escortTo.Substring(4), out ushort target))
                {
                    Mob to = FindNpc(client.playerData, target);
                    bool arrived = to != null && !to.getIsDead();
                    foreach (ushort id in objective.Npcs)
                    {
                        Mob follower = FindNpc(client.playerData, id);
                        arrived &= follower != null && Distance2(to, follower) <= EscortArrivalUnits * EscortArrivalUnits;
                    }

                    if (arrived)
                    {
                        foreach (ushort id in objective.Npcs)
                        {
                            progress.following.Remove(id);
                        }

                        Complete(client, progress, objective);
                        sent = true;
                    }
                }
            }

            return sent;
        }

        /// <summary>How near the NPC or object an escort ends: 7 m.</summary>
        public const double EscortArrivalUnits = 700;

        /// <summary>An escort to an object is there: the player and every NPC it names within reach of the object.</summary>
        private static bool AtObject(WorldClient client, MissionProgress progress, MissionObjective objective, MissionObjectState state,
            double x, double y, double z)
        {
            var pose = MissionObjectPoses.For(state);
            double ox = pose.X, oy = pose.Y, oz = pose.Z;
            bool arrived = Distance2(ox, oy, oz, x, y, z) <= EscortArrivalUnits * EscortArrivalUnits;
            foreach (ushort id in objective.Npcs)
            {
                Mob follower = FindNpc(client.playerData, id);
                arrived &= follower != null && !follower.getIsDead() &&
                           Distance2(ox, oy, oz, follower.getXPos(), follower.getYPos(), follower.getZPos()) <= EscortArrivalUnits * EscortArrivalUnits;
            }

            return arrived;
        }

        private static double Distance2(double ax, double ay, double az, double bx, double by, double bz)
        {
            double dx = ax - bx, dy = ay - by, dz = az - bz;
            return dx * dx + dy * dy + dz * dz;
        }

        private static double Distance2(Mob a, Mob b)
        {
            if (a == null || b == null)
            {
                return double.MaxValue;
            }

            double dx = a.getXPos() - b.getXPos(), dy = a.getYPos() - b.getYPos(), dz = a.getZPos() - b.getZPos();
            return dx * dx + dy * dy + dz * dz;
        }

        /// <param name="friendly">The side it comes on, when a goal has changed it (MissionProgress.IsFriendly); otherwise the file's.</param>
        public static Mob SpawnNpc(WorldClient client, Mission mission, MissionNpc npc, double x = double.NaN, double y = double.NaN,
            double z = double.NaN, ushort level = 0, bool? friendly = null, NavPoint? placement = null, bool sideChange = false)
        {
            double sx = double.IsNaN(x) ? npc.x : x;
            double sy = double.IsNaN(y) ? npc.y : y;
            double sz = double.IsNaN(z) ? npc.z : z;
            NavPoint onFloor;
            if (placement.HasValue) onFloor = placement.Value;
            else if (!MissionPlacement.ForSpawn(client, sx, sy, sz, npc.arrival.AtPlayer && !sideChange, out onFloor,
                replacingNpc: sideChange ? npc.id : (ushort)0)) return null;
            x = onFloor.x; y = onFloor.y; z = onFloor.z;
            if (!double.IsNaN(x) || (friendly != null && friendly.Value != npc.friendly))
            {
                // Placed in a generated site, or on another side: a copy, since the mission's definition is shared
                bool placed = !double.IsNaN(x);
                npc = new MissionNpc
                {
                    id = npc.id, handle = npc.handle, friendly = friendly ?? npc.friendly, attacksOnSight = npc.attacksOnSight, dead = npc.dead,
                    phase = npc.phase, x = placed ? x : npc.x, y = placed ? y : npc.y, z = placed ? z : npc.z, rsiHex = npc.rsiHex,
                    weapon = npc.weapon, at = npc.at, lines = npc.lines, arrival = npc.arrival, wave = npc.wave,
                    hostileAfter = npc.hostileAfter, friendlyAfter = npc.friendlyAfter,
                    level = level > 0 ? level : npc.level,
                    maxHealth = level > 0 ? (ushort)Math.Max((int)npc.maxHealth, 150 + 40 * level) : npc.maxHealth
                };
            }

            Mob mob = new Mob();
            mob.setEntityId(WorldServer.entityIdCounter++);
            mob.setDistrict(mission.district);
            mob.setDistrictName(mission.districtName);
            mob.setName(npc.handle);
            mob.setLevel(npc.level);
            mob.setHealthM(npc.maxHealth);
            mob.setHealthC(npc.dead ? (ushort)0 : npc.maxHealth);
            mob.setMobId(npc.id);
            mob.setRsiHex(npc.rsiHex);
            mob.setXPos(npc.x);
            mob.setYPos(npc.y);
            mob.setZPos(npc.z);
            mob.xBase = npc.x;
            mob.yBase = npc.y;
            mob.zBase = npc.z;
            mob.setRotation(0);
            mob.setIsDead(npc.dead);
            mob.ScriptedCorpse = npc.dead;
            mob.setIsLootable(false);
            mob.setWeapon(npc.weapon);
            mob.isFriendly = npc.friendly;
            mob.aggroKind = npc.attacksOnSight ? MobAggroKind.Unfiltered : MobAggroKind.Passive;
            mob.missionNpcId = npc.id;
            mob.missionPhase = npc.phase;
            foreach (MissionPhase phase in mission.phases)
            {
                foreach (MissionObjective objective in phase.missionObjectives)
                {
                    mob.missionGiver |= objective.Has(npc.id) && objective.type == MissionGoalType.Get;
                    mob.missionTaker |= objective.Has(npc.id) && objective.type == MissionGoalType.Give;
                }
            }

            mob.missionInstance = client.playerData.missionInstanceCounter;

            lock (WorldServer.mobs.SyncRoot)
            {
                WorldServer.mobs.Add(mob);
            }

            lock (WorldServer.gameServerEntities.SyncRoot)
            {
                WorldServer.gameServerEntities.Add(mob);
            }

            Output.WriteDebugLog("[MISSION] Spawned " + (npc.friendly ? "friendly" : "hostile") + " NPC " +
                                 npc.handle + " at " + npc.x + "," + npc.y + "," + npc.z);
            return mob;
        }
    }

    /// <summary>Experience and info rewards for a specific client (safe off the client's own thread).</summary>
    public static class PlayerRewards
    {
        public static void GrantExperience(WorldClient client, UInt32 amount, byte gainType = ServerPackets.ExpGainFighting)
        {
            if (amount == 0)
            {
                return;
            }

            long exp = client.playerData.getExperience() + amount;
            client.playerData.setExperience(exp);
            Store.dbManager.WorldDbHandler.SaveExperience(client, exp);
            new ServerPackets().SendEXPGain(client, amount, gainType);
            client.messageQueue.addRpcMessage(ServerPackets.BuildEXPTotal((UInt32)exp, gainType));
            PlayerProgression.ApplyLevelUps(client);
        }

        public static void GrantInfo(WorldClient client, UInt32 amount, UInt32 reason)
        {
            if (amount == 0)
            {
                return;
            }

            long info = Math.Min(client.playerData.getInfo() + (long)amount, UInt32.MaxValue);
            client.playerData.setInfo(info);
            Store.dbManager.WorldDbHandler.SaveInfo(client, info);
            new ServerPackets().SendInfoCurrent(client, (UInt32)info, reason);
        }
    }
}
