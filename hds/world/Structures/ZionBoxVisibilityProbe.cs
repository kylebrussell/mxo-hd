using System;
using System.Runtime.CompilerServices;
using System.Threading;
using hds.shared;
using hds.resources.gameobjects;
namespace hds
{
    /// <summary>Disposable key0 duplicate visibility experiment; original mission object/executor/statics remain untouched.</summary>
    public static class ZionBoxVisibilityProbe
    {
        public const string EnvironmentKey="MXO_ZION_BOX_VISIBILITY_QA";
        public const string Identity="00000000F304353F00000000F204353F";
        public const double X=23814.611328125,Y=840,Z=14241.5712890625;
        public static bool South(int delta)=>delta==0||delta==-50||delta==-100||delta==-150||delta==-200||delta==-300;
        private sealed class Lease
        {
            public ClientData Data;public ViewManager Views;public ClientView View;
            public uint Spawn;public ushort Goid;public int World,Life;public long Epoch;
            public MissionOpenFloorPlacement Placement;public MissionProgress Progress;public MissionObjectState Original;public ClientView OriginalView;public uint OriginalSpawn;public ushort Instance;
        }
        private sealed class Holder { public Lease Active; }
        private static readonly ConditionalWeakTable<WorldClient,Holder> leases=new ConditionalWeakTable<WorldClient,Holder>();
        private static long nextEntity=989548100000;
        public static bool Allowed(string dev,string mode,string database,uint character,bool ready) =>
            mode=="1"&&character==110&&QaLoadout.Allowed(dev,database,character,ready,false);
        private static bool Ready(WorldClient c) => c!=null&&c.Alive&&c.playerData!=null&&c.messageQueue!=null&&c.viewMan!=null&&
            !c.playerData.waitForRPCShutDown&&!c.playerData.worldChangePending&&!c.playerData.isDead&&!c.playerData.duelDeath&&
            c.playerData.getOnWorld()&&!c.playerData.DistrictIsLA()&&c.playerData.getDistrictId()==1;
        private static bool Authorized(WorldClient c) => Ready(c)&&Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
            Environment.GetEnvironmentVariable(EnvironmentKey),Store.config?.dbParams?.DatabaseName,c.playerData.getCharID(),true);
        public static bool Asset(ushort goid)=>goid==548;
        public static AttributeClass46 CreateObject(int delta)
        {
            if(!South(delta))throw new ArgumentOutOfRangeException(nameof(delta));
            var container=new AttributeClass46("Object548",548);container.UnsetRelatedStaticObjId();container.DisableAllAttributes();
            Set(container.Position,NumericalUtils.doublesToLtVector3d(X,Y,Z+delta));Set(container.Orientation,StringUtils.hexStringToBytes(Identity));
            Set(container.UseCount,new byte[]{1});Set(container.MissionKey,new byte[4]);return container; // same rawGOB default0x835 as accepted unlocked Box
        }
        internal static bool Exact(MissionProgress p,out MissionObjectState obj)
        {
            obj=null;
            if(p==null||p.mission?.id!=101209||p.phaseIndex!=0||p.failed||p.waitingForExit||p.objects.Count!=1||!p.objects.TryGetValue(1,out obj)||
                obj.def==null||obj.def.id!=1||obj.def.locked||obj.goid!=548||obj.spot==null||obj.spot.mxoStaticId!=0x0d100397||
                obj.spot.pos_x!=24070||obj.spot.pos_y!=988||obj.spot.pos_z!=15112.5||MissionSites.Goid(obj.spot)!=1412||obj.shownState!=0)return false;
            var w=obj.OpenFloor;
            return w!=null&&w.Basic(obj.spot,548,1)&&w.Mission==101209&&w.Phase==0&&w.World==1&&w.Quaternion.Equals(Identity,StringComparison.OrdinalIgnoreCase)&&
                System.Linq.Enumerable.SequenceEqual(w.Centre,new[]{X,Y,Z});
        }
        private static bool MissionCurrent(WorldClient c,Lease l)
        {
            var p=c.playerData.missionProgress;
            return ReferenceEquals(p,l.Progress)&&c.playerData.missionInstanceCounter==l.Instance&&Exact(p,out var obj)&&ReferenceEquals(obj,l.Original)&&ReferenceEquals(obj.OpenFloor,l.Placement)&&ReferenceEquals(c.playerData.activeMission,p.mission)&&
                ReferenceEquals(c.viewMan.FindViewForEntity(obj.entity),l.OriginalView)&&l.OriginalView.spawnId==l.OriginalSpawn&&
                l.OriginalView.GoID==548&&l.OriginalView.viewCreated&&!l.OriginalView.viewNeedsToBeDeleted;
        }
        private static void Set(Attribute a,byte[] value){a.enable();a.setValue(value);}
        private static bool SameWorld(WorldClient c,Lease l) => ReferenceEquals(c.playerData,l.Data)&&ReferenceEquals(c.viewMan,l.Views)&&
            c.playerData.worldGeneration==l.World&&c.TransportFence.Capture()==l.Epoch;
        private static bool SameView(WorldClient c,Lease l) => l.View!=null&&ReferenceEquals(c.viewMan.getViewById(l.View.ViewID),l.View)&&
            l.View.spawnId==l.Spawn&&l.View.GoID==l.Goid&&!l.View.viewNeedsToBeDeleted;
        internal static bool Activate(WorldClient c,int delta)=>Activate(c,548,delta);
        internal static bool ActivateSame(WorldClient c)=>Activate(c,548,0,true);
        internal static bool Activate(WorldClient c,ushort goid,int delta,bool sameKey=false)
        {
            if((sameKey&&delta!=0)||!Asset(goid)||!South(delta)||!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||MissionOutput.Buffered(c.messageQueue))return false;
            if(!Exact(c.playerData.missionProgress,out var original))return false;
            var originalView=c.viewMan.FindViewForEntity(original.entity);
            if(originalView==null||originalView.GoID!=548||!originalView.viewCreated||originalView.viewNeedsToBeDeleted)return false;
            var holder=leases.GetOrCreateValue(c);
            lock(holder)
            {
                Remove(c,holder);
                var l=new Lease{Goid=goid,Data=c.playerData,Views=c.viewMan,World=c.playerData.worldGeneration,Life=c.playerData.lifeGeneration,Epoch=c.TransportFence.Capture(),Placement=original.OpenFloor,Progress=c.playerData.missionProgress,Original=original,OriginalView=originalView,OriginalSpawn=originalView.spawnId,Instance=c.playerData.missionInstanceCounter};
                bool allocated=false;
                c.TransportFence.TryRun(l.Epoch,()=>
                {
                    if(!Ready(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life||!MissionCurrent(c,l))return;
                    l.View=l.Views.GetViewForEntityAndGo((ulong)Interlocked.Increment(ref nextEntity),goid);
                    l.Spawn=c.playerData.assignSpawnIdCounter();l.View.spawnId=l.Spawn;allocated=true;
                });
                if(!allocated)return false;
                holder.Active=l;
                // Build the complete native creation outside the transport gate; no Flush or callbacks inside it.
                var packet=new PacketContent();packet.AddUint16(1,1);
                var duplicate=CreateObject(delta);
                if(sameKey)Set(duplicate.MissionKey,new byte[]{MissionStaging.ContainerElement,(byte)l.Instance,(byte)(l.Instance>>8),2});
                packet.AddByteArray(new ObjectManager().GenerateCreationPacket(duplicate,0,(byte)l.Spawn).getBytes());
                packet.AddUint16(l.View.ViewID,1);packet.AddByte(0);byte[] encoded=packet.ReturnFinalPacket();bool queued=false;
                c.TransportFence.TryRun(l.Epoch,()=>
                {
                    if(!Ready(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life||!MissionCurrent(c,l)||!SameView(c,l)||l.View.viewCreated)return;
                    c.messageQueue.addObjectMessage(encoded,false);l.View.viewCreated=true;queued=true;
                });
                if(!queued){Remove(c,holder);return false;}
                Output.WriteDebugLog("[ZION-BOX-VISIBILITY] on char=110 view="+l.View.ViewID+" spawn="+l.Spawn+" goid="+goid+" free=1 position="+X+","+Y+","+(Z+delta)+" delta="+delta+" state="+"raw-default0x835"+" missionKey="+Convert.ToHexString(duplicate.MissionKey.getValue())+" sameKey="+sameKey+" diagnosticOnly=1");return true;
            }
        }
        internal static bool Parse(string[] words,out ushort goid,out int delta,out bool off)=>Parse(words,out goid,out delta,out off,out _);
        internal static bool Parse(string[] words,out ushort goid,out int delta,out bool off,out bool sameKey)
        {
            goid=548;delta=0;sameKey=words.Length==2&&words[1]=="same";off=words.Length==2&&words[1]=="off";
            if(off||sameKey)return true;
            if(words.Length!=2)return false;
            string value=words[words.Length-1];return int.TryParse(value,out delta)&&South(delta)&&value==delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public static bool Dev(WorldClient c,string[] words)
        {
            if(words.Length==0||!words[0].Equals("?zionbox",StringComparison.OrdinalIgnoreCase))return false;
            if(!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||!Parse(words,out ushort goid,out int delta,out bool off,out bool sameKey)||MissionOutput.Buffered(c.messageQueue))
            {if(c!=null)new ServerPackets().sendSystemChatMessage(c,"Zion Box visibility diagnostic refused; use ?zionbox <same|0|-50|-100|-150|-200|-300> or off only with exact live101209 phase0 source0d100397 in its disposable CSR110 mode.","MODAL");return true;}
            if(off)Clear(c);else Activate(c,goid,delta,sameKey);
            c.FlushQueue();return true;
        }
        public static void Check(WorldClient c)
        {
            if(c?.playerData==null||!leases.TryGetValue(c,out var holder))return;
            lock(holder)
            {
                var l=holder.Active;if(l==null)return;
                if(!Authorized(c)||!SameWorld(c,l)||c.playerData.lifeGeneration!=l.Life||!MissionCurrent(c,l)||!SameView(c,l)||!l.View.viewCreated)Remove(c,holder);
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
            Output.WriteDebugLog("[ZION-BOX-VISIBILITY] off char=110 view="+l.View.ViewID+" spawn="+l.Spawn);
        }
    }
}
