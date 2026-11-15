using System;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>Refresh restored goal widgets after OnEnterWorld; never advances mission state.</summary>
    internal static class MissionObjectiveReplay
    {
        // LoadingComplete is a client lifecycle marker, not proof that a UI frame has rendered.
        internal const double CompletionDelaySeconds = 0.5;
        private sealed class Pending
        {
            internal MissionProgress Progress;
            internal int Phase, World, Life;
            internal bool Started;
        }
        private static readonly ConditionalWeakTable<WorldClient,Pending> pending = new ConditionalWeakTable<WorldClient,Pending>();

        internal static void Arm(WorldClient client, MissionProgress progress)
        {
            lock(client.playerData.missionSync)
            {
                pending.Remove(client);
                pending.Add(client,new Pending { Progress=progress, Phase=progress.phaseIndex,
                    World=client.playerData.worldGeneration, Life=client.playerData.lifeGeneration });
            }
        }
        private static bool Current(WorldClient client, Pending replay)
        {
            var data=client.playerData;
            return pending.TryGetValue(client,out Pending current) && ReferenceEquals(current,replay) &&
                data.getOnWorld() && !data.worldChangePending && !data.isDead &&
                data.worldGeneration==replay.World && data.lifeGeneration==replay.Life &&
                ReferenceEquals(data.missionProgress,replay.Progress) && replay.Progress.phaseIndex==replay.Phase &&
                data.getDistrictId()==replay.Progress.mission.district;
        }
        // A separate completion stage permits a flush and client UI work between the two batches.
        internal static Func<bool> Begin(WorldClient client, byte[] payload)
        {
            if(payload==null || payload.Length!=1 || payload[0]!=1)return null;
            lock(client.playerData.missionSync)
            {
                if(!pending.TryGetValue(client,out Pending replay) || replay.Started)return null;
                if(!Current(client,replay)){pending.Remove(client);return null;}
                replay.Started=true;
                MissionRuntime.SendObjectiveRows(client,replay.Progress,true);
                return () =>
                {
                    lock(client.playerData.missionSync)
                    {
                        if(!Current(client,replay))return false;
                        MissionRuntime.SendObjectiveCompletions(client,replay.Progress);
                        return true;
                    }
                };
            }
        }
        internal static void LoadingComplete(WorldClient client, byte[] payload)
        {
            Func<bool> complete=Begin(client,payload);
            if(complete==null)return;
            client.FlushQueue(force:true);
            DelayedActions.AfterFor(client,CompletionDelaySeconds,()=>
            {
                if(complete())client.FlushQueue();
            });
        }
    }
}
