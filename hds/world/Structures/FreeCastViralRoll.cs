using System;
using System.Numerics;
namespace hds
{

/// <summary>Isolated modern free-cast roll policy. Table/math are native-derived; selecting this policy and modeled inputs is OURS.</summary>
internal static class FreeCastViralRoll
{
    private static readonly uint[] bits = { 0x0u,0x3dcccccdu,0x3e4ccccdu,0x3e99999au,0x3ecccccdu,0x3f000000u,0x3f19999au,0x3f333333u,0x3f4ccccdu,0x3f666666u,0x3f800000u,0x3f8ccccdu,0x3f99999au,0x3fa66666u,0x3fb33333u,0x3fc00000u,0x3fcccccdu,0x3fd9999au,0x3fe66666u,0x3ff33333u,0x40000000u,0x40066666u,0x400ccccdu,0x40133333u,0x4019999au,0x40200000u,0x40266666u,0x402ccccdu,0x40333333u,0x4039999au,0x40400000u,0x40466666u,0x404ccccdu,0x40533333u,0x4059999au,0x40600000u,0x40666666u,0x406ccccdu,0x40733333u,0x4079999au };
    private static readonly int[] thresholds = { 0,1304,2598,3863,5091,6274,7398,8453,9440,10351,11183,11937,12612,13211,13735,14194,14587,14922,15207,15443,15636,15796,15928,16032,16114,16180,16229,16268,16298,16321,16340,16350,16360,16367,16373,16376,16376,16376,16380,16383 };
    // Dyadic arithmetic mirrors each x87 operation with a 64-bit significand, avoiding a double/Box-Muller substitution.
    private readonly struct Extended
    {
        internal readonly BigInteger Value; internal readonly int Exponent;
        internal Extended(BigInteger value,int exponent){Value=value;Exponent=exponent;}
        internal static Extended Integer(uint n) => new Extended(n,0);
        internal static Extended Float(float n)
        {
            uint b=BitConverter.SingleToUInt32Bits(n);int e=(int)((b>>23)&255);uint m=b&0x7fffff;
            if(e==0 && m==0)return new Extended(0,0);
            if(e!=0)m|=0x800000;
            return new Extended((b&0x80000000)!=0?-(BigInteger)m:m,e==0?-149:e-150);
        }
        private static Extended Round(BigInteger v,int e)
        {
            int sign=v.Sign;var a=BigInteger.Abs(v);int shift=(int)a.GetBitLength()-64;
            if(shift<=0)return new Extended(v,e);
            var q=a>>shift;var rem=a-(q<<shift);var half=BigInteger.One<<(shift-1);
            if(rem>half || rem==half && !q.IsEven)q++;
            return new Extended(sign*q,e+shift);
        }
        internal static Extended Add(Extended a,Extended b)
        {
            if(a.Value.IsZero)return b;if(b.Value.IsZero)return a;
            int e=Math.Min(a.Exponent,b.Exponent);
            return Round((a.Value<<(a.Exponent-e))+(b.Value<<(b.Exponent-e)),e);
        }
        internal static Extended Negate(Extended a)=>new Extended(-a.Value,a.Exponent);
        internal static Extended Multiply(Extended a,Extended b)=>Round(a.Value*b.Value,a.Exponent+b.Exponent);
        internal uint TruncateUInt32()
        {
            var n=Exponent>=0?Value<<Exponent:(Value.Sign<0?-(BigInteger.Abs(Value)>>-Exponent):Value>>-Exponent);
            return (uint)(n&uint.MaxValue);
        }
    }
    /// <summary>6258da00 modern Gaussian branch, signed16 inputs, MaxConsistency policy100; returns native signed AX.</summary>
    internal static short Roll(int bonus,int consistency,int range,int rand15)
    {
        if(range!=100 && range!=69)throw new ArgumentOutOfRangeException(nameof(range));
        int b=unchecked((short)bonus),c=unchecked((short)consistency),n=b+range;
        int mean=Math.Max(1,n/2),k=c==0?n/4:n/6;
        int sigma=Math.Max(0,c==0?k:k-c*k/100);
        uint sample=Sample((uint)mean,(uint)sigma,rand15);
        //6279f470 uses unsigned comparisons. A negative truncated left tail wraps and clamps to the upper bound.
        uint upper=unchecked((uint)n);
        uint clamped=sample<1?1:sample<upper?sample:upper;
        return unchecked((short)clamped);
    }
    /// <summary>6279f2f0 inverse table, including the32767 fallback-to-mean and zero-width threshold skips.</summary>
    internal static uint Sample(uint mean,uint sigma,int rand15)
    {
        if(rand15<0 || rand15>32767)throw new ArgumentOutOfRangeException(nameof(rand15));
        bool positive=rand15>=16383;int draw=positive?rand15-16383:rand15;
        int i=1;while(i<40 && draw>thresholds[i])i++;
        if(i==40)return mean;
        if(!positive && mean<sigma)return 0;
        float ratio=(float)(draw-thresholds[i-1])/(float)(thresholds[i]-thresholds[i-1]);
        var lo=Extended.Float(BitConverter.UInt32BitsToSingle(bits[i-1]));
        var hi=Extended.Float(BitConverter.UInt32BitsToSingle(bits[i]));
        var z=Extended.Add(Extended.Multiply(Extended.Add(hi,Extended.Negate(lo)),Extended.Float(ratio)),lo);
        var scaled=Extended.Multiply(z,Extended.Float(sigma));
        return Extended.Add(Extended.Float(mean),positive?scaled:Extended.Negate(scaled)).TruncateUInt32();
    }
}

}
