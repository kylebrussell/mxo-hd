using System;
using System.Collections.Generic;
using System.Linq;
using G = hds.MissionOpenFloorResolver;

namespace hds
{
    internal sealed partial class MissionInteriorEvidence
    {
        internal sealed class StreetAccess
        {
            internal readonly string OutdoorDigest,InteriorDigest,Kind;
            internal readonly Node Outside,Inside,Lobby;
            internal readonly Node[] OutsidePath,InsidePath;
            internal readonly DoorPose Door;
            internal readonly string RawDoor;
            internal readonly double[][] Opening;
            internal StreetAccess(string outdoor,string interior,string kind,Node outside,Node inside,Node lobby,Node[] path,Node[] interiorPath,DoorPose door,string raw,double[][] opening)
            {OutdoorDigest=outdoor;InteriorDigest=interior;Kind=kind;Outside=outside;Inside=inside;Lobby=lobby;OutsidePath=(Node[])path.Clone();InsidePath=(Node[])interiorPath.Clone();Door=door;RawDoor=raw;Opening=opening.Select(p=>(double[])p.Clone()).ToArray();}
        }
        private bool StreetInteriorPath(Node start,Node end,double floor,uint shaft,out Node[] path)
        {
            path=null;var previous=new Dictionary<Node,Node?>();var queue=new Queue<Node>();if(!Level(start,floor)||!Level(end,floor))return false;
            previous.Add(start,null);queue.Enqueue(start);
            while(queue.Count!=0)
            {
                Node n=queue.Dequeue();if(n.Equals(end))break;
                foreach(Edge edge in Poly(n).Edges)if(LocalReference(edge))Add(new Node(n.Region,edge.Neighbor),true);
                // Do not silently authorize a new internal door schema. Its existing certified route remains mandatory.
                foreach(Portal p in Outgoing(n).Where(p=>PortalUsable(p)&&!p.DoorKeys.Contains(shaft)&&p.DoorKeys.Length==0))Add(new Node(p.TargetRegion,p.TargetPolygon),true);
                void Add(Node next,bool allowed){if(allowed&&Level(next,floor)&&!previous.ContainsKey(next)){previous.Add(next,n);queue.Enqueue(next);}}
            }
            if(!previous.ContainsKey(end))return false;var result=new List<Node>();for(Node? n=end;n.HasValue;n=previous[n.Value])result.Add(n.Value);
            result.Reverse();path=result.ToArray();return true;
        }
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(Node,double,uint),HashSet<Node>> streetRealms=new System.Collections.Concurrent.ConcurrentDictionary<(Node,double,uint),HashSet<Node>>();
        internal bool StreetRealm(MissionNpcInteriorEntry saved,NavLocation at,uint shaft)
        {
            double floor=saved.Standing[1]-95;var root=new Node(saved.Node.Region,saved.Node.Polygon);
            if(at.sector!=document.Sector||!at.indoors||!Same(at.floorY,floor))return false;
            var nodes=streetRealms.GetOrAdd((root,floor,shaft),key=>
            {
                var found=new HashSet<Node>();var queue=new Queue<Node>();if(!Level(root,floor))return found;found.Add(root);queue.Enqueue(root);
                while(queue.Count!=0)
                {
                    Node n=queue.Dequeue();foreach(Edge edge in Poly(n).Edges)if(LocalReference(edge))Add(new Node(n.Region,edge.Neighbor));
                    foreach(Portal p in Outgoing(n).Where(p=>PortalUsable(p)&&p.DoorKeys.Length==0&&!p.DoorKeys.Contains(shaft)))Add(new Node(p.TargetRegion,p.TargetPolygon));
                    void Add(Node next){if(Level(next,floor)&&found.Add(next))queue.Enqueue(next);}
                }
                return found;
            });
            return nodes.Contains(new Node(at.region,at.polygon));
        }
        // This proves only the counted reciprocal public opening, never collision/pick success.
        private bool StreetOpening(MissionStreetAccessData outside,Portal portal,double floor)
        {
            var inside=new Node(portal.SourceRegion,portal.SourcePolygon);var external=new Node(portal.TargetRegion,portal.TargetPolygon);
            if(!Available(inside.Region)||!outside.Has(external)||portal.TargetEdge>=outside.At(external).Edges.Length||!Level(inside,floor))return false;
            byte[] reverse=outside.At(external).Edges[portal.TargetEdge].Bytes;
            if(BitConverter.ToUInt16(reverse)!=inside.Region||BitConverter.ToUInt16(reverse,2)!=inside.Polygon)return false;
            double[][] interiorEdge=Segment(inside.Region,inside.Polygon,portal.SourceEdge),exteriorEdge=outside.Segment(external,portal.TargetEdge),opening=portal.Endpoints;
            if(opening.Any(v=>!OnSegment(new double[]{v[0],0,v[1]},interiorEdge)||!OnSegment(new double[]{v[0],0,v[1]},exteriorEdge)))return false;
            return Math.Sqrt(Math.Pow(opening[1][0]-opening[0][0],2)+Math.Pow(opening[1][1]-opening[0][1],2))>=NavMesh.MinOpenEdge&&
                opening.All(v=>Same(Floor(Poly(inside),v[0],v[1]),floor)&&Same(outside.Floor(external,v[0],v[1]),floor));
        }
        // Canonical world-door identity has no mission/phase lease. Only an exact audited public portal qualifies.
        internal bool TryCanonicalPublicDoor(MissionStreetAccessData outside,StaticWorldObject raw,out DoorPose pose,out string refusal)
        {
            pose=null;refusal="InvalidCanonicalPublicDoor";
            if(outside==null||outside.InteriorDigest!=Digest||!outside.LegacyMatches(raw)||!outside.UniqueNativeKey(raw.mxoStaticId)||!groups.TryGetValue(raw.mxoStaticId,out Group group))return false;
            var natives=outside.Doors(raw.mxoStaticId);
            if(natives.Length!=1||natives[0].Class!=364||!MissionInteriorDoorTypes.IsSupported(natives[0].Goid))return false;
            var native=natives[0];
            Occurrence[] own=group.Occurrences.Where(o=>o.Goid==native.Goid&&o.RecordSha256.Equals(native.RecordSha256,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(own.Length!=1||native.Region!=own[0].Region)return false;
            foreach(Portal portal in document.Portals.Where(p=>p.SourceRegion==own[0].Region&&p.TargetRegion<document.OutdoorRegions&&p.DoorKeys.Length==1&&p.DoorKeys[0]==raw.mxoStaticId))
            {
                double floor=outside.NativeOrigin(native).Y;
                if(!StreetOpening(outside,portal,floor))continue;
                var on=outside.NativeOnOpening(portal.SourceRegion,portal.Endpoints,floor);
                if(on.Length!=1||on[0].Key!=raw.mxoStaticId)continue;
                pose=new DoorPose(raw.mxoStaticId,own[0]);refusal=null;return true;
            }
            return false;
        }
        // Fresh policy: a current-sector outdoor component must join an exactly reciprocal original sector-network boundary.
        // No traversal across that boundary is invented; all travel below stays in the validated current-sector graph.
        internal bool TryStreetAccess(MissionStreetAccessData outside,Entry lobby,uint shaft,Func<uint,StaticWorldObject> selectedRaw,out StreetAccess proof,out string refusal)
        {
            proof=null;refusal="NoCertifiedPublicNetworkComponent";if(outside==null)return false;
            foreach(Node start in outside.NetworkAnchors())
                if(TryStreetAccess(outside,lobby,start,shaft,selectedRaw,out StreetAccess candidate,out string why)&&
                    (proof==null||candidate.OutsidePath.Length+candidate.InsidePath.Length<proof.OutsidePath.Length+proof.InsidePath.Length||
                    candidate.OutsidePath.Length+candidate.InsidePath.Length==proof.OutsidePath.Length+proof.InsidePath.Length&&candidate.Door.Key<proof.Door.Key))proof=candidate;
            if(proof==null)return false;refusal=null;return true;
        }
        // The public starting node is explicit evidence. Choosing a fresh public world-network anchor is not done here.
        internal bool TryStreetAccess(MissionStreetAccessData outside,Entry lobby,Node publicStart,uint shaft,Func<uint,StaticWorldObject> selectedRaw,
            out StreetAccess proof,out string refusal)
        {
            proof=null;refusal="MissingStreetAccess";
            if(outside==null||lobby==null||!ReferenceEquals(lobby.Owner,this)||outside.InteriorDigest!=Digest||!outside.Has(publicStart)||selectedRaw==null)return false;
            double floor=lobby.Standing.Y-95;var choices=new List<StreetAccess>();bool unprovedDoorless=false;
            foreach(Portal portal in document.Portals.Where(p=>p.TargetRegion<document.OutdoorRegions).OrderBy(p=>p.SourceRegion).ThenBy(p=>p.SourcePolygon).ThenBy(p=>p.TargetRegion).ThenBy(p=>p.TargetPolygon))
            {
                var inside=new Node(portal.SourceRegion,portal.SourcePolygon);var external=new Node(portal.TargetRegion,portal.TargetPolygon);
                if(!StreetOpening(outside,portal,floor))continue;
                double[][] opening=portal.Endpoints;
                if(!outside.TryOutsidePath(publicStart,external,out Node[] route)||!StreetInteriorPath(inside,lobby.Node,floor,shaft,out Node[] inner))continue;
                var native=outside.NativeOnOpening(inside.Region,opening,floor);
                if(native.Length==0&&portal.DoorKeys.Length==0)
                {
                    // An empty origin/key set is not positive open-arch semantics or bounds clearance.
                    // Keep this category refused until its original no-door flags are independently decoded.
                    unprovedDoorless=true;continue;
                }
                if(portal.DoorKeys.Length!=1||native.Length!=1||native[0].Key!=portal.DoorKeys[0]||native[0].Class!=364||!MissionInteriorDoorTypes.IsSupported(native[0].Goid))continue;
                uint key=native[0].Key;if(!outside.UniqueNativeKey(key))continue;StaticWorldObject raw=selectedRaw(key);
                if(!outside.LegacyMatches(raw)||!outside.UniqueNativeKey(raw.mxoStaticId)||!groups.TryGetValue(key,out Group group)||outside.Doors(key).Length!=1)continue;
                Occurrence[] own=group.Occurrences.Where(o=>o.Region==inside.Region&&o.Goid==native[0].Goid&&o.RecordSha256.Equals(native[0].RecordSha256,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(own.Length!=1)continue;var door=new DoorPose(key,own[0]);
                choices.Add(new StreetAccess(outside.Digest,Digest,"OriginalPublicDoor",external,inside,lobby.Node,route,inner,door,MissionNpcInteriorPlanning.RawJson(raw),opening));
            }
            proof=choices.OrderBy(p=>p.OutsidePath.Length+p.InsidePath.Length).ThenBy(p=>p.Door==null?0:p.Door.Key).ThenBy(p=>p.Outside.Region).ThenBy(p=>p.Outside.Polygon).FirstOrDefault();
            if(proof==null){refusal=unprovedDoorless?"UnprovedOriginalDoorlessSemantics":"NoCertifiedPublicLobbyRoute";return false;}refusal=null;return true;
        }
    }
}
