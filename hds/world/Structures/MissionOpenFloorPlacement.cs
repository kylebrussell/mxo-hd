using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    // Immutable fixed-result checkpoint. Raw source remains SavedObject.Spot; no restore-time replanning.
    public sealed class MissionOpenFloorPlacement
    {
        public int Policy {get;} public ushort World {get;} public int Mission {get;} public int Phase {get;} public ushort ObjectId {get;}
        public uint SourceKey {get;} public ushort SourceGoid {get;} public ushort ConcreteGoid {get;}
        public string RawFingerprint {get;} public string InputFingerprint {get;} public string Quaternion {get;}
        private readonly double[] centre,standing,route;private readonly ushort[] surface,rootSurface,routeSurfaces;
        public double[] Centre=>(double[])centre.Clone();public double[] Standing=>(double[])standing.Clone();public double[] Route=>(double[])route.Clone();
        public ushort[] Surface=>(ushort[])surface.Clone();public ushort[] RootSurface=>(ushort[])rootSurface.Clone();public ushort[] RouteSurfaces=>(ushort[])routeSurfaces.Clone();
        [JsonConstructor] public MissionOpenFloorPlacement(int policy,ushort world,int mission,int phase,ushort objectId,uint sourceKey,ushort sourceGoid,ushort concreteGoid,
            string rawFingerprint,string inputFingerprint,string quaternion,double[] centre,double[] standing,double[] route,ushort[] surface,ushort[] rootSurface,ushort[] routeSurfaces)
        {
            Policy=policy;World=world;Mission=mission;Phase=phase;ObjectId=objectId;SourceKey=sourceKey;SourceGoid=sourceGoid;ConcreteGoid=concreteGoid;
            RawFingerprint=rawFingerprint;InputFingerprint=inputFingerprint;Quaternion=quaternion;
            this.centre=(double[])(centre??Array.Empty<double>()).Clone();this.standing=(double[])(standing??Array.Empty<double>()).Clone();this.route=(double[])(route??Array.Empty<double>()).Clone();
            this.surface=(ushort[])(surface??Array.Empty<ushort>()).Clone();this.rootSurface=(ushort[])(rootSurface??Array.Empty<ushort>()).Clone();this.routeSurfaces=(ushort[])(routeSurfaces??Array.Empty<ushort>()).Clone();
        }
        internal static string RawHash(StaticWorldObject raw)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(raw,MissionProgress.SaveJson))));
        private static ushort[] Id(G.Id id)=>new[]{id.Sector,id.Region,id.Polygon};
        internal static MissionOpenFloorPlacement From(int mission,int phase,ushort objectId,StaticWorldObject raw,G.Result result)
        {
            if(result==null||!result.HasPlan)throw new ArgumentException("MissingOpenFloorPlan");
            return new MissionOpenFloorPlacement(G.PolicyVersion,result.Input.World,mission,phase,objectId,raw.mxoStaticId,MissionSites.Goid(raw),result.Input.Model.Goid,
                RawHash(raw),result.InputFingerprint,result.Input.Anchor.QuaternionHex,new[]{result.Centre.X,result.Centre.Y,result.Centre.Z},new[]{result.Standing.X,result.Standing.Y,result.Standing.Z},
                result.Route.SelectMany(p=>new[]{p.X,p.Y,p.Z}).ToArray(),Id(result.Surface),Id(result.Input.Entry.Surface),result.RoutePolygons.SelectMany(Id).ToArray());
        }
        internal bool Basic(StaticWorldObject raw,ushort concrete,ushort id)
        {
            if(raw==null||raw.type==null||(raw.type.Length!=2&&raw.type.Length!=4)||Policy!=G.PolicyVersion||ObjectId!=id||World!=raw.metrId||SourceKey!=raw.mxoStaticId||
                SourceGoid!=MissionSites.Goid(raw)||ConcreteGoid!=concrete||RawFingerprint!=RawHash(raw)||!Hex(InputFingerprint,64)||!Hex(Quaternion,32)||
                centre.Length!=3||standing.Length!=3||route.Length<6||route.Length%3!=0||surface.Length!=3||rootSurface.Length!=3||routeSurfaces.Length<3||routeSurfaces.Length%3!=0||
                route.Length!=routeSurfaces.Length+3||centre.Concat(standing).Concat(route).Any(x=>!double.IsFinite(x))||!standing.SequenceEqual(route.Skip(route.Length-3)))return false;
            try{_ = new OpenFloorOrientation(Quaternion);}catch(ArgumentException){return false;}return true;
        }
        internal bool Replay(G.Request request,int mission,int phase,MissionObjectState state,out string refusal)
        {
            refusal="InvalidOpenFloorCheckpointIdentity";
            if(state==null||state.def==null||Mission!=mission||Phase!=phase||!Basic(state.spot,state.goid,state.def.id)||request.World!=World||request.Anchor.QuaternionHex!=Quaternion||
                !Id(request.Entry.Surface).SequenceEqual(rootSurface))return false;
            var points=Enumerable.Range(0,route.Length/3).Select(i=>new G.Position(route[i*3],route[i*3+1],route[i*3+2])).ToArray();
            if(points[0].X!=request.Entry.Standing.X||points[0].Y!=request.Entry.Standing.Y||points[0].Z!=request.Entry.Standing.Z)return false;
            var ids=Enumerable.Range(0,routeSurfaces.Length/3).Select(i=>new G.Id(routeSurfaces[i*3],routeSurfaces[i*3+1],routeSurfaces[i*3+2])).ToArray();
            if(!G.ValidateRouteIds(request,ids,points))return false;
            return G.ValidateFixed(request,InputFingerprint,new G.Id(surface[0],surface[1],surface[2]),new G.Position(centre[0],centre[1],centre[2]),points,out refusal);
        }
        private static bool Hex(string s,int length)=>s!=null&&s.Length==length&&s.All(Uri.IsHexDigit);
    }
    /// <summary>One effective fixed pose for every admitted object consumer; untouched objects retain the existing facade.</summary>
    public static class MissionObjectPoses
    {
        public static MissionComputerAnchors.Pose For(MissionObjectState state)
        {
            if(state?.OpenFloor==null)return MissionComputerAnchors.For(state);
            var pose=state.OpenFloor;if(state.def==null||!pose.Basic(state.spot,state.goid,state.def.id))throw new InvalidOperationException("InvalidOpenFloorPose");
            var centre=pose.Centre;return new MissionComputerAnchors.Pose(centre[0],centre[1],centre[2],pose.Quaternion);
        }
    }
}
