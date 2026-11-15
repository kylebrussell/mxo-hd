using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    // Immutable per-sector source packs. No global corpus materialization, file writes or source selection.
    internal static class MissionOpenFloorScenes
    {
        private static readonly ConcurrentDictionary<string,Lazy<G.StaticScene>> cache=new ConcurrentDictionary<string,Lazy<G.StaticScene>>(StringComparer.OrdinalIgnoreCase);
        internal static bool TryLoad(ushort world,ushort sector,out G.StaticScene scene,out string refusal)
        {
            scene=null;refusal=null;string path=DataLoader.DataPath("mission_open_floor",world.ToString(System.Globalization.CultureInfo.InvariantCulture),sector+".json.gz");
            try
            {
                scene=cache.GetOrAdd(path,p=>new Lazy<G.StaticScene>(()=>{using(var file=File.OpenRead(p))using(var gz=new GZipStream(file,CompressionMode.Decompress))return Read(gz,world,sector);})).Value;
                return true;
            }
            catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is JsonException||ex is ArgumentException||ex is InvalidOperationException||ex is OverflowException||ex is KeyNotFoundException)
            {refusal="UnsupportedOpenFloorScene:"+ex.Message;return false;}
        }
        internal static G.StaticScene Read(Stream json,ushort world,ushort sector)
        {
            using(var doc=JsonDocument.Parse(json))
            {
                var root=doc.RootElement;
                if(root.GetProperty("format").GetInt32()!=1||root.GetProperty("world").GetUInt16()!=world||root.GetProperty("sector").GetUInt16()!=sector)throw new InvalidDataException("SceneIdentity");
                if(!root.GetProperty("complete").GetBoolean()||!root.GetProperty("allHeights").GetBoolean()||root.GetProperty("unsupportedLayers").GetArrayLength()!=0)throw new InvalidDataException("MissingSourceCoverage");
                var coverage=Numbers(root.GetProperty("coverage"),4);var bounds=new G.Bounds(coverage[0],coverage[1],coverage[2],coverage[3]);if(!bounds.Valid)throw new InvalidDataException("InvalidSceneCoverage");
                var profiles=root.GetProperty("profiles").EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new[]{p.Name,p.Value.GetString()}).ToArray();
                foreach(string required in new[]{"go","osid","sid8","epi","ipi"})if(!profiles.Any(p=>p[0]==required&&!string.IsNullOrWhiteSpace(p[1])))throw new InvalidDataException("MissingSourceProfile:"+required);
                var obstacles=new List<G.StaticObstacle>();var identities=new HashSet<string>(StringComparer.Ordinal);
                foreach(var row in root.GetProperty("rows").EnumerateArray())
                {
                    if(row.GetArrayLength()!=10)throw new InvalidDataException("StaticRowWidth");
                    string identity=row[0].GetString(),evidence=row[9].GetString();if(string.IsNullOrWhiteSpace(identity)||!identities.Add(identity)||!Hex64(evidence))throw new InvalidDataException("StaticRowIdentity");
                    uint? key=row[1].ValueKind==JsonValueKind.Null?(uint?)null:row[1].GetUInt32();ushort? goid=row[2].ValueKind==JsonValueKind.Null?(ushort?)null:row[2].GetUInt16();
                    if(key.HasValue!=goid.HasValue||goid==0)throw new InvalidDataException("StaticSourceIdentity");
                    double x1=row[3].GetDouble(),y1=row[4].GetDouble(),z1=row[5].GetDouble(),x2=row[6].GetDouble(),y2=row[7].GetDouble(),z2=row[8].GetDouble();
                    if(new[]{x1,y1,z1,x2,y2,z2}.Any(v=>!double.IsFinite(v))||x1>x2||y1>y2||z1>z2)throw new InvalidDataException("InvalidStaticBounds");
                    // A line/point is a closed obstacle, never empty. Only zero axes receive the same numerical
                    // native-f32 representability/epsilon guard as the resolver. No avatar/gameplay padding.
                    bool zero=x1==x2||z1==z2;if(x1==x2)ExpandZero(ref x1,ref x2);if(z1==z2)ExpandZero(ref z1,ref z2);
                    var polygon=new[]{new G.Point(x1,z1),new G.Point(x2,z1),new G.Point(x2,z2),new G.Point(x1,z2)};
                    obstacles.Add(new G.StaticObstacle(identity,key,goid,y1,y2,polygon,evidence+(zero?":zero-XZ-native-ULP-v1":"")));
                }
                string profileEvidence=JsonSerializer.Serialize(new{Format=1,World=world,Sector=sector,Profiles=profiles,StaticZeroAxisProfile=1,AllHeights=true});
                return new G.StaticScene(world,true,bounds,-double.MaxValue,double.MaxValue,obstacles,profileEvidence);
            }
        }
        private static void ExpandZero(ref double min,ref double max)
        {
            float f=(float)min;if(!float.IsFinite(f))throw new InvalidDataException("UnrepresentableStaticAxis");
            double ulp=Math.Max(Math.Abs((double)MathF.BitIncrement(f)-f),Math.Abs((double)f-MathF.BitDecrement(f)));
            double guard=Math.Max(G.Epsilon*8,ulp*2);min-=guard;max+=guard;
            if(!double.IsFinite(min)||!double.IsFinite(max)||min>=max)throw new InvalidDataException("UnrepresentableStaticAxis");
        }
        private static bool Hex64(string s)=>s!=null&&s.Length==64&&s.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F'));
        private static double[] Numbers(JsonElement e,int length)
        {if(e.GetArrayLength()!=length)throw new InvalidDataException("ArrayWidth");var a=e.EnumerateArray().Select(x=>x.GetDouble()).ToArray();if(a.Any(x=>!double.IsFinite(x)))throw new InvalidDataException("NonfiniteArray");return a;}
    }
}
