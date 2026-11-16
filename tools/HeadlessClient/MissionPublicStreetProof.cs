using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
/// <summary>Independent persisted public approach. No source selection, runtime admission, or resynchronization.</summary>
public static class MissionPublicStreetProof
{
    public const string Name="mission-public-street-route";
    public sealed record Plan(string Identity,long Started,int Phase,string Raw,MissionNpcInteriorProof.Door Door,double[][] Outside,double[][] Inside,double[] Lobby,double[] Floor);
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static void Need(bool b,string why){if(!b)throw new InvalidDataException(why);}
    static double[] V(JsonElement e){var a=e.EnumerateArray().Select(x=>x.GetDouble()).ToArray();Need(a.Length==3&&a.All(double.IsFinite),"Street finite triple");return a;}
    static StaticWorldObject Raw(string s)=>JsonSerializer.Deserialize<StaticWorldObject>(s,Fields)??throw new InvalidDataException("Street raw identity");
    // Idle Mob.Tick may change Position/Rotation before any authored goal. All other owned fields remain exact.
    public static void RequireStableActors(IReadOnlyList<MissionProgress.SavedMob> before,IReadOnlyList<MissionProgress.SavedMob> after)
    {
        Need(before!=null&&after!=null&&before.Count==after.Count,"Owned actor count changed");
        foreach(var list in new[]{before,after})Need(list.All(m=>m!=null&&Guid.TryParseExact(m.Identity,"N",out _)&&new[]{m.X,m.Y,m.Z}.All(double.IsFinite))&&list.Select(m=>m.Identity).Distinct().Count()==list.Count,"Owned actor UUID/pose invalid or duplicated");
        string Stable(MissionProgress.SavedMob mob)
        {
            var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(mob,Fields))!.AsObject();
            foreach(string motion in new[]{"X","Y","Z","Rotation"})node.Remove(motion);return node.ToJsonString();
        }
        Need(before.OrderBy(m=>m.Identity).Select(Stable).SequenceEqual(after.OrderBy(m=>m.Identity).Select(Stable)),"Owned actor identity/role/side/health/state changed");
    }
    public static JsonElement Select(MissionProgress.SavedState s)
    {
        Need(s.Version==2&&s.Mission==101213&&s.Phase==0&&s.Started>0&&Guid.TryParseExact(s.Identity,"N",out _)&&s.OpenFloorSites!=null&&s.OpenFloorCurrentSiteIndex.HasValue,"Current101213 accepted owner");
        int index=s.OpenFloorCurrentSiteIndex!.Value;Need(index>=0&&index<s.OpenFloorSites!.Length&&index<s.SiteDoors.Length,"Street selected site index");
        var site=s.OpenFloorSites[index];var raw=s.SiteDoors[index];Need(site.District==1&&raw.metrId==1&&site.DoorKey==raw.mxoStaticId&&site.DoorX==raw.pos_x&&site.DoorY==raw.pos_y&&site.DoorZ==raw.pos_z,"Street raw/current site identity");
        var e=s.StreetAccess;Need(e.ValueKind==JsonValueKind.Object&&e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="StreetAccess"&&e.GetProperty("Enabled").GetBoolean()&&(!e.TryGetProperty("Pending",out var pending)||pending.ValueKind==JsonValueKind.Null),"Fresh committed street enrollment");
        var rows=e.GetProperty("Witnesses").EnumerateArray().Where(w=>w.GetProperty("Phase").GetInt32()==s.Phase).ToArray();Need(rows.Length==1,"Unique current street witness");var row=rows[0];
        Need(row.GetProperty("Version").GetInt32()==1&&row.GetProperty("Mission").GetInt32()==s.Mission&&row.GetProperty("Identity").GetString()==s.Identity&&row.GetProperty("Started").GetInt64()==s.Started&&row.GetProperty("Kind").GetString()=="Npc"&&row.GetProperty("Refusal").ValueKind==JsonValueKind.Null,"Street exact owner/kind/refusal");
        var selected=JsonSerializer.Deserialize<MissionOpenFloorSite>(row.GetProperty("Site").GetRawText())!;Need(JsonSerializer.Serialize(selected)==JsonSerializer.Serialize(site),"Street exact current site fingerprint/raw pose");
        var c=row.GetProperty("Context");Need(c.ValueKind==JsonValueKind.Object&&c.GetProperty("Kind").GetString()=="Npc"&&c.GetProperty("Refusal").ValueKind==JsonValueKind.Null,"Unproved doorless/refused context cannot be route");
        var floor=Raw(c.GetProperty("Floor").GetProperty("RawDoorJson").GetString()!);Need(JsonSerializer.Serialize(floor,Fields)==JsonSerializer.Serialize(raw,Fields),"Street floor exact saved raw");
        Need(s.Waypoints.Length==1&&s.Exits.Length==1&&BitConverter.ToUInt32(Convert.FromHexString(s.Waypoints[0].doorHex))==raw.mxoStaticId&&new[]{s.Waypoints[0].x,s.Waypoints[0].y,s.Waypoints[0].z}.SequenceEqual(V(c.GetProperty("Floor").GetProperty("Position")))&&s.Exits[0].SequenceEqual(V(c.GetProperty("Lobby").GetProperty("Position"))),"Street car waypoints remain saved measured poses");
        return row.Clone();
    }
    public static bool TrySharedSegment(double[] a,double[] b,double[] c,double[] d,out double[][] segment)
    {
        segment=Array.Empty<double[]>();if(new[]{a,b,c,d}.Any(v=>v.Length!=3||v.Any(x=>!double.IsFinite(x))))return false;
        double dx=b[0]-a[0],dz=b[2]-a[2],n=dx*dx+dz*dz;if(n==0)return false;
        if(new[]{c,d}.Any(p=>Math.Abs((p[0]-a[0])*dz-(p[2]-a[2])*dx)>1e-7))return false;
        var t=new[]{c,d}.Select(p=>((p[0]-a[0])*dx+(p[2]-a[2])*dz)/n).ToArray();double lo=Math.Max(0,t.Min()),hi=Math.Min(1,t.Max());if((hi-lo)*Math.Sqrt(n)<NavMesh.MinOpenEdge)return false;
        segment=new[]{new[]{a[0]+lo*dx,0d,a[2]+lo*dz},new[]{a[0]+hi*dx,0d,a[2]+hi*dz}};return true;
    }
    public static double[] SideMidpoint(double[] opening,double[] centre)
    {
        Need(opening.Length==3&&centre.Length==3&&opening.All(double.IsFinite)&&centre.All(double.IsFinite),"Finite public side sample");
        double dx=centre[0]-opening[0],dz=centre[2]-opening[2];Need(dx*dx+dz*dz>0,"Nonzero public side direction");return new[]{(opening[0]+centre[0])/2,centre[1],(opening[2]+centre[2])/2};
    }
    public static Plan Read(string repository,MissionProgress.SavedState saved)=>ReadSelected(repository,saved,Select(saved));
    // Shared geometry only: the existing generic Street reader validates exact owner/site/raw/context first.
    public static Plan ReadCurrent(string repository,MissionProgress.SavedState saved)
    {
        var current=MervFollowupStreetEntry.ForSite(saved)??throw new InvalidDataException("Missing current Street context");
        Need(current.Raw.GetProperty("Phase").GetInt32()==saved.Phase,"Current phase, not historical Street context");
        return ReadSelected(repository,saved,current.Raw);
    }
    static Plan ReadSelected(string repository,MissionProgress.SavedState saved,JsonElement row)
    {
        var c=row.GetProperty("Context");var lobby=c.GetProperty("Lobby");var raw=Raw(lobby.GetProperty("RawDoorJson").GetString()!);Need(raw.metrId==1,"Street lobby world");
        byte[] compressed=File.ReadAllBytes(Path.Combine(repository,"data","mission_street_access","1",raw.sectorID+".json.gz"));using var input=new MemoryStream(compressed);using var gz=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();gz.CopyTo(output);byte[] bytes=output.ToArray();
        Need(Convert.ToHexString(SHA256.HashData(bytes))==c.GetProperty("StreetDigest").GetString(),"Street payload matches saved digest");using var street=JsonDocument.Parse(bytes);using var interior=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repository,raw.sectorID,lobby.GetProperty("EvidenceDigest").GetString()!));var outer=street.RootElement;var inner=interior.RootElement;
        Need(outer.GetProperty("World").GetInt32()==1&&outer.GetProperty("Sector").GetInt32()==raw.sectorID&&inner.GetProperty("World").GetInt32()==1&&inner.GetProperty("Sector").GetInt32()==raw.sectorID,"Exact street/interior world/sector");
        var d=c.GetProperty("Door");var doorRaw=Raw(d.GetProperty("RawDoorJson").GetString()!);uint key=d.GetProperty("Key").GetUInt32();ushort goid=d.GetProperty("Goid").GetUInt16();var pose=V(d.GetProperty("Position"));string q=d.GetProperty("Quaternion").GetString()!;
        Need(doorRaw.metrId==1&&doorRaw.sectorID==raw.sectorID&&doorRaw.mxoStaticId==key&&key==((uint)doorRaw.sectorID<<20|doorRaw.staticId)&&MissionInteriorDoorTypes.IsSupported(goid)&&Convert.FromHexString(q).Length==16,"Public raw/native key/schema");
        Need(outer.GetProperty("NativeObjects").EnumerateArray().Count(n=>n.GetProperty("Key").GetUInt32()==key)==1,"Public counted key cannot alias another native object");
        var native=outer.GetProperty("NativeDoors").EnumerateArray().Where(n=>n.GetProperty("Key").GetUInt32()==key).ToArray();Need(native.Length==1&&native[0].GetProperty("Class").GetInt32()==364&&native[0].GetProperty("Goid").GetUInt16()==goid,"Unique counted native public door");
        var group=inner.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==key);Need(group.GetProperty("Legacy").EnumerateArray().Any(l=>{var s=l.GetProperty("Signature");return s.GetProperty("Goid").GetUInt16()==BitConverter.ToUInt16(doorRaw.type)&&V(s.GetProperty("Position")).SequenceEqual(new[]{doorRaw.pos_x,doorRaw.pos_y,doorRaw.pos_z})&&s.GetProperty("Rotation").GetDouble()==doorRaw.rot&&string.Equals(s.GetProperty("Quaternion").GetString(),doorRaw.quat,StringComparison.OrdinalIgnoreCase);}),"Exact public raw legacy definition");var occurrences=group.GetProperty("Occurrences").EnumerateArray().Where(o=>o.GetProperty("Goid").GetUInt16()==goid&&string.Equals(o.GetProperty("RecordSha256").GetString(),native[0].GetProperty("RecordSha256").GetString(),StringComparison.OrdinalIgnoreCase)).ToArray();
        Need(occurrences.Length==1&&V(occurrences[0].GetProperty("ModelPosition")).SequenceEqual(pose)&&occurrences[0].GetProperty("Quaternion").GetString()!.Equals(q,StringComparison.OrdinalIgnoreCase),"Public original record native pose/quaternion");
        var outside=c.GetProperty("Outside").EnumerateArray().ToArray();var inside=c.GetProperty("Inside").EnumerateArray().ToArray();Need(outside.Length>0&&inside.Length>0,"Street nonempty paths");
        int R(JsonElement n)=>n.GetProperty("Region").GetInt32();int P(JsonElement n)=>n.GetProperty("Polygon").GetInt32();
        JsonElement Region(JsonElement n,bool ext)=>(ext?outer.GetProperty("Outdoor"):inner).GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(n));
        JsonElement Poly(JsonElement n,bool ext)=>Region(n,ext).GetProperty("Polygons")[P(n)];
        double[][] Vertices(JsonElement n,bool ext)
        {var edges=Poly(n,ext).GetProperty("Edges").EnumerateArray();if(!ext)return edges.Select(e=>V(e.GetProperty("Vertex"))).ToArray();var corner=outer.GetProperty("ParentCorner");return edges.Select(e=>Convert.FromHexString(e.GetProperty("Raw").GetString()!)).Select(e=>new[]{(double)(float)(BitConverter.ToSingle(e,12)+(float)corner[0].GetDouble()),0d,(double)(float)(BitConverter.ToSingle(e,16)+(float)corner[2].GetDouble())}).ToArray();}
        double Floor(JsonElement n,bool ext,double x,double z)
        {
            var poly=Poly(n,ext);byte[] h=Convert.FromHexString(poly.GetProperty("RawHeader").GetString()!);Need(h.Length>=16,"Original floor plane");var f=Enumerable.Range(0,4).Select(i=>(double)BitConverter.ToSingle(h,i*4)).ToArray();Need(f[1]!=0,"Nonvertical floor plane");
            double a,b,y;if(ext){var corner=outer.GetProperty("ParentCorner");a=x-corner[0].GetDouble();b=z-corner[2].GetDouble();y=corner[1].GetDouble();}
            else{var region=Region(n,false);var pivot=region.GetProperty("Pivot");var corner=inner.GetProperty("ParentCorner");double dx=x-(float)((float)(pivot[0].GetInt32()*10)+(float)corner[0].GetDouble()),dz=z-(float)((float)(pivot[2].GetInt32()*10)+(float)corner[2].GetDouble());(a,b)=region.GetProperty("Rotation").GetInt32() switch{0=>(dx,dz),1=>(-dz,dx),2=>(-dx,-dz),_=>(dz,-dx)};y=(float)((float)(pivot[1].GetInt32()*10)+(float)corner[1].GetDouble());}
            return ext&&f[0]==0&&f[2]==0?(float)(-f[3]/f[1]):-(f[0]*a+f[2]*b+f[3])/f[1]+y;
        }
        double[] At(JsonElement n,bool ext,double x,double z){double y=Floor(n,ext,x,z)+95;Need(NavMesh.Locate(1,x,y,z,out var nav)&&nav.sector==raw.sectorID&&Math.Abs(nav.floorY+95-y)<1e-7,"Original path point actual NAV floor");return new[]{x,y,z};}
        double[] Centre(JsonElement n,bool ext){var v=Vertices(n,ext);var at=At(n,ext,v.Average(p=>p[0]),v.Average(p=>p[2]));Need(NavMesh.Locate(1,at[0],at[1],at[2],out var nav)&&nav.region==R(n)&&nav.polygon==P(n),"Exact original node centre");return at;}
        double[][] Portal(JsonElement a,JsonElement b,bool publicOpening)
        {
            var portals=inner.GetProperty("Portals").EnumerateArray().Where(p=>p.GetProperty("SourceRegion").GetInt32()==R(a)&&p.GetProperty("SourcePolygon").GetInt32()==P(a)&&p.GetProperty("TargetRegion").GetInt32()==R(b)&&p.GetProperty("TargetPolygon").GetInt32()==P(b)).ToArray();Need(portals.Length==1,"Unique original directed portal");var p=portals[0];var keys=p.GetProperty("DoorKeys").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();Need(publicOpening?keys.SequenceEqual(new[]{key}):keys.Length==0,"Only declared public Open/no hidden internal doors");
            if(publicOpening){var edge=Convert.FromHexString(Poly(b,true).GetProperty("Edges")[p.GetProperty("TargetEdge").GetInt32()].GetProperty("Raw").GetString()!);Need(BitConverter.ToUInt16(edge)==R(a)&&BitConverter.ToUInt16(edge,2)==P(a),"Counted reciprocal outdoor opening");}
            else Need(inner.GetProperty("Portals").EnumerateArray().Count(q=>q.GetProperty("SourceRegion").GetInt32()==R(b)&&q.GetProperty("SourcePolygon").GetInt32()==P(b)&&q.GetProperty("TargetRegion").GetInt32()==R(a)&&q.GetProperty("TargetPolygon").GetInt32()==P(a)&&q.GetProperty("SourceEdge").GetInt32()==p.GetProperty("TargetEdge").GetInt32()&&q.GetProperty("TargetEdge").GetInt32()==p.GetProperty("SourceEdge").GetInt32())==1,"Reciprocal interior portal");
            return p.GetProperty("Endpoints").EnumerateArray().Select(v=>new[]{v[0].GetDouble(),0d,v[1].GetDouble()}).ToArray();
        }
        double[][] Walk(JsonElement[] nodes,bool ext)
        {
            var steps=new List<double[]>{Centre(nodes[0],ext)};
            for(int i=1;i<nodes.Length;i++)
            {
                var a=nodes[i-1];var b=nodes[i];double[][] ends;
                if(ext)
                {
                    var forward=Poly(a,true).GetProperty("Edges").EnumerateArray().Select((edge,index)=>(Raw:Convert.FromHexString(edge.GetProperty("Raw").GetString()!),Index:index)).Where(e=>BitConverter.ToUInt16(e.Raw)==R(b)&&BitConverter.ToUInt16(e.Raw,2)==P(b)).ToArray();
                    var reverse=Poly(b,true).GetProperty("Edges").EnumerateArray().Select((edge,index)=>(Raw:Convert.FromHexString(edge.GetProperty("Raw").GetString()!),Index:index)).Where(e=>BitConverter.ToUInt16(e.Raw)==R(a)&&BitConverter.ToUInt16(e.Raw,2)==P(a)).ToArray();
                    var av=Vertices(a,true);var bv=Vertices(b,true);var matches=new List<double[][]>();foreach(var f in forward)foreach(var r in reverse)if(TrySharedSegment(av[f.Index],av[(f.Index+1)%av.Length],bv[r.Index],bv[(r.Index+1)%bv.Length],out var shared))matches.Add(shared);
                    Need(matches.Count==1,"Unique exact reciprocal counted outdoor subsegment");ends=matches[0];
                }
                else if(R(a)!=R(b))ends=Portal(a,b,false);
                else
                {
                    var edges=Poly(a,false).GetProperty("Edges").EnumerateArray().ToArray();int index=Array.FindIndex(edges,e=>e.GetProperty("Neighbor").GetInt32()==P(b)&&e.GetProperty("RegionTag").GetInt32()==65535);Need(index>=0&&Poly(b,false).GetProperty("Edges").EnumerateArray().Count(e=>e.GetProperty("Neighbor").GetInt32()==P(a)&&e.GetProperty("RegionTag").GetInt32()==65535)==1,"Reciprocal original polygon edge");var vertices=Vertices(a,false);ends=new[]{vertices[index],vertices[(index+1)%vertices.Length]};
                }
                double x=(ends[0][0]+ends[1][0])/2,z=(ends[0][2]+ends[1][2])/2;Need(Math.Abs(Floor(a,ext,x,z)-Floor(b,ext,x,z))<1e-7,"Shared-edge floor agreement");steps.Add(At(a,ext,x,z));steps.Add(Centre(b,ext));
            }
            return steps.ToArray();
        }
        var open=Portal(inside[0],outside[^1],true);Need(open.Length==2&&Math.Sqrt(Math.Pow(open[1][0]-open[0][0],2)+Math.Pow(open[1][2]-open[0][2],2))>=NavMesh.MinOpenEdge,"Counted public opening width");double ox=(open[0][0]+open[1][0])/2,oz=(open[0][2]+open[1][2])/2;
        Need(Math.Abs(Floor(inside[0],false,ox,oz)-Floor(outside[^1],true,ox,oz))<1e-7,"Public floor agreement");
        double[] Approach(JsonElement n,bool ext){var centre=Centre(n,ext);var sample=SideMidpoint(new[]{ox,0d,oz},centre);var result=At(n,ext,sample[0],sample[2]);Need(NavMesh.Locate(1,result[0],result[1],result[2],out var nav)&&nav.region==R(n)&&nav.polygon==P(n)&&nav.indoors!=ext,"Public midpoint sample belongs to prescribed exact side/node");return result;}
        var before=Approach(outside[^1],true);var after=Approach(inside[0],false);Need(Math.Sqrt(Math.Pow(before[0]-pose[0],2)+Math.Pow(before[2]-pose[2],2))<=600,"Declared outside Open point in ordinary600range");var outSteps=Walk(outside,true).Append(before).ToArray();var inSteps=new[]{after}.Concat(Walk(inside,false)).Append(V(lobby.GetProperty("Standing"))).ToArray();
        return new(saved.Identity,saved.Started,saved.Phase,row.GetRawText(),new(key,goid,pose,q,before,d.GetProperty("RawDoorJson").GetString()!),outSteps,inSteps,V(lobby.GetProperty("Standing")),V(c.GetProperty("Floor").GetProperty("Standing")));
    }
}
