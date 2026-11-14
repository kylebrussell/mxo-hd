using System;
using System.Runtime.CompilerServices;
using System.Threading;
using hds.shared;
using hds.resources.gameobjects;
namespace hds
{
    /// <summary>Disposable asset/picking experiment only; never a mission object or loot executor.</summary>
    public static class PropSupportProbe
    {
        public const string EnvironmentKey="MXO_PROP_SUPPORT_QA";
        public const string Identity="00000000F30435B300000000FFFF7F3F";
        public const double X=-4242.57080078125,Y=1625.003776550293,Z=-5901.43408203125;
        public static bool Height(int delta)=>delta==0||delta==25||delta==50||delta==75||delta==100;
        private sealed class Lease
        {
            public ClientData Data;public ViewManager Views;public ClientView View;
            public uint Spawn;public ushort Goid;public int World,Life;public long Epoch;
        }
        private sealed class Holder { public Lease Active; }
        private static readonly ConditionalWeakTable<WorldClient,Holder> leases=new ConditionalWeakTable<WorldClient,Holder>();
        private static long nextEntity=989003000000;
        public static bool Allowed(string dev,string mode,string database,uint character,bool ready) =>
            mode=="1"&&character==110&&QaLoadout.Allowed(dev,database,character,ready,false);
        private static bool Ready(WorldClient c) => c!=null&&c.Alive&&c.playerData!=null&&c.messageQueue!=null&&c.viewMan!=null&&
            !c.playerData.waitForRPCShutDown&&!c.playerData.worldChangePending&&!c.playerData.isDead&&!c.playerData.duelDeath&&
            c.playerData.getOnWorld()&&!c.playerData.DistrictIsLA()&&c.playerData.getDistrictId()==1;
        private static bool Authorized(WorldClient c) => Ready(c)&&Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
            Environment.GetEnvironmentVariable(EnvironmentKey),Store.config?.dbParams?.DatabaseName,c.playerData.getCharID(),true);
        public static bool Asset(ushort goid)=>goid==3||goid==548||goid==552;
        public static double CentreY(ushort goid,int delta)
        {if(!Asset(goid)||!Height(delta))throw new ArgumentOutOfRangeException();return (goid==3?Y:goid==548?1640:1650)+delta;}
        public static AttributeClass20 CreateObject(int delta)=>(AttributeClass20)CreateObject(3,delta);
        public static GameObject CreateObject(ushort goid,int delta)
        {
            double y=CentreY(goid,delta);
            if(goid==3)
            {
                var obj=new AttributeClass20("Object3",3);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
                Set(obj.Position,NumericalUtils.doublesToLtVector3d(X,y,Z));Set(obj.Orientation,StringUtils.hexStringToBytes(Identity));
                Set(obj.CurrentState,NumericalUtils.uint32ToByteArray(0x961,1));Set(obj.UseCount,new byte[]{1});Set(obj.MissionKey,new byte[4]);return obj;
            }
            var container=new AttributeClass46("Object"+goid,goid);container.UnsetRelatedStaticObjId();container.DisableAllAttributes();
            Set(container.Position,NumericalUtils.doublesToLtVector3d(X,y,Z));Set(container.Orientation,StringUtils.hexStringToBytes(Identity));
            Set(container.UseCount,new byte[]{1});Set(container.MissionKey,new byte[4]);return container; // actual rawGOB Closed0x835 remains omitted
        }
        private static void Set(Attribute a,byte[] value){a.enable();a.setValue(value);}
        private static bool SameWorld(WorldClient c,Lease l) => ReferenceEquals(c.playerData,l.Data)&&ReferenceEquals(c.viewMan,l.Views)&&
            c.playerData.worldGeneration==l.World&&c.TransportFence.Capture()==l.Epoch;
        private static bool SameView(WorldClient c,Lease l) => l.View!=null&&ReferenceEquals(c.viewMan.getViewById(l.View.ViewID),l.View)&&
            l.View.spawnId==l.Spawn&&l.View.GoID==l.Goid&&!l.View.viewNeedsToBeDeleted;
        internal static bool Activate(WorldClient c,int delta)=>Activate(c,3,delta);
        internal static bool Activate(WorldClient c,ushort goid,int delta)
        {
            if(!Asset(goid)||!Height(delta)||!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||MissionOutput.Buffered(c.messageQueue))return false;
            var holder=leases.GetOrCreateValue(c);
            lock(holder)
            {
                Remove(c,holder);
                var l=new Lease{Goid=goid,Data=c.playerData,Views=c.viewMan,World=c.playerData.worldGeneration,Life=c.playerData.lifeGeneration,Epoch=c.TransportFence.Capture()};
                bool allocated=false;
                c.TransportFence.TryRun(l.Epoch,()=>
                {
                    if(!Ready(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life)return;
                    l.View=l.Views.GetViewForEntityAndGo((ulong)Interlocked.Increment(ref nextEntity),goid);
                    l.Spawn=c.playerData.assignSpawnIdCounter();l.View.spawnId=l.Spawn;allocated=true;
                });
                if(!allocated)return false;
                holder.Active=l;
                // Build the complete native creation outside the transport gate; no Flush or callbacks inside it.
                var packet=new PacketContent();packet.AddUint16(1,1);
                packet.AddByteArray(new ObjectManager().GenerateCreationPacket(CreateObject(goid,delta),0,(byte)l.Spawn).getBytes());
                packet.AddUint16(l.View.ViewID,1);packet.AddByte(0);byte[] encoded=packet.ReturnFinalPacket();bool queued=false;
                c.TransportFence.TryRun(l.Epoch,()=>
                {
                    if(!Ready(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life||!SameView(c,l)||l.View.viewCreated)return;
                    c.messageQueue.addObjectMessage(encoded,false);l.View.viewCreated=true;queued=true;
                });
                if(!queued){Remove(c,holder);return false;}
                Output.WriteDebugLog("[PROP-SUPPORT] on char=110 view="+l.View.ViewID+" spawn="+l.Spawn+" goid="+goid+" free=1 position="+X+","+CentreY(goid,delta)+","+Z+" delta="+delta+" state="+(goid==3?"0x961":"raw-default0x835")+" missionKey=0 diagnosticOnly=1");return true;
            }
        }
        internal static bool Parse(string[] words,out ushort goid,out int delta,out bool off)
        {
            goid=3;delta=0;off=words.Length==2&&words[1]=="off";
            if(off)return true;
            if(words.Length!=2&&words.Length!=3)return false;
            if(words.Length==3&&(!ushort.TryParse(words[1],out goid)||!Asset(goid)||words[1]!=goid.ToString(System.Globalization.CultureInfo.InvariantCulture)))return false;
            string value=words[words.Length-1];return int.TryParse(value,out delta)&&Height(delta)&&value==delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public static bool Dev(WorldClient c,string[] words)
        {
            if(words.Length==0||!words[0].Equals("?propsupport",StringComparison.OrdinalIgnoreCase))return false;
            if(!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||!Parse(words,out ushort goid,out int delta,out bool off)||MissionOutput.Buffered(c.messageQueue))
            {if(c!=null)new ServerPackets().sendSystemChatMessage(c,"Prop support diagnostic refused; use ?propsupport <3|548|552> <0|25|50|75|100> or off in its disposable CSR110 mode.","MODAL");return true;}
            if(off)Clear(c);else Activate(c,goid,delta);
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
            Output.WriteDebugLog("[PROP-SUPPORT] off char=110 view="+l.View.ViewID+" spawn="+l.Spawn);
        }
    }
}
