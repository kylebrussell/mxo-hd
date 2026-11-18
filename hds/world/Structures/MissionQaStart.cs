using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using hds.shared;

namespace hds
{
    // Direct phase starts are fixture setup. They do not replay or earn the earlier phases.
    internal sealed class MissionQaStart
    {
        internal const string EnableKey = "MXO_QA_MISSION_PHASE_COMMAND";
        internal const string ReadyPrefix = "QA READY mission ";
        private readonly HashSet<uint> items = new HashSet<uint>();
        private readonly HashSet<ushort> carried = new HashSet<ushort>();

        internal static void Dispatch(WorldClient client, string[] words)
        {
            var data = client.playerData;
            string handle = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
            string Text(string message)
            {
                client.messageQueue.addRpcMessage(ServerPackets.BuildSystemChatMessage(message, "BROADCAST"));
                client.FlushQueue();
                return message;
            }

            if (Environment.GetEnvironmentVariable(EnableKey) != "1" || data.getCharID() != 110 ||
                !string.Equals(handle, "CSR_Tester", StringComparison.OrdinalIgnoreCase) ||
                !MissionPersistence.DevAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                    Store.config?.dbParams?.DatabaseName, data.getCharID(), client.Alive, data.getOnWorld(), data.isDead, handle) ||
                data.worldChangePending || data.waitForRPCShutDown || data.DistrictIsLA())
            {
                Text("QA phase start is available only on the disposable catalog fixture while alive in the world.");
                return;
            }

            if (words.Length != 3 || !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                !int.TryParse(words[2], NumberStyles.None, CultureInfo.InvariantCulture, out int area))
            {
                Text("Usage: ?missionphase <mission id> <one-based area>");
                return;
            }

            var mission = DataLoader.getInstance().missions.Find(m => m.id == id);
            if (mission == null || area < 1 || area > mission.phases.Count)
            {
                Text("QA phase start refused: choose a catalog mission and a valid one-based area.");
                return;
            }

            lock (data.missionSync)
            {
                if (data.activeMission != null || TutorialMission.Of(client) != null ||
                    CombatManager.GetHandler(client).isCombatRunning)
                {
                    Text("QA phase start refused: finish or abort the current mission and leave combat first.");
                    return;
                }
                if (data.getDistrictId() != mission.district)
                {
                    Text($"QA phase start refused: start this sample in district {mission.district}.");
                    return;
                }
                if (mission.entryItem != 0)
                {
                    Text("QA phase start refused: ticket missions use their ordinary ticket path.");
                    return;
                }

                Position(client, out double x, out double y, out double z);
                string hostile = AmbientHostileInRange(client, x, y, z);
                if (hostile != null)
                {
                    Text($"QA phase start refused: ambient hostile {hostile} is in range.");
                    return;
                }
                var progress = new MissionProgress(mission);
                for (int i = 1; i < area; i++) progress.NextPhase();
                var setup = new MissionQaStart(progress);
                progress.qaStart = setup;
                if (!MissionHandler.AcceptQa(client, progress))
                {
                    Text($"QA phase start refused for mission {id}, area {area}. Check the placement diagnostics.");
                    return;
                }
                Output.WriteDebugLog(Text($"{ReadyPrefix}{id} area {area}. Contact bypassed, {area - 1} earlier areas skipped, " +
                    $"{setup.items.Count} carried items and {setup.carried.Count} followers staged for QA."));
            }
        }

        private MissionQaStart(MissionProgress progress)
        {
            foreach (var goal in progress.mission.phases.Take(progress.phaseIndex)
                .SelectMany(p => p.missionObjectives).Where(goal => !goal.optional))
            {
                // This only reconstructs carry-over. Goals in the selected area remain open.
                switch (goal.type)
                {
                    case MissionGoalType.Get:
                    case MissionGoalType.Search:
                    case MissionGoalType.Loot:
                        if (goal.item != 0) items.Add(goal.item);
                        break;
                    case MissionGoalType.Give:
                    case MissionGoalType.Put:
                    case MissionGoalType.Upload:
                        items.Remove(goal.item);
                        break;
                    case MissionGoalType.Escort:
                        if (goal.escortTo == "exit") carried.UnionWith(goal.Npcs);
                        else carried.ExceptWith(goal.Npcs);
                        break;
                    case MissionGoalType.Defeat:
                        carried.ExceptWith(goal.Npcs);
                        break;
                }
            }
        }

        internal void RequireStaged(WorldClient client, MissionProgress progress)
        {
            var phase = progress.Phase;
            if (phase.site != null && (progress.site == null || progress.waypoints.Count == 0 || progress.exits.Count == 0))
                throw new MissionCheckpointException("QA phase is missing its generated site or elevator");
            foreach (var prop in phase.objects)
                if (!progress.objects.TryGetValue(prop.id, out var obj) || obj.goid == 0 || obj.spot == null)
                    throw new MissionCheckpointException($"QA phase is missing required prop {prop.id}");

            Position(client, out double x, out double y, out double z);
            int index = 0;
            foreach (ushort id in carried.OrderBy(id => id))
            {
                var definition = progress.mission.npcs.Find(n => n.id == id);
                var follower = MissionRuntime.FindNpc(client.playerData, id);
                if (follower == null && definition != null)
                {
                    var point = MissionRuntime.BesidePlayer(x, y, z, index++);
                    follower = MissionRuntime.SpawnNpc(client, progress.mission, definition, point[0], point[1], point[2],
                        level: MissionStaging.PlayerLevel(client), friendly: true);
                    MissionRuntime.AddMob(client, follower);
                }
                if (follower == null || follower.getIsDead() || follower.getHealthC() == 0 || !follower.isFriendly)
                    throw new MissionCheckpointException($"QA phase cannot stage carried follower {id}");
                progress.following.Add(id);
            }

            foreach (var npc in progress.mission.npcs.Where(n => (n.at == 0 || n.at == progress.phaseIndex + 1) && !n.arrival.Late))
            {
                var actor = MissionRuntime.FindNpc(client.playerData, npc.id);
                if (actor == null || (npc.dead ? !actor.getIsDead() : actor.getIsDead() || actor.getHealthC() == 0))
                    throw new MissionCheckpointException($"QA phase is missing required NPC {npc.handle}");
            }
            if (progress.pendingWavePlacements.Count != 0 || progress.wavesJustArrived.Count != 0)
                throw new MissionCheckpointException("QA phase is missing a required initial enemy group");

            var bag = Store.dbManager.WorldDbHandler.GetInventoryItems(client.playerData.getCharID()).Select(item => (int)item.Slot).ToArray();
            foreach (uint item in items.OrderBy(id => id))
            {
                ushort slot = MissionContainerClaim.FreeSlot(bag, progress.itemSlots.Values);
                if (slot == 0) throw new MissionCheckpointException("QA phase needs free backpack slots for carried mission items");
                progress.itemSlots.Add(item, slot);
            }
        }

        private static void Position(WorldClient client, out double x, out double y, out double z)
        {
            x = y = z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
        }

        private static string AmbientHostileInRange(WorldClient client, double x, double y, double z)
        {
            var data = client.playerData;
            int level = MissionStaging.PlayerLevel(client);
            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (Mob mob in WorldServer.mobs.OfType<Mob>())
                {
                    if (mob.missionInstance != 0 || mob.getDistrict() != data.getDistrictId() || !MobAggro.SameFloor(mob.getYPos(), y))
                        continue;
                    double dx = mob.getXPos() - x, dz = mob.getZPos() - z;
                    if (MobAggro.WouldAttack(mob, Math.Sqrt(dx * dx + dz * dz), level)) return mob.getName();
                }
            }
            return null;
        }
    }
}
