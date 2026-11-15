using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>Read-only picking evidence for the disposable Digger fixture; never changes an object or request.</summary>
    public static class MissionComputer1407Qa
    {
        public const uint SourceKey = 0x07f005a3;
        public static bool Enabled(string dev, string database, string optIn, ulong character, int mission, int phase, ushort sourceGoid, uint key) =>
            dev == "1" && optIn == "1" && database != null && database.StartsWith("mxotest_", StringComparison.Ordinal) &&
            database.Length > 8 && database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') &&
            character == 110 && mission == 3082 && phase == 0 && sourceGoid == 1407 && key == SourceKey;

        private static bool OptedIn(WorldClient client) => client?.playerData != null && Enabled(
            Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
            Environment.GetEnvironmentVariable("MXO_MISSION_COMPUTER_1407_QA"), client.playerData.getCharID(), 3082, 0, 1407, SourceKey);

        private static bool Scope(WorldClient client, MissionProgress progress) => progress != null && progress.objects.Values.Any(state =>
            state.goid == 3 && state.spot != null && Enabled(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable("MXO_MISSION_COMPUTER_1407_QA"), client.playerData.getCharID(),
                progress.mission.id, progress.phaseIndex, MissionSites.Goid(state.spot), state.spot.mxoStaticId));

        private static string Identity(MissionProgress progress, ClientView view)
        {
            if (view == null) return "resolved=none";
            var state = progress.objects.Values.FirstOrDefault(s => s.entity == view.entityId);
            var spot = state?.spot ?? view.staticObject;
            return "entity=" + view.entityId + " goid=" + view.GoID + " expectedSpawn=" + view.spawnId +
                " sourceGoid=" + (spot == null ? "none" : MissionSites.Goid(spot).ToString()) +
                " sourceKey=" + (spot == null ? "none" : spot.mxoStaticId.ToString("x8"));
        }

        public static void Spawn(WorldClient client, GameObject obj, ClientView view, byte[] creation)
        {
            if (!OptedIn(client)) return;
            var computer = obj as AttributeClass20;
            lock (client.playerData.missionSync)
            {
                var progress = client.playerData.missionProgress;
                if (computer == null) return;
                if (!Scope(client, progress) || !progress.objects.Values.Any(s => s.entity == view.entityId && s.goid == 3 &&
                    s.spot?.mxoStaticId == SourceKey && MissionSites.Goid(s.spot) == 1407)) return;
                Output.WriteDebugLog("[COMPUTER1407-QA] outgoing view=" + view.ViewID + " " + Identity(progress, view) +
                    " relatedStatic=" + obj.GetRelatedStaticObjId().ToString("x8") + " free=" + (obj.GetRelatedStaticObjId() == uint.MaxValue) +
                    " currentState=" + Convert.ToHexString(computer.CurrentState.getValue()) +
                    " missionKey=" + Convert.ToHexString(computer.MissionKey.getValue()) +
                    " useCount=" + Convert.ToHexString(computer.UseCount.getValue()) +
                    " position=" + Convert.ToHexString(computer.Position.getValue()) +
                    " orientation=" + Convert.ToHexString(computer.Orientation.getValue()) + " creation=" + Convert.ToHexString(creation));
            }
        }

        public static void Request(WorldClient client, string kind, ushort viewId, ushort spawn, int action, byte[] raw)
        {
            if (!OptedIn(client)) return;
            lock (client.playerData.missionSync)
            {
                var progress = client.playerData.missionProgress;
                if (!Scope(client, progress)) return;
                var view = client.viewMan.getViewById(viewId);
                Output.WriteDebugLog("[COMPUTER1407-QA] " + kind + " view=" + viewId + " receivedSpawn=" + spawn +
                    " action=" + action + " " + Identity(progress, view) + " raw=" + Convert.ToHexString(raw));
            }
        }
    }
}
