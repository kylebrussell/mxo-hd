using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace hds
{
    // Native static creation6231ae00 uses transformed authored bounds, NOT the logical-origin anchor.
    internal sealed class MissionBoundFurniturePoses
    {
        internal const string AuditedManifestSha256="ECAE0AEE396F5754B30818EC73E9A652A42BE42B6584CB5F46BA5C63524AA13E";
        // Exact original GOB Model half-extents; no inferred range or model-family conversion.
        internal const string OriginalGobSha256="3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103";
        private static readonly Dictionary<ushort,string> modelHalfHex=new Dictionary<ushort,string>
        {
            {573,"00002042000096420000d041"},
            {1431,"08eda74229fe1f4202311942"},
            {1432,"9872b442790f204285072642"},
            {1433,"8e63b4420000204200002042"},
            {1434,"c771b3420000204200002042"},
            {1435,"c771b342b907204200002042"},
            {1438,"fff8f1425c03204200002042"},
            {1439,"9446f04200002042f6ff1f42"},
            {1443,"0000304200009b422486e941"},
            {1444,"000030428aed47422486e941"},
            {1445,"0000a8428aed47422486e941"},
            {1446,"0000304200009b422486e941"},
            {1447,"0000304200009b422486e941"},
            {1448,"000030428aed47422486e941"},
            {1449,"000030428aed47422486e941"},
            {1450,"0000a8428aed47422486e941"},
            {1451,"0000a8428aed47422486e941"},
            {6987,"fff8f1425c03204200002042"},
            {37913,"000080420000e642cf761f42"},
            {38484,"000080420000e642cf761f42"},
        };
        internal static bool SupportsGoid(ushort goid)=>modelHalfHex.ContainsKey(goid);
        internal static float[] ModelHalf(ushort goid)
        {
            if(!modelHalfHex.TryGetValue(goid,out string hex))throw new ArgumentOutOfRangeException(nameof(goid));
            byte[] bytes=Convert.FromHexString(hex);
            return Enumerable.Range(0,3).Select(i=>BitConverter.Int32BitsToSingle(
                System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i*4,4)))).ToArray();
        }
        internal sealed class Entry
        {
            internal ushort Goid;
            internal double[] Legacy;
            internal double LegacyRotation;
            internal byte[] LegacyQuaternion;
            internal MissionComputerAnchors.Pose Pose;
            internal int Region;
            internal double[] Minimum,Maximum;
            internal string NavSha256;
        }
        private readonly Dictionary<uint,Entry> entries=new Dictionary<uint,Entry>();
        private readonly ushort sector;
        private MissionBoundFurniturePoses(ushort sector){this.sector=sector;}
        private static readonly ConcurrentDictionary<string,Lazy<MissionBoundFurniturePoses>> cache=
            new ConcurrentDictionary<string,Lazy<MissionBoundFurniturePoses>>(StringComparer.OrdinalIgnoreCase);
        internal int Count=>entries.Count;
        internal static bool Supported(MissionObjectState state)=>state!=null&&state.OpenFloor==null&&SupportsGoid(state.goid)&&
            state.spot?.metrId==1&&MissionSites.Goid(state.spot)==state.goid;

        internal static bool TryCurrent(MissionObjectState state,out MissionComputerAnchors.Pose pose,out string refusal)
            =>TryCurrentRegion(state,out pose,out _,out refusal);
        internal static bool TryCurrentRegion(MissionObjectState state,out MissionComputerAnchors.Pose pose,out int region,out string refusal)
        {
            pose=null;region=-1;refusal=null;
            if(!Supported(state)){refusal="NotSupportedBoundFurniture";return false;}
            var raw=state.spot;
            string path=DataLoader.DataPath("mission_bound_furniture_poses","1",raw.sectorID+".json.gz");
            try
            {
                var index=cache.GetOrAdd(path,p=>new Lazy<MissionBoundFurniturePoses>(()=>
                    LoadAudited(Path.GetDirectoryName(p),raw.sectorID))).Value;
                return index.TryResolve(raw,out pose,out region,out refusal);
            }
            catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is JsonException||ex is FormatException||ex is OverflowException||ex is ArgumentException||ex is InvalidOperationException||ex is KeyNotFoundException)
            {refusal="UnavailableOrInvalidBoundFurnitureProfile";return false;}
        }

        internal sealed class Bounds
        {
            internal readonly string NavSha256;internal readonly MissionComputerAnchors.Pose Pose;internal readonly int Region;internal readonly double[] Minimum,Maximum;
            internal readonly ushort Goid; internal float[] HalfExtents=>ModelHalf(Goid);
            internal Bounds(Entry entry){Goid=entry.Goid;NavSha256=entry.NavSha256;Pose=entry.Pose;Region=entry.Region;Minimum=(double[])entry.Minimum.Clone();Maximum=(double[])entry.Maximum.Clone();}
        }
        internal static bool TryBounds(MissionObjectState state,out Bounds bounds,out string refusal)
        {
            bounds=null;if(!TryCurrentRegion(state,out _,out _,out refusal))return false;
            string path=DataLoader.DataPath("mission_bound_furniture_poses","1",state.spot.sectorID+".json.gz");
            var index=cache[path].Value;bounds=new Bounds(index.entries[state.spot.mxoStaticId]);return true;
        }
        internal static void RequireCurrent(MissionObjectState state)
        {
            if(Supported(state)&&!TryCurrent(state,out _,out string why))
                throw new MissionCheckpointException("BoundFurniture pose refused: "+why);
        }
        internal static void ValidateRestored(MissionProgress progress)
        {
            foreach(var state in progress.objects.Values)
                if(Supported(state)&&!TryCurrent(state,out _,out string why))
                    throw new MissionPlacementRestoreException(progress,"BoundFurniture restore pose refused: "+why);
        }

        internal static MissionBoundFurniturePoses Read(Stream stream,ushort sector)
        {
            using(var document=JsonDocument.Parse(stream))return Read(document.RootElement,sector);
        }
        internal static MissionBoundFurniturePoses LoadVerified(string directory,ushort sector)
            =>LoadVerified(directory,sector,null);
        internal static MissionBoundFurniturePoses LoadAudited(string directory,ushort sector)
            =>LoadVerified(directory,sector,AuditedManifestSha256);
        private static MissionBoundFurniturePoses LoadVerified(string directory,ushort sector,string requiredManifestHash)
        {
            byte[] manifestBytes=File.ReadAllBytes(Path.Combine(directory,"manifest.json"));
            // Git's Windows checkout may use CRLF. Pin the committed UTF8/LF text, not platform line endings.
            string manifestText=new UTF8Encoding(false,true).GetString(manifestBytes).Replace("\r\n","\n");
            if(requiredManifestHash!=null&&!Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestText))).Equals(requiredManifestHash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("BoundPoseManifestHash");
            using var manifest=JsonDocument.Parse(manifestText);
            var root=manifest.RootElement;UniqueProperties(root);
            if(root.GetProperty("Format").GetInt32()!=1||root.GetProperty("World").GetInt32()!=1||root.GetProperty("Kind").GetString()!="NativeBoundFurniture")
                throw new InvalidDataException("BoundPoseManifestIdentity");
            var goids=root.GetProperty("Goids").EnumerateArray().Select(v=>v.GetUInt16()).ToArray();
            if(!goids.SequenceEqual(modelHalfHex.Keys.OrderBy(v=>v)))throw new InvalidDataException("FurnitureManifestGoids");
            var contracts=root.GetProperty("ModelContracts");UniqueProperties(contracts);
            if(contracts.GetProperty("Format").GetInt32()!=1||contracts.GetProperty("Kind").GetString()!="NativeBoundFurnitureModelContracts"||
               !contracts.GetProperty("SourceGobSha256").GetString().Equals(OriginalGobSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("FurnitureOriginalGobContract");
            var seen=new HashSet<ushort>();
            foreach(var model in contracts.GetProperty("Models").EnumerateArray())
            {UniqueProperties(model);ushort id=model.GetProperty("Goid").GetUInt16();if(!seen.Add(id)||!modelHalfHex.TryGetValue(id,out string half)||
              model.GetProperty("ModelHalfExtentsHex").GetString()!=half||model.GetProperty("OpenMaxDistance").GetDouble()!=100)throw new InvalidDataException("FurnitureNumericContract");}
            if(!seen.SetEquals(modelHalfHex.Keys))throw new InvalidDataException("FurnitureModelSet");
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
        internal static MissionBoundFurniturePoses Read(JsonElement root,ushort sector)
        {
            UniqueProperties(root);
            if(root.GetProperty("Format").GetInt32()!=1||root.GetProperty("World").GetInt32()!=1||
                root.GetProperty("Sector").GetUInt16()!=sector||sector>0x7ff||root.GetProperty("Kind").GetString()!="NativeBoundFurniture")
                throw new InvalidDataException("BoundPoseProfileIdentity");
            var result=new MissionBoundFurniturePoses(sector);
            foreach(var row in root.GetProperty("Rows").EnumerateArray())
            {
                UniqueProperties(row);
                uint id=row.GetProperty("Static").GetUInt32(),key=row.GetProperty("NativeKey").GetUInt32();
                if(id>0xfffff||key!=((uint)sector<<20|id)||!SupportsGoid(row.GetProperty("Goid").GetUInt16()))
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
                var entry=new Entry{NavSha256=row.GetProperty("NavSha256").GetString(),Minimum=minimum,Maximum=maximum,LegacyRotation=legacyRotation,Legacy=Point(row,"LegacyPosition",false),LegacyQuaternion=Quaternion(row.GetProperty("LegacyQuaternion").GetString()),
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
                !SupportsGoid(MissionSites.Goid(raw)))return false;
            if(!entries.TryGetValue(raw.mxoStaticId,out var entry)||entry.Goid!=MissionSites.Goid(raw)){refusal="MissingOrAmbiguousBoundPose";return false;}
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
