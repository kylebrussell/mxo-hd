using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using hds;
namespace HeadlessClient;

/// <summary>QA-only persisted-envelope and independent audited topology checks. Never supplies runtime evidence.</summary>
public static class MissionNpcInteriorProof
{
    public const string Name="mission-npc-interior-route";
    public const uint Experience=168042,Information=133350;
    public const string Manifest="DD15FBEEE0B0359245EB4147C772FEE926E8257759ABE019A797EA3EA3FED8F4";
    public sealed record Door(uint Key,ushort Goid,double[] Position,string Quaternion,double[]? Standing,string Raw);
    public sealed record Plan(string Identity,long Started,int Phase,string Site,string Raw,JsonElement Witness,Door Floor,Door Lobby);
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static void Need(bool value,string why){if(!value)throw new InvalidDataException(why);}
    static double[] Vector(JsonElement e,string name,int count)
    {var a=e.GetProperty(name).EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need(a.Length==count&&a.All(double.IsFinite),"NPC proof vector "+name);return a;}
    static StaticWorldObject Raw(string text)=>JsonSerializer.Deserialize<StaticWorldObject>(text,Fields)??throw new InvalidDataException("Missing NPC raw source");
    static bool Same(StaticWorldObject a,StaticWorldObject b)=>JsonSerializer.Serialize(a,Fields)==JsonSerializer.Serialize(b,Fields);
    static void Object(JsonElement e,params string[] names)
    {Need(e.ValueKind==JsonValueKind.Object,"NPC proof object");Need(e.EnumerateObject().Select(p=>p.Name).Distinct().Count()==e.EnumerateObject().Count()&&names.All(n=>e.TryGetProperty(n,out _)),"NPC proof fields");}
    static Door Entry(JsonElement e,StaticWorldObject? expected=null)
    {
        Object(e,"RawDoorJson","Position","Quaternion","Standing","Node","EvidenceDigest");string raw=e.GetProperty("RawDoorJson").GetString()!;var source=Raw(raw);ushort goid=BitConverter.ToUInt16(source.type);
        Need(source.metrId==1&&new ushort[]{417,419}.Contains(goid)&&source.mxoStaticId==((uint)source.sectorID<<20|source.staticId),"NPC entry raw key/type");
        if(expected!=null)Need(Same(source,expected),"NPC floor raw source differs from exact saved site");
        string q=e.GetProperty("Quaternion").GetString()!;Need(q.Length==32&&q.All(Uri.IsHexDigit),"NPC entry quaternion");
        return new(source.mxoStaticId,goid,Vector(e,"Position",3),q,Vector(e,"Standing",3),raw);
    }
    public static bool HasCurrent(MissionProgress.SavedState s)=>s.NpcInterior.ValueKind==JsonValueKind.Object&&s.NpcInterior.TryGetProperty("CurrentPhase",out var phase)&&phase.ValueKind==JsonValueKind.Number&&phase.GetInt32()==s.Phase;
    // DataLoader's first raw identity for a key already owned by the persisted v1 witness. Never pose-to-key recovery.
    public static ushort RawGoidForKnownKey(IEnumerable<string> rows,uint key)
    {
        var found=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(',')).FirstOrDefault(f=>f.Length>=11&&f[0]=="1"&&BitConverter.ToUInt32(Convert.FromHexString(f[2]))==key);
        if(found==null)throw new InvalidDataException("Known persisted lobby key is absent from raw catalog");ushort goid=BitConverter.ToUInt16(Convert.FromHexString(found[4]));
        if(!new ushort[]{417,419}.Contains(goid))throw new InvalidDataException("Known persisted lobby key selected a non-elevator raw type");return goid;
    }
    public static Plan Require(MissionProgress.SavedState s,int expectedPhase=2,ushort expectedNpc=6)
    {
        Need(s.Version==2&&s.Mission==201201&&s.Phase==expectedPhase&&Guid.TryParseExact(s.Identity,"N",out _)&&s.Started>0,"NPC actual accepted201201 current phase identity");
        var e=s.NpcInterior;Object(e,"Version","Kind","Pending","CurrentPhase","Witnesses");
        Need(e.GetProperty("Version").GetInt32()==2&&e.GetProperty("Kind").GetString()=="NpcInterior"&&e.GetProperty("Pending").ValueKind==JsonValueKind.Null&&e.GetProperty("CurrentPhase").GetInt32()==s.Phase,"NPC current witness marker");
        var matches=e.GetProperty("Witnesses").EnumerateArray().Where(w=>w.GetProperty("Selection").GetProperty("Phase").GetInt32()==s.Phase).ToArray();Need(matches.Length==1,"NPC unique current witness");var w=matches[0];
        Object(w,"Version","Kind","Selection","Floor","Lobby","Actors","Doors");Need(w.GetProperty("Version").GetInt32()==2&&w.GetProperty("Kind").GetString()=="NpcInterior","NPC witness version/kind");
        var choice=w.GetProperty("Selection");Object(choice,"Mission","Identity","Started","Phase","Site","Actors");
        Need(choice.GetProperty("Mission").GetInt32()==s.Mission&&choice.GetProperty("Identity").GetString()==s.Identity&&choice.GetProperty("Started").GetInt64()==s.Started&&choice.GetProperty("Phase").GetInt32()==s.Phase,"NPC accepted owner token");
        Need(s.OpenFloorSites!=null&&s.OpenFloorCurrentSiteIndex.HasValue&&s.OpenFloorCurrentSiteIndex>=0&&s.OpenFloorCurrentSiteIndex<s.OpenFloorSites.Length&&s.SiteDoors.Length==s.OpenFloorSites.Length,"NPC exact current history");
        int index=s.OpenFloorCurrentSiteIndex!.Value;var site=s.OpenFloorSites![index];
        Need(choice.GetProperty("Site").GetRawText()==JsonSerializer.SerializeToElement(site,Fields).GetRawText(),"NPC exact saved site DTO");
        var floor=Entry(w.GetProperty("Floor"),s.SiteDoors[index]);var lobby=Entry(w.GetProperty("Lobby"));
        Need(floor.Key!=lobby.Key&&s.Waypoints.Length==1&&BitConverter.ToUInt32(Convert.FromHexString(s.Waypoints[0].doorHex))==floor.Key&&new[]{s.Waypoints[0].x,s.Waypoints[0].y,s.Waypoints[0].z}.SequenceEqual(floor.Position)&&s.Exits.Single().SequenceEqual(lobby.Position),"NPC waypoint/exit agree with measured separate doors");
        var roles=choice.GetProperty("Actors").EnumerateArray().ToArray();var proofs=w.GetProperty("Actors").EnumerateArray().ToArray();
        Need(roles.Length==1&&proofs.Length==1&&roles[0].GetProperty("Kind").GetString()=="Npc"&&roles[0].GetProperty("Id").GetInt32()==expectedNpc&&roles[0].GetProperty("WirePhase").GetByte()==0&&proofs[0].GetProperty("Role").GetString()==$"Npc:{expectedNpc}:0","NPC current actual Ophiuchi role, not story stage as wire byte");
        var marker=Raw(roles[0].GetProperty("RawMarkerJson").GetString()!);var standing=Vector(roles[0],"Standing",4);var actor=s.Mobs.Single(m=>m.Npc==expectedNpc&&!m.Dead);
        Need(marker.metrId==1&&BitConverter.ToUInt16(marker.type)==19&&new[]{marker.pos_x,marker.pos_y-50,marker.pos_z,marker.rot}.SequenceEqual(standing)&&new[]{actor.X,actor.Y,actor.Z}.SequenceEqual(standing.Take(3))&&actor.Friendly&&actor.Health>0,"NPC raw marker/planned prefix/current saved actor");
        return new(s.Identity,s.Started,s.Phase,site.Fingerprint,e.GetRawText(),w.Clone(),floor,lobby);
    }
    public static Door[] RequiredDoors(Plan p)=>p.Witness.GetProperty("Doors").EnumerateArray().Select(d=>
    {var raw=Raw(d.GetProperty("RawDoorJson").GetString()!);uint key=d.GetProperty("Key").GetUInt32();ushort goid=d.GetProperty("Goid").GetUInt16();Need(raw.mxoStaticId==key&&BitConverter.ToUInt16(raw.type)==goid&&new ushort[]{343,346}.Contains(goid),"NPC portal own GO/key");return new Door(key,goid,Vector(d,"Position",3),d.GetProperty("Quaternion").GetString()!,null,d.GetProperty("RawDoorJson").GetString()!);}).ToArray();
    public sealed record RouteStep(double[] Standing,uint[] Doors,int Region,int Polygon);
    public static RouteStep[] Route(string repository,Plan p)
    {
        var source=Raw(p.Floor.Raw);string directory=Path.Combine(repository,"data","mission_interior_evidence","1");byte[] manifest=File.ReadAllBytes(Path.Combine(directory,"manifest.json"));
        string normalized=new UTF8Encoding(false,true).GetString(manifest).Replace("\r\n","\n");Need(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))==Manifest,"NPC QA audited manifest pin");
        using var m=JsonDocument.Parse(manifest);Need(m.RootElement.GetProperty("World").GetInt32()==1,"NPC QA manifest world");var row=m.RootElement.GetProperty("Sectors").EnumerateArray().Single(s=>s.GetProperty("Sector").GetInt32()==source.sectorID);
        byte[] compressed=File.ReadAllBytes(Path.Combine(directory,source.sectorID+".json.gz"));Need(string.Equals(Convert.ToHexString(SHA256.HashData(compressed)),row.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase),"NPC QA payload hash");
        using var input=new MemoryStream(compressed);using var gzip=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();gzip.CopyTo(output);byte[] bytes=output.ToArray();
        Need(Convert.ToHexString(SHA256.HashData(bytes))==p.Witness.GetProperty("Floor").GetProperty("EvidenceDigest").GetString(),"NPC saved evidence digest");using var doc=JsonDocument.Parse(bytes);var root=doc.RootElement;
        Need(root.GetProperty("World").GetInt32()==1&&root.GetProperty("Sector").GetInt32()==source.sectorID,"NPC QA explicit world/sector");
        void NativeDoor(Door door,JsonElement? entry)
        {
            var raw=Raw(door.Raw);var group=root.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==door.Key);
            Need(group.GetProperty("Legacy").EnumerateArray().Any(l=>
            {var s=l.GetProperty("Signature");return s.GetProperty("Goid").GetUInt16()==door.Goid&&s.GetProperty("Position").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(new[]{raw.pos_x,raw.pos_y,raw.pos_z})&&s.GetProperty("Rotation").GetDouble()==raw.rot&&string.Equals(s.GetProperty("Quaternion").GetString(),raw.quat,StringComparison.OrdinalIgnoreCase);}),"NPC QA exact legacy source signature");
            var occurrences=group.GetProperty("Occurrences").EnumerateArray().ToArray();Need(occurrences.All(o=>o.GetProperty("ModelPosition").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(door.Position)),"NPC QA native position consensus");
            var own=occurrences.Where(o=>o.GetProperty("Goid").GetUInt16()==door.Goid&&(!entry.HasValue||o.GetProperty("Region").GetInt32()==entry.Value.GetProperty("Node").GetProperty("Region").GetInt32())).ToArray();
            Need(own.Length>0&&(!entry.HasValue||own.Length==1)&&own.All(o=>string.Equals(o.GetProperty("Quaternion").GetString(),door.Quaternion,StringComparison.OrdinalIgnoreCase)),"NPC QA context-selected own-GO yaw");
        }
        NativeDoor(p.Floor,p.Witness.GetProperty("Floor"));NativeDoor(p.Lobby,p.Witness.GetProperty("Lobby"));foreach(var door in RequiredDoors(p))NativeDoor(door,null);
        var nodes=p.Witness.GetProperty("Actors")[0].GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0,"NPC nonempty topology path");
        int R(JsonElement n)=>n.GetProperty("Region").GetInt32();int P(JsonElement n)=>n.GetProperty("Polygon").GetInt32();
        JsonElement Polygon(JsonElement n){var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(n));Need(region.GetProperty("Available").GetBoolean(),"NPC missing prefab is not route");return region.GetProperty("Polygons")[P(n)];}
        Need(nodes[0].GetRawText()==p.Witness.GetProperty("Floor").GetProperty("Node").GetRawText(),"NPC topology begins actual entry");var steps=new List<RouteStep>{new(p.Floor.Standing!,Array.Empty<uint>(),R(nodes[0]),P(nodes[0]))};
        for(int i=1;i<nodes.Length;i++)
        {
            var a=Polygon(nodes[i-1]);var b=Polygon(nodes[i]);double[] x,z;uint[] doors;
            if(R(nodes[i-1])!=R(nodes[i]))
            {
                var edges=root.GetProperty("Portals").EnumerateArray().Where(q=>q.GetProperty("SourceRegion").GetInt32()==R(nodes[i-1])&&q.GetProperty("SourcePolygon").GetInt32()==P(nodes[i-1])&&q.GetProperty("TargetRegion").GetInt32()==R(nodes[i])&&q.GetProperty("TargetPolygon").GetInt32()==P(nodes[i])).ToArray();Need(edges.Length==1,"NPC unique original portal");var edge=edges[0];
                Need(root.GetProperty("Portals").EnumerateArray().Count(q=>q.GetProperty("SourceRegion").GetInt32()==R(nodes[i])&&q.GetProperty("SourcePolygon").GetInt32()==P(nodes[i])&&q.GetProperty("TargetRegion").GetInt32()==R(nodes[i-1])&&q.GetProperty("TargetPolygon").GetInt32()==P(nodes[i-1])&&q.GetProperty("SourceEdge").GetInt32()==edge.GetProperty("TargetEdge").GetInt32()&&q.GetProperty("TargetEdge").GetInt32()==edge.GetProperty("SourceEdge").GetInt32())==1,"NPC reciprocal portal");
                var ends=edge.GetProperty("Endpoints");x=ends[0].EnumerateArray().Select(v=>v.GetDouble()).ToArray();z=ends[1].EnumerateArray().Select(v=>v.GetDouble()).ToArray();doors=edge.GetProperty("DoorKeys").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();
            }
            else
            {
                var edges=a.GetProperty("Edges").EnumerateArray().ToArray();int edge=Array.FindIndex(edges,e=>e.GetProperty("Neighbor").GetInt32()==P(nodes[i])&&e.GetProperty("RegionTag").GetInt32()==65535);Need(edge>=0,"NPC original polygon neighbor");
                var start=edges[edge].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray();var end=edges[(edge+1)%edges.Length].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray();x=new[]{start[0],start[2]};z=new[]{end[0],end[2]};doors=Array.Empty<uint>();
                Need(b.GetProperty("Edges").EnumerateArray().Any(e=>e.GetProperty("Neighbor").GetInt32()==P(nodes[i-1])&&e.GetProperty("RegionTag").GetInt32()==65535),"NPC reciprocal polygon neighbor");
            }
            double px=(x[0]+z[0])/2,pz=(x[1]+z[1])/2;double Floor(JsonElement poly){var f=poly.GetProperty("Plane").EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need(f.Length==4&&f[1]!=0,"NPC plane shape");return -(f[0]*px+f[2]*pz+f[3])/f[1];}
            Need(Floor(a)==Floor(b),"NPC portal/edge floor agreement");Need(doors.All(k=>RequiredDoors(p).Count(d=>d.Key==k)==1),"NPC every portal door is saved own-GO proof");steps.Add(new(new[]{px,Floor(a)+95,pz},doors,R(nodes[i]),P(nodes[i])));
        }
        var actor=p.Witness.GetProperty("Selection").GetProperty("Actors")[0];var marker=Raw(actor.GetProperty("RawMarkerJson").GetString()!);var markerGroup=root.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==marker.mxoStaticId);
        Need(markerGroup.GetProperty("Occurrences").EnumerateArray().Where(o=>o.GetProperty("Goid").GetInt32()==19).Select(o=>o.GetProperty("Region").GetInt32()).Distinct().SequenceEqual(new[]{R(nodes[^1])}),"NPC QA exact marker original room association");
        var standing=Vector(actor,"Standing",4);var target=Polygon(nodes[^1]);var vertices=target.GetProperty("Edges").EnumerateArray().Select(e=>e.GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray()).ToArray();
        var crosses=vertices.Select((v,i)=>(vertices[(i+1)%vertices.Length][0]-v[0])*(standing[2]-v[2])-(vertices[(i+1)%vertices.Length][2]-v[2])*(standing[0]-v[0])).ToArray();
        var plane=target.GetProperty("Plane").EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need((crosses.All(v=>v>=0)||crosses.All(v=>v<=0))&&standing[1]==-(plane[0]*standing[0]+plane[2]*standing[2]+plane[3])/plane[1]+95,"NPC QA exact actor standing inside original target polygon/floor");
        steps.Add(new(standing.Take(3).ToArray(),Array.Empty<uint>(),R(nodes[^1]),P(nodes[^1])));return steps.ToArray();
    }
    public static void Awards(IReadOnlyList<byte[]> records,long baselineExperience,long baselineInformation,IReadOnlySet<int>? restoredBaselines=null)
    {
        var summaries=records.Where(b=>b.Length>=2&&b[0]==0x80&&b[1]==0xa2).ToArray();Need(summaries.Length==1&&summaries[0].Length==26&&summaries[0][2]==0&&BitConverter.ToUInt32(summaries[0],11)==Information&&BitConverter.ToUInt32(summaries[0],15)==Experience,"NPC exactly one successful literal mission summary");
        foreach(var expected in new[]{(Code:(byte)0xe6,Value:(long)Experience),(Code:(byte)0xe5,Value:baselineExperience+Experience),(Code:(byte)0xe4,Value:baselineInformation+Information)})
        {var rows=records.Where(b=>b.Length>=2&&b[0]==0x80&&b[1]==expected.Code&&b.Length==10&&BitConverter.ToUInt32(b,6)==2).ToArray();Need(rows.Length==1&&BitConverter.ToUInt32(rows[0],2)==expected.Value,"NPC exactly one literal mission award kind/total");}
        Need(!records.Any(b=>b.Length==10&&b[0]==0x80&&b[1]==0xe6&&BitConverter.ToUInt32(b,6)==1),"NPC no invented fighting XP");
        for(int i=0;i<records.Count;i++)
        {
            var b=records[i];if(b.Length<2||b[0]!=0x80||!new byte[]{0xe4,0xe5,0xe6}.Contains(b[1]))continue;
            Need(b.Length==10,"NPC wallet record length");uint type=BitConverter.ToUInt32(b,6);
            if(type==0)Need(restoredBaselines?.Contains(i)==true&&b[1]!=0xe6&&BitConverter.ToUInt32(b,2)==(b[1]==0xe5?baselineExperience:baselineInformation),"NPC only exact restored session-boundary baseline");
            else Need(type==2,"NPC unexpected unbracketed wallet event");
        }
    }
}
