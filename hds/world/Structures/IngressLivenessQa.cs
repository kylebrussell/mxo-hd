using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using hds.shared;
namespace hds
{
    /// <summary>Explicit disposable-only proof of ingress while this one endpoint's real worker is blocked.</summary>
    internal static class IngressLivenessQa
    {
        private sealed class Use {internal bool Taken;}
        private static readonly ConditionalWeakTable<WorldClient,Use> uses=new ConditionalWeakTable<WorldClient,Use>();
        internal static bool Allowed(string dev,string database,string feature,uint character,bool onWorld,bool dead) =>
            feature=="1"&&QaLoadout.Allowed(dev,database,character,onWorld,dead)&&character==110;
        internal static bool Arm(WorldClient c)
        {
            if(c==null||c.IsBot||!ReferenceEquals(Store.currentClient,c)||!c.Alive||c.playerData==null||
                !c.playerData.getUDPSEssionEstablished()||c.playerData.DistrictIsLA()||c.playerData.worldChangePending||c.playerData.waitForRPCShutDown||
                MissionOutput.Buffered(c.messageQueue)||!Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                    Store.config?.dbParams?.DatabaseName,Environment.GetEnvironmentVariable("MXO_INGRESS_LIVENESS_QA"),
                    c.playerData.getCharID(),c.playerData.getOnWorld(),c.playerData.isDead))return false;
            lock(WorldServer.Clients)if(!WorldServer.Clients.Values.Any(x=>ReferenceEquals(x,c)))return false;
            var use=uses.GetOrCreateValue(c);lock(use){if(use.Taken)return false;use.Taken=true;return true;}
        }
        public static bool Dev(WorldClient c,string[] words)
        {
            if(words==null||words.Length==0||words[0]!="?ingresspause")return false;
            if(words.Length!=1||!Arm(c))
            {
                new ServerPackets().sendSystemChatMessage(c,"Ingress pause refused: one use, current CSR110/dev1/ownedmxotest/feature only.","SYSTEM");return true;
            }
            var owner=c.playerData;uint character=owner.getCharID(),district=owner.getDistrictId();int world=owner.worldGeneration,life=owner.lifeGeneration;long transport=c.TransportFence.Capture();
            var before=c.IngressSnapshot;
            new ServerPackets().sendSystemChatMessage(c,"INGRESS PAUSE START35","SYSTEM");c.FlushQueue(force:true);
            var watch=Stopwatch.StartNew();Thread.Sleep(35000); // Intentional, explicit QA delay of ONLY this packet worker.
            var after=c.IngressSnapshot;int pending=c.PendingPacketCount;
            bool current=ReferenceEquals(c.playerData,owner)&&owner.worldGeneration==world&&owner.lifeGeneration==life&&
                owner.getCharID()==character&&owner.getDistrictId()==district&&c.TransportFence.IsCurrent(transport)&&c.Alive&&owner.getOnWorld()&&!owner.worldChangePending&&!owner.waitForRPCShutDown;
            lock(WorldServer.Clients)current&=WorldServer.Clients.Values.Any(x=>ReferenceEquals(x,c));
            File.WriteAllText("IngressLivenessQa.json",JsonSerializer.Serialize(new{Character=character,District=district,Endpoint=owner.getUniqueKey(),World=world,Life=life,Transport=transport,
                Before=new{before.Last,before.Generation,before.Claimed},After=new{after.Last,after.Generation,after.Claimed},Pending=pending,
                CurrentOwner=current,ElapsedMs=watch.Elapsed.TotalMilliseconds,Scope="35s intentional worker delay; not normal cold-preparation latency"},new JsonSerializerOptions{WriteIndented=true}));
            Output.WriteDebugLog("[INGRESS-QA] resumed current="+current+" ingressDelta="+(after.Generation-before.Generation)+" pending="+pending);
            if(current){new ServerPackets().sendSystemChatMessage(c,"INGRESS PAUSE DONE35","SYSTEM");c.FlushQueue(force:true);}
            return true;
        }
    }
}
