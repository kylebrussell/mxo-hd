using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using E=hds.MissionInteriorEvidence;
namespace hds
{
 // Optional owned authority for NEW ordinary bound326 Street selections. It never upgrades an old checkpoint.
 internal static class MissionStreetBoundRoutes
 {
  internal sealed class Route
  {
   public int ObjectId {get;set;}public string RawSourceJson {get;set;}public ushort Region {get;set;}
   public double[] NativePosition {get;set;}public string NativeQuaternion {get;set;}public double[] Standing {get;set;}
   public MissionNpcInteriorNode Target {get;set;}public MissionNpcInteriorNode[] Nodes {get;set;}public MissionNpcInteriorDoor[] Doors {get;set;}
  }
  internal sealed class Envelope
  {
   public int Version {get;set;}public string Kind {get;set;}public string EvidenceDigest {get;set;}public string BoundManifest {get;set;}public Route[] Routes {get;set;}
  }
  private static void Need(bool ok,string reason){if(!ok)throw new InvalidDataException("BoundStreetRoute:"+reason);}
  private static bool Same<T>(T a,T b)=>JsonSerializer.Serialize(a,MissionProgress.SaveJson)==JsonSerializer.Serialize(b,MissionProgress.SaveJson);
  private static bool Point(double[] p)=>p!=null&&p.Length==3&&p.All(double.IsFinite);
  private static bool Hex(string s,int n)=>s!=null&&s.Length==n&&s.All(Uri.IsHexDigit);
  private static void Fields(JsonElement e,params string[] names)=>Need(e.ValueKind==JsonValueKind.Object&&e.EnumerateObject().Count()==names.Length&&names.All(n=>e.TryGetProperty(n,out _)),"Fields");
  internal static void Shape(MissionProgress p,JsonElement json)
  {
   try
   {
    Fields(json,"Version","Kind","EvidenceDigest","BoundManifest","Routes");Need(json.GetProperty("Routes").ValueKind==JsonValueKind.Array,"RoutesArray");
    foreach(var row in json.GetProperty("Routes").EnumerateArray())
    {
     Fields(row,"ObjectId","RawSourceJson","Region","NativePosition","NativeQuaternion","Standing","Target","Nodes","Doors");
     Fields(row.GetProperty("Target"),"Region","Polygon");
     foreach(string key in new[]{"Nodes","Doors"})Need(row.GetProperty(key).ValueKind==JsonValueKind.Array,key);
     foreach(var node in row.GetProperty("Nodes").EnumerateArray())Fields(node,"Region","Polygon");
     foreach(var door in row.GetProperty("Doors").EnumerateArray())Fields(door,"Key","Goid","RawDoorJson","Quaternion","Position");
    }
    var e=Read(json);Need(e.Version==1&&e.Kind=="Bound326Routes"&&Hex(e.EvidenceDigest,64)&&e.BoundManifest==MissionBoundObjectPoses.AuditedManifestSha256&&e.Routes!=null&&e.Routes.Length>0&&
     e.Routes.Select(r=>r.ObjectId).Distinct().Count()==e.Routes.Length&&e.Routes.All(r=>r.ObjectId>0&&r.ObjectId<=ushort.MaxValue&&!string.IsNullOrWhiteSpace(r.RawSourceJson)&&Point(r.NativePosition)&&Hex(r.NativeQuaternion,32)&&Point(r.Standing)&&
      r.Nodes!=null&&r.Nodes.Length>0&&r.Target.Region==r.Region&&r.Nodes.Last().Equals(r.Target)&&r.Doors!=null&&r.Doors.Select(d=>d?.Key).Distinct().Count()==r.Doors.Length&&r.Doors.All(d=>d!=null&&d.Key!=0&&!string.IsNullOrWhiteSpace(d.RawDoorJson)&&MissionInteriorDoorTypes.IsSupported(d.Goid)&&Point(d.Position)&&Hex(d.Quaternion,32))),"EnvelopeShape");
   }
   catch(Exception ex) when(ex is InvalidDataException||ex is JsonException||ex is InvalidOperationException||ex is ArgumentException)
   {throw new MissionPlacementRestoreException(p,"Invalid recognized bound street route:"+ex.Message);}
  }
  internal static Envelope Read(JsonElement json)=>JsonSerializer.Deserialize<Envelope>(json.GetRawText(),MissionProgress.SaveJson);
  internal static void ValidateShapeOwner(MissionProgress p,MissionStreetSelection selected)
  {
   try{ValidateShapeOwnerCore(p,selected);}
   catch(Exception ex) when(ex is InvalidDataException||ex is JsonException||ex is ArgumentException)
   {throw new MissionPlacementRestoreException(p,"Invalid recognized bound street owner/source:"+ex.Message);}
  }
  private static void ValidateShapeOwnerCore(MissionProgress p,MissionStreetSelection selected)
  {
   if(selected.BoundRoutes.ValueKind==JsonValueKind.Undefined)return;
   Need(selected.Mission==p.mission.id&&selected.Identity==p.persistenceId&&selected.Started==p.startedAt&&selected.Site?.District==p.mission.district&&
    selected.Phase>=0&&selected.Phase<p.mission.phases.Count&&selected.Phase<=p.phaseIndex+1,"OwnerMissionPhase");
   Shape(p,selected.BoundRoutes);var e=Read(selected.BoundRoutes);var site=selected.Site.Resolve(MissionSites.For(p.mission.district));
   Need(selected.Version==2&&selected.Kind=="Objects"&&selected.Refusal==null&&selected.Context!=null&&e.EvidenceDigest==selected.Context.Floor.EvidenceDigest,"Parent");
   var choices=e.Routes.Select(r=>new MissionOpenFloorChoice((ushort)r.ObjectId,326,r.RawSourceJson)).ToArray();
   Need(site!=null&&MissionOpenFloorAdmission.ChoicesValid(p.mission.phases[selected.Phase],site,choices)&&choices.All(c=>MissionSites.Goid(c.ReadSpot())==326),"ExactSourceChoices");
  }
  private static E Evidence(MissionStreetSelection s)
  {
   var raw=MissionNpcInteriorPlanning.ReadRaw(s.Context.Floor.RawDoorJson);
   return MissionNpcCoverage.SavedEvidence(raw.metrId,raw.sectorID,s.Context.Floor.EvidenceDigest);
  }
  internal static MissionStreetSelection Build(MissionProgress p,MissionStreetSelection selected,MissionOpenFloorChoice[] choices,Func<uint,StaticWorldObject> lookup)
  {
   if(choices.Length==0||choices.Any(c=>c.Goid!=326||MissionSites.Goid(c.ReadSpot())!=326))return selected;
   var index=Evidence(selected);var entry=MissionNpcInteriorCheckpoint.SavedEntry(index,selected.Context.Floor);var routes=new List<Route>();
   foreach(var choice in choices)
   {
    var raw=choice.ReadSpot();Need(MissionBoundObjectPoses.TryBounds(new MissionObjectState{spot=raw,goid=326},out var bounds,out string why),why);
    Need(bounds.NavSha256==index.NavSourceSha256&&raw.metrId==p.mission.district&&raw.sectorID==(selected.Site.DoorKey>>20)&&bounds.Region>=0&&bounds.Region<=ushort.MaxValue,"SourceWorldSector");
    double[] centre=MissionBound326Range.Centre(bounds),from=selected.Context.Floor.Standing;
    Need(NavMesh.Locate(raw.metrId,from[0],from[1],from[2],out var at)&&at.sector==raw.sectorID&&at.region==entry.Node.Region&&at.polygon==entry.Node.Polygon&&from[1]==at.floorY+95,"ActualFixedEntry");
    Need(NavMesh.Project(raw.metrId,centre[0],centre[1],centre[2],out var target,MissionBound326Range.Envelope(bounds),from,false),"NoNativeRangeStanding");
    double[] standing=target.Position.Select(v=>(double)(float)v).ToArray();
    Need(NavMesh.Locate(raw.metrId,standing[0],standing[1],standing[2],out var located)&&located.sector==raw.sectorID&&located.region==bounds.Region&&located.floorY==at.floorY&&standing[1]==located.floorY+95&&MissionBound326Range.Valid(standing,bounds),"NativeStandingScope");
    Need(index.TryPointRoute(entry,(ushort)bounds.Region,standing,lookup,out var route,out why),why);
    routes.Add(new Route{ObjectId=choice.ObjectId,RawSourceJson=choice.RawSpotJson,Region=(ushort)bounds.Region,NativePosition=centre,NativeQuaternion=Convert.ToHexString(bounds.Pose.Orientation),Standing=standing,
     Target=new MissionNpcInteriorNode(route.Target.Region,route.Target.Polygon),Nodes=route.Nodes.Select(n=>new MissionNpcInteriorNode(n.Region,n.Polygon)).ToArray(),
     Doors=route.RequiredDoors.OrderBy(d=>d.Key).Select(d=>new MissionNpcInteriorDoor(d.Key,d.Goid,MissionNpcInteriorPlanning.RawJson(lookup(d.Key)),d.Quaternion,new[]{d.Position.X,d.Position.Y,d.Position.Z})).ToArray()});
   }
   var envelope=new Envelope{Version=1,Kind="Bound326Routes",EvidenceDigest=index.Digest,BoundManifest=MissionBoundObjectPoses.AuditedManifestSha256,Routes=routes.ToArray()};
   return new MissionStreetSelection(2,selected.Mission,selected.Identity,selected.Started,selected.Phase,selected.Site,selected.Kind,selected.Context,null,JsonSerializer.SerializeToElement(envelope,MissionProgress.SaveJson));
  }
  // Replay uses saved points/nodes only. Never Project, choose a source, or consume RNG.
  internal static void Validate(MissionProgress p,MissionStreetSelection selected,Func<uint,StaticWorldObject> lookup,MissionOpenFloorChoice[] expected=null)
  {
   if(selected.BoundRoutes.ValueKind==JsonValueKind.Undefined)return;
   ValidateShapeOwner(p,selected);var saved=Read(selected.BoundRoutes);var index=Evidence(selected);var entry=MissionNpcInteriorCheckpoint.SavedEntry(index,selected.Context.Floor);
   if(expected!=null)Need(Same(expected.OrderBy(c=>c.ObjectId).ToArray(),saved.Routes.Select(r=>new MissionOpenFloorChoice((ushort)r.ObjectId,326,r.RawSourceJson)).OrderBy(c=>c.ObjectId).ToArray()),"ChangedPendingSources");
   foreach(var row in saved.Routes)
   {
    var raw=MissionNpcInteriorPlanning.ReadRaw(row.RawSourceJson);Need(MissionBoundObjectPoses.TryBounds(new MissionObjectState{spot=raw,goid=326},out var bounds,out string why),why);
    Need(bounds.NavSha256==index.NavSourceSha256&&raw.metrId==p.mission.district&&raw.sectorID==(selected.Site.DoorKey>>20)&&row.Region==bounds.Region&&row.NativePosition.SequenceEqual(MissionBound326Range.Centre(bounds))&&row.NativeQuaternion==Convert.ToHexString(bounds.Pose.Orientation)&&MissionBound326Range.Valid(row.Standing,bounds),"ChangedFixedSourceOrStanding");
    Need(NavMesh.Locate(raw.metrId,row.Standing[0],row.Standing[1],row.Standing[2],out var at)&&at.sector==raw.sectorID&&at.region==row.Region&&at.polygon==row.Target.Polygon&&row.Standing[1]==at.floorY+95&&at.floorY==selected.Context.Floor.Standing[1]-95,"ChangedFixedFloor");
    var selectedDoors=row.Doors.ToDictionary(d=>d.Key,d=>MissionNpcInteriorPlanning.ReadRaw(d.RawDoorJson));
    StaticWorldObject Door(uint key){Need(selectedDoors.TryGetValue(key,out var savedRaw),"MissingSavedDoor");var current=lookup(key);Need(current!=null&&Same(current,savedRaw),"ChangedSelectedOwnGoidDoor");return current;}
    Need(index.ValidatePointRoute(entry,row.Region,row.Standing,row.Nodes.Select(n=>new E.Node(n.Region,n.Polygon)).ToArray(),Door,out var doors,out why),why);
    var actual=doors.OrderBy(d=>d.Key).Select(d=>new MissionNpcInteriorDoor(d.Key,d.Goid,MissionNpcInteriorPlanning.RawJson(Door(d.Key)),d.Quaternion,new[]{d.Position.X,d.Position.Y,d.Position.Z})).ToArray();Need(Same(actual,row.Doors),"ChangedRequiredDoorPose");
   }
  }
  internal static void RequireCurrentSources(MissionProgress p,MissionStreetSelection selected)
  {
   if(selected.Phase!=p.phaseIndex||selected.BoundRoutes.ValueKind==JsonValueKind.Undefined)return;
   var expected=Read(selected.BoundRoutes).Routes.OrderBy(r=>r.ObjectId).ToArray();var current=p.objects.Values.OrderBy(o=>o.def.id).ToArray();
   Need(current.Length==expected.Length&&current.Select((o,i)=>o.OpenFloor==null&&o.goid==326&&o.def.id==expected[i].ObjectId&&MissionNpcInteriorPlanning.RawJson(o.spot)==expected[i].RawSourceJson).All(v=>v),"ChangedCurrentObjects");
  }
  internal static MissionNpcInteriorDoor[] CurrentDoors(MissionProgress p)
  {
   if(p==null||p.site==null||!p.streetWitnesses.TryGetValue(p.phaseIndex,out var selected)||selected.BoundRoutes.ValueKind==JsonValueKind.Undefined)return Array.Empty<MissionNpcInteriorDoor>();
   Need(selected.Mission==p.mission.id&&selected.Identity==p.persistenceId&&selected.Started==p.startedAt&&selected.Phase==p.phaseIndex&&selected.Site.Fingerprint==MissionOpenFloorSite.CatalogFingerprint(p.site),"CurrentOwner");
   return Read(selected.BoundRoutes).Routes.SelectMany(r=>r.Doors).GroupBy(d=>d.Key).Select(g=>{Need(g.All(d=>Same(d,g.First())),"ConflictingDoor");return g.First();}).ToArray();
  }
 }
}
