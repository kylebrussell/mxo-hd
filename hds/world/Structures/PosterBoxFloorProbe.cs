using System;
using System.Runtime.CompilerServices;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>Disposable visual/picking experiment. Never a mission object or container executor.</summary>
    public static class PosterBoxFloorProbe
    {
        public const string EnvironmentKey = "MXO_POSTER_BOX_FLOOR_QA";
        public const string Identity = "0000000000000000000000000000803f";
        private sealed class Lease
        {
            public ClientData Data;
            public MissionProgress Progress;
            public MissionObjectState Source;
            public ViewManager Views;
            public ClientView SourceView, View;
            public uint SourceSpawn, Spawn;
            public int World, Life;
            public long Epoch;
        }
        private sealed class Holder { public Lease Active; }
        private static readonly ConditionalWeakTable<WorldClient, Holder> leases = new ConditionalWeakTable<WorldClient, Holder>();
        private static long nextEntity = 989548000000;
        public static bool Allowed(string dev, string mode, string database, uint character, bool ready) =>
            mode == "1" && character == 110 && QaLoadout.Allowed(dev,database,character,ready,false);
        public static bool SourceMatches(MissionObjectState state) =>
            state?.def?.id == 1 && state.goid == 548 && state.spot != null &&
            state.spot.metrId == 1 && state.spot.sectorID == 209 && state.spot.staticId == 988 &&
            state.spot.mxoStaticId == 0x0d1003dc && !state.spot.exterior &&
            state.spot.type != null && (state.spot.type.Length == 2 || state.spot.type.Length == 4) &&
            state.spot.type[0] == 0x87 && state.spot.type[1] == 5 &&
            (state.spot.type.Length == 2 || (state.spot.type[2] == 0 && state.spot.type[3] == 0)) &&
            state.spot.pos_x == 23940 && state.spot.pos_y == 2245 && state.spot.pos_z == 15050 &&
            string.Equals(state.spot.quat,"00000000f204353f00000000f304353f",StringComparison.OrdinalIgnoreCase);
        private static bool Ready(WorldClient client) => client != null && client.Alive && client.playerData != null &&
            Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Environment.GetEnvironmentVariable(EnvironmentKey),
                Store.config?.dbParams?.DatabaseName,client.playerData.getCharID(),client.playerData.getOnWorld()) &&
            !client.playerData.isDead && !client.playerData.duelDeath && !client.playerData.worldChangePending &&
            !client.playerData.waitForRPCShutDown && !client.playerData.DistrictIsLA() && client.playerData.getDistrictId() == 1;
        private static bool Context(WorldClient c, out MissionObjectState source, out ClientView view)
        {
            source=null;view=null;
            var p=c?.playerData?.missionProgress;
            if(!Ready(c) || c.playerData.activeMission?.id != 201202 || p?.mission?.id != 201202 ||
                !ReferenceEquals(p.mission,c.playerData.activeMission) || p.phaseIndex != 0 || !p.objects.TryGetValue(1,out source) || !SourceMatches(source))return false;
            view=c.viewMan.FindViewForEntity(source.entity);
            return SourceViewCurrent(view);
        }
        internal static bool SourceViewCurrent(ClientView view) => view != null && view.GoID == 548 && view.viewCreated && !view.viewNeedsToBeDeleted;
        public static AttributeClass46 CreateObject()
        {
            var obj=new AttributeClass46("Object548",548);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
            Set(obj.Position,NumericalUtils.doublesToLtVector3d(25100,2040,14100));
            Set(obj.Orientation,StringUtils.hexStringToBytes(Identity));Set(obj.CurrentState,BitConverter.GetBytes(0x835u));
            Set(obj.UseCount,new byte[]{1});Set(obj.MissionKey,new byte[4]);return obj;
        }
        private static void Set(Attribute attribute,byte[] value){attribute.enable();attribute.setValue(value);}
        public static bool Dev(WorldClient client,string[] words)
        {
            if(words.Length == 0 || !words[0].Equals("?posterboxfloor",StringComparison.OrdinalIgnoreCase))return false;
            if(client?.playerData == null)return true;
            lock(client.playerData.missionSync)
            {
                if(!ReferenceEquals(Store.currentClient,client) || !Ready(client) || words.Length != 2 ||
                    (words[1] != "on" && words[1] != "off"))
                {new ServerPackets().sendSystemChatMessage(client,"Floor Box diagnostic refused; requires its disposable CSR110 mode and on/off.","MODAL");return true;}
                var holder=leases.GetOrCreateValue(client);
                lock(holder)
                {
                    Remove(client,holder);
                    if(words[1] == "off")return true;
                    if(!Context(client,out var source,out var sourceView))return true;
                    var l=new Lease{Data=client.playerData,Progress=client.playerData.missionProgress,Source=source,
                        SourceView=sourceView,SourceSpawn=sourceView.spawnId,Views=client.viewMan,
                        World=client.playerData.worldGeneration,Life=client.playerData.lifeGeneration,Epoch=client.TransportFence.Capture()};
                    client.TransportFence.TryRun(l.Epoch,()=>
                    {
                        if(l.World!=client.playerData.worldGeneration || l.Life!=client.playerData.lifeGeneration ||
                            !ReferenceEquals(l.Progress,client.playerData.missionProgress) || !Context(client,out var liveSource,out var liveView) ||
                            !ReferenceEquals(liveSource,source) || !ReferenceEquals(liveView,sourceView) || liveView.spawnId!=l.SourceSpawn)return;
                        ulong entity=(ulong)Interlocked.Increment(ref nextEntity);
                        new ServerPackets().SendSpawnGameObject(client,CreateObject(),entity);
                        // The unique QA namespace is never a mission entity or a bound static.
                        l.View=client.viewMan.FindViewForEntity(entity);
                        if(l.View==null)throw new InvalidOperationException("Floor probe spawn was not registered");
                        l.Spawn=l.View.spawnId;holder.Active=l;
                        Output.WriteDebugLog("[POSTER-BOX-FLOOR] on char=110 view="+l.View.ViewID+" spawn="+l.Spawn+" goid=548 position=25100,2040,14100 missionKey=0");
                    });
                }
            }
            return true;
        }
        public static void Check(WorldClient client)
        {
            if(client?.playerData == null || !leases.TryGetValue(client,out var holder))return;
            lock(client.playerData.missionSync)lock(holder)
            {
                var l=holder.Active;if(l==null)return;
                if(!ReferenceEquals(l.Data,client.playerData) || l.World!=client.playerData.worldGeneration || l.Life!=client.playerData.lifeGeneration ||
                    l.Epoch!=client.TransportFence.Capture() || !ReferenceEquals(l.Progress,client.playerData.missionProgress) ||
                    !ReferenceEquals(l.Views,client.viewMan) || !Context(client,out var s,out var v) || !ReferenceEquals(s,l.Source) ||
                    !ReferenceEquals(v,l.SourceView) || v.spawnId!=l.SourceSpawn || !ReferenceEquals(client.viewMan.getViewById(l.View.ViewID),l.View) || l.View.spawnId!=l.Spawn)
                    Remove(client,holder);
            }
        }
        public static void Clear(WorldClient client)
        {
            if(client==null || !leases.TryGetValue(client,out var holder))return;
            lock(holder)Remove(client,holder);
        }
        private static void Remove(WorldClient client,Holder holder)
        {
            var l=holder.Active;holder.Active=null;if(l==null)return;
            // Never address a reused view, a mission source, or a new world's handle.
            if(ReferenceEquals(l.Views,client.viewMan) && ReferenceEquals(l.Views.getViewById(l.View.ViewID),l.View) &&
                l.View.spawnId==l.Spawn && l.View.GoID==548 && !ReferenceEquals(l.View,l.SourceView))
            {
                client.TransportFence.TryRun(l.Epoch,()=>new ServerPackets().sendDeleteViewPacket(client,l.View.ViewID));
                l.Views.removeViewByViewId(l.View.ViewID);
            }
            Output.WriteDebugLog("[POSTER-BOX-FLOOR] off char=110 view="+l.View.ViewID+" spawn="+l.Spawn);
        }
    }
}
