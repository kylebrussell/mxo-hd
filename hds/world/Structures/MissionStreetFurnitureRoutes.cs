using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using E=hds.MissionInteriorEvidence;
namespace hds
{
 // Separate tagged authority. Existing Bound326Routes/V1 and Street selections1/2 are never reinterpreted.
 internal static class MissionStreetFurnitureRoutes
 {
  internal sealed class Route
  {
   public int ObjectId {get;set;}public ushort Goid {get;set;}public string ProfileManifestSha256 {get;set;}public string RawSourceJson {get;set;}public ushort Region {get;set;}
   public double[] NativePosition {get;set;}public string NativeQuaternion {get;set;}public double[] Standing {get;set;}
   public MissionNpcInteriorNode Target {get;set;}public MissionNpcInteriorNode[] Nodes {get;set;}public MissionNpcInteriorDoor[] Doors {get;set;}
  }
  internal sealed class Envelope
  {
   public int Version {get;set;}public string Kind {get;set;}public string EvidenceDigest {get;set;}public string FurnitureManifest {get;set;}
   public MissionOpenFloorChoice[] Choices {get;set;}public Route[] Routes {get;set;}
  }
  static void Need(bool condition,string why){if(!condition)throw new InvalidDataException("FurnitureStreetRoute:"+why);}
  static bool Same<T>(T a,T b)=>JsonSerializer.Serialize(a,MissionProgress.SaveJson)==JsonSerializer.Serialize(b,MissionProgress.SaveJson);
  static bool Point(double[] p)=>p?.Length==3&&p.All(double.IsFinite);
  static bool Hex(string s,int n)=>s?.Length==n&&s.All(Uri.IsHexDigit);
  static void Fields(JsonElement e,params string[] names)=>Need(e.ValueKind==JsonValueKind.Object&&e.EnumerateObject().Count()==names.Length&&names.All(n=>e.TryGetProperty(n,out _)),"Fields");
  internal static Envelope Read(JsonElement json)=>JsonSerializer.Deserialize<Envelope>(json.GetRawText(),MissionProgress.SaveJson);
  internal static void Shape(MissionProgress p,JsonElement json)
  {
   try
   {
    Fields(json,"Version","Kind","EvidenceDigest","FurnitureManifest","Choices","Routes");
    foreach(var choice in json.GetProperty("Choices").EnumerateArray())Fields(choice,"ObjectId","Goid","RawSpotJson");
    foreach(var row in json.GetProperty("Routes").EnumerateArray())
    {Fields(row,"ObjectId","Goid","ProfileManifestSha256","RawSourceJson","Region","NativePosition","NativeQuaternion","Standing","Target","Nodes","Doors");Fields(row.GetProperty("Target"),"Region","Polygon");
     foreach(var n in row.GetProperty("Nodes").EnumerateArray())Fields(n,"Region","Polygon");foreach(var d in row.GetProperty("Doors").EnumerateArray())Fields(d,"Key","Goid","RawDoorJson","Quaternion","Position");}
    var e=Read(json);Need(e.Version==1&&e.Kind=="BoundFurnitureRoutes"&&Hex(e.EvidenceDigest,64)&&e.FurnitureManifest==MissionBoundFurniturePoses.AuditedManifestSha256&&
     e.Choices!=null&&e.Choices.Length>0&&e.Choices.Select(c=>c?.ObjectId).Distinct().Count()==e.Choices.Length&&e.Routes!=null&&e.Routes.Length>0&&
     e.Routes.Any(r=>MissionBoundFurniturePoses.SupportsGoid(r.Goid))&&e.Routes.Select(r=>r?.ObjectId).Distinct().Count()==e.Routes.Length,"Envelope");
    foreach(var r in e.Routes)
     Need(r!=null&&r.ObjectId>0&&r.ObjectId<=ushort.MaxValue&&MissionBoundFurnitureRange.Supports(r.Goid)&&
      r.ProfileManifestSha256==(r.Goid==326?MissionBoundObjectPoses.AuditedManifestSha256:MissionBoundFurniturePoses.AuditedManifestSha256)&&!string.IsNullOrWhiteSpace(r.RawSourceJson)&&
      Point(r.NativePosition)&&Hex(r.NativeQuaternion,32)&&Point(r.Standing)&&r.Target.Region==r.Region&&r.Nodes?.Length>0&&r.Nodes.Last().Equals(r.Target)&&r.Nodes.Distinct().Count()==r.Nodes.Length&&
      r.Doors!=null&&r.Doors.Select(d=>d?.Key).Distinct().Count()==r.Doors.Length&&r.Doors.All(d=>d!=null&&d.Key!=0&&MissionInteriorDoorTypes.IsSupported(d.Goid)&&!string.IsNullOrWhiteSpace(d.RawDoorJson)&&Point(d.Position)&&Hex(d.Quaternion,32)),"RouteShape");
   }
   catch(Exception ex)when(ex is InvalidDataException||ex is JsonException||ex is InvalidOperationException||ex is ArgumentException)
   {throw new MissionPlacementRestoreException(p,"Invalid recognized furniture route:"+ex.Message);}
  }
  internal static void ValidateShapeOwner(MissionProgress p,MissionStreetSelection selected)
  {
   if(selected.FurnitureRoutes.ValueKind==JsonValueKind.Undefined)return;
   try
   {
    Shape(p,selected.FurnitureRoutes);var e=Read(selected.FurnitureRoutes);
    Need(selected.Version==3&&selected.BoundRoutes.ValueKind==JsonValueKind.Undefined&&selected.Kind=="Objects"&&selected.Refusal==null&&selected.Context!=null&&
      selected.Mission==p.mission.id&&selected.Identity==p.persistenceId&&selected.Started==p.startedAt&&selected.Phase>=0&&selected.Phase<p.mission.phases.Count&&selected.Phase<=p.phaseIndex+1&&selected.Site?.District==p.mission.district&&e.EvidenceDigest==selected.Context.Floor.EvidenceDigest,"Owner");
    var site=selected.Site.Resolve(p.sites)??selected.Site.Resolve(MissionSites.For(p.mission.district));
    Need(site!=null&&MissionOpenFloorAdmission.ChoicesValid(p.mission.phases[selected.Phase],site,e.Choices)&&e.Choices.All(c=>c.Goid==MissionSites.Goid(c.ReadSpot())),"ExactChoiceSet");
    var bound=e.Choices.Where(c=>MissionBoundFurnitureRange.Supports(c.Goid)).ToArray();
    Need(bound.Length==e.Routes.Length&&e.Routes.All(r=>bound.Count(c=>c.ObjectId==r.ObjectId&&c.Goid==r.Goid&&c.RawSpotJson==r.RawSourceJson)==1),"CompleteTypedRows");
   }
   catch(Exception ex)when(ex is InvalidDataException||ex is JsonException||ex is ArgumentException)
   {throw new MissionPlacementRestoreException(p,"Invalid recognized furniture owner/source:"+ex.Message);}
  }
  static E Evidence(MissionStreetContexts.Context context)
  {var raw=MissionNpcInteriorPlanning.ReadRaw(context.Floor.RawDoorJson);return MissionNpcCoverage.SavedEvidence(raw.metrId,raw.sectorID,context.Floor.EvidenceDigest);}
  // Pure necessary source compatibility used BEFORE immutable random selection. No source substitution/RNG.
  internal static bool TrySource(MissionSite site,MissionStreetContexts.Context context,StaticWorldObject raw,Func<uint,StaticWorldObject> lookup,out Route row,out string refusal)
  {
   row=null;refusal=null;
   try
   {
    ushort goid=MissionSites.Goid(raw);Need(MissionBoundFurnitureRange.Supports(goid),"UnsupportedFamily");
    Need(MissionBoundFurnitureRange.TryBounds(new MissionObjectState{spot=raw,goid=goid},out var bounds,out string why),why);
    var index=Evidence(context);var entry=MissionNpcInteriorCheckpoint.SavedEntry(index,context.Floor,lookup(site.elevatorDoor.mxoStaticId));
    Need(raw.metrId==site.metrId&&raw.sectorID==site.elevatorDoor.sectorID&&bounds.NavSha256==index.NavSourceSha256&&bounds.Region>=0&&bounds.Region<=ushort.MaxValue,"NativeEvidenceDomain");
    double[] centre=MissionBoundFurnitureRange.Centre(bounds),from=context.Floor.Standing;double floor=from[1]-95;
    Need(NavMesh.Locate(raw.metrId,from[0],from[1],from[2],out var root)&&root.sector==raw.sectorID&&root.region==entry.Node.Region&&root.polygon==entry.Node.Polygon&&root.floorY==floor,"FixedEntry");
    IEnumerable<double[]> Candidates()
    {
     // Keep the original cabinet approach when valid. Authored doors can connect rooms
     // that the component projection cannot reach from the elevator.
     if(goid==326&&NavMesh.Project(raw.metrId,centre[0],centre[1],centre[2],out var old,MissionBoundFurnitureRange.Envelope(bounds),from,false))
      yield return old.Position.Select(v=>(double)(float)v).ToArray();
     foreach(var point in index.FurniturePoints((ushort)bounds.Region,floor,centre,bounds.Minimum,bounds.Maximum))
      yield return point;
    }
    refusal="NoNativeFurnitureStandingOrAuthoredRoute";
    foreach(var standing in Candidates())
    {
     if(!MissionBoundFurnitureRange.Valid(standing,bounds)||!NavMesh.Locate(raw.metrId,standing[0],standing[1],standing[2],out var at)||
        at.sector!=raw.sectorID||at.region!=bounds.Region||at.floorY!=floor||standing[1]!=at.floorY+95)continue;
     if(!index.TryPointRoute(entry,(ushort)bounds.Region,standing,lookup,out var route,out string routeWhy)){refusal=routeWhy;continue;}
     row=new Route{Goid=goid,ProfileManifestSha256=bounds.Manifest,RawSourceJson=MissionNpcInteriorPlanning.RawJson(raw),Region=(ushort)bounds.Region,NativePosition=centre,NativeQuaternion=Convert.ToHexString(bounds.Pose.Orientation),Standing=standing,
       Target=new MissionNpcInteriorNode(route.Target.Region,route.Target.Polygon),Nodes=route.Nodes.Select(n=>new MissionNpcInteriorNode(n.Region,n.Polygon)).ToArray(),
       Doors=route.RequiredDoors.OrderBy(d=>d.Key).Select(d=>new MissionNpcInteriorDoor(d.Key,d.Goid,MissionNpcInteriorPlanning.RawJson(lookup(d.Key)),d.Quaternion,new[]{d.Position.X,d.Position.Y,d.Position.Z})).ToArray()};
     refusal=null;return true;
    }
    return false;
   }
   catch(Exception ex)when(ex is InvalidDataException||ex is MissionCheckpointException||ex is JsonException||ex is IOException||ex is ArgumentException){refusal=ex.Message;return false;}
  }
  internal static MissionStreetSelection Build(MissionProgress p,MissionStreetSelection selected,MissionOpenFloorChoice[] choices,Func<uint,StaticWorldObject> lookup)
  {
   if(!choices.Any(c=>MissionBoundFurniturePoses.SupportsGoid(c.Goid)&&MissionSites.Goid(c.ReadSpot())==c.Goid))return selected;
   return BuildOnSite(p,selected,selected.Site.Resolve(MissionSites.For(p.mission.district)),choices,lookup);
  }
  // Pure injection seam shared by real admission and the explicitly captured disposable QA fixture.
  internal static MissionStreetSelection BuildOnSite(MissionProgress p,MissionStreetSelection selected,MissionSite site,MissionOpenFloorChoice[] choices,Func<uint,StaticWorldObject> lookup)
  {
   Need(selected.Version==1&&selected.Refusal==null&&selected.Context!=null&&selected.Kind=="Objects"&&selected.Mission==p.mission.id&&selected.Identity==p.persistenceId&&selected.Started==p.startedAt&&
    selected.Phase>=0&&selected.Phase<p.mission.phases.Count&&selected.Phase<=p.phaseIndex+1&&site!=null&&selected.Site.Resolve(new[]{site})!=null&&
    MissionOpenFloorAdmission.ChoicesValid(p.mission.phases[selected.Phase],site,choices)&&choices.All(c=>c.Goid==MissionSites.Goid(c.ReadSpot())),"FreshSelection");
   var routes=new List<Route>();
   foreach(var c in choices.Where(c=>MissionBoundFurnitureRange.Supports(c.Goid)))
   {Need(TrySource(site,selected.Context,c.ReadSpot(),lookup,out var row,out string why),why);row.ObjectId=c.ObjectId;Need(row.RawSourceJson==c.RawSpotJson,"RawChoiceEncoding");routes.Add(row);}
   var e=new Envelope{Version=1,Kind="BoundFurnitureRoutes",EvidenceDigest=selected.Context.Floor.EvidenceDigest,FurnitureManifest=MissionBoundFurniturePoses.AuditedManifestSha256,Choices=(MissionOpenFloorChoice[])choices.Clone(),Routes=routes.ToArray()};
   return new MissionStreetSelection(3,selected.Mission,selected.Identity,selected.Started,selected.Phase,selected.Site,selected.Kind,selected.Context,null,furnitureRoutes:JsonSerializer.SerializeToElement(e,MissionProgress.SaveJson));
  }
  internal static void Validate(MissionProgress p,MissionStreetSelection selected,Func<uint,StaticWorldObject> lookup,MissionOpenFloorChoice[] expected=null)
  {
   if(selected.FurnitureRoutes.ValueKind==JsonValueKind.Undefined)return;ValidateShapeOwner(p,selected);var e=Read(selected.FurnitureRoutes);var index=Evidence(selected.Context);var entry=MissionNpcInteriorCheckpoint.SavedEntry(index,selected.Context.Floor);
   if(expected!=null)Need(Same(expected,e.Choices),"ChangedPendingChoices");
   foreach(var r in e.Routes)
   {
    var raw=MissionNpcInteriorPlanning.ReadRaw(r.RawSourceJson);Need(MissionBoundFurnitureRange.TryBounds(new MissionObjectState{spot=raw,goid=r.Goid},out var b,out string why),why);
    Need(b.Manifest==r.ProfileManifestSha256&&b.NavSha256==index.NavSourceSha256&&r.Region==b.Region&&raw.metrId==p.mission.district&&raw.sectorID==(selected.Site.DoorKey>>20)&&r.NativePosition.SequenceEqual(MissionBoundFurnitureRange.Centre(b))&&r.NativeQuaternion==Convert.ToHexString(b.Pose.Orientation)&&MissionBoundFurnitureRange.Valid(r.Standing,b),"ChangedFixedProfile");
    Need(NavMesh.Locate(raw.metrId,r.Standing[0],r.Standing[1],r.Standing[2],out var at)&&at.sector==raw.sectorID&&at.region==r.Region&&at.floorY==selected.Context.Floor.Standing[1]-95&&r.Standing[1]==at.floorY+95,"ChangedFixedFloor");
    var savedDoors=r.Doors.ToDictionary(d=>d.Key,d=>MissionNpcInteriorPlanning.ReadRaw(d.RawDoorJson));
    StaticWorldObject Door(uint key){Need(savedDoors.TryGetValue(key,out var saved),"MissingSavedDoor");var current=lookup(key);Need(current!=null&&Same(current,saved),"ChangedOwnGoidDoor");return current;}
    Need(index.ValidatePointRoute(entry,r.Region,r.Standing,r.Nodes.Select(n=>new E.Node(n.Region,n.Polygon)).ToArray(),Door,out var doors,out why),why);
    var actual=doors.OrderBy(d=>d.Key).Select(d=>new MissionNpcInteriorDoor(d.Key,d.Goid,MissionNpcInteriorPlanning.RawJson(Door(d.Key)),d.Quaternion,new[]{d.Position.X,d.Position.Y,d.Position.Z})).ToArray();Need(Same(actual,r.Doors),"ChangedDoorPose");
   }
  }
  internal static void RequireCurrentSources(MissionProgress p,MissionStreetSelection s)
  {
   if(s.Phase!=p.phaseIndex||s.FurnitureRoutes.ValueKind==JsonValueKind.Undefined)return;var expected=Read(s.FurnitureRoutes).Choices.OrderBy(c=>c.ObjectId).ToArray();var actual=p.objects.Values.OrderBy(o=>o.def.id).ToArray();
   Need(actual.Length==expected.Length&&actual.Select((o,i)=>o.OpenFloor==null&&o.def.id==expected[i].ObjectId&&o.goid==expected[i].Goid&&MissionNpcInteriorPlanning.RawJson(o.spot)==expected[i].RawSpotJson).All(v=>v),"ChangedCurrentChoices");
  }
  // Called only after the current fresh/pending/restore V3 route has passed full validation.
  // The closure is runtime-only and rechecks its exact owner/source/selection before every pose use.
  internal static void BindValidatedCurrent(MissionProgress p,MissionObjectState state)
  {
   state.FurniturePresentation=null;
   if(!p.streetWitnesses.TryGetValue(p.phaseIndex,out var selected)||selected.FurnitureRoutes.ValueKind==JsonValueKind.Undefined)return;
   ValidateShapeOwner(p,selected);
   if(!MissionBoundFurniturePoses.SupportsGoid(state.goid))return;
   var expected=Read(selected.FurnitureRoutes).Choices.SingleOrDefault(c=>c.ObjectId==state.def?.id);
   Need(expected!=null&&expected.Goid==state.goid&&state.OpenFloor==null&&expected.RawSpotJson==MissionNpcInteriorPlanning.RawJson(state.spot),"PresentationSource");
   string signature=JsonSerializer.Serialize(selected,MissionProgress.SaveJson);int phase=p.phaseIndex;var owner=p.mission;
   var identity=p.persistenceId;var started=p.startedAt;var source=state.spot;var definition=state.def;
   MissionComputerAnchors.Pose Current()
   {
    Need(ReferenceEquals(p.mission,owner)&&p.persistenceId==identity&&p.startedAt==started&&p.phaseIndex==phase&&p.site!=null&&
      p.streetWitnesses.TryGetValue(phase,out var now)&&ReferenceEquals(now,selected)&&JsonSerializer.Serialize(now,MissionProgress.SaveJson)==signature&&
      now.Site.Fingerprint==MissionOpenFloorSite.CatalogFingerprint(p.site)&&ReferenceEquals(state.spot,source)&&ReferenceEquals(state.def,definition)&&
      p.objects.TryGetValue(expected.ObjectId,out var actual)&&ReferenceEquals(actual,state)&&state.OpenFloor==null&&state.goid==expected.Goid&&
      MissionNpcInteriorPlanning.RawJson(source)==expected.RawSpotJson,"ChangedPresentationBinding");
    Need(MissionBoundFurniturePoses.TryCurrent(state,out var pose,out string why),why);return pose;
   }
   state.FurniturePresentation=Current;_ = Current();
  }
  internal static MissionNpcInteriorDoor[] CurrentDoors(MissionProgress p)
  {
   if(p?.site==null||!p.streetWitnesses.TryGetValue(p.phaseIndex,out var s)||s.FurnitureRoutes.ValueKind==JsonValueKind.Undefined)return Array.Empty<MissionNpcInteriorDoor>();
   Need(s.Mission==p.mission.id&&s.Identity==p.persistenceId&&s.Started==p.startedAt&&s.Phase==p.phaseIndex&&s.Site.Fingerprint==MissionOpenFloorSite.CatalogFingerprint(p.site),"CurrentOwner");
   return Read(s.FurnitureRoutes).Routes.SelectMany(r=>r.Doors).GroupBy(d=>d.Key).Select(g=>{Need(g.All(d=>Same(d,g.First())),"ConflictingDoor");return g.First();}).ToArray();
  }
 }
}
