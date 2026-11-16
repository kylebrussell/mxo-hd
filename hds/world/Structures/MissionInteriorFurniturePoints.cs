using System;
using System.Collections.Generic;
using System.Linq;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
 internal sealed partial class MissionInteriorEvidence
 {
  // Candidate geometry only. The caller MUST validate native range, NAV identity AND TryPointRoute.
  // Clip each original convex polygon to the complement of the closed source-authored body AABB.
  // One outward float ULP is the representable strict-outside boundary, not a gameplay clearance radius.
  internal double[][] FurniturePoints(ushort region,double floor,double[] centre,double[] minimum,double[] maximum)
  {
   if(!Available(region)||!Vector(centre,3)||!Vector(minimum,3)||!Vector(maximum,3))return Array.Empty<double[]>();
   var points=new List<(double[] Point,int Polygon,double Distance)>();
   foreach(var polygon in regions[region].Polygons)
   {
    var node=new Node(region,(ushort)polygon.Index);if(!Level(node,floor))continue;
    var original=polygon.Edges.Select(e=>new[]{e.Vertex[0],e.Vertex[2]}).ToArray();
    var pieces=new List<double[][]>();double height=(double)(float)(floor+95);
    if(height<minimum[1]||height>maximum[1])pieces.Add(original);
    else
    {
     pieces.Add(Clip(original,0,MathF.BitDecrement((float)minimum[0]),false));
     pieces.Add(Clip(original,0,MathF.BitIncrement((float)maximum[0]),true));
     pieces.Add(Clip(original,1,MathF.BitDecrement((float)minimum[2]),false));
     pieces.Add(Clip(original,1,MathF.BitIncrement((float)maximum[2]),true));
    }
    foreach(var piece in pieces.Where(x=>x.Length>=3&&x.All(p=>p.All(double.IsFinite))))
    {
     var nearest=Closest(piece,centre[0],centre[2]);var p=new[]{(double)(float)nearest[0],height,(double)(float)nearest[1]};
     if(!Contains(polygon,new G.Position(p[0],p[1],p[2])))continue;
     points.Add((p,polygon.Index,(p[0]-centre[0])*(p[0]-centre[0])+(p[2]-centre[2])*(p[2]-centre[2])));
    }
   }
   return points.OrderBy(x=>x.Distance).ThenBy(x=>x.Polygon).ThenBy(x=>x.Point[0]).ThenBy(x=>x.Point[2]).Select(x=>x.Point)
    .GroupBy(p=>(p[0],p[1],p[2])).Select(g=>g.First()).ToArray();
  }
  private static double[][] Clip(double[][] input,int axis,double boundary,bool above)
  {
   var output=new List<double[]>();if(input.Length==0)return output.ToArray();
   bool Inside(double[] p)=>above?p[axis]>=boundary:p[axis]<=boundary;
   var a=input[^1];bool aIn=Inside(a);
   foreach(var b in input)
   {
    bool bIn=Inside(b);if(aIn!=bIn){double t=(boundary-a[axis])/(b[axis]-a[axis]);var p=new[]{a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])};p[axis]=boundary;output.Add(p);}
    if(bIn)output.Add((double[])b.Clone());a=b;aIn=bIn;
   }
   return output.ToArray();
  }
  private static double[] Closest(double[][] polygon,double x,double z)
  {
   bool positive=true,negative=true;
   for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];double cross=(b[0]-a[0])*(z-a[1])-(b[1]-a[1])*(x-a[0]);positive&=cross>=-G.Epsilon;negative&=cross<=G.Epsilon;}
   if(positive||negative)return new[]{x,z};
   double[] best=null;double distance=double.PositiveInfinity;
   for(int i=0;i<polygon.Length;i++)
   {var a=polygon[i];var b=polygon[(i+1)%polygon.Length];double dx=b[0]-a[0],dz=b[1]-a[1],length=dx*dx+dz*dz;double t=length>0?Math.Clamp(((x-a[0])*dx+(z-a[1])*dz)/length,0,1):0;
    var p=new[]{a[0]+t*dx,a[1]+t*dz};double d=(p[0]-x)*(p[0]-x)+(p[1]-z)*(p[1]-z);if(d<distance){distance=d;best=p;}}
   return best;
  }
 }
}
