using System;
using System.Runtime.CompilerServices;
using System.Threading;
using hds.shared;
using hds.resources.gameobjects;
namespace hds
{
    /// <summary>Disposable asset/picking experiment only; never a mission object or loot executor.</summary>
    public static class SafeFloorProbe
    {
        public const string EnvironmentKey="MXO_SAFE_FLOOR_QA";
        public const string Identity="0000000000000000000000000000803f";
        private sealed class Lease
        {
            public ClientData Data;public ViewManager Views;public ClientView View;
            public uint Spawn;public int World,Life;public long Epoch;
        }
        private sealed class Holder { public Lease Active; }
        private static readonly ConditionalWeakTable<WorldClient,Holder> leases=new ConditionalWeakTable<WorldClient,Holder>();
        private static long nextEntity=989552000000;
        public static bool Allowed(string dev,string mode,string database,uint character,bool ready) =>
            mode=="1"&&character==110&&QaLoadout.Allowed(dev,database,character,ready,false);
        private static bool Ready(WorldClient c) => c!=null&&c.Alive&&c.playerData!=null&&c.messageQueue!=null&&c.viewMan!=null&&
            !c.playerData.waitForRPCShutDown&&!c.playerData.worldChangePending&&!c.playerData.isDead&&!c.playerData.duelDeath&&
            c.playerData.getOnWorld()&&!c.playerData.DistrictIsLA()&&c.playerData.getDistrictId()==1;
        private static bool Authorized(WorldClient c) => Ready(c)&&Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
            Environment.GetEnvironmentVariable(EnvironmentKey),Store.config?.dbParams?.DatabaseName,c.playerData.getCharID(),true);
        public static AttributeClass46 CreateObject()
        {
            var obj=new AttributeClass46("Object552",552);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
            Set(obj.Position,NumericalUtils.doublesToLtVector3d(25100,2050,14100));Set(obj.Orientation,StringUtils.hexStringToBytes(Identity));
            Set(obj.UseCount,new byte[]{1});Set(obj.MissionKey,new byte[4]);return obj;
        }
        private static void Set(Attribute a,byte[] value){a.enable();a.setValue(value);}
        private static bool SameWorld(WorldClient c,Lease l) => ReferenceEquals(c.playerData,l.Data)&&ReferenceEquals(c.viewMan,l.Views)&&
            c.playerData.worldGeneration==l.World&&c.TransportFence.Capture()==l.Epoch;
        private static bool SameView(WorldClient c,Lease l) => l.View!=null&&ReferenceEquals(c.viewMan.getViewById(l.View.ViewID),l.View)&&
            l.View.spawnId==l.Spawn&&l.View.GoID==552&&!l.View.viewNeedsToBeDeleted;
        internal static bool Activate(WorldClient c)
        {
            if(!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||MissionOutput.Buffered(c.messageQueue))return false;
            var holder=leases.GetOrCreateValue(c);
            lock(holder)
            {
                Remove(c,holder);
                var l=new Lease{Data=c.playerData,Views=c.viewMan,World=c.playerData.worldGeneration,Life=c.playerData.lifeGeneration,Epoch=c.TransportFence.Capture()};
                bool allocated=false;
                c.TransportFence.TryRun(l.Epoch,()=>
                {
                    if(!Ready(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life)return;
                    l.View=l.Views.GetViewForEntityAndGo((ulong)Interlocked.Increment(ref nextEntity),552);
                    l.Spawn=c.playerData.assignSpawnIdCounter();l.View.spawnId=l.Spawn;allocated=true;
                });
                if(!allocated)return false;
                holder.Active=l;
                // Build the complete native creation outside the transport gate; no Flush or callbacks inside it.
                var packet=new PacketContent();packet.AddUint16(1,1);
                packet.AddByteArray(new ObjectManager().GenerateCreationPacket(CreateObject(),0,(byte)l.Spawn).getBytes());
                packet.AddUint16(l.View.ViewID,1);packet.AddByte(0);byte[] encoded=packet.ReturnFinalPacket();bool queued=false;
                c.TransportFence.TryRun(l.Epoch,()=>
                {
                    if(!Ready(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life||!SameView(c,l)||l.View.viewCreated)return;
                    c.messageQueue.addObjectMessage(encoded,false);l.View.viewCreated=true;queued=true;
                });
                if(!queued){Remove(c,holder);return false;}
                Output.WriteDebugLog("[SAFE-FLOOR] on char=110 view="+l.View.ViewID+" spawn="+l.Spawn+" goid=552 free=1 position=25100,2050,14100 state=raw-default835 missionKey=0 diagnosticOnly=1");return true;
            }
        }
        public static bool Dev(WorldClient c,string[] words)
        {
            if(words.Length==0||!words[0].Equals("?safefloor",StringComparison.OrdinalIgnoreCase))return false;
            if(!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||words.Length!=2||(words[1]!="on"&&words[1]!="off")||MissionOutput.Buffered(c.messageQueue))
            {if(c!=null)new ServerPackets().sendSystemChatMessage(c,"Safe floor diagnostic refused; requires its disposable CSR110 mode and on/off.","MODAL");return true;}
            if(words[1]=="off")Clear(c);else Activate(c);
            c.FlushQueue();return true;
        }
        public static void Check(WorldClient c)
        {
            if(c?.playerData==null||!leases.TryGetValue(c,out var holder))return;
            lock(holder)
            {
                var l=holder.Active;if(l==null)return;
                if(!Authorized(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life||!SameView(c,l)||!l.View.viewCreated)Remove(c,holder);
            }
        }
        public static void ClearBeforeEpoch(WorldClient c,long postResetEpoch)
        {
            if(c==null||!leases.TryGetValue(c,out var holder))return;
            lock(holder)if(holder.Active!=null&&holder.Active.Epoch<postResetEpoch)Remove(c,holder);
        }
        public static void Clear(WorldClient c)
        {
            if(c==null||!leases.TryGetValue(c,out var holder))return;
            lock(holder)Remove(c,holder);
        }
        private static void Remove(WorldClient c,Holder holder)
        {
            var l=holder.Active;holder.Active=null;if(l==null)return;
            var packet=new PacketContent();packet.AddUint16(1,1);packet.AddByte(1);packet.AddUint16(1,1);packet.AddUint16(l.View.ViewID,1);byte[] encoded=packet.ReturnFinalPacket();
            c.TransportFence.TryRun(l.Epoch,()=>
            {
                if(!SameWorld(c,l)||!SameView(c,l))return;
                if(l.View.viewCreated)c.messageQueue.addObjectMessage(encoded,false);
                l.Views.removeViewByViewId(l.View.ViewID);
            });
            Output.WriteDebugLog("[SAFE-FLOOR] off char=110 view="+l.View.ViewID+" spawn="+l.Spawn);
        }
    }
}
