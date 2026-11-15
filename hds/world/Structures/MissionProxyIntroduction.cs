using System;
using System.Runtime.CompilerServices;
namespace hds
{
    // Candidate presentation policy: transport receipt/current client interest, NOT room-ready proof.
    internal static class MissionProxyIntroduction
    {
        private sealed class Owner
        {
            internal readonly WorldClient Client; internal readonly ClientData Data;
            internal readonly PlayerCharacter Player; internal readonly MessageQueue Queue;
            internal readonly int World,Life; internal readonly uint Character,District; internal readonly long Epoch;
            internal Owner(WorldClient c){Client=c;Data=c.playerData;Player=c.playerInstance;Queue=c.messageQueue;
                World=Data.worldGeneration;Life=Data.lifeGeneration;Character=Data.getCharID();District=Data.getDistrictId();Epoch=c.TransportFence.Capture();}
            internal bool Current()=>Client.Alive&&ReferenceEquals(Client.playerData,Data)&&ReferenceEquals(Client.playerInstance,Player)&&
                ReferenceEquals(Client.messageQueue,Queue)&&Character!=0&&Data.getCharID()==Character&&Data.getDistrictId()==District&&
                Data.worldGeneration==World&&Data.lifeGeneration==Life&&Data.getOnWorld()&&!Data.isDead&&!Data.duelDeath&&
                !Data.worldChangePending&&!Data.waitForRPCShutDown&&Client.TransportFence.Capture()==Epoch;
        }
        private sealed class Observation
        {
            internal Owner Owner;internal NavPoint Position;internal ObjectMessageReceipt Receipt;
            internal bool Current()=>Owner.Current()&&(Receipt==null||Receipt.Received);
        }
        private sealed class Holder {internal readonly object Sync=new object();internal Observation Arrival,PendingArrival,Position;}
        private static readonly ConditionalWeakTable<WorldClient,Holder> observations=new ConditionalWeakTable<WorldClient,Holder>();
        [ThreadStatic] private static Owner incoming;
        private sealed class IncomingScope:IDisposable
        {
            private readonly Owner previous;
            internal IncomingScope(WorldClient c,bool accepted){previous=incoming;incoming=accepted?new Owner(c):null;}
            public void Dispose(){incoming=previous;}
        }
        internal static IDisposable Incoming(WorldClient c,bool accepted)=>new IncomingScope(c,accepted);
        internal static void AcceptedPosition(WorldClient c,double x,double y,double z)
        {
            var owner=incoming;if(owner==null||!ReferenceEquals(owner.Client,c)||!owner.Current()||
                !NavMesh.Locate((ushort)owner.District,x,y,z,out _))return;
            var holder=observations.GetOrCreateValue(c);
            lock(holder.Sync)holder.Position=new Observation {Owner=owner,Position=new NavPoint {x=x,y=y,z=z}};
        }
        // Explicit ordinary arrival producer; no global packet-shape detection or byte[] identity registry.
        internal static void Arrival(WorldClient c,double x,double y,double z)
        {
            var owner=new Owner(c);var receipt=new ObjectMessageReceipt();
            var value=new Observation {Owner=owner,Position=new NavPoint {x=(float)x,y=(float)y,z=(float)z},Receipt=receipt};
            var holder=observations.GetOrCreateValue(c);Observation old;
            // Do not replace prior interest evidence until this committed message is received.
            // A rolled-back batch cancels only this pending receipt, preserving the prior observation.
            lock(holder.Sync)
            {
                if(holder.PendingArrival?.Receipt.Received==true)holder.Arrival=holder.PendingArrival;
                old=holder.PendingArrival;holder.PendingArrival=value;
                if(ReferenceEquals(old,holder.Arrival))old=null;
            }
            old?.Receipt?.Cancel();
            c.messageQueue.AddTrackedObject(PacketsUtils.createTeleportPacket(c,(float)x,(float)y,(float)z),receipt);
        }
        internal static bool Deferred(MissionObjectState state)=>state?.OpenFloor!=null&&
            state.OpenFloor.SourceGoid!=state.OpenFloor.ConcreteGoid&&MissionSites.Goid(state.spot)!=state.goid;
        internal static bool Ready(WorldClient c,MissionProgress progress,MissionObjectState state)
        {
            if(!Deferred(state)||progress==null||progress.failed||progress.restoreRefusal!=null||
                !ReferenceEquals(c.playerData.missionProgress,progress)||!ReferenceEquals(c.playerData.activeMission,progress.mission)||
                c.playerData.getDistrictId()!=progress.mission.district||state.OpenFloor.World!=c.playerData.getDistrictId()||
                progress.site==null||!progress.objects.TryGetValue(state.def.id,out var current)||!ReferenceEquals(current,state)||
                state.OpenFloor.Mission!=progress.mission.id||state.OpenFloor.Phase!=progress.phaseIndex||
                state.entity!=MissionStaging.EntityBase+(ulong)c.playerData.missionInstanceCounter*1000UL+(ulong)(progress.phaseIndex*50+state.def.id)||
                c.viewMan.FindViewForEntity(state.entity)!=null||!MissionOutput.Buffered(c.messageQueue)||
                !observations.TryGetValue(c,out var holder))return false;
            Observation arrival,position;
            lock(holder.Sync)
            {
                if(holder.PendingArrival?.Receipt.Received==true)
                {holder.Arrival=holder.PendingArrival;holder.PendingArrival=null;}
                arrival=holder.Arrival;position=holder.Position;
            }
            double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(),ref x,ref y,ref z);
            if(!MissionInteriorSites.Contains(progress.site,new NavPoint {x=x,y=y,z=z}))return false;
            bool Matches(Observation at)=>at!=null&&at.Current()&&MissionInteriorSites.Contains(progress.site,at.Position);
            var selected=Matches(arrival)?arrival:Matches(position)?position:null;
            return selected!=null&&selected.Owner.Current()&&
                MissionOutput.BindCurrent(c.messageQueue,c.TransportFence,selected.Owner.Epoch,selected.Owner.World);
        }
    }
}
