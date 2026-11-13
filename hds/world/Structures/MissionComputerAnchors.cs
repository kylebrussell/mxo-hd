using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace hds
{
    /// <summary>Dormant audited Slums1415→freeGO3 lookup. No production caller loads or applies this data.</summary>
    public sealed class MissionComputerAnchors
    {
        public const string Header = "metr,sector,static_id,native_id,source_goid,native_x,native_y,native_z,native_quat,legacy_x,legacy_y,legacy_z,legacy_quat,source_rid,placement,record_offset";
        public sealed class Pose
        {
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            private readonly byte[] orientation;
            public byte[] Orientation => (byte[])orientation.Clone();
            internal Pose(double x, double y, double z, byte[] q) { X=x;Y=y;Z=z;orientation=(byte[])q.Clone(); }
        }
        private sealed class Anchor
        {
            public Pose Native;
            public double LegacyX,LegacyY,LegacyZ;
            public byte[] LegacyQuaternion;
        }
        private readonly Dictionary<ulong,Anchor> anchors = new Dictionary<ulong,Anchor>();
        public int Count => anchors.Count;
        private static ulong Key(ushort sector,uint id) => ((ulong)sector<<32)|id;
        private static double Number(string value)
        {
            double n=double.Parse(value,NumberStyles.Float,CultureInfo.InvariantCulture);
            if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite anchor coordinate");
            return n;
        }
        private static byte[] Quaternion(string value)
        {
            if(value.Length!=32 || value.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid quaternion bytes");
            byte[] bytes=Enumerable.Range(0,16).Select(i=>byte.Parse(value.Substring(i*2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture)).ToArray();
            for(int i=0;i<16;i+=4)if(float.IsNaN(BitConverter.ToSingle(bytes,i))||float.IsInfinity(BitConverter.ToSingle(bytes,i)))throw new InvalidDataException("Nonfinite quaternion");
            return bytes;
        }
        /// <summary>Strict all-or-nothing parser. Duplicate keys, including identical duplicates, refuse the artifact.</summary>
        public static MissionComputerAnchors Read(TextReader reader)
        {
            if(reader==null)throw new ArgumentNullException(nameof(reader));
            if(reader.ReadLine()!=Header)throw new InvalidDataException("Unknown computer anchor format");
            var result=new MissionComputerAnchors();string line;
            while((line=reader.ReadLine())!=null)
            {
                string[] f=line.Split(',');if(f.Length!=16)throw new InvalidDataException("Invalid computer anchor row");
                uint metr=uint.Parse(f[0],CultureInfo.InvariantCulture),sector=uint.Parse(f[1],CultureInfo.InvariantCulture),id=uint.Parse(f[2],CultureInfo.InvariantCulture),native=uint.Parse(f[3],CultureInfo.InvariantCulture);
                if(metr!=1||sector>0x7ff||id>0xfffff||native!=((sector<<20)|id)||f[4]!="1415")throw new InvalidDataException("Invalid anchor identity");
                if(uint.Parse(f[13],CultureInfo.InvariantCulture)==0||int.Parse(f[14],CultureInfo.InvariantCulture)<0||int.Parse(f[15],CultureInfo.InvariantCulture)<0)throw new InvalidDataException("Missing counted SIDI provenance");
                var a=new Anchor {Native=new Pose(Number(f[5]),Number(f[6]),Number(f[7]),Quaternion(f[8])),LegacyX=Number(f[9]),LegacyY=Number(f[10]),LegacyZ=Number(f[11]),LegacyQuaternion=Quaternion(f[12])};
                if(!result.anchors.TryAdd(Key((ushort)sector,id),a))throw new InvalidDataException("Duplicate anchor identity");
            }
            return result;
        }
        /// <summary>Require BOTH exact source identity and known legacy/canonical source pose; custom moved spots never match by key alone.</summary>
        public bool TryResolve(StaticWorldObject source,ushort concreteGoid,out Pose pose)
        {
            pose=null;
            if(source==null||concreteGoid!=3||source.metrId!=1||source.exterior||source.sectorID>0x7ff||source.staticId>0xfffff||source.mxoStaticId!=((uint)source.sectorID<<20|source.staticId)||source.type==null||
               (source.type.Length!=2&&source.type.Length!=4)||source.type[0]!=0x87||source.type[1]!=5||(source.type.Length==4&&(source.type[2]!=0||source.type[3]!=0))||
               !anchors.TryGetValue(Key(source.sectorID,source.staticId),out Anchor a))return false;
            byte[] q;
            try { q=Quaternion(source.quat??""); }catch(InvalidDataException){return false;}
            bool legacy=source.pos_x==a.LegacyX&&source.pos_y==a.LegacyY&&source.pos_z==a.LegacyZ&&q.SequenceEqual(a.LegacyQuaternion);
            // Canonical MissionSites input representation only. Native free-object Y remains separate.
            bool canonical=source.pos_x==a.Native.X&&source.pos_y==a.Native.Y+145&&source.pos_z==a.Native.Z&&q.SequenceEqual(a.Native.Orientation);
            if(!legacy&&!canonical)return false;
            pose=a.Native;return true;
        }
    }
}
