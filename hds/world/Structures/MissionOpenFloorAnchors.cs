using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace hds
{
    internal sealed class MissionOpenFloorAnchors
    {
        internal const string Header=MissionComputerAnchors.Header;
        private sealed class Entry
        {
            internal ushort Goid;internal MissionComputerAnchors.Pose Native;internal double X,Y,Z;internal byte[] Quaternion;internal string Evidence;
        }
        private readonly Dictionary<uint,Entry> rows=new Dictionary<uint,Entry>();
        internal int Count=>rows.Count;
        private static readonly ConcurrentDictionary<string,Lazy<MissionOpenFloorAnchors>> cache=new ConcurrentDictionary<string,Lazy<MissionOpenFloorAnchors>>(StringComparer.OrdinalIgnoreCase);
        internal static bool TryCurrent(StaticWorldObject raw,out MissionComputerAnchors.Pose pose,out string evidence)
        {
            pose=null;evidence=null;if(raw==null||raw.metrId!=1)return false;
            string path=DataLoader.DataPath("mission_open_floor","anchors_1",raw.sectorID+".csv.gz");
            try
            {
                var index=cache.GetOrAdd(path,p=>new Lazy<MissionOpenFloorAnchors>(()=>{using(var file=File.OpenRead(p))using(var gz=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(gz,Encoding.UTF8))return Read(reader,raw.sectorID);})).Value;
                return index.TryResolve(raw,out pose,out evidence);
            }
            catch(Exception ex) when(ex is IOException||ex is FormatException||ex is OverflowException||ex is ArgumentException){return false;}
        }
        internal static MissionOpenFloorAnchors Read(TextReader reader,ushort sector)
        {
            if(reader==null||reader.ReadLine()!=Header)throw new InvalidDataException("AnchorHeader");
            var index=new MissionOpenFloorAnchors();string line;
            while((line=reader.ReadLine())!=null)
            {
                var f=line.Split(',');if(f.Length!=16)throw new InvalidDataException("AnchorRowWidth");
                uint world=uint.Parse(f[0],CultureInfo.InvariantCulture),s=uint.Parse(f[1],CultureInfo.InvariantCulture),id=uint.Parse(f[2],CultureInfo.InvariantCulture),key=uint.Parse(f[3],CultureInfo.InvariantCulture);
                ushort goid=ushort.Parse(f[4],CultureInfo.InvariantCulture);
                if(world!=1||s!=sector||s>0x7ff||id>0xfffff||key!=((s<<20)|id)||goid==0||uint.Parse(f[13],CultureInfo.InvariantCulture)==0||
                    int.Parse(f[14],CultureInfo.InvariantCulture)<-1||int.Parse(f[15],CultureInfo.InvariantCulture)<0)throw new InvalidDataException("AnchorIdentity");
                var q=Quaternion(f[8]);var entry=new Entry{Goid=goid,Native=new MissionComputerAnchors.Pose(Number(f[5]),Number(f[6]),Number(f[7]),q),
                    X=Number(f[9]),Y=Number(f[10]),Z=Number(f[11]),Quaternion=Quaternion(f[12]),Evidence="native-source-row:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line)))};
                if(!index.rows.TryAdd(key,entry))throw new InvalidDataException("AmbiguousAnchorIdentity");
            }
            return index;
        }
        internal bool TryResolve(StaticWorldObject raw,out MissionComputerAnchors.Pose pose,out string evidence)
        {
            pose=null;evidence=null;
            if(raw==null||raw.metrId!=1||raw.exterior||raw.sectorID>0x7ff||raw.staticId>0xfffff||raw.mxoStaticId!=((uint)raw.sectorID<<20|raw.staticId)||raw.type==null||
                (raw.type.Length!=2&&raw.type.Length!=4)||(raw.type.Length==4&&(raw.type[2]!=0||raw.type[3]!=0))||!rows.TryGetValue(raw.mxoStaticId,out var entry)||MissionSites.Goid(raw)!=entry.Goid)return false;
            byte[] q;try{q=Quaternion(raw.quat??"");}catch(InvalidDataException){return false;}
            bool legacy=raw.pos_x==entry.X&&raw.pos_y==entry.Y&&raw.pos_z==entry.Z&&q.SequenceEqual(entry.Quaternion);
            bool canonical=raw.pos_x==entry.Native.X&&raw.pos_y==entry.Native.Y+145&&raw.pos_z==entry.Native.Z&&q.SequenceEqual(entry.Native.Orientation);
            if(!legacy&&!canonical)return false;
            pose=entry.Native;evidence=entry.Evidence+(legacy?":legacy-input":":canonical-marker-input");return true;
        }
        private static double Number(string value){double n=double.Parse(value,NumberStyles.Float,CultureInfo.InvariantCulture);if(!double.IsFinite(n))throw new InvalidDataException("NonfiniteAnchor");return n;}
        private static byte[] Quaternion(string value)
        {
            if(value.Length!=32||!value.All(Uri.IsHexDigit))throw new InvalidDataException("AnchorQuaternion");byte[] bytes=Convert.FromHexString(value);
            for(int i=0;i<16;i+=4)if(!float.IsFinite(BitConverter.ToSingle(bytes,i)))throw new InvalidDataException("NonfiniteAnchorQuaternion");return bytes;
        }
    }
}
