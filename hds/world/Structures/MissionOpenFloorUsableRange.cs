using System;
namespace hds
{
    // Native Usable5:625d6b20 ->625d72b0 ->625d7230 ->62393c60.
    // Positive stored values or the explicitly proven Computer initial CVar profile.
    // No server authority, current client configuration or live actor expansion is changed.
    internal static class MissionOpenFloorUsableRange
    {
        internal enum RangeSource { PositiveStoredRange, InitialClientFallback }
        internal sealed class Profile
        {
            public ushort Goid {get;} public byte Use {get;} public float StoredMaxDistance {get;} public OpenFloorPoint HalfExtents {get;} public string Evidence {get;}
            public RangeSource Source {get;}
            public bool AssumesInitialClientConfiguration=>Source==RangeSource.InitialClientFallback;
            public Profile(ushort goid,byte use,float max,OpenFloorPoint half,string evidence)
            {
                if(goid==0||use==0||!float.IsFinite(max)||max<=0||!half.Finite||half.X<=0||half.Y<=0||half.Z<=0||
                    half.X>float.MaxValue||half.Y>float.MaxValue||half.Z>float.MaxValue||string.IsNullOrWhiteSpace(evidence))throw new ArgumentException("UnprovenUsableRange");
                Goid=goid;Use=use;StoredMaxDistance=max;HalfExtents=half;Evidence=evidence;
            }
            // Native omitted/nonpositive branch bypasses the size floor and reads current CVar.
            // This profile is ONLY its proven initial400 configuration, not an observed runtime value.
            private Profile(ushort goid,byte use,OpenFloorPoint half,string evidence):this(goid,use,400,half,evidence)
            {Source=RangeSource.InitialClientFallback;StoredMaxDistance=0;}
            public static Profile InitialClient400(ushort goid,byte use,OpenFloorPoint half,string evidence)
            {
                if(goid!=3||use!=5)throw new ArgumentException("UnprovenInitialFallbackProfile");
                return new Profile(goid,use,half,evidence);
            }
            public float LowerBound=>Source==RangeSource.InitialClientFallback?400f:Math.Max(StoredMaxDistance,(float)((float)HalfExtents.Z+65f));
        }
        internal readonly struct Witness
        {
            public bool Within {get;} public float DistanceSquared {get;} public float LowerBound {get;} public float LimitSquared {get;}
            public Witness(bool within,float distance,float lower,float limit){Within=within;DistanceSquared=distance;LowerBound=lower;LimitSquared=limit;}
        }
        internal static Witness Test(Profile profile,OpenFloorPoint standing,OpenFloorPoint centre,OpenFloorOrientation q)
        {
            if(profile==null||q==null||!standing.Finite||!centre.Finite)return new Witness(false,float.NaN,float.NaN,float.NaN);
            float[] p=q.NativeLocalDelta(standing,centre);var e=profile.HalfExtents;float[] ext={(float)e.X,(float)e.Y,(float)e.Z};float sum=0;
            for(int i=0;i<3;i++)
            {
                if(!float.IsFinite(p[i]))return new Witness(false,float.NaN,profile.LowerBound,float.NaN);
                float outside=p[i]<-ext[i]?(float)(p[i]+ext[i]):p[i]>ext[i]?(float)(p[i]-ext[i]):0;
                sum=(float)((double)outside*outside+sum);
            }
            float lower=profile.LowerBound,limit=(float)((double)lower*lower);
            return new Witness(float.IsFinite(sum)&&float.IsFinite(limit)&&sum<=limit,sum,lower,limit);
        }
    }
}
