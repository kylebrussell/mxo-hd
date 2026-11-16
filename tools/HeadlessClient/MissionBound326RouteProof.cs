using System.Text.Json;
using hds;
using Door=HeadlessClient.MissionNpcInteriorProof.Door;
namespace HeadlessClient;
// QA-only: consumes an already committed route. No chooser, Project, checkpoint write or geometry waiver.
public static class MissionBound326RouteProof
{
    const string BoundManifest="95657513146af308f60d6e532624e143a572b649a1cad98c4601b9eb8ac1c234";
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static void Need(bool ok,string why){if(!ok)throw new InvalidDataException("Bound route QA: "+why);}
    static StaticWorldObject Raw(string value)=>JsonSerializer.Deserialize<StaticWorldObject>(value,Fields)??throw new InvalidDataException("Bound route raw source");
    static double[] V(JsonElement e,string key){var p=e.GetProperty(key).EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need(p.Length==3&&p.All(double.IsFinite),"finite triple");return p;}
    static void Shape(JsonElement e,params string[] names)=>Need(e.ValueKind==JsonValueKind.Object&&e.EnumerateObject().Count()==names.Length&&names.All(n=>e.TryGetProperty(n,out _)),"recognized fields");
    public sealed record Step(double[] Standing,uint[] Doors,int Region,int Polygon);
    public sealed record Plan(MissionProgress.SavedState Before,JsonElement Selection,JsonElement Route,Door Floor,Door Lobby,Door[] Doors,double[] Standing,ushort Sector,ushort Region,ushort Polygon,double FloorY);
    public static void SelectionVersion(JsonElement w)
    {
        int version=w.GetProperty("Version").GetInt32();Need(version==1||version==2,"known Street selection version");
        if(version==1){Need(!w.TryGetProperty("BoundRoutes",out _),"V1 cannot carry V2 authority");return;}
        Shape(w,"Version","Mission","Identity","Started","Phase","Site","Kind","Context","Refusal","BoundRoutes");
        Need(w.TryGetProperty("BoundRoutes",out var e),"recognized V2 route required");
        Shape(e,"Version","Kind","EvidenceDigest","BoundManifest","Routes");
        Need(e.GetProperty("Version").GetInt32()==1&&e.GetProperty("Kind").GetString()=="Bound326Routes"&&e.GetProperty("BoundManifest").GetString()==BoundManifest&&
            e.GetProperty("EvidenceDigest").GetString()==w.GetProperty("Context").GetProperty("Floor").GetProperty("EvidenceDigest").GetString()&&w.GetProperty("Kind").GetString()=="Objects","bound envelope authority");
        var rows=e.GetProperty("Routes").EnumerateArray().ToArray();Need(rows.Length>0&&rows.Select(r=>r.GetProperty("ObjectId").GetInt32()).Distinct().Count()==rows.Length,"nonempty unique object routes");
        foreach(var r in rows){Shape(r,"ObjectId","RawSourceJson","Region","NativePosition","NativeQuaternion","Standing","Target","Nodes","Doors");Shape(r.GetProperty("Target"),"Region","Polygon");foreach(var n in r.GetProperty("Nodes").EnumerateArray())Shape(n,"Region","Polygon");foreach(var d in r.GetProperty("Doors").EnumerateArray())Shape(d,"Key","Goid","RawDoorJson","Quaternion","Position");}
    }
    static bool Wire(double[] a,double[] b)=>a.Length==3&&b.Length==3&&a.All(double.IsFinite)&&b.All(double.IsFinite)&&Enumerable.Range(0,3).All(i=>BitConverter.SingleToInt32Bits((float)a[i])==BitConverter.SingleToInt32Bits((float)b[i]));
    public static Plan? Read(MissionProgress.SavedState state,int objectId,MissionBound326Approach.Bounds bounds,MissionBound326Approach.Arrival arrival,Func<double[],MissionBound326Approach.Sample?> locate)
    {
        var street=ZionAfter1212StreetEntry.ForSite(state);if(street==null)return null;
        SelectionVersion(street.Raw);if(street.Raw.GetProperty("Version").GetInt32()==1)return null;
        Need(street.Raw.GetProperty("Phase").GetInt32()==state.Phase,"current route phase, not historical adoption");
        var objects=state.Objects.Where(o=>o.Id==objectId).ToArray();Need(objects.Length==1,"one current object");var obj=objects[0];
        Need(obj.OpenFloor==null&&obj.Goid==326&&obj.Spot.metrId==1&&MissionSites.Goid(obj.Spot)==326,"actual bound326");
        var all=street.Raw.GetProperty("BoundRoutes").GetProperty("Routes").EnumerateArray().ToArray();
        Need(all.Length==state.Objects.Count&&state.Objects.All(o=>o.Goid==326&&o.OpenFloor==null&&all.Count(r=>r.GetProperty("ObjectId").GetInt32()==o.Id&&r.GetProperty("RawSourceJson").GetString()==JsonSerializer.Serialize(o.Spot,Fields))==1),"complete current bound-source association");
        var rows=all.Where(r=>r.GetProperty("ObjectId").GetInt32()==objectId).ToArray();Need(rows.Length==1,"exact route object");var row=rows[0];
        Need(row.GetProperty("RawSourceJson").GetString()==JsonSerializer.Serialize(obj.Spot,Fields)&&bounds.SourceKey==obj.Spot.mxoStaticId&&
            V(row,"NativePosition").SequenceEqual(bounds.Centre)&&Convert.FromHexString(row.GetProperty("NativeQuaternion").GetString()!).SequenceEqual(Convert.FromHexString(bounds.Quaternion)),"raw source and audited native bounds pose");
        var at=locate(arrival.Root);var floor=street.Raw.GetProperty("Context").GetProperty("Floor");var start=floor.GetProperty("Node");
        Need(at!=null&&at.Sector==obj.Spot.sectorID&&arrival.Sector==at.Sector&&at.Region==arrival.Region&&at.FloorY==arrival.FloorY&&arrival.Root[1]==at.FloorY+95&&
            Wire(arrival.Root,street.Floor.Standing!)&&at.Region==start.GetProperty("Region").GetUInt16()&&at.Polygon==start.GetProperty("Polygon").GetUInt16(),"actual ordinary Ride root and saved start node/floor");
        var nodes=row.GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0&&nodes[0].GetRawText()==start.GetRawText(),"route starts at actual car entry");
        var target=row.GetProperty("Target");ushort region=row.GetProperty("Region").GetUInt16(),polygon=target.GetProperty("Polygon").GetUInt16();
        Need(region==bounds.Region&&target.GetProperty("Region").GetUInt16()==region&&nodes[^1].GetRawText()==target.GetRawText(),"audited source region and exact path target");
        var standing=V(row,"Standing");Need(standing.All(v=>(double)(float)v==v),"fixed wire-representable Standing");var n=locate(standing);
        float distance=MissionBound326Approach.NativeDistanceSquared(standing,bounds);
        Need(n!=null&&n.Sector==at!.Sector&&n.Region==region&&n.FloorY==at.FloorY&&standing[1]==n.FloorY+95&&
            float.IsFinite(distance)&&distance>0&&distance<=10000&&ContainerActions.InReach(standing.SelectMany(BitConverter.GetBytes).ToArray(),bounds.Centre[0],bounds.Centre[1],bounds.Centre[2]),"fixed target NAV/floor/native OBB100/server600");
        var doors=row.GetProperty("Doors").EnumerateArray().Select(d=>{string text=d.GetProperty("RawDoorJson").GetString()!;var raw=Raw(text);uint key=d.GetProperty("Key").GetUInt32();ushort goid=d.GetProperty("Goid").GetUInt16();
            Need(raw.metrId==1&&raw.sectorID==at!.Sector&&raw.mxoStaticId==key&&key==((uint)raw.sectorID<<20|raw.staticId)&&MissionSites.Goid(raw)==goid&&MissionInteriorDoorTypes.IsSupported(goid),"required door exact own-GOID/raw/key");
            byte[] q=Convert.FromHexString(d.GetProperty("Quaternion").GetString()!);Need(q.Length==16&&Enumerable.Range(0,4).All(i=>float.IsFinite(BitConverter.ToSingle(q,4*i))),"required door quaternion");return new Door(key,goid,V(d,"Position"),Convert.ToHexString(q),null,text);}).ToArray();
        Need(doors.Select(d=>d.Key).Distinct().Count()==doors.Length,"unambiguous required doors");
        return new(state,street.Raw.Clone(),row.Clone(),street.Floor,street.Lobby,doors,standing,at!.Sector,region,polygon,at.FloorY);
    }
    // Log proof is not a packet matcher or ACK. It proves THIS ordinary request reached the actual own-GOID door handler after the cutpoint.
    public static string RequireDoorUse(string debug,int offset,Door expected)
    {
        Need(offset>=0&&offset<=debug.Length,"valid post-approach debug cursor");string prefix=$"[DOOR] Use of static {expected.Key} type {expected.Goid} (";
        var rows=debug[offset..].Split('\n').Select(s=>s.TrimEnd('\r')).Where(s=>s.StartsWith(prefix,StringComparison.Ordinal)&&s.EndsWith(")",StringComparison.Ordinal)).ToArray();
        Need(rows.Length==1,"one fresh actual key/own-GOID door handler use after request cursor");return rows[0];
    }
    public static NpcInteriorComputerState.DoorObservation RequireOpen(OwnViewLifeSnapshot current,OwnViewLifeSnapshot before,Door expected)
    {Need(before.Current&&current.Current&&current.OwnViewReady&&ReferenceEquals(current.Owner,before.Owner)&&current.Life==before.Life,"portal Open current own-life owner");return NpcInteriorComputerState.CurrentDoor(current,expected);}
    public static void RequireCurrent(Plan plan,MissionProgress.SavedState current)
    {MissionOutsideContinuation.Require(plan.Before,current);var now=ZionAfter1212StreetEntry.ForSite(current);Need(now!=null&&now.Raw.GetRawText()==plan.Selection.GetRawText(),"current owner/source/route identity");}
    // Original TryWoodDoor uses the selected raw GOID globally; a portal may approach it from the companion side.
    public static void RequireOwnGoidDoorEvidence(JsonElement group,JsonElement regions,ushort goid,double[] position,string quaternion)
    {
        var own=group.GetProperty("Occurrences").EnumerateArray().Where(o=>o.GetProperty("Goid").GetUInt16()==goid).ToArray();
        Need(own.Length>0&&own.All(o=>o.GetProperty("ModelPosition").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(position)&&string.Equals(o.GetProperty("Quaternion").GetString(),quaternion,StringComparison.OrdinalIgnoreCase)),"Bound route QA global own-GO pose consensus");
        Need(own.All(o=>regions.EnumerateArray().Any(r=>r.GetProperty("Id").GetInt32()==o.GetProperty("Region").GetInt32()&&r.GetProperty("Available").GetBoolean())),"Bound route QA available own-GO evidence");
    }
    public static Step[] Steps(string repository,Plan plan)
    {
        var source=Raw(plan.Floor.Raw);string expectedDigest=plan.Selection.GetProperty("Context").GetProperty("Floor").GetProperty("EvidenceDigest").GetString()!;
        using var doc=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repository,source.sectorID,expectedDigest));var root=doc.RootElement;
        bool localEvidence=root.GetProperty("Format").GetInt32()==2;
        Need(root.GetProperty("World").GetInt32()==1&&root.GetProperty("Sector").GetInt32()==source.sectorID,"Bound route QA explicit world/sector");
        void NativeDoor(Door door,JsonElement? entry)
        {
            var raw=Raw(door.Raw);using var ownDoc=JsonDocument.Parse(MissionNpcInteriorProof.EvidencePayload(repository,raw.sectorID,entry?.GetProperty("EvidenceDigest").GetString()??expectedDigest));var group=ownDoc.RootElement.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==door.Key);
            Need(group.GetProperty("Legacy").EnumerateArray().Any(l=>
            {var s=l.GetProperty("Signature");return s.GetProperty("Goid").GetUInt16()==door.Goid&&s.GetProperty("Position").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(new[]{raw.pos_x,raw.pos_y,raw.pos_z})&&s.GetProperty("Rotation").GetDouble()==raw.rot&&string.Equals(s.GetProperty("Quaternion").GetString(),raw.quat,StringComparison.OrdinalIgnoreCase);}),"Bound route QA exact legacy source signature");
            if(!entry.HasValue){MissionBound326RouteProof.RequireOwnGoidDoorEvidence(group,ownDoc.RootElement.GetProperty("Regions"),door.Goid,door.Position,door.Quaternion);return;}
            var occurrences=group.GetProperty("Occurrences").EnumerateArray().ToArray();var positionScope=MissionInteriorDoorTypes.IsSupported(door.Goid)?occurrences.Where(o=>o.GetProperty("Goid").GetUInt16()==door.Goid):occurrences;Need(positionScope.Any()&&positionScope.All(o=>o.GetProperty("ModelPosition").EnumerateArray().Select(v=>v.GetDouble()).SequenceEqual(door.Position)),"Bound route QA native position consensus");
            var own=occurrences.Where(o=>o.GetProperty("Goid").GetUInt16()==door.Goid&&(!entry.HasValue||o.GetProperty("Region").GetInt32()==entry.Value.GetProperty("Node").GetProperty("Region").GetInt32())).ToArray();
            Need(own.Length>0&&(!entry.HasValue||own.Length==1)&&own.All(o=>string.Equals(o.GetProperty("Quaternion").GetString(),door.Quaternion,StringComparison.OrdinalIgnoreCase)),"Bound route QA context-selected own-GO yaw");
            Need(own.All(o=>ownDoc.RootElement.GetProperty("Regions").EnumerateArray().Any(r=>r.GetProperty("Id").GetInt32()==o.GetProperty("Region").GetInt32()&&r.GetProperty("Available").GetBoolean())),"Bound route QA available own-GO evidence");
        }
        NativeDoor(plan.Floor,plan.Selection.GetProperty("Context").GetProperty("Floor"));NativeDoor(plan.Lobby,plan.Selection.GetProperty("Context").GetProperty("Lobby"));
        var nodes=plan.Route.GetProperty("Nodes").EnumerateArray().ToArray();Need(nodes.Length>0,"Bound route nonempty topology path");
        int R(JsonElement n)=>n.GetProperty("Region").GetInt32();int P(JsonElement n)=>n.GetProperty("Polygon").GetInt32();
        JsonElement Polygon(JsonElement n){var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(n));Need(region.GetProperty("Available").GetBoolean(),"Bound route missing prefab is not route");return region.GetProperty("Polygons")[P(n)];}
        double[] Local(JsonElement node,double x,double z)
        {
            var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(node));var pivot=region.GetProperty("Pivot");var corner=root.GetProperty("ParentCorner");
            double a=x-(float)((float)(pivot[0].GetInt32()*10)+(float)corner[0].GetDouble()),b=z-(float)((float)(pivot[2].GetInt32()*10)+(float)corner[2].GetDouble());
            return region.GetProperty("Rotation").GetInt32() switch{0=>new[]{a,b},1=>new[]{-b,a},2=>new[]{-a,-b},_=>new[]{b,-a}};
        }
        double Floor(JsonElement poly,JsonElement node,double x,double z)
        {
            if(!localEvidence){var f=poly.GetProperty("Plane").EnumerateArray().Select(v=>v.GetDouble()).ToArray();Need(f.Length==4&&f[1]!=0,"Bound route plane shape");return -(f[0]*x+f[2]*z+f[3])/f[1];}
            byte[] raw=Convert.FromHexString(poly.GetProperty("RawHeader").GetString()!);Need(raw.Length==20,"Bound route raw local plane header");var plane=Enumerable.Range(0,4).Select(i=>(double)BitConverter.ToSingle(raw,i*4)).ToArray();var at=Local(node,x,z);
            var region=root.GetProperty("Regions").EnumerateArray().Single(r=>r.GetProperty("Id").GetInt32()==R(node));double y=(float)((float)(region.GetProperty("Pivot")[1].GetInt32()*10)+(float)root.GetProperty("ParentCorner")[1].GetDouble());
            return -(plane[0]*at[0]+plane[2]*at[1]+plane[3])/plane[1]+y;
        }
        bool Contains(JsonElement polygon,JsonElement node,double[] point)
        {
            if(Math.Abs(point[1]-(Floor(polygon,node,point[0],point[2])+95))>1e-7)return false;
            double[][] vertices=polygon.GetProperty("Edges").EnumerateArray().Select(e=>{if(!localEvidence){var v=e.GetProperty("Vertex");return new[]{v[0].GetDouble(),v[2].GetDouble()};}var raw=Convert.FromHexString(e.GetProperty("Raw").GetString()!);Need(raw.Length==20,"original local edge bytes");return new[]{(double)BitConverter.ToSingle(raw,12),(double)BitConverter.ToSingle(raw,16)};}).ToArray();
            var at=localEvidence?Local(node,point[0],point[2]):new[]{point[0],point[2]};bool positive=true,negative=true;
            for(int i=0;i<vertices.Length;i++){var a=vertices[i];var b=vertices[(i+1)%vertices.Length];double cross=(b[0]-a[0])*(at[1]-a[1])-(b[1]-a[1])*(at[0]-a[0]);positive&=cross>=-1e-7;negative&=cross<=1e-7;}
            return positive||negative;
        }
        Need(nodes.Select(n=>(R(n),P(n))).Distinct().Count()==nodes.Length,"no repeated/cyclic saved nodes");
        foreach(var n in nodes){var poly=Polygon(n);Need(poly.GetProperty("Edges").EnumerateArray().All(e=>{var v=e.GetProperty("Vertex");return Math.Abs(Floor(poly,n,v[0].GetDouble(),v[2].GetDouble())-plan.FloorY)<=1e-7;}),"every original polygon stays on actual floor");}
        Need(Contains(Polygon(nodes[^1]),nodes[^1],plan.Standing),"exact persisted target polygon contains fixed Standing; Locate tie cannot rewrite it");
        Need(nodes[0].GetRawText()==plan.Selection.GetProperty("Context").GetProperty("Floor").GetProperty("Node").GetRawText(),"Bound route topology begins actual entry");var steps=new List<Step>{new(plan.Floor.Standing!,Array.Empty<uint>(),R(nodes[0]),P(nodes[0]))};
        for(int i=1;i<nodes.Length;i++)
        {
            var a=Polygon(nodes[i-1]);var b=Polygon(nodes[i]);double[] x,z;uint[] doors;
            if(R(nodes[i-1])!=R(nodes[i]))
            {
                var edges=root.GetProperty("Portals").EnumerateArray().Where(q=>q.GetProperty("SourceRegion").GetInt32()==R(nodes[i-1])&&q.GetProperty("SourcePolygon").GetInt32()==P(nodes[i-1])&&q.GetProperty("TargetRegion").GetInt32()==R(nodes[i])&&q.GetProperty("TargetPolygon").GetInt32()==P(nodes[i])).ToArray();Need(edges.Length==1,"Bound route unique original portal");var edge=edges[0];if(localEvidence)Need(edge.GetProperty("Transition").GetString()=="SameFloor","Bound route route cannot cross an unproved/partial/different-floor portal");
                Need(root.GetProperty("Portals").EnumerateArray().Count(q=>q.GetProperty("SourceRegion").GetInt32()==R(nodes[i])&&q.GetProperty("SourcePolygon").GetInt32()==P(nodes[i])&&q.GetProperty("TargetRegion").GetInt32()==R(nodes[i-1])&&q.GetProperty("TargetPolygon").GetInt32()==P(nodes[i-1])&&q.GetProperty("SourceEdge").GetInt32()==edge.GetProperty("TargetEdge").GetInt32()&&q.GetProperty("TargetEdge").GetInt32()==edge.GetProperty("SourceEdge").GetInt32())==1,"Bound route reciprocal portal");
                var ends=edge.GetProperty("Endpoints");x=ends[0].EnumerateArray().Select(v=>v.GetDouble()).ToArray();z=ends[1].EnumerateArray().Select(v=>v.GetDouble()).ToArray();doors=edge.GetProperty("DoorKeys").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();foreach(uint key in doors){Need(plan.Doors.Count(d=>d.Key==key)==1,"crossing requires one saved door");NativeDoor(plan.Doors.Single(d=>d.Key==key),null);}
            }
            else
            {
                var edges=a.GetProperty("Edges").EnumerateArray().ToArray();int edge=Array.FindIndex(edges,e=>e.GetProperty("Neighbor").GetInt32()==P(nodes[i])&&e.GetProperty("RegionTag").GetInt32()==65535&&(!localEvidence||e.GetProperty("Reference").GetString()=="Local"));Need(edge>=0,"Bound route original polygon neighbor");
                var start=edges[edge].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray();var end=edges[(edge+1)%edges.Length].GetProperty("Vertex").EnumerateArray().Select(v=>v.GetDouble()).ToArray();x=new[]{start[0],start[2]};z=new[]{end[0],end[2]};doors=Array.Empty<uint>();
                Need(b.GetProperty("Edges").EnumerateArray().Any(e=>e.GetProperty("Neighbor").GetInt32()==P(nodes[i-1])&&e.GetProperty("RegionTag").GetInt32()==65535&&(!localEvidence||e.GetProperty("Reference").GetString()=="Local")),"Bound route reciprocal polygon neighbor");
            }
            double px=(x[0]+z[0])/2,pz=(x[1]+z[1])/2;double floorA=Floor(a,nodes[i-1],px,pz),floorB=Floor(b,nodes[i],px,pz);
            Need(localEvidence?Math.Abs(floorA-floorB)<=1e-7:floorA==floorB,"Bound route portal/edge floor agreement");Need(doors.All(k=>plan.Doors.Count(d=>d.Key==k)==1),"Bound route every portal door is saved own-GO proof");steps.Add(new(new[]{px,floorA+95,pz},doors,R(nodes[i]),P(nodes[i])));
        }
        Need(steps.SelectMany(s=>s.Doors).Distinct().Order().SequenceEqual(plan.Doors.Select(d=>d.Key).Order()),"every saved required door is used and every crossed door is saved");
        Need(steps.All(s=>Math.Abs(s.Standing[1]-(plan.FloorY+95))<=1e-7),"whole route stays on actual arrival floor");
        steps.Add(new(plan.Standing,Array.Empty<uint>(),plan.Region,plan.Polygon));return steps.ToArray();
    }
}
