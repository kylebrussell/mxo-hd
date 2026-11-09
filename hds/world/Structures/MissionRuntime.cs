using System;
using System.Collections.Generic;
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
    /// nearest exit quickly!"), and the next phase or the mission's end comes once the player has left.
    /// </summary>
    public static class MissionRuntime
    {
        /// <summary>Retail's 80 e4 after a completed mission carried reason 2 (80 e4 ... 02 00 00 00).</summary>
        public const UInt32 MissionRewardInfoReason = 0x02;

        public const byte ObjectiveOpen = 0;
        public const byte ObjectiveDone = 1;

        public const UInt32 StringGoalsCompleteLeave = 0x2600000c;

        private const UInt32 OperatorImage = 0x28000a70;
        private const UInt32 OperatorName = 0x26000014;
        private const UInt32 ContactImage = 0x28000cc0;
        private const UInt32 ContactName = 0xb4000031;
        private const UInt32 FriendlyNpcImage = 0x2800121f;
        private const UInt32 HostileNpcImage = 0x2800121e;

        private static readonly object timerLock = new object();
        private static Timer timer;

        public static void Start(WorldClient client, Mission mission)
        {
            End(client);

            ClientData data = client.playerData;
            MissionProgress progress = new MissionProgress(mission);
            data.activeMission = mission;
            data.missionProgress = progress;

            foreach (MissionNpc npc in mission.npcs)
            {
                Mob mob = SpawnNpc(client, mission, npc);
                lock (data.missionMobs)
                {
                    data.missionMobs.Add(mob);
                }
            }

            SendPhase(client, progress);
            EnsureTimer();
        }

        /// <summary>Removes the mission's NPCs and deletes their views from the clients that have them.</summary>
        public static void End(WorldClient client)
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

            // Mission items don't outlive the mission (an abort, or leaving before handing them over).
            MissionProgress progress = data.missionProgress;
            if (progress != null)
            {
                foreach (UInt32 item in new List<UInt32>(progress.itemSlots.Keys))
                {
                    TakeItem(client, progress, item);
                }
            }

            data.activeMission = null;
            data.missionProgress = null;
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
            MissionProgress progress = client.playerData.missionProgress;
            if (npc == null || npc.missionNpcId == 0 || progress == null)
            {
                return false;
            }

            MissionObjective objective = progress.FindOpenFor(npc.missionNpcId);
            if (objective != null && objective.talkLines.Count > 0)
            {
                Say(client, objective.talkLines);
            }

            if (objective != null && objective.type == MissionGoalType.Talk)
            {
                Complete(client, progress, objective);
            }
            else if (objective == null)
            {
                MissionLine line = LatestLine(progress, npc.missionNpcId);
                if (line != null)
                {
                    Say(client, new List<MissionLine> { line });
                }
            }

            client.FlushQueue();
            return true;
        }

        /// <summary>Right-click Get on an NPC (80 c7 ... 0d): it hands over the objective's item.</summary>
        public static bool OnGet(WorldClient client, Mob npc)
        {
            MissionProgress progress = client.playerData.missionProgress;
            MissionObjective objective = npc == null ? null : progress?.FindOpen(MissionGoalType.Get, npc.missionNpcId);
            if (objective == null)
            {
                return false;
            }

            GiveItem(client, progress, objective.item);
            Complete(client, progress, objective);
            client.FlushQueue();
            return true;
        }

        /// <summary>Right-click Give on an NPC (80 c7 ... 0e): the player hands over the objective's item.</summary>
        public static bool OnGive(WorldClient client, Mob npc)
        {
            MissionProgress progress = client.playerData.missionProgress;
            MissionObjective objective = npc == null ? null : progress?.FindOpen(MissionGoalType.Give, npc.missionNpcId);
            if (objective == null)
            {
                return false;
            }

            TakeItem(client, progress, objective.item);
            Complete(client, progress, objective);
            client.FlushQueue();
            return true;
        }

        /// <summary>A mob the player fought died. Completes a Defeat objective for it.</summary>
        public static void OnMobDefeated(WorldClient client, Mob mob)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null || mob == null)
            {
                return;
            }

            progress.kills++;
            MissionObjective objective = mob.missionNpcId == 0
                ? null
                : progress.FindOpen(MissionGoalType.Defeat, mob.missionNpcId);
            if (objective != null)
            {
                Complete(client, progress, objective);
                client.FlushQueue();
            }
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

        /// <summary>The player accepted a loot window holding a Loot objective's item.</summary>
        public static void OnMissionItemLooted(WorldClient client, UInt32 itemGoId)
        {
            MissionProgress progress = client.playerData.missionProgress;
            if (progress == null)
            {
                return;
            }

            foreach (MissionObjective objective in progress.OpenObjectives())
            {
                if (objective.type == MissionGoalType.Loot && objective.item == itemGoId)
                {
                    GiveItem(client, progress, itemGoId);
                    Complete(client, progress, objective);
                    client.FlushQueue();
                    return;
                }
            }
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

        private static void GiveItem(WorldClient client, MissionProgress progress, UInt32 goId)
        {
            UInt16 slot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            Store.dbManager.WorldDbHandler.AddItemToInventory(slot, goId);
            client.messageQueue.addRpcMessage(ServerPackets.BuildMissionItemAdd(slot, goId, ItemElement(progress, goId),
                client.playerData.missionInstanceCounter));
            progress.itemSlots[goId] = slot;
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

        private static void TakeItem(WorldClient client, MissionProgress progress, UInt32 goId)
        {
            if (progress.itemSlots.TryGetValue(goId, out UInt16 slot))
            {
                Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, client.playerData.getCharID());
                new ServerPackets().sendInventoryItemDelete(slot, client);
                progress.itemSlots.Remove(goId);
            }
        }

        private static void Complete(WorldClient client, MissionProgress progress, MissionObjective objective)
        {
            ServerPackets packets = new ServerPackets();
            packets.SendSetMissionObjective((ushort)objective.goal, ObjectiveDone, Fill(client, objective.description), client);
            Output.WriteDebugLog("[MISSION] Goal " + objective.goal + " done: " + objective.description);

            List<MissionObjective> opened = progress.Complete(objective);
            foreach (MissionObjective next in opened)
            {
                packets.SendSetMissionObjective((ushort)next.goal, ObjectiveOpen, Fill(client, next.description), client);
            }

            Say(client, objective.doneLines);

            if (progress.AllDone)
            {
                Say(client, progress.Phase.completeLines);

                // Retail: the operator's "Goals for this area are complete. Find the nearest exit quickly!" (client string).
                client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(OperatorImage, OperatorName, StringGoalsCompleteLeave, 1, 1, "", ""));
                Output.WriteDebugLog("[MISSION] Phase " + progress.phaseIndex + " goals done; waiting for the player to leave");
            }
        }

        private static void SendPhase(WorldClient client, MissionProgress progress)
        {
            MissionPhase phase = progress.Phase;
            ServerPackets packets = new ServerPackets();
            if (!string.IsNullOrEmpty(phase.briefing))
            {
                client.messageQueue.addRpcMessage(ServerPackets.BuildMissionBriefing(Fill(client, phase.briefing),
                    phase.briefingHeaderHex, phase.areaA, phase.areaB));
            }

            for (int i = 0; i < phase.waypoints.Count; i++)
            {
                MissionWaypoint door = phase.waypoints[i];
                client.messageQueue.addRpcMessage(ServerPackets.BuildMissionWaypoint(door.x, door.y, door.z, (byte)i, door.doorHex));
            }

            foreach (MissionObjective objective in progress.OpenObjectives())
            {
                packets.SendSetMissionObjective((ushort)objective.goal, ObjectiveOpen, Fill(client, objective.description), client);
            }

            client.FlushQueue();
        }

        private static void Finish(WorldClient client, MissionProgress progress)
        {
            Mission mission = progress.mission;
            ServerPackets packets = new ServerPackets();
            if (!string.IsNullOrEmpty(mission.finishLine))
            {
                client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(ContactImage, ContactName, 0, 1, 0, "",
                    Fill(client, mission.finishLine)));
            }

            // Retail order: the mission-end message (status 00 = complete), then the rewards.
            UInt32 seconds = (UInt32)Math.Max(0, TimeUtils.getUnixTimeUint32() - progress.startedAt);
            packets.SendMissionComplete(client, (UInt32)Math.Max(0, mission.exp), (UInt32)Math.Max(0, mission.info),
                (UInt16)Math.Min(progress.kills, UInt16.MaxValue), seconds);
            PlayerRewards.GrantExperience(client, (UInt32)Math.Max(0, mission.exp), ServerPackets.ExpGainMission);
            PlayerRewards.GrantInfo(client, (UInt32)Math.Max(0, mission.info), MissionRewardInfoReason);
            Store.dbManager.WorldDbHandler.SaveMissionCompleted(client.playerData.getCharID(), mission.id);
            client.playerData.completedMissions?.Add(mission.id);
            Output.WriteDebugLog("[MISSION] Completed '" + mission.title + "'");
            End(client);
            client.FlushQueue();
        }

        private static void Say(WorldClient client, List<MissionLine> lines)
        {
            foreach (MissionLine line in lines)
            {
                string text = Fill(client, line.text);
                switch (line.speaker)
                {
                    case MissionSpeaker.Npc:
                        MissionNpc npc = client.playerData.activeMission?.npcs.Find(n => n.id == line.npcId);
                        client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(
                            npc != null && !npc.friendly ? HostileNpcImage : FriendlyNpcImage, 0, 0, line.display, 0,
                            npc?.handle ?? "", text));
                        break;
                    case MissionSpeaker.Contact:
                        client.messageQueue.addRpcMessage(ServerPackets.BuildDialog(ContactImage, ContactName, 0, line.display, 0, "", text));
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
                try
                {
                    if (client?.playerData?.missionProgress != null)
                    {
                        Tick(client);
                    }
                }
                catch (Exception ex)
                {
                    Output.WriteLine("[MISSION] Tick failed: " + ex.Message);
                }
            }
        }

        /// <summary>Where the player walked to: lines set off by a place, and leaving a finished phase.</summary>
        private static void Tick(WorldClient client)
        {
            MissionProgress progress = client.playerData.missionProgress;
            MissionPhase phase = progress?.Phase;
            if (phase == null)
            {
                return;
            }

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);

            bool sent = false;
            foreach (MissionTrigger trigger in phase.triggers)
            {
                double tx = trigger.x, ty = trigger.y, tz = trigger.z;
                if (trigger.npcId != 0)
                {
                    MissionNpc npc = progress.mission.npcs.Find(n => n.id == trigger.npcId);
                    if (npc == null)
                    {
                        continue;
                    }

                    tx = npc.x;
                    ty = npc.y;
                    tz = npc.z;
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
                if (progress.NextPhase())
                {
                    Output.WriteDebugLog("[MISSION] Left the area: phase " + progress.phaseIndex);
                    SendPhase(client, progress);
                }
                else
                {
                    Finish(client, progress);
                }

                return;
            }

            if (sent)
            {
                client.FlushQueue();
            }
        }

        private static Mob SpawnNpc(WorldClient client, Mission mission, MissionNpc npc)
        {
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
                    mob.missionGiver |= objective.npcId == npc.id && objective.type == MissionGoalType.Get;
                    mob.missionTaker |= objective.npcId == npc.id && objective.type == MissionGoalType.Give;
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
