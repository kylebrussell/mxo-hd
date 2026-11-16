using System;
using System.IO;
using System.Linq;
namespace hds
{
 internal static class MissionBoundFurnitureRange
 {
  internal sealed class Bounds
  {
   internal readonly ushort Goid; internal readonly string Manifest,NavSha256;internal readonly MissionComputerAnchors.Pose Pose;internal readonly int Region;
   internal readonly double[] Minimum,Maximum;internal readonly float[] HalfExtents;
   internal Bounds(MissionBoundFurniturePoses.Bounds b){Goid=b.Goid;Manifest=MissionBoundFurniturePoses.AuditedManifestSha256;NavSha256=b.NavSha256;Pose=b.Pose;Region=b.Region;Minimum=b.Minimum;Maximum=b.Maximum;HalfExtents=b.HalfExtents;}
   internal Bounds(MissionBoundObjectPoses.Bounds b){Goid=326;Manifest=MissionBoundObjectPoses.AuditedManifestSha256;NavSha256=b.NavSha256;Pose=b.Pose;Region=b.Region;Minimum=b.Minimum;Maximum=b.Maximum;HalfExtents=new float[]{25,80,30};}
  }
  internal static bool Supports(ushort goid)=>goid==326||MissionBoundFurniturePoses.SupportsGoid(goid);
  internal static bool TryBounds(MissionObjectState state,out Bounds bounds,out string refusal)
  {
   bounds=null;
   if(state?.goid==326){if(!MissionBoundObjectPoses.TryBounds(state,out var old,out refusal))return false;bounds=new Bounds(old);return true;}
   if(!MissionBoundFurniturePoses.TryBounds(state,out var current,out refusal))return false;bounds=new Bounds(current);return true;
  }

  internal static double[] Centre(Bounds b)=>new[]{b.Pose.X,b.Pose.Y,b.Pose.Z};
  static bool Point(double[] p)=>p!=null&&p.Length==3&&p.All(double.IsFinite);
    static float[] Basis(string hex)
    {
   byte[] b;try{b=Convert.FromHexString(hex);}catch(Exception e)when(e is FormatException || e is ArgumentNullException){throw new InvalidDataException("Invalid native quaternion",e);}
        if(b.Length!=16)throw new InvalidDataException("Invalid native quaternion");var q=Enumerable.Range(0,4).Select(i=>BitConverter.ToSingle(b,i*4)).ToArray();
        if(q.Any(v=>!float.IsFinite(v))||Math.Abs(q.Sum(v=>(double)v*v)-1)>1e-5)throw new InvalidDataException("Invalid native quaternion");
        float x=q[0],y=q[1],z=q[2],w=q[3],tx=2*x,ty=2*y,tz=2*z,xx=tx*x,xy=ty*x,xz=tz*x,yy=ty*y,yz=tz*y,zz=tz*z,xw=tx*w,yw=ty*w,zw=tz*w;
        return new[]{1-(zz+yy),xy+zw,xz-yw,xy-zw,1-(zz+xx),yz+xw,xz+yw,yz-xw,1-(xx+yy)};
    }
    // Exact existing native float predicate, restricted to separately audited20-type furniture Open100 and per-GOID original model halves.
    public static float NativeDistanceSquared(double[] standing,Bounds bounds)
    {
        if(!Point(standing)||!Point(Centre(bounds)))return float.NaN;var b=Basis(Convert.ToHexString(bounds.Pose.Orientation));float x=(float)(standing[0]-Centre(bounds)[0]),y=(float)(standing[1]-Centre(bounds)[1]),z=(float)(standing[2]-Centre(bounds)[2]);
        float[] p={(float)((double)x*b[0]+(double)y*b[1]+(double)z*b[2]),(float)((double)x*b[3]+(double)y*b[4]+(double)z*b[5]),(float)((double)x*b[6]+(double)y*b[7]+(double)z*b[8])};
        float[] ext=bounds.HalfExtents;float sum=0;for(int i=0;i<3;i++){if(!float.IsFinite(p[i]))return float.NaN;float outside=p[i]<-ext[i]?(float)(p[i]+ext[i]):p[i]>ext[i]?(float)(p[i]-ext[i]):0;sum=(float)((double)outside*outside+sum);}return sum;
    }
    public static double Envelope(Bounds bounds)
    {
        var b=Basis(Convert.ToHexString(bounds.Pose.Orientation));double error2=0;
        for(int i=0;i<3;i++)for(int j=0;j<3;j++){double dot=0;for(int k=0;k<3;k++)dot+=(double)b[k*3+i]*b[k*3+j];double error=dot-(i==j?1:0);error2+=error*error;}
        var ext=bounds.HalfExtents;double errorNorm=Math.Sqrt(error2);double radius=errorNorm<1?(100+Math.Sqrt(ext.Sum(v=>(double)v*v)))/Math.Sqrt(1-errorNorm):double.NaN;
        if(!double.IsFinite(radius))throw new InvalidDataException("Unbounded native basis");return radius;
    }

  internal static bool Valid(double[] standing,Bounds bounds)
  {
   if(!Point(standing)||!standing.Where((v,i)=>v<bounds.Minimum[i]||v>bounds.Maximum[i]).Any())return false;
   float distance=NativeDistanceSquared(standing,bounds);
   return float.IsFinite(distance)&&distance>0&&distance<=10000&&ContainerActions.InReach(standing.SelectMany(BitConverter.GetBytes).ToArray(),bounds.Pose.X,bounds.Pose.Y,bounds.Pose.Z);
  }
 }
}
