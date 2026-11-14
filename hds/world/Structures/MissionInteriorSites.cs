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
        internal static bool Eligible(MissionPhase phase,MissionSite site,out string refusal,int npcCount=0)
        {
            refusal=null;
            if(!MissionOpenFloorAdmission.Requires(phase))return true;
            if(!TryAllocate(site,npcCount,out _,out refusal))return false;
            var first=phase.objects.FirstOrDefault();if(first==null)return true;
            if(site.realObjects.Any(s=>MissionSites.RealObjects.TryGetValue(MissionSites.Goid(s),out var kind)&&
                string.Equals(kind,first.kind,StringComparison.OrdinalIgnoreCase)))return true; // already-bound native object
            var matching=site.objectSpots.Where(s=>MissionSites.ObjectSpawners.TryGetValue(MissionSites.Goid(s),out var kind)&&
                string.Equals(kind,first.kind,StringComparison.OrdinalIgnoreCase)).ToArray();
            // Exactly the first source picker's priority; no RNG is drawn and no source is rerolled.
            var sources=matching.Length>0?matching:site.objectSpots.Count>0?site.objectSpots.ToArray():site.containers.ToArray();
            if(!TryRegion(site,out var region,out refusal))return false;
            bool known=false;
            foreach(var raw in sources)
            {
                ushort goid=MissionStaging.ExpectedConcrete(first,raw);
                if(!MissionOpenFloorAdmission.Target(goid,raw)||!MissionOpenFloorProfiles.TryModel(goid,out var model)||
                    !MissionOpenFloorAnchors.TryCurrent(raw,out var anchor,out _))return true; // not proven by this necessary check
                OpenFloorOrientation orientation;
                try{orientation=new OpenFloorOrientation(Convert.ToHexString(anchor.Orientation));}catch(ArgumentException){return true;}
                if(!orientation.YawOnly)return true;
                known=true;
                if(region.CanFit(model,orientation,new G.Bounds(site.minX,site.maxX,site.minZ,site.maxZ)))return true;
            }
            if(known){refusal="NoInteriorFootprint:"+first.kind;return false;}
            return true;
        }
        internal static bool TryAllocate(MissionSite site,int count,out double[][] spots,out string refusal)
        {
            spots=Array.Empty<double[]>();
            if(!TryRegion(site,out var region,out refusal))return false;
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
        internal static NavPoint? Project(MissionSite site,double[] target,double radius)
        {
            return TryRegion(site,out var region,out _)?Project(region,site,target,radius):null;
        }
        private static NavPoint? Project(G.ReachableRegion region,MissionSite site,double[] target,double radius)
        {
            if(target==null||target.Length<3||!region.Project(new G.Position(target[0],target[1],target[2]),new G.Bounds(site.minX,site.maxX,site.minZ,site.maxZ),radius,out var p)||
                !NavMesh.Locate(site.metrId,p.X,p.Y,p.Z,out var at)||Math.Abs(at.floorY-region.Floor)>G.Epsilon)return null;
            return new NavPoint{x=p.X,y=p.Y,z=p.Z,location=at};
        }
        internal static bool TryRegion(MissionSite site,out G.ReachableRegion region,out string refusal)
        {
            region=null;refusal=null;
            if(!MissionOpenFloorAdmission.ValidSite(site)||site.metrId!=1){refusal="UnsupportedInteriorSite";return false;}
            string key=NavMesh.SnapshotRevision+":"+DataLoader.DataPath("mission_open_floor")+":"+MissionOpenFloorSite.CatalogFingerprint(site);
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
        private static bool Build(MissionSite site,out G.ReachableRegion region,out string refusal)
        {
            region=null;refusal=null;
            if(!MissionOpenFloorAdmission.ValidSite(site)||site.metrId!=1){refusal="UnsupportedInteriorSite";return false;}
            var door=site.elevatorDoor;
            var entry=MissionOpenFloorEntry.Calculate(site.metrId,new G.Position(door.pos_x,door.pos_y,door.pos_z),site.CentreX,site.CentreZ,
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
