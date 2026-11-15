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
        // The public starting node is explicit evidence. Choosing a fresh public world-network anchor is not done here.
        internal bool TryStreetAccess(MissionStreetAccessData outside,Entry lobby,Node publicStart,uint shaft,Func<uint,StaticWorldObject> selectedRaw,
            out StreetAccess proof,out string refusal)
        {
            proof=null;refusal="MissingStreetAccess";
            if(outside==null||lobby==null||!ReferenceEquals(lobby.Owner,this)||outside.InteriorDigest!=Digest||!outside.Has(publicStart)||selectedRaw==null)return false;
            double floor=lobby.Standing.Y-95;var choices=new List<StreetAccess>();
            foreach(Portal portal in document.Portals.Where(p=>p.TargetRegion<document.OutdoorRegions).OrderBy(p=>p.SourceRegion).ThenBy(p=>p.SourcePolygon).ThenBy(p=>p.TargetRegion).ThenBy(p=>p.TargetPolygon))
            {
                var inside=new Node(portal.SourceRegion,portal.SourcePolygon);var external=new Node(portal.TargetRegion,portal.TargetPolygon);
                if(!Available(inside.Region)||!outside.Has(external)||portal.TargetEdge>=outside.At(external).Edges.Length||!Level(inside,floor))continue;
                byte[] reverse=outside.At(external).Edges[portal.TargetEdge].Bytes;
                if(BitConverter.ToUInt16(reverse)!=inside.Region||BitConverter.ToUInt16(reverse,2)!=inside.Polygon)continue;
                double[][] interiorEdge=Segment(inside.Region,inside.Polygon,portal.SourceEdge),exteriorEdge=outside.Segment(external,portal.TargetEdge),opening=portal.Endpoints;
                if(opening.Any(v=>!OnSegment(new double[]{v[0],0,v[1]},interiorEdge)||!OnSegment(new double[]{v[0],0,v[1]},exteriorEdge)))continue;
                if(Math.Sqrt(Math.Pow(opening[1][0]-opening[0][0],2)+Math.Pow(opening[1][1]-opening[0][1],2))<NavMesh.MinOpenEdge||
                    opening.Any(v=>!Same(Floor(Poly(inside),v[0],v[1]),floor)||!Same(outside.Floor(external,v[0],v[1]),floor)))continue;
                if(!outside.TryOutsidePath(publicStart,external,out Node[] route)||!StreetInteriorPath(inside,lobby.Node,floor,shaft,out Node[] inner))continue;
                var native=outside.NativeOnOpening(inside.Region,opening,floor);
                if(native.Length==0&&portal.DoorKeys.Length==0&&outside.DoorlessOriginClear(inside.Region,opening,floor))
                {choices.Add(new StreetAccess(outside.Digest,Digest,"OriginalDoorlessOpening",external,inside,lobby.Node,route,inner,null,null,opening));continue;}
                if(portal.DoorKeys.Length!=1||native.Length!=1||native[0].Key!=portal.DoorKeys[0]||native[0].Class!=364||!MissionInteriorDoorTypes.IsSupported(native[0].Goid))continue;
                uint key=native[0].Key;StaticWorldObject raw=selectedRaw(key);
                if(!outside.LegacyMatches(raw)||!groups.TryGetValue(key,out Group group)||outside.Doors(key).Length!=1)continue;
                Occurrence[] own=group.Occurrences.Where(o=>o.Region==inside.Region&&o.Goid==native[0].Goid&&o.RecordSha256.Equals(native[0].RecordSha256,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(own.Length!=1)continue;var door=new DoorPose(key,own[0]);
                choices.Add(new StreetAccess(outside.Digest,Digest,"OriginalPublicDoor",external,inside,lobby.Node,route,inner,door,MissionNpcInteriorPlanning.RawJson(raw),opening));
            }
            proof=choices.OrderBy(p=>p.OutsidePath.Length+p.InsidePath.Length).ThenBy(p=>p.Door==null?0:p.Door.Key).ThenBy(p=>p.Outside.Region).ThenBy(p=>p.Outside.Polygon).FirstOrDefault();
            if(proof==null){refusal="NoCertifiedPublicLobbyRoute";return false;}refusal=null;return true;
        }
    }
}
