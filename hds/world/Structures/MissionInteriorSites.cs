using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using G=hds.MissionOpenFloorResolver;

namespace hds
{
    // Generated mission floors are inferred clusters, not authoritative building volumes. Before
    // selecting one for a free prop, require a proven footprint in the entry's interior. Phases
    // placing actors also need same-interior anchors and actual separated standing positions.
    internal static class MissionInteriorSites
    {
        private sealed class Cached
        {
            internal string Key,Refusal;
            internal G.ReachableRegion Region;
            internal bool Result;
        }
        private static readonly ConditionalWeakTable<MissionSite,Cached> cache=new ConditionalWeakTable<MissionSite,Cached>();
        // One latest pure result per measured site, independent of the unchanged legacy slot.
        private static readonly ConditionalWeakTable<MissionSite,Cached> measuredCache=new ConditionalWeakTable<MissionSite,Cached>();
        internal static bool Eligible(MissionPhase phase,MissionSite site,out string refusal,int npcCount=0)
            =>CheckSite(phase,site,out refusal,npcCount,true);
        // A committed choice is proved by its exact request/fixed witness, not unused pool rows.
        internal static bool SelectedSiteEligible(MissionPhase phase,MissionSite site,out string refusal,int npcCount=0,MissionElevatorWitness elevator=null)
            =>CheckSite(phase,site,out refusal,npcCount,false,elevator);
        private static bool CheckSite(MissionPhase phase,MissionSite site,out string refusal,int npcCount,bool checkSourcePool,MissionElevatorWitness elevator=null)
        {
            refusal=null;
            if(!MissionOpenFloorAdmission.Requires(phase))return true;
            // With no runtime actors and enough same-kind native objects, SelectObjects never
            // creates a free prop. Preserve that existing bound-only path without a new NAV proof.
            if(npcCount==0&&site!=null&&phase.objects.Where(o=>new[]{"Box","Computer","Safe","Mainframe"}.Contains(o.kind,StringComparer.OrdinalIgnoreCase))
                .GroupBy(o=>o.kind,StringComparer.OrdinalIgnoreCase).All(g=>site.realObjects.Count(s=>
                    MissionSites.RealObjects.TryGetValue(MissionSites.Goid(s),out var kind)&&string.Equals(kind,g.Key,StringComparison.OrdinalIgnoreCase))>=g.Count()))return true;
            MissionStaging.SourcePools pools=null;
            if(checkSourcePool&&!MissionStaging.TrySourcePools(phase,site,npcCount,out pools,out refusal))return false;
            elevator=elevator??pools?.Elevator;
            if(!TryAllocate(site,npcCount,out _,out refusal,elevator))return false;
            var first=phase.objects.FirstOrDefault();if(first==null)return true;
            var bound=site.realObjects.Where(s=>MissionSites.RealObjects.TryGetValue(MissionSites.Goid(s),out var kind)&&
                string.Equals(kind,first.kind,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(bound.Length>0)
            {
                if(!TryRegion(site,out var nativeRegion,out refusal,elevator))return false;
                bool Possible(StaticWorldObject s)
                {
                    if(!MissionOpenFloorProfiles.TryUsable(MissionSites.Goid(s),out var usable))return true;
                    OpenFloorOrientation orientation;try{orientation=new OpenFloorOrientation(s.quat);}catch(ArgumentException){return true;}
                    return nativeRegion.CouldReachNative(new G.Position(s.pos_x,s.pos_y,s.pos_z),usable,orientation);
                }
                // SelectObjects binds the first matching native row deterministically.
                if(Possible(bound[0]))return true;
                refusal="NoInteriorNativeUse:"+first.kind;return false;
            }
            if(!checkSourcePool)return true;
            var matching=site.objectSpots.Where(s=>(pools==null||pools.Allows(s))&&MissionSites.ObjectSpawners.TryGetValue(MissionSites.Goid(s),out var kind)&&
                string.Equals(kind,first.kind,StringComparison.OrdinalIgnoreCase)).ToArray();
            // Exactly the first source picker's priority; no RNG is drawn and no source is rerolled.
            var available=site.objectSpots.Where(s=>pools==null||pools.Allows(s)).ToArray();
            var sources=matching.Length>0?matching:available.Length>0?available:site.containers.Where(s=>pools==null||pools.Allows(s)).ToArray();
            if(!TryRegion(site,out var region,out refusal,elevator))return false;
            foreach(var raw in sources.Where(s=>pools==null||pools.Allows(s)))
            {
                ushort goid=MissionStaging.ExpectedConcrete(first,raw);
                if(!MissionOpenFloorAdmission.Target(goid,raw)||!MissionOpenFloorProfiles.TryModel(goid,out var model)||
                    !MissionOpenFloorAnchors.TryCurrent(raw,out var anchor,out _))continue; // unknown cannot hide another proved impossible source
                OpenFloorOrientation orientation;
                try{orientation=new OpenFloorOrientation(Convert.ToHexString(anchor.Orientation));}catch(ArgumentException){continue;}
                if(!orientation.YawOnly)continue;
                // The ordinary shuffle may choose ANY row in this priority pool. A fitting
                // alternative does not authorize committing a different, impossible row.
                if(!region.CanFit(model,orientation,new G.Bounds(site.minX,site.maxX,site.minZ,site.maxZ)))
                {refusal="NoInteriorFootprint:"+first.kind;return false;}
            }
            return true;
        }
        internal static bool TryAllocate(MissionSite site,int count,out double[][] spots,out string refusal,MissionElevatorWitness elevator=null)
        {
            spots=Array.Empty<double[]>();
            if(!TryRegion(site,out var region,out refusal,elevator))return false;
            if(count<0){refusal="InvalidInteriorNpcCount";return false;}
            var markers=site.npcSpots.Where(s=>s!=null&&s.Length>=4&&s.Take(4).All(double.IsFinite))
                .OrderByDescending(s=>Math.Pow(s[0]-site.elevatorDoor.pos_x,2)+Math.Pow(s[2]-site.elevatorDoor.pos_z,2)).ToArray();
            if(count>0&&markers.Length==0){refusal="InteriorNoNpcMarkers";return false;}
            if(count>0&&!markers.Any(s=>NavMesh.Locate(site.metrId,s[0],s[1],s[2],out var at)&&region.SameSpace(at)))
            {refusal="InteriorEntryHasNoSiteAnchor";return false;}
            var occupied=new List<double[]>();
            NavPoint? ProjectAt(double[] target,double radius)=>Project(region,site,target,radius);
            for(int i=0;i<count;i++)
            {
                var marker=markers[i%markers.Length];int lap=i/markers.Length;
                var desired=new[]{marker[0]+120*lap,marker[1],marker[2]+80*lap,marker[3]};
                // No player fallback: a remote/outdoor marker must not be collapsed into a sealed lift.
                if(!MissionPlacement.Resolve(site.metrId,desired,null,markers,false,out var p,out _,occupied,site,
                    at=>region.Contains(new G.Position(at.x,at.y,at.z)),ProjectAt))
                {refusal="InteriorNpcCapacity:"+i+"/"+count;return false;}
                occupied.Add(new[]{p.x,p.y,p.z,marker[3]});
            }
            spots=occupied.ToArray();return true;
        }
        internal static bool Contains(MissionSite site,NavPoint point)
            =>TryRegion(site,out var region,out _)&&region.Contains(new G.Position(point.x,point.y,point.z));
        // The caller supplies its current persisted elevator context; the legacy overload stays unchanged.
        internal static bool Contains(MissionSite site,NavPoint point,MissionElevatorWitness elevator)
            =>TryRegion(site,out var region,out _,elevator)&&region.Contains(new G.Position(point.x,point.y,point.z));
        internal static NavPoint? Project(MissionSite site,double[] target,double radius)
        {
            return TryRegion(site,out var region,out _)?Project(region,site,target,radius):null;
        }
        private static NavPoint? Project(G.ReachableRegion region,MissionSite site,double[] target,double radius)
        {
            if(target==null||target.Length<3||!region.Project(new G.Position(target[0],target[1],target[2]),new G.Bounds(site.minX,site.maxX,site.minZ,site.maxZ),radius,out var p)||
                !NavMesh.Locate(site.metrId,p.X,p.Y,p.Z,out var at)||!region.SameSpace(at))return null;
            return new NavPoint{x=p.X,y=p.Y,z=p.Z,location=at};
        }
        internal static bool TryRegion(MissionSite site,out G.ReachableRegion region,out string refusal,MissionElevatorWitness elevator=null)
        {
            region=null;refusal=null;
            if(!MissionOpenFloorAdmission.ValidSite(site)||site.metrId!=1){refusal="UnsupportedInteriorSite";return false;}
            string key=NavMesh.SnapshotRevision+":"+DataLoader.DataPath("mission_open_floor")+":"+MissionOpenFloorSite.CatalogFingerprint(site);
            if(elevator!=null)
            {
                string witness;
                try {witness=System.Text.Json.JsonSerializer.Serialize(elevator,MissionProgress.SaveJson);}
                catch(Exception ex) when(ex is ArgumentException||ex is System.Text.Json.JsonException||ex is NotSupportedException)
                {
                    // Key construction must not introduce a new rejection/exception for malformed inputs.
                    return Build(site,out region,out refusal,elevator);
                }
                key+=":"+MissionOpenFloorSite.Hash(witness);
                var measured=measuredCache.GetOrCreateValue(site);
                lock(measured)
                {
                    if(measured.Key!=key)
                    {
                        measured.Result=Build(site,out measured.Region,out measured.Refusal,elevator);measured.Key=key;
                    }
                    region=measured.Region;refusal=measured.Refusal;return measured.Result;
                }
            }
            var entry=cache.GetOrCreateValue(site);
            lock(entry)
            {
                if(entry.Key!=key)
                {
                    entry.Result=Build(site,out entry.Region,out entry.Refusal);entry.Key=key;
                }
                region=entry.Region;refusal=entry.Refusal;return entry.Result;
            }
        }
        private static bool Build(MissionSite site,out G.ReachableRegion region,out string refusal,MissionElevatorWitness elevator=null)
        {
            region=null;refusal=null;
            if(!MissionOpenFloorAdmission.ValidSite(site)||site.metrId!=1){refusal="UnsupportedInteriorSite";return false;}
            var door=site.elevatorDoor;
            var entry=MissionOpenFloorEntry.Calculate(site.metrId,MissionElevatorPoses.Floor(site,elevator),site.CentreX,site.CentreZ,
                "normal Ride entry for generated interior admission");
            if(entry.Root==null){refusal=entry.Refusal;return false;}
            if(!NavMesh.Locate(site.metrId,entry.Root.Standing.X,entry.Root.Standing.Y,entry.Root.Standing.Z,out var rootNav)||!rootNav.indoors)
            {refusal="InteriorEntryIsNotAPrefab";return false;}
            if(!MissionOpenFloorScenes.TryLoad(site.metrId,door.sectorID,out var scene,out refusal))return false;
            var sectors=new HashSet<ushort>{door.sectorID,entry.Root.Surface.Sector};
            foreach(var spot in site.npcSpots)
            {
                if(spot==null||spot.Length<3||!spot.Take(3).All(double.IsFinite))continue;
                if(NavMesh.Locate(site.metrId,spot[0],spot[1],spot[2],out var at))
                {
                    sectors.Add(at.sector);
                }
            }
            if(!NavMesh.TryOpenFloorSurfaces(site.metrId,sectors.OrderBy(s=>s).ToArray(),out var surfaces,out refusal)||
                !G.TryReachableRegion(site.metrId,surfaces,entry.Root,scene,out region,out refusal))return false;
            return true;
        }
    }
}
