using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace hds
{
    internal readonly struct OpenFloorPoint
    {
        public double X {get;} public double Y {get;} public double Z {get;}
        public OpenFloorPoint(double x,double y,double z){X=x;Y=y;Z=z;}
        public bool Finite=>double.IsFinite(X)&&double.IsFinite(Y)&&double.IsFinite(Z);
    }
    internal sealed class OpenFloorOrientation
    {
        public string Hex {get;}
        private readonly float[] quaternion;
        // Native6200e130 produces these rows. World model points use the transposed basis.
        private readonly float[] basis;
        public bool YawOnly=>quaternion[0]==0&&quaternion[2]==0;
        public OpenFloorOrientation(string hex)
        {
            byte[] b;try{b=Convert.FromHexString(hex??"");}catch(FormatException){throw new ArgumentException("InvalidQuaternion");}
            if(b.Length!=16)throw new ArgumentException("InvalidQuaternion");
            quaternion=Enumerable.Range(0,4).Select(i=>BitConverter.Int32BitsToSingle(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(i*4,4)))).ToArray();
            if(quaternion.Any(q=>!float.IsFinite(q))||Math.Abs(quaternion.Sum(q=>(double)q*q)-1)>1e-5)throw new ArgumentException("InvalidQuaternion");
            Hex=hex;float x=quaternion[0],y=quaternion[1],z=quaternion[2],w=quaternion[3];
            float tx=2*x,ty=2*y,tz=2*z,xx=tx*x,xy=ty*x,xz=tz*x,yy=ty*y,yz=tz*y,zz=tz*z,xw=tx*w,yw=ty*w,zw=tz*w;
            basis=new[]{1-(zz+yy),xy+zw,xz-yw,xy-zw,1-(zz+xx),yz+xw,xz+yw,yz-xw,1-(xx+yy)};
        }
        public OpenFloorPoint ToWorld(OpenFloorPoint p)=>new OpenFloorPoint(
            basis[0]*p.X+basis[3]*p.Y+basis[6]*p.Z,basis[1]*p.X+basis[4]*p.Y+basis[7]*p.Z,basis[2]*p.X+basis[5]*p.Y+basis[8]*p.Z);
        internal float[] NativeLocalDelta(OpenFloorPoint point,OpenFloorPoint centre)
        {
            float x=(float)(point.X-centre.X),y=(float)(point.Y-centre.Y),z=(float)(point.Z-centre.Z);
            return new[]{(float)((double)x*basis[0]+(double)y*basis[1]+(double)z*basis[2]),
                (float)((double)x*basis[3]+(double)y*basis[4]+(double)z*basis[5]),
                (float)((double)x*basis[6]+(double)y*basis[7]+(double)z*basis[8])};
        }
    }
    internal sealed class OpenFloorModelBounds
    {
        public ushort Goid {get;} public double SupportMinY {get;} public OpenFloorPoint Min {get;} public OpenFloorPoint Max {get;} public string Evidence {get;}
        public OpenFloorModelBounds(ushort goid,OpenFloorPoint min,OpenFloorPoint max,string evidence):this(goid,min,max,min.Y,evidence) {}
        public OpenFloorModelBounds(ushort goid,OpenFloorPoint min,OpenFloorPoint max,double supportMinY,string evidence)
        {
            if(goid==0||!min.Finite||!max.Finite||min.X>=max.X||min.Y>=max.Y||min.Z>=max.Z||!double.IsFinite(supportMinY)||supportMinY<min.Y||supportMinY>max.Y||string.IsNullOrWhiteSpace(evidence))throw new ArgumentException("UnprovenModelBounds");
            Goid=goid;SupportMinY=supportMinY;Min=min;Max=max;Evidence=evidence;
        }
        public double CentreY(double floor)
        {
            double centre=floor-SupportMinY;if(!double.IsFinite(floor)||!double.IsFinite(centre))throw new ArgumentException("NonfiniteFloor");return centre;
        }
        public IReadOnlyList<OpenFloorPoint> Corners(OpenFloorPoint centre,OpenFloorOrientation q)
        {
            var list=new List<OpenFloorPoint>();foreach(double x in new[]{Min.X,Max.X})foreach(double y in new[]{Min.Y,Max.Y})foreach(double z in new[]{Min.Z,Max.Z})
            {var p=q.ToWorld(new OpenFloorPoint(x,y,z));list.Add(new OpenFloorPoint(centre.X+p.X,centre.Y+p.Y,centre.Z+p.Z));}
            return Array.AsReadOnly(list.ToArray());
        }
    }
}
