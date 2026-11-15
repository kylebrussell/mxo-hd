using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using hds;
namespace HeadlessClient;

/// <summary>QA-only native326/NAV approach witness; never changes placement or authorizes a route.</summary>
public static class MervBound326Approach
{
    const string ManifestSha="95657513146af308f60d6e532624e143a572b649a1cad98c4601b9eb8ac1c234";
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    public sealed record Arrival(int Mission,int Phase,ushort Instance,string Identity,long Started,string Site,double[] Root,ushort Sector,ushort Region,double FloorY,long OwnLife)
    { public string Operation=>"bound326-arrival-context"; [JsonIgnore] internal OwnViewLifeSnapshot? Token {get;init;} }
    public sealed record Bounds(uint SourceKey,double[] Centre,double[] Minimum,double[] Maximum,string Quaternion,int Region);
    public sealed record Sample(double[] Position,ushort Sector,ushort Region,ushort Polygon,double FloorY);
    public sealed record Result(double[] Standing,Sample Nav,double Envelope,float NativeDistanceSquared,double[] NativeMinimum,double[] NativeMaximum)
    { public bool DirectRouteProven=>false;public bool CollisionProven=>false;public bool NativePickingProven=>false; }
    public sealed record Nav(Func<double[],Sample?> Locate,Func<double[],double,double[],Sample?> Project);
    static bool Point(double[] p)=>p?.Length==3&&p.All(double.IsFinite);
    static string Site(MissionProgress.SavedState s)=>JsonSerializer.Serialize(new{s.Waypoints,s.Exits,Door=s.SiteDoors.LastOrDefault(),s.OpenFloorCurrentSiteIndex,s.OpenFloorSites},Fields);
    public static Arrival Capture(MissionProgress.SavedState state,double[] root,NavLocation nav,OwnViewLifeSnapshot life)
    {
        if(!Point(root)||!life.Current||!life.OwnViewReady)throw new InvalidDataException("No actual current arrival life");
        return new(state.Mission,state.Phase,state.Instance,state.Identity,state.Started,Site(state),root.ToArray(),nav.sector,nav.region,nav.floorY,life.Life){Token=life};
    }
    public static void RequireScope(MissionProgress.SavedState state,int id,Arrival arrival,OwnViewLifeSnapshot life)
    {
        if(arrival==null||arrival.Token==null||!arrival.Token.Current||!life.Current||!life.OwnViewReady||
            !ReferenceEquals(arrival.Token.Owner,life.Owner)||arrival.OwnLife!=life.Life||state.Mission!=arrival.Mission||state.Phase!=arrival.Phase||
            state.Instance!=arrival.Instance||state.Identity!=arrival.Identity||state.Started!=arrival.Started||Site(state)!=arrival.Site||
            !state.Flags.TryGetValue("entered",out bool entered)||!entered||state.Flags.GetValueOrDefault("failed"))throw new InvalidDataException("Bound approach arrival scope changed");
        var obj=state.Objects.Single(o=>o.Id==id);
        if(obj.Goid!=326||obj.OpenFloor!=null||obj.Spot?.metrId!=1||MissionSites.Goid(obj.Spot)!=326)throw new InvalidDataException("Only genuinely bound326 uses this sampler");
    }
    public static Bounds Load(string repository,MissionProgress.SavedObject obj)
    {
        if(obj.Goid!=326||obj.OpenFloor!=null||obj.Spot?.metrId!=1||MissionSites.Goid(obj.Spot)!=326)throw new InvalidDataException("Not native bound326");
        // Existing runtime facade validates the same audited source before read-only bounds evidence is consumed.
        var pose=MissionObjectPoses.For(new MissionObjectState{spot=obj.Spot,goid=obj.Goid});
        string directory=Path.Combine(repository,"data/mission_bound_poses/1");
        string manifest=new UTF8Encoding(false,true).GetString(File.ReadAllBytes(Path.Combine(directory,"manifest.json"))).Replace("\r\n","\n");
        if(!Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))).Equals(ManifestSha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("QA bound manifest hash");
        using var m=JsonDocument.Parse(manifest);var file=m.RootElement.GetProperty("Files").EnumerateArray().Single(f=>f.GetProperty("Sector").GetUInt16()==obj.Spot.sectorID);
        string name=file.GetProperty("File").GetString()!;if(name!=obj.Spot.sectorID+".json.gz")throw new InvalidDataException("QA bound sector identity");
        byte[] bytes=File.ReadAllBytes(Path.Combine(directory,name));if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("QA bound payload hash");
        using var input=new MemoryStream(bytes);using var gzip=new GZipStream(input,CompressionMode.Decompress);using var doc=JsonDocument.Parse(gzip);
        var row=doc.RootElement.GetProperty("Rows").EnumerateArray().Single(r=>r.GetProperty("NativeKey").GetUInt32()==obj.Spot.mxoStaticId);
        double[] P(string key)=>row.GetProperty(key).EnumerateArray().Select(v=>v.GetDouble()).ToArray();
        if(row.GetProperty("Goid").GetInt32()!=326||!P("LegacyPosition").SequenceEqual(new[]{obj.Spot.pos_x,obj.Spot.pos_y,obj.Spot.pos_z})||
            row.GetProperty("LegacyRotation").GetDouble()!=obj.Spot.rot||!Convert.FromHexString(row.GetProperty("LegacyQuaternion").GetString()!).SequenceEqual(Convert.FromHexString(obj.Spot.quat))||
            !P("Centre").SequenceEqual(new[]{pose.X,pose.Y,pose.Z})||!Convert.FromHexString(row.GetProperty("Quaternion").GetString()!).SequenceEqual(pose.Orientation))throw new InvalidDataException("QA bound source/presentation signature");
        return new(obj.Spot.mxoStaticId,P("Centre"),P("Minimum"),P("Maximum"),Convert.ToHexString(pose.Orientation),row.GetProperty("Region").GetInt32());
    }
    static float[] Basis(string hex)
    {
        byte[] b;try{b=Convert.FromHexString(hex);}catch(Exception e)when(e is FormatException or ArgumentNullException){throw new InvalidDataException("Invalid native quaternion",e);}
        if(b.Length!=16)throw new InvalidDataException("Invalid native quaternion");var q=Enumerable.Range(0,4).Select(i=>BitConverter.ToSingle(b,i*4)).ToArray();
        if(q.Any(v=>!float.IsFinite(v))||Math.Abs(q.Sum(v=>(double)v*v)-1)>1e-5)throw new InvalidDataException("Invalid native quaternion");
        float x=q[0],y=q[1],z=q[2],w=q[3],tx=2*x,ty=2*y,tz=2*z,xx=tx*x,xy=ty*x,xz=tz*x,yy=ty*y,yz=tz*y,zz=tz*z,xw=tx*w,yw=ty*w,zw=tz*w;
        return new[]{1-(zz+yy),xy+zw,xz-yw,xy-zw,1-(zz+xx),yz+xw,xz+yw,yz-xw,1-(xx+yy)};
    }
    // Exact existing native float predicate, restricted to primary stored GO326 Open100 / Model25,80,30.
    public static float NativeDistanceSquared(double[] standing,Bounds bounds)
    {
        if(!Point(standing)||!Point(bounds.Centre))return float.NaN;var b=Basis(bounds.Quaternion);float x=(float)(standing[0]-bounds.Centre[0]),y=(float)(standing[1]-bounds.Centre[1]),z=(float)(standing[2]-bounds.Centre[2]);
        float[] p={(float)((double)x*b[0]+(double)y*b[1]+(double)z*b[2]),(float)((double)x*b[3]+(double)y*b[4]+(double)z*b[5]),(float)((double)x*b[6]+(double)y*b[7]+(double)z*b[8])};
        float[] ext={25,80,30};float sum=0;for(int i=0;i<3;i++){if(!float.IsFinite(p[i]))return float.NaN;float outside=p[i]<-ext[i]?(float)(p[i]+ext[i]):p[i]>ext[i]?(float)(p[i]-ext[i]):0;sum=(float)((double)outside*outside+sum);}return sum;
    }
    public static double Envelope(Bounds bounds)
    {
        var b=Basis(bounds.Quaternion);double error2=0;
        for(int i=0;i<3;i++)for(int j=0;j<3;j++){double dot=0;for(int k=0;k<3;k++)dot+=(double)b[k*3+i]*b[k*3+j];double error=dot-(i==j?1:0);error2+=error*error;}
        double errorNorm=Math.Sqrt(error2);double radius=errorNorm<1?(100+Math.Sqrt(25*25+80*80+30*30))/Math.Sqrt(1-errorNorm):double.NaN;
        if(!double.IsFinite(radius))throw new InvalidDataException("Unbounded native basis");return radius;
    }
    public static Result Select(Bounds bounds,Arrival arrival,Nav nav)
    {
        if(bounds.SourceKey==0||bounds.Region<0||!Point(bounds.Centre)||!Point(bounds.Minimum)||!Point(bounds.Maximum)||!Point(arrival.Root)||arrival.Sector!=(bounds.SourceKey>>20)||arrival.Region!=bounds.Region||
            !double.IsFinite(arrival.FloorY)||bounds.Minimum.Where((v,i)=>v>=bounds.Maximum[i]||bounds.Centre[i]!=(double)(float)((float)(v+bounds.Maximum[i])*.5f)).Any())throw new InvalidDataException("Bound/arrival bounds/floor identity");
        var root=nav.Locate(arrival.Root);var self=nav.Project(arrival.Root,0,arrival.Root);
        bool Root(Sample? p)=>p!=null&&p.Sector==arrival.Sector&&p.Region==arrival.Region&&p.FloorY==arrival.FloorY&&p.Position.SequenceEqual(arrival.Root)&&p.Position[1]==p.FloorY+95;
        if(!Root(root)||!Root(self))throw new InvalidDataException("Root reference cannot fall back");
        double envelope=Envelope(bounds);var candidate=nav.Project(bounds.Centre,envelope,arrival.Root)??throw new InvalidDataException("No nearest native-range NAV candidate");
        if(!Point(candidate.Position))throw new InvalidDataException("Nonfinite projected candidate");
        var point=candidate.Position.Select(v=>(double)(float)v).ToArray();if(!Point(point))throw new InvalidDataException("Unrepresentable projected candidate");var located=nav.Locate(point);
        if(!Point(point)||located==null||located.Sector!=arrival.Sector||located.Region!=arrival.Region||located.FloorY!=arrival.FloorY||point[1]!=located.FloorY+95||
            candidate.Sector!=located.Sector||candidate.Region!=located.Region||candidate.Polygon!=located.Polygon||candidate.FloorY!=located.FloorY||
            !point.Where((v,i)=>v<bounds.Minimum[i]||v>bounds.Maximum[i]).Any())throw new InvalidDataException("Candidate is inside bounds or on another floor/component");
        float distance=NativeDistanceSquared(point,bounds);
        if(!float.IsFinite(distance)||distance<=0||distance>10000||!ContainerActions.InReach(point.SelectMany(BitConverter.GetBytes).ToArray(),bounds.Centre[0],bounds.Centre[1],bounds.Centre[2]))throw new InvalidDataException("Native100/server range failed");
        return new(point,located,envelope,distance,bounds.Minimum.ToArray(),bounds.Maximum.ToArray());
    }
    public static Result Choose(string repository,MissionProgress.SavedState state,int id,Arrival arrival,OwnViewLifeSnapshot life)
    {
        RequireScope(state,id,arrival,life);var bounds=Load(repository,state.Objects.Single(o=>o.Id==id));
        Sample? Locate(double[] p)=>NavMesh.Locate(1,p[0],p[1],p[2],out var at)?new(p.ToArray(),at.sector,at.region,at.polygon,at.floorY):null;
        Sample? Project(double[] p,double radius,double[] root)=>NavMesh.Project(1,p[0],p[1],p[2],out var at,radius,root,false)?new(at.Position,at.location.sector,at.location.region,at.location.polygon,at.location.floorY):null;
        var result=Select(bounds,arrival,new(Locate,Project));RequireScope(state,id,arrival,life);return result;
    }
}
