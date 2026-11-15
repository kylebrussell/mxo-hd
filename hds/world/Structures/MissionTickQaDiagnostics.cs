using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using hds.shared;

namespace hds
{
    // Opt-in observations only: no result from this helper is a gameplay authority.
    internal static class MissionTickQaDiagnostics
    {
        internal const int Limit = 512;
        private sealed class Scope
        {
            internal readonly WeakReference<WorldClient> Client;
            internal readonly MissionProgress Progress;
            internal readonly ClientData Owner;
            internal readonly int[] Counts;
            internal int Registered=-1,SnapshotCount;
            internal Scope(WorldClient c,MissionProgress p)
            {Client=new WeakReference<WorldClient>(c);Progress=p;Owner=c.playerData;Counts=new int[p.mission.phases.Count];}
        }
        private static readonly ConditionalWeakTable<MissionProgress,Scope> scopes=new ConditionalWeakTable<MissionProgress,Scope>();
        private static readonly ConcurrentQueue<WeakReference<Scope>> active=new ConcurrentQueue<WeakReference<Scope>>();
        // Tests replace the output sink under a nonparallel collection; production has no alternate command path.
        internal static Action<string> Sink = Output.WriteDebugLog;
        internal static bool Allowed(string flag,string dev,string database,uint character,string name)
            =>flag=="1"&&dev=="1"&&database!=null&&database.StartsWith("mxotest_",StringComparison.Ordinal)&&database.Length>8&&
                System.Linq.Enumerable.All(database,c=>char.IsAsciiLetterOrDigit(c)||c=='_')&&character!=0&&character!=2&&
                !string.IsNullOrWhiteSpace(name)&&!string.Equals(name,"TheNeo",StringComparison.OrdinalIgnoreCase);
        private static bool Enabled(WorldClient c)
        {
            if(c?.playerData==null||c.playerInstance?.CharacterName==null||c.TransportFence==null||string.IsNullOrWhiteSpace(c.playerData.getUniqueKey("")))return false;
            return Allowed(Environment.GetEnvironmentVariable("MXO_MISSION_TICK_QA"),Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName,c.playerData.getCharID(),StringUtils.charBytesToString_NZ(c.playerInstance.CharacterName.getValue()));
        }
        internal static void Arm(WorldClient c,MissionProgress p)
        {
            try
            {
                if(!Enabled(c)||p==null||!ReferenceEquals(c.playerData.missionProgress,p)||!p.waitingForExit||!p.HasGeneratedDeparture)return;
                scopes.GetValue(p,_=>{var s=new Scope(c,p);active.Enqueue(new WeakReference<Scope>(s));return s;});
                Record(c,"departure-committed");
            }
            catch { } // Diagnostics must never change the result of a committed ride.
        }
        // Called only while the existing client dictionary lock is held; no output or new owner lock.
        internal static void CaptureRegistration(IReadOnlyDictionary<string,WorldClient> snapshot)
        {
            try
            {
                foreach(var weak in active)
                    if(weak.TryGetTarget(out var s)&&s.Client.TryGetTarget(out var c)&&Enabled(c))
                    {
                        bool same=snapshot.TryGetValue(c.playerData.getUniqueKey(""),out var current)&&ReferenceEquals(current,c);
                        Volatile.Write(ref s.SnapshotCount,snapshot.Count);Volatile.Write(ref s.Registered,same?1:0);
                    }
            }
            catch { }
        }
        internal static void Active(string stage,IReadOnlyDictionary<string,WorldClient> snapshot=null)
        {
            try
            {
                if(snapshot!=null)CaptureRegistration(snapshot);
                foreach(var weak in active)
                    if(weak.TryGetTarget(out var s)&&s.Client.TryGetTarget(out var c))
                    {
                        int registered=Volatile.Read(ref s.Registered);
                        Record(c,stage,registered<0?(bool?)null:registered==1,Volatile.Read(ref s.SnapshotCount));
                    }
            }
            catch { }
        }
        // Identity wrapper preserves the original predicate's short circuit and evaluates each field once.
        internal static bool Gate(WorldClient c,string name,bool value)
        {Record(c,"tick-gate-value",detail:name+"="+value,alive:name=="alive"?(bool?)value:null);return value;}
        internal static void Record(WorldClient c,string stage,bool? registered=null,int? snapshotCount=null,string detail=null,bool? alive=null)
        {
            try
            {
                if(!Enabled(c))return;
                var d=c.playerData;var p=d.missionProgress;
                if(p==null||!scopes.TryGetValue(p,out var s)||!s.Client.TryGetTarget(out var armed)||!ReferenceEquals(armed,c)||
                    !ReferenceEquals(s.Owner,d)||!p.waitingForExit||!p.HasGeneratedDeparture||p.phaseIndex<0||p.phaseIndex>=s.Counts.Length)return;
                int ordinal=Interlocked.Increment(ref s.Counts[p.phaseIndex]);if(ordinal>Limit)return;
                var transport=c.TransportFence?.DiagnosticObservation;
                Sink("[MISSION-TICK-QA] "+JsonSerializer.Serialize(new
                {
                    utc=DateTime.UtcNow.ToString("o"),monotonicTicks=System.Diagnostics.Stopwatch.GetTimestamp(),tickFrequency=System.Diagnostics.Stopwatch.Frequency,thread=Environment.CurrentManagedThreadId,stage,ordinal,
                    database=Store.config?.dbParams?.DatabaseName,endpoint=d.getUniqueKey(""),client=RuntimeHelpers.GetHashCode(c),owner=RuntimeHelpers.GetHashCode(d),
                    registered,snapshotCount,registrationObservation="last TickAll snapshot; not authority",alive,character=d.getCharID(),world=d.worldGeneration,life=d.lifeGeneration,
                    onWorld=d.getOnWorld(),worldChangePending=d.worldChangePending,shutdown=d.waitForRPCShutDown,
                    district=d.getDistrictId(),activeMission=d.activeMission?.id,mission=p.mission.id,phase=p.phaseIndex,
                    site=p.site?.elevatorDoor?.mxoStaticId,waiting=p.waitingForExit,departed=p.HasGeneratedDeparture,
                    restoreRefusal=p.restoreRefusal,transportEpoch=transport?.Epoch,transportResetPending=transport?.ResetPending,
                    transportObservation="non-atomic; not authority",detail
                }));
            }
            catch { }
        }
    }
}
