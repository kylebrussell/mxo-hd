using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.Json;
using hds.shared;
using hds.resources.gameobjects;
namespace hds
{
    /// <summary>Disposable current-mission render experiment. Never a mission object or loot executor.</summary>
    public static class MissionPropProbe
    {
        public const string EnvironmentKey="MXO_MISSION_PROP_QA";
        private sealed class Origin
        {
            internal ClientData Data;internal MessageQueue Queue;internal PlayerCharacter Player;internal ViewManager Views;internal MissionProgress Progress;internal MissionObjectState State;
            internal ClientView View;internal uint Spawn;internal ushort Instance;internal int World,Life;internal long Epoch,Started;internal string Identity;
            internal byte[] Position,Quaternion,Key,UseCount;internal uint CurrentState;internal string RawHash;internal MissionOpenFloorPlacement Placement;
        }
        private sealed class Lease {internal Origin Origin;internal ClientView View;internal uint Spawn;internal ushort Goid;}
        private sealed class Holder {internal Origin Original;internal Lease Active;}
        private static readonly ConditionalWeakTable<WorldClient,Holder> holders=new ConditionalWeakTable<WorldClient,Holder>();
        private static long entity=989004000000;
        public static bool Allowed(string dev,string mode,string database,uint character,bool ready)=>
            mode=="1"&&character==110&&QaLoadout.Allowed(dev,database,character,ready,false);
        public static bool Asset(ushort goid)=>goid==3||goid==548||goid==552;
        public static bool Height(int delta)=>delta==0||delta==25||delta==50||delta==75||delta==95||delta==100;
        private static bool Ready(WorldClient c)=>c!=null&&c.Alive&&c.playerData!=null&&c.playerInstance!=null&&c.messageQueue!=null&&c.viewMan!=null&&
            c.playerData.getOnWorld()&&!c.playerData.DistrictIsLA()&&c.playerData.getDistrictId()==1&&!c.playerData.worldChangePending&&!c.playerData.waitForRPCShutDown&&!c.playerData.isDead&&!c.playerData.duelDeath;
        private static bool Authorized(WorldClient c)=>Ready(c)&&Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Environment.GetEnvironmentVariable(EnvironmentKey),Store.config?.dbParams?.DatabaseName,c.playerData.getCharID(),true);
        private static bool Scope(WorldClient c,MissionProgress p,MissionObjectState s)=>p!=null&&p.mission.id==201201&&p.phaseIndex==1&&!p.failed&&p.restoreRefusal==null&&!p.waitingForExit&&
            ReferenceEquals(c.playerData.activeMission,p.mission)&&ReferenceEquals(c.playerData.missionProgress,p)&&s!=null&&s.def?.id==1&&s.goid==3&&s.entity!=0&&
            p.objects.TryGetValue(1,out var owned)&&ReferenceEquals(owned,s)&&s.OpenFloor!=null&&s.OpenFloor.Mission==201201&&s.OpenFloor.Phase==1&&s.OpenFloor.Basic(s.spot,3,1)&&
            MissionSites.Goid(s.spot)!=3&&!s.lootOpen&&!s.putOpen&&p.OpenObjectives().Any(o=>o.type==MissionGoalType.Search&&o.objectId==1&&o.item==6783);
        private static bool World(WorldClient c,Origin o)=>ReferenceEquals(c.playerData,o.Data)&&ReferenceEquals(c.messageQueue,o.Queue)&&ReferenceEquals(c.playerInstance,o.Player)&&ReferenceEquals(c.viewMan,o.Views)&&c.playerData.worldGeneration==o.World&&c.TransportFence.IsCurrent(o.Epoch);
        private static bool OriginalCurrent(WorldClient c,Origin o)=>Authorized(c)&&World(c,o)&&c.playerData.lifeGeneration==o.Life&&c.playerData.missionInstanceCounter==o.Instance&&Scope(c,o.Progress,o.State)&&
            o.Progress.persistenceId==o.Identity&&o.Progress.startedAt==o.Started&&ReferenceEquals(o.State.OpenFloor,o.Placement)&&MissionOpenFloorPlacement.RawHash(o.State.spot)==o.RawHash&&
            o.State.shownState==o.CurrentState&&ReferenceEquals(c.viewMan.FindViewForEntity(o.State.entity),o.View)&&o.View.GoID==3&&o.View.spawnId==o.Spawn&&o.View.viewCreated&&!o.View.viewNeedsToBeDeleted;
        private static bool PrivateCurrent(WorldClient c,Lease l)=>l.View!=null&&ReferenceEquals(c.viewMan.getViewById(l.View.ViewID),l.View)&&l.View.spawnId==l.Spawn&&l.View.GoID==l.Goid&&!l.View.viewNeedsToBeDeleted;
        private static Lease Take(Holder h){lock(h){var l=h.Active;h.Active=null;return l;}}
        private static void Remove(WorldClient c,Lease l)
        {
            if(l==null)return;var o=l.Origin;
            c.TransportFence.TryRun(o.Epoch,()=>
            {
                if(!World(c,o)||!PrivateCurrent(c,l))return;
                if(l.View.viewCreated){var p=new PacketContent();p.AddUint16(1,1);p.AddByte(1);p.AddUint16(1,1);p.AddUint16(l.View.ViewID,1);c.messageQueue.addObjectMessage(p.ReturnFinalPacket(),false);}
                l.Origin.Views.removeViewByViewId(l.View.ViewID);
            });
            Output.WriteDebugLog("[MISSION-PROP] off char=110 view="+l.View?.ViewID+" spawn="+l.Spawn);
        }
        // Capture the literal original fields supplied to the real creation codec, not guessed defaults.
        public static void Spawn(WorldClient c,GameObject obj,ClientView view,byte[] creation)
        {
            if(!Authorized(c)||!(obj is AttributeClass20 computer)||obj.GetRelatedStaticObjId()!=uint.MaxValue)return;
            var data=c.playerData;lock(data.missionSync)
            {
                if(!ReferenceEquals(c.playerData,data))return;var p=data.missionProgress;var s=p?.objects.Values.SingleOrDefault(x=>x.entity==view.entityId);
                if(!Scope(c,p,s)||view.GoID!=3||!ReferenceEquals(c.viewMan.FindViewForEntity(s.entity),view))return;
                var o=new Origin{Data=data,Queue=c.messageQueue,Player=c.playerInstance,Views=c.viewMan,Progress=p,State=s,View=view,Spawn=view.spawnId,Instance=data.missionInstanceCounter,World=data.worldGeneration,Life=data.lifeGeneration,Epoch=c.TransportFence.Capture(),Started=p.startedAt,Identity=p.persistenceId,
                    Position=(byte[])computer.Position.getValue().Clone(),Quaternion=(byte[])computer.Orientation.getValue().Clone(),Key=(byte[])computer.MissionKey.getValue().Clone(),UseCount=(byte[])computer.UseCount.getValue().Clone(),CurrentState=BitConverter.ToUInt32(computer.CurrentState.getValue(),0),RawHash=MissionOpenFloorPlacement.RawHash(s.spot),Placement=s.OpenFloor};
                var h=holders.GetOrCreateValue(c);var old=Take(h);Remove(c,old);lock(h)h.Original=o;
                double x=BitConverter.ToDouble(o.Position,0),y=BitConverter.ToDouble(o.Position,8),z=BitConverter.ToDouble(o.Position,16);
                MissionOpenFloorProfiles.TryModel(3,out var model);double? navFloor=null;
                if(NavMesh.TryOpenFloorSurfaces(1,new[]{o.Placement.Surface[0]},out var surfaces,out _))
                {var id=o.Placement.Surface;var surface=surfaces.SingleOrDefault(a=>a.Identity.Sector==id[0]&&a.Identity.Region==id[1]&&a.Identity.Polygon==id[2]);if(surface!=null)navFloor=surface.FloorX*x+surface.FloorZ*z+surface.FloorC;}
                Output.WriteDebugLog("[MISSION-PROP] original "+JsonSerializer.Serialize(new{Mission=p.mission.id,Phase=p.phaseIndex,Identity=o.Identity,Started=o.Started,Object=1,Entity=s.entity,Source=s.spot.mxoStaticId.ToString("x8"),SourceGoid=MissionSites.Goid(s.spot),view.ViewID,view.spawnId,Position=new[]{x,y,z},Quaternion=Convert.ToHexString(o.Quaternion),CurrentState=o.CurrentState,MissionKey=Convert.ToHexString(o.Key),UseCount=Convert.ToHexString(o.UseCount),SupportFloor=y+model.SupportMinY,NavFloor=navFloor,Surface=o.Placement.Surface,Standing=o.Placement.Standing,RawSource=MissionOpenFloorPlacement.RawHash(s.spot),Creation=Convert.ToHexString(creation),Scope="Queued original creation observation; not renderer readiness"}));
            }
        }
        internal static GameObject Build(ushort goid,int delta,bool sameKey,byte[] originalPosition,byte[] quaternion,uint state,byte[] missionKey,byte[] useCount)
        {
            if(!Asset(goid)||!Height(delta)||originalPosition?.Length!=24||quaternion?.Length!=16||missionKey?.Length!=4||useCount?.Length!=1||
                Enumerable.Range(0,3).Any(i=>!double.IsFinite(BitConverter.ToDouble(originalPosition,i*8)))||Enumerable.Range(0,4).Any(i=>!float.IsFinite(BitConverter.ToSingle(quaternion,i*4))))throw new ArgumentException("Invalid current-mission diagnostic fields");
            var position=(byte[])originalPosition.Clone();
            if(goid!=3||delta!=0)
            {
                MissionOpenFloorProfiles.TryModel(3,out var computer);MissionOpenFloorProfiles.TryModel(goid,out var model);
                double originalY=BitConverter.ToDouble(position,8),y=goid==3?originalY:originalY+computer.SupportMinY-model.SupportMinY;
                Buffer.BlockCopy(BitConverter.GetBytes(y+delta),0,position,8,8);
            }
            GameObject obj=goid==3?(GameObject)new AttributeClass20("Object3",3):new AttributeClass46("Object"+goid,goid);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
            void Set(string name,byte[] bytes){var attribute=(Attribute)obj.GetType().GetField(name).GetValue(obj);attribute.enable();attribute.setValue((byte[])bytes.Clone());}
            Set("Position",position);Set("Orientation",quaternion);Set("MissionKey",sameKey?missionKey:new byte[4]);Set("UseCount",useCount);
            if(goid==3)Set("CurrentState",NumericalUtils.uint32ToByteArray(state,1));return obj;
        }
        internal static bool Activate(WorldClient c,bool sameKey,ushort goid,int delta)
        {
            if(!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||!Asset(goid)||!Height(delta)||MissionOutput.Buffered(c.messageQueue))return false;
            var data=c.playerData;lock(data.missionSync)
            {
                if(!ReferenceEquals(c.playerData,data)||!holders.TryGetValue(c,out var h))return false;Origin o;lock(h)o=h.Original;
                if(o==null||!o.Progress.entered||!OriginalCurrent(c,o))return false;Remove(c,Take(h));
                var l=new Lease{Origin=o,Goid=goid};bool allocated=false;
                c.TransportFence.TryRun(o.Epoch,()=>{if(!OriginalCurrent(c,o))return;l.View=c.viewMan.GetViewForEntityAndGo((ulong)Interlocked.Increment(ref entity),goid);l.Spawn=c.playerData.assignSpawnIdCounter();l.View.spawnId=l.Spawn;allocated=true;});
                if(!allocated)return false;lock(h)h.Active=l;
                var obj=Build(goid,delta,sameKey,o.Position,o.Quaternion,o.CurrentState,o.Key,o.UseCount);var pak=new PacketContent();pak.AddUint16(1,1);pak.AddByteArray(new ObjectManager().GenerateCreationPacket(obj,0,(byte)l.Spawn).getBytes());pak.AddUint16(l.View.ViewID,1);pak.AddByte(0);byte[] encoded=pak.ReturnFinalPacket();bool queued=false;
                c.TransportFence.TryRun(o.Epoch,()=>{bool selected;lock(h)selected=ReferenceEquals(h.Active,l);if(!selected||!OriginalCurrent(c,o)||!PrivateCurrent(c,l)||l.View.viewCreated)return;c.messageQueue.addObjectMessage(encoded,false);l.View.viewCreated=true;queued=true;});
                if(!queued){lock(h)if(ReferenceEquals(h.Active,l))h.Active=null;Remove(c,l);return false;}
                Output.WriteDebugLog("[MISSION-PROP] on char=110 view="+l.View.ViewID+" spawn="+l.Spawn+" goid="+goid+" key="+(sameKey?"same":"zero")+" delta="+delta+" originalView="+o.View.ViewID+" originalSpawn="+o.Spawn+" mission="+o.Progress.mission.id+" phase="+o.Progress.phaseIndex+" identity="+o.Identity+" diagnosticOnly=1");return true;
            }
        }
        internal static bool Parse(string[] words,out bool same,out ushort goid,out int delta,out bool off)
        {
            same=false;goid=0;delta=0;off=words.Length==2&&words[1]=="off";if(off)return true;
            return words.Length==4&&(words[1]=="same"||words[1]=="zero")&&((same=words[1]=="same")||words[1]=="zero")&&ushort.TryParse(words[2],out goid)&&Asset(goid)&&words[2]==goid.ToString(System.Globalization.CultureInfo.InvariantCulture)&&int.TryParse(words[3],out delta)&&Height(delta)&&words[3]==delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public static bool Dev(WorldClient c,string[] words)
        {
            if(words.Length==0||!words[0].Equals("?missionprop",StringComparison.OrdinalIgnoreCase))return false;
            if(!ReferenceEquals(Store.currentClient,c)||!Authorized(c)||!Parse(words,out bool same,out ushort goid,out int delta,out bool off)||MissionOutput.Buffered(c.messageQueue))
            {if(c!=null)new ServerPackets().sendSystemChatMessage(c,"Mission prop diagnostic refused; use ?missionprop <zero|same> <3|548|552> <0|25|50|75|95|100> or off before Take in its disposable CSR110 mode.","MODAL");return true;}
            if(off)Clear(c);else if(!Activate(c,same,goid,delta))new ServerPackets().sendSystemChatMessage(c,"No current original free GO3 mission view in scope.","MODAL");c.FlushQueue();return true;
        }
        public static void Check(WorldClient c)
        {
            if(c?.playerData==null||!holders.TryGetValue(c,out var h))return;var data=c.playerData;lock(data.missionSync)
            {Origin o;Lease l;lock(h){o=h.Original;l=h.Active;}bool valid=o!=null&&OriginalCurrent(c,o);if(!valid){lock(h){if(ReferenceEquals(h.Original,o))h.Original=null;if(ReferenceEquals(h.Active,l))h.Active=null;}Remove(c,l);}else if(l!=null&&!PrivateCurrent(c,l)){lock(h)if(ReferenceEquals(h.Active,l))h.Active=null;Remove(c,l);}}
        }
        public static void Clear(WorldClient c){if(c!=null&&holders.TryGetValue(c,out var h)){var l=Take(h);Remove(c,l);}}
        public static void ClearBeforeEpoch(WorldClient c,long epoch)
        {if(c==null||!holders.TryGetValue(c,out var h))return;Lease l=null;lock(h){if(h.Active?.Origin.Epoch<epoch){l=h.Active;h.Active=null;}if(h.Original?.Epoch<epoch)h.Original=null;}Remove(c,l);}
    }
}
