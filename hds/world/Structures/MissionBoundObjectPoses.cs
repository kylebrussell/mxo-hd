using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace hds
{
    // Native static creation6231ae00 uses transformed authored bounds, NOT the logical-origin anchor.
    internal sealed class MissionBoundObjectPoses
    {
        private sealed class Entry
        {
            internal double[] Legacy;
            internal double LegacyRotation;
            internal byte[] LegacyQuaternion;
            internal MissionComputerAnchors.Pose Pose;
            internal int Region;
        }
        private readonly Dictionary<uint,Entry> entries=new Dictionary<uint,Entry>();
        private readonly ushort sector;
        private MissionBoundObjectPoses(ushort sector){this.sector=sector;}
        private static readonly ConcurrentDictionary<string,Lazy<MissionBoundObjectPoses>> cache=
            new ConcurrentDictionary<string,Lazy<MissionBoundObjectPoses>>(StringComparer.OrdinalIgnoreCase);
        internal int Count=>entries.Count;
        internal static bool Supported(MissionObjectState state)=>state!=null&&state.OpenFloor==null&&state.goid==326&&
            state.spot?.metrId==1&&MissionSites.Goid(state.spot)==326;

        internal static bool TryCurrent(MissionObjectState state,out MissionComputerAnchors.Pose pose,out string refusal)
        {
            pose=null;refusal=null;
            if(!Supported(state)){refusal="NotSupportedBound326";return false;}
            var raw=state.spot;
            string path=DataLoader.DataPath("mission_bound_poses","1",raw.sectorID+".json.gz");
            try
            {
                var index=cache.GetOrAdd(path,p=>new Lazy<MissionBoundObjectPoses>(()=>
                    LoadVerified(Path.GetDirectoryName(p),raw.sectorID))).Value;
                return index.TryResolve(raw,out pose,out _,out refusal);
            }
            catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is JsonException||ex is FormatException||ex is OverflowException||ex is ArgumentException||ex is InvalidOperationException||ex is KeyNotFoundException)
            {refusal="UnavailableOrInvalidBound326Profile";return false;}
        }

        internal static void RequireCurrent(MissionObjectState state)
        {
            if(Supported(state)&&!TryCurrent(state,out _,out string why))
                throw new MissionCheckpointException("Bound326 pose refused: "+why);
        }
        internal static void ValidateRestored(MissionProgress progress)
        {
            foreach(var state in progress.objects.Values)
                if(Supported(state)&&!TryCurrent(state,out _,out string why))
                    throw new MissionPlacementRestoreException(progress,"Bound326 restore pose refused: "+why);
        }

        internal static MissionBoundObjectPoses Read(Stream stream,ushort sector)
        {
            using(var document=JsonDocument.Parse(stream))return Read(document.RootElement,sector);
        }
        internal static MissionBoundObjectPoses LoadVerified(string directory,ushort sector)
        {
            using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"manifest.json")));
            var root=manifest.RootElement;UniqueProperties(root);
            if(root.GetProperty("Format").GetInt32()!=1||root.GetProperty("World").GetInt32()!=1||root.GetProperty("Goid").GetInt32()!=326)
                throw new InvalidDataException("BoundPoseManifestIdentity");
            var declared=new HashSet<ushort>();string expectedHash=null,fileName=null;int expectedRows=0;
            foreach(var file in root.GetProperty("Files").EnumerateArray())
            {
                UniqueProperties(file);ushort id=file.GetProperty("Sector").GetUInt16();
                string name=file.GetProperty("File").GetString(),hash=file.GetProperty("Sha256").GetString();
                int count=file.GetProperty("Rows").GetInt32();
                if(id>0x7ff||!declared.Add(id)||name!=id+".json.gz"||count<=0||!Hash(hash))
                    throw new InvalidDataException("BoundPoseManifestFile");
                if(id==sector){expectedHash=hash;fileName=name;expectedRows=count;}
            }
            if(fileName==null)throw new InvalidDataException("UndeclaredBoundPoseSector");
            byte[] compressed=File.ReadAllBytes(Path.Combine(directory,fileName));
            if(!Convert.ToHexString(SHA256.HashData(compressed)).Equals(expectedHash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("BoundPosePayloadHash");
            using var source=new MemoryStream(compressed);using var gzip=new GZipStream(source,CompressionMode.Decompress);
            var result=Read(gzip,sector);
            if(result.Count!=expectedRows)throw new InvalidDataException("BoundPosePayloadRows");
            return result;
        }
        internal static MissionBoundObjectPoses Read(JsonElement root,ushort sector)
        {
            UniqueProperties(root);
            if(root.GetProperty("Format").GetInt32()!=1||root.GetProperty("World").GetInt32()!=1||
                root.GetProperty("Sector").GetUInt16()!=sector||sector>0x7ff||root.GetProperty("Goid").GetInt32()!=326)
                throw new InvalidDataException("BoundPoseProfileIdentity");
            var result=new MissionBoundObjectPoses(sector);
            foreach(var row in root.GetProperty("Rows").EnumerateArray())
            {
                UniqueProperties(row);
                uint id=row.GetProperty("Static").GetUInt32(),key=row.GetProperty("NativeKey").GetUInt32();
                if(id>0xfffff||key!=((uint)sector<<20|id)||row.GetProperty("Goid").GetInt32()!=326)
                    throw new InvalidDataException("BoundPoseRecordIdentity");
                var minimum=Point(row,"Minimum",true);var maximum=Point(row,"Maximum",true);
                var centre=Point(row,"Centre",true);Point(row,"Origin",true);
                for(int i=0;i<3;i++)
                    if(minimum[i]>=maximum[i]||centre[i]!=Midpoint(minimum[i],maximum[i]))
                        throw new InvalidDataException("BoundPoseNativeMidpoint");
                int placement=row.GetProperty("Placement").GetInt32(),region=row.GetProperty("Region").GetInt32();
                string layer=row.GetProperty("Layer").GetString();
                bool interior=layer=="SIDI";
                int outdoor=row.GetProperty("OutdoorRegions").GetInt32();
                if(row.GetProperty("SourceRid").GetUInt32()==0||row.GetProperty("RecordOffset").GetInt32()<0||
                    !Hash(row.GetProperty("SourceSha256").GetString())||!Hash(row.GetProperty("RecordSha256").GetString())||
                    (interior?(placement<0||outdoor<0||region!=outdoor+placement||region>ushort.MaxValue||row.GetProperty("NavRid").GetUInt32()==0||!Hash(row.GetProperty("NavSha256").GetString())||row.GetProperty("PrefabRid").GetUInt32()==0||
                               !Hash(row.GetProperty("PrefabSha256").GetString())):
                              (layer!="SID"&&layer!="SIDG"||placement!=-1||region!=-1||outdoor!=-1||row.GetProperty("NavRid").GetUInt32()!=0||row.GetProperty("NavSha256").ValueKind!=JsonValueKind.Null||row.GetProperty("PrefabRid").GetUInt32()!=0||
                               row.GetProperty("PrefabSha256").ValueKind!=JsonValueKind.Null)))
                    throw new InvalidDataException("BoundPoseSourceProvenance");
                double legacyRotation=row.GetProperty("LegacyRotation").GetDouble();if(!double.IsFinite(legacyRotation))throw new InvalidDataException("BoundPoseLegacyRotation");
                var entry=new Entry{LegacyRotation=legacyRotation,Legacy=Point(row,"LegacyPosition",false),LegacyQuaternion=Quaternion(row.GetProperty("LegacyQuaternion").GetString()),
                    Pose=new MissionComputerAnchors.Pose(centre[0],centre[1],centre[2],Quaternion(row.GetProperty("Quaternion").GetString())),Region=region};
                if(!result.entries.TryAdd(key,entry))throw new InvalidDataException("AmbiguousBoundPoseIdentity");
            }
            if(result.Count==0)throw new InvalidDataException("EmptyBoundPoseProfile");
            return result;
        }

        internal bool TryResolve(StaticWorldObject raw,out MissionComputerAnchors.Pose pose,out int region,out string refusal)
        {
            pose=null;region=-1;refusal="BoundPoseRawIdentity";
            if(raw==null||raw.metrId!=1||raw.exterior||raw.sectorID!=sector||raw.staticId>0xfffff||
                raw.mxoStaticId!=((uint)raw.sectorID<<20|raw.staticId)||raw.type==null||
                (raw.type.Length!=2&&raw.type.Length!=4)||(raw.type.Length==4&&(raw.type[2]!=0||raw.type[3]!=0))||
                MissionSites.Goid(raw)!=326)return false;
            if(!entries.TryGetValue(raw.mxoStaticId,out var entry)){refusal="MissingOrAmbiguousBoundPose";return false;}
            byte[] quaternion;try{quaternion=Quaternion(raw.quat);}catch(InvalidDataException){return false;}
            if(raw.pos_x!=entry.Legacy[0]||raw.pos_y!=entry.Legacy[1]||raw.pos_z!=entry.Legacy[2]||raw.rot!=entry.LegacyRotation||!quaternion.SequenceEqual(entry.LegacyQuaternion))
            {refusal="BoundPoseRawSignature";return false;}
            pose=entry.Pose;region=entry.Region;refusal=null;return true;
        }
        internal static double Midpoint(double minimum,double maximum)
        {
            //6231aeb7 stores the sum as f32 BEFORE6231aec6 multiplies by initial .5.
            float sum=(float)((double)(float)minimum+(float)maximum);
            return (double)(float)((double)sum*.5);
        }
        private static void UniqueProperties(JsonElement value)
        {
            if(value.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("BoundPoseObjectExpected");
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in value.EnumerateObject())if(!names.Add(property.Name))throw new InvalidDataException("DuplicateBoundPoseProperty");
        }
        private static double[] Point(JsonElement row,string name,bool stored)
        {
            var point=row.GetProperty(name).EnumerateArray().Select(v=>v.GetDouble()).ToArray();
            if(point.Length!=3||point.Any(v=>!double.IsFinite(v)||(stored&&v!=(double)(float)v)))
                throw new InvalidDataException("InvalidBoundPosePoint");
            return point;
        }
        private static bool Hash(string hash)=>hash?.Length==64&&hash.All(Uri.IsHexDigit)&&hash.Any(c=>c!='0');
        private static byte[] Quaternion(string value)
        {
            if(value?.Length!=32||!value.All(Uri.IsHexDigit))throw new InvalidDataException("InvalidBoundPoseQuaternion");
            var bytes=Convert.FromHexString(value);
            for(int i=0;i<16;i+=4){float v=BitConverter.ToSingle(bytes,i);if(!float.IsFinite(v))throw new InvalidDataException("InvalidBoundPoseQuaternion");}
            return bytes;
        }
    }
}
