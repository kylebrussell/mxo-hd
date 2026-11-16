using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
 // Fresh ordinary Furniture/Phone phases only. Computer/free-Safe/mixed spawned phases retain their exact selector.
 internal static class MissionBoundFurnitureAdmission
 {
  internal static bool Applies(MissionPhase phase,MissionSite site)=>phase?.site!=null&&site?.metrId==1&&phase.objects.Count>0&&
   phase.objects.All(d=>string.Equals(d.kind,"Furniture",StringComparison.OrdinalIgnoreCase)||string.Equals(d.kind,"Phone",StringComparison.OrdinalIgnoreCase))&&
   phase.objects.Any(d=>string.Equals(d.kind,"Furniture",StringComparison.OrdinalIgnoreCase))&&site.containers.Any(r=>MissionBoundFurniturePoses.SupportsGoid(MissionSites.Goid(r)));
  internal static bool BuildPools(MissionPhase phase,MissionSite site,Func<StaticWorldObject,bool> compatible,out MissionStaging.SourcePools pools,out string refusal)
  {
   pools=null;refusal=null;var rows=site.objectSpots.Concat(site.containers).ToArray();
   if(rows.Select(MissionOpenFloorSite.SpotFingerprint).Distinct().Count()!=rows.Length){refusal="AmbiguousFurnitureSources";return false;}
   bool Edge(MissionObjectDef def,StaticWorldObject row)=>!MissionBoundFurnitureRange.Supports(MissionSites.Goid(row))||compatible(row);
   // Callback is per definition: Phone priority/fallback and non-family rows keep their old behavior.
   var assignment=new MissionStaging.GeneratedSafeAssignment(new Dictionary<MissionObjectDef,StaticWorldObject>(),phase.objects,rows,site.containers,Edge);
   if(!assignment.Complete()){refusal="InsufficientCompatibleNativeFurnitureSources";return false;}
   pools=new MissionStaging.SourcePools(null,rows,assignment);return true;
  }
  internal static bool TryPools(MissionPhase phase,MissionSite site,out MissionStaging.SourcePools pools,out string refusal)
  {
   pools=null;refusal=null;
   if(!MissionStreetCoverage.TryFurnitureContext(site,out var context,out refusal))return false;
   if(context==null)return true; // No active enrolled server: preserve the isolated legacy geometry API.
   var loader=DataLoader.getInstance();var memo=new Dictionary<StaticWorldObject,bool>();
   bool Compatible(StaticWorldObject raw)
   {if(memo.TryGetValue(raw,out bool value))return value;value=MissionStreetFurnitureRoutes.TrySource(site,context,raw,key=>loader.WorldObjectsById.Find(key,site.metrId),out _,out _);memo.Add(raw,value);return value;}
   return BuildPools(phase,site,Compatible,out pools,out refusal);
  }
 }
}
