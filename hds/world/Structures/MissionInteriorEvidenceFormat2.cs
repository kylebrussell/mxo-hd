using System;
using System.Collections.Generic;
using System.Linq;
using G = hds.MissionOpenFloorResolver;
namespace hds
{
    internal sealed partial class MissionInteriorEvidence
    {
        private static double[] LocalPoint(Region region,double x,double z)
        {
            double a=x-region.Translation[0],b=z-region.Translation[2];
            switch(region.Rotation){case 0:return new double[]{a,b};case 1:return new double[]{-b,a};case 2:return new double[]{-a,-b};default:return new double[]{b,-a};}
        }
        private static bool InsideLocal(double[][] vertices,double x,double z)
        {
            bool positive=true,negative=true;
            for(int i=0;i<vertices.Length;i++)
            {
                double[] a=vertices[i],b=vertices[(i+1)%vertices.Length];double cross=(b[0]-a[0])*(z-a[1])-(b[1]-a[1])*(x-a[0]);
                positive &= cross>=-G.Epsilon;negative &= cross<=G.Epsilon;
            }
            return positive||negative;
        }
        private bool LocalReference(Edge edge) => edge.Neighbor!=0xffff&&(document.Format==1||edge.Reference=="Local");
        private bool PortalUsable(Portal portal) => document.Format==1||portal.Transition=="SameFloor";
        private void ValidateFormat2Region(Region region)
        {
            Require(region.PrefabLength>0&&region.InavOffset>=0&&(long)region.InavOffset+16<=region.PrefabLength&&Hex(region.RawInavHeader,32),"InteriorRawINAVHeader");
            byte[] header=Convert.FromHexString(region.RawInavHeader);
            Require(BitConverter.ToUInt32(header,0)==0x56414e49&&BitConverter.ToUInt16(header,12)==region.Polygons.Length,"InteriorRawINAVCount");
            region.Translation=Enumerable.Range(0,3).Select(i=>(double)(float)((float)(region.Pivot[i]*10)+(float)document.ParentCorner[i])).ToArray();
            long cursor=region.InavOffset+16;
            foreach(Polygon polygon in region.Polygons)
            {
                Require(polygon!=null&&polygon.Edges!=null&&polygon.Edges.All(e=>e!=null)&&polygon.SourceOffset==cursor&&Hex(polygon.RawHeader,40)&&cursor+20+20L*polygon.Edges.Length<=region.PrefabLength,"InteriorRawPolygonBounds");
                byte[] raw=Convert.FromHexString(polygon.RawHeader);Require(BitConverter.ToUInt32(raw,16)==polygon.Edges.Length,"InteriorRawPolygonCount");
                cursor+=20+20L*polygon.Edges.Length;
            }
        }
        private void ValidateFormat2Polygon(Region region,Polygon polygon)
        {
            byte[] header=Convert.FromHexString(polygon.RawHeader);double[] local=Enumerable.Range(0,4).Select(i=>(double)BitConverter.ToSingle(header,i*4)).ToArray();
            Require(Vector(local,4)&&Math.Abs(local[1])>G.Epsilon,"InteriorRawPlane");
            double[] normal=Turn(local[0],local[1],local[2],region.Rotation);
            double[] expectedPlane=new double[]{normal[0],normal[1],normal[2],local[3]-((normal[0]*region.Translation[0]+normal[1]*region.Translation[1])+normal[2]*region.Translation[2])};
            Require(polygon.Plane.SequenceEqual(expectedPlane),"InteriorRawPlaneTransform");
            polygon.Owner=region;polygon.LocalPlane=local;polygon.LocalVertices=new double[polygon.Edges.Length][];
            for(int i=0;i<polygon.Edges.Length;i++)
            {
                Edge edge=polygon.Edges[i];Require(Hex(edge.Raw,40)&&edge.SourceOffset==polygon.SourceOffset+20+i*20&&Vector(edge.Vertex,3),"InteriorRawEdgeBounds");
                byte[] raw=Convert.FromHexString(edge.Raw);Require(BitConverter.ToUInt16(raw,0)==edge.RegionTag&&BitConverter.ToUInt16(raw,2)==edge.Neighbor,"InteriorRawEdgeReference");
                double[] values=Enumerable.Range(0,4).Select(j=>(double)BitConverter.ToSingle(raw,4+j*4)).ToArray();Require(Vector(values,4),"InteriorRawEdgeValues");
                string reference=edge.Neighbor==0xffff?"Boundary":edge.RegionTag==0xffff?"Local":"UnprovedRegionReference";
                Require(edge.Reference==reference&&(reference!="Local"||edge.Neighbor<region.Polygons.Length),"InteriorRawLocalReference");
                double x=values[2],z=values[3],y=-(local[0]*x+local[2]*z+local[3])/local[1];
                Require(edge.Vertex.SequenceEqual(NativePointValues(x,y,z,region)),"InteriorRawVertexTransform");polygon.LocalVertices[i]=new double[]{x,z};
            }
        }
        private void ValidateFormat2Tables()
        {
            foreach(Region region in document.Regions)
            {
                Require(region.PortalCountOffset>=0&&region.PortalCount>=0&&Hex(region.RawPortalCount,8)&&BitConverter.ToUInt32(Convert.FromHexString(region.RawPortalCount),0)==region.PortalCount&&
                    (long)region.PortalCountOffset+4+26L*region.PortalCount<=document.SidiBufferLength,"InteriorRawPortalCount");
                Portal[] rows=document.Portals.Where(p=>p.SourceRegion==region.Id).ToArray();
                Require(rows.Length==region.PortalCount&&rows.All(p=>p.CountOffset==region.PortalCountOffset&&p.Count==region.PortalCount),"InteriorRawPortalTableRows");
            }
        }
        private double[][] SectorSegment(ushort region,ushort polygon,byte edge)
        {
            Segment(region,polygon,edge);Polygon p=regions[region].Polygons[polygon];Region r=regions[region];
            double[] Transform(double[] local)
            {
                double[] v=Turn(local[0],0,local[1],r.Rotation);
                return new double[]{(float)((float)v[0]+(float)(r.Pivot[0]*10)),(float)((float)v[2]+(float)(r.Pivot[2]*10))};
            }
            return new double[][]{Transform(p.LocalVertices[edge]),Transform(p.LocalVertices[(edge+1)%p.Edges.Length])};
        }
        private static double[][] LocalPortal(Portal p)
        {
            Require(p!=null&&Hex(p.Raw,52),"InteriorPortalTableBounds");
            byte[] raw=Convert.FromHexString(p.Raw);return new double[][]{new double[]{BitConverter.ToSingle(raw,10),BitConverter.ToSingle(raw,14)},new double[]{BitConverter.ToSingle(raw,18),BitConverter.ToSingle(raw,22)}};
        }
        private string ClassifyPortal(Portal p)
        {
            if(Available(p.SourceRegion)&&regions[p.SourceRegion].Polygons.Length==0 || Available(p.TargetRegion)&&regions[p.TargetRegion].Polygons.Length==0)return "EmptyRegion";
            if(Available(p.SourceRegion))Segment(p.SourceRegion,p.SourcePolygon,p.SourceEdge);
            if(Available(p.TargetRegion))Segment(p.TargetRegion,p.TargetPolygon,p.TargetEdge);
            if(!Available(p.SourceRegion)||!Available(p.TargetRegion))return "UnavailableRegion";
            double[][] local=LocalPortal(p);if(EqualPoint(local[0],local[1]))return "DegeneratePortal";
            Portal[] reverse=document.Portals.Where(q=>q.SourceRegion==p.TargetRegion&&q.SourcePolygon==p.TargetPolygon&&q.SourceEdge==p.TargetEdge&&q.TargetRegion==p.SourceRegion&&q.TargetPolygon==p.SourcePolygon&&q.TargetEdge==p.SourceEdge).ToArray();
            if(reverse.Length!=1||!EqualSegment(local,LocalPortal(reverse[0])))return "UnprovedReverse";
            double[][] source=SectorSegment(p.SourceRegion,p.SourcePolygon,p.SourceEdge),target=SectorSegment(p.TargetRegion,p.TargetPolygon,p.TargetEdge);
            if(!EqualSegment(source,local)||!EqualSegment(target,local))
                return local.All(a=>OnSegment(new double[]{a[0],0,a[1]},source)&&OnSegment(new double[]{a[0],0,a[1]},target))?"PartialPortal":"UnprovedEndpoints";
            double[] a=p.Endpoints.Select(at=>Floor(Poly(new Node(p.SourceRegion,p.SourcePolygon)),at[0],at[1])).ToArray();
            double[] b=p.Endpoints.Select(at=>Floor(Poly(new Node(p.TargetRegion,p.TargetPolygon)),at[0],at[1])).ToArray();
            if(!a.Zip(b,Same).All(x=>x))return "DifferentFloor";
            if(!Same(a[0],a[1])||!Same(b[0],b[1]))return "NonLevelPortal";
            return "SameFloor";
        }
        private double[] SectorOccurrence(Occurrence occurrence)
        {
            byte[] raw=Convert.FromHexString(occurrence.Raw);Region r=regions[occurrence.Region];double[] point=Turn(BitConverter.ToSingle(raw,8),BitConverter.ToSingle(raw,12),BitConverter.ToSingle(raw,16),r.Rotation);
            return Enumerable.Range(0,3).Select(i=>(double)(float)((float)point[i]+(float)(r.Pivot[i]*10))).ToArray();
        }
        private void ValidatePortalDoors(Portal portal)
        {
            double[][] local=LocalPortal(portal);
            uint[] expected=document.Groups.Where(g=>g.Occurrences.Any(o=>o.Goid!=19&&(o.Region==portal.SourceRegion||o.Region==portal.TargetRegion)&&OnSegment(SectorOccurrence(o),local))).Select(g=>g.Key).OrderBy(k=>k).ToArray();
            Require(expected.SequenceEqual(portal.DoorKeys.OrderBy(k=>k)),"InteriorPortalDoorAssociation");
        }
    }
}
