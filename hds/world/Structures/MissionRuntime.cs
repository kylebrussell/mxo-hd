using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Runs an accepted mission: spawns its NPCs, points the waypoint at the current objective and completes
    /// objectives in order as the player talks to, defeats, loots and hands items to mission NPCs.
    ///
    /// Retail behaviour this follows (first-mission capture, crashover2k create_char_devoted, 2009-07-31):
    /// the client talks to an NPC with 80 c7 &lt;view u16&gt; &lt;spawn u16&gt; 09 00, and the server answers with
    /// 80 a0 &lt;objective&gt; 06 00 01 &lt;text&gt; (state 01 = done) followed by an 80 a1 NPC dialog.
    /// </summary>
    public static class MissionRuntime
    {
        /// <summary>Retail's 80 e4 after a completed mission carried reason 2 (80 e4 ... 02 00 00 00).</summary>
        public const UInt32 MissionRewardInfoReason = 0x02;

        public const byte ObjectiveOpen = 0;
        public const byte ObjectiveDone = 1;

        public static void Start(WorldClient client, Mission mission)
        {
            End(client);

            ClientData data = client.playerData;
            data.activeMission = mission;
            data.missionObjectiveIndex = 0;
            data.missionItemSlot = -1;

            foreach (MissionNpc npc in mission.npcs)
            {
                Mob mob = SpawnNpc(client, npc);
                lock (data.missionMobs)
                {
                    data.missionMobs.Add(mob);
                }
            }

            SendWaypointForCurrentObjective(client);
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

            data.activeMission = null;
            data.missionObjectiveIndex = 0;
        }

        public static MissionObjective CurrentObjective(ClientData data)
        {
            Mission mission = data.activeMission;
            if (mission == null || mission.phases.Count == 0)
            {
                return null;
            }

            List<MissionObjective> objectives = mission.phases[0].missionObjectives;
            return data.missionObjectiveIndex < objectives.Count ? objectives[data.missionObjectiveIndex] : null;
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
            ClientData data = client.playerData;
            if (npc == null || npc.missionNpcId == 0 || data.activeMission == null)
            {
                return false;
            }

            MissionObjective objective = CurrentObjective(data);
            ServerPackets packets = new ServerPackets();
            if (objective != null && objective.npcId == npc.missionNpcId)
            {
                string command = (objective.command ?? "").ToUpperInvariant();
                if (command == "TALK")
                {
                    Complete(client, objective, npc);
                    return true;
                }

                if (command == "GIVE" && data.missionItemSlot >= 0)
                {
                    UInt16 slot = (UInt16)data.missionItemSlot;
                    Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, data.getCharID());
                    packets.sendInventoryItemDelete(slot, client);
                    data.missionItemSlot = -1;
                    Complete(client, objective, npc);
                    return true;
                }
            }

            // Not this NPC's turn: let them repeat what the player should do next.
            if (npc.isFriendly)
            {
                string hint = objective != null ? objective.description : "Thank you.";
                packets.SendNpcDialog(client, npc.getName(), hint);
                client.FlushQueue();
            }

            return true;
        }

        /// <summary>A mob the player fought died. Completes a DEFEAT objective for it.</summary>
        public static void OnMobDefeated(WorldClient client, Mob mob)
        {
            ClientData data = client.playerData;
            MissionObjective objective = CurrentObjective(data);
            if (objective == null || mob == null || mob.missionNpcId == 0 || objective.npcId != mob.missionNpcId)
            {
                return;
            }

            if (string.Equals(objective.command, "DEFEAT", StringComparison.OrdinalIgnoreCase))
            {
                Complete(client, objective, null);
            }
        }

        /// <summary>The item a LOOT objective puts in the dead mob's loot window, or 0.</summary>
        public static UInt32 LootItemFor(ClientData data, Mob mob)
        {
            MissionObjective objective = CurrentObjective(data);
            if (objective == null || mob == null || mob.missionNpcId == 0 || objective.npcId != mob.missionNpcId ||
                !string.Equals(objective.command, "LOOT", StringComparison.OrdinalIgnoreCase) ||
                objective.itemId == null || objective.itemId.Length < 4)
            {
                return 0;
            }

            return NumericalUtils.ByteArrayToUint32(objective.itemId, 1);
        }

        /// <summary>The player accepted a loot window holding the LOOT objective's item.</summary>
        public static void OnMissionItemLooted(WorldClient client, UInt32 itemGoId)
        {
            ClientData data = client.playerData;
            MissionObjective objective = CurrentObjective(data);
            if (objective == null || !string.Equals(objective.command, "LOOT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UInt16 slot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            Store.dbManager.WorldDbHandler.AddItemToInventory(slot, itemGoId);
            new ServerPackets().sendInventoryItemAdd(slot, itemGoId, 0, 0, client);
            data.missionItemSlot = slot;
            Complete(client, objective, null);
        }

        private static void Complete(WorldClient client, MissionObjective objective, Mob speaker)
        {
            ClientData data = client.playerData;
            ServerPackets packets = new ServerPackets();
            ushort index = (ushort)data.missionObjectiveIndex;
            packets.SendSetMissionObjective(index, ObjectiveDone, objective.description, client);

            if (speaker != null && !string.IsNullOrEmpty(objective.dialog) &&
                !string.Equals(objective.dialog, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                string handle = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
                packets.SendNpcDialog(client, speaker.getName(), objective.dialog.Replace("%", handle));
            }

            Output.WriteDebugLog("[MISSION] Objective " + index + " done: " + objective.description);
            data.missionObjectiveIndex++;

            if (CurrentObjective(data) == null)
            {
                Finish(client);
            }
            else
            {
                SendWaypointForCurrentObjective(client);
            }

            client.FlushQueue();
        }

        private static void Finish(WorldClient client)
        {
            ClientData data = client.playerData;
            Mission mission = data.activeMission;
            ServerPackets packets = new ServerPackets();

            // Retail order: the mission-end message (status 03 = complete), then the rewards.
            packets.SendMissionComplete(client, (UInt32)Math.Max(0, mission.exp), (UInt32)Math.Max(0, mission.info));
            PlayerRewards.GrantExperience(client, (UInt32)Math.Max(0, mission.exp), ServerPackets.ExpGainMission);
            PlayerRewards.GrantInfo(client, (UInt32)Math.Max(0, mission.info), MissionRewardInfoReason);
            Output.WriteDebugLog("[MISSION] Completed '" + mission.title + "'");
            End(client);
        }

        private static void SendWaypointForCurrentObjective(WorldClient client)
        {
            MissionObjective objective = CurrentObjective(client.playerData);
            if (objective == null)
            {
                return;
            }

            Mob target;
            lock (client.playerData.missionMobs)
            {
                target = client.playerData.missionMobs.Find(m => m.missionNpcId == objective.npcId);
            }

            if (target != null)
            {
                client.messageQueue.addRpcMessage(
                    ServerPackets.BuildMissionWaypoint(target.getXPos(), target.getYPos(), target.getZPos()));
            }
        }

        private static Mob SpawnNpc(WorldClient client, MissionNpc npc)
        {
            Mob mob = new Mob();
            mob.setEntityId(WorldServer.entityIdCounter++);
            mob.setDistrict((ushort)client.playerData.getDistrictId());
            mob.setDistrictName(client.playerData.getDistrict());
            mob.setName(npc.handle);
            mob.setLevel(npc.level);
            mob.setHealthM(npc.maxHealth);
            mob.setHealthC(npc.maxHealth);
            mob.setMobId(npc.id);
            mob.setRsiHex(npc.rsiHex);
            mob.setXPos(npc.x);
            mob.setYPos(npc.y);
            mob.setZPos(npc.z);
            mob.xBase = npc.x;
            mob.yBase = npc.y;
            mob.zBase = npc.z;
            mob.setRotation(0);
            mob.setIsDead(false);
            mob.setIsLootable(false);
            mob.isFriendly = npc.friendly;
            mob.missionNpcId = npc.id;
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
