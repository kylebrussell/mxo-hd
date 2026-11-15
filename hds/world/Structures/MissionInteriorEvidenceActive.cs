using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace hds
{
    internal sealed partial class MissionInteriorEvidence
    {
        internal const string ActiveManifestSha256 = "E3D18C84A23ED4CA170B902F803FE63D4E0906082CB117BDFEE30ED0BD7168CE";
        internal static Manifest ActiveManifest(string directory,ushort world)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(directory,"manifest.json"));
            Require(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeManifest(bytes)))).Equals(ActiveManifestSha256,StringComparison.OrdinalIgnoreCase),"ActiveInteriorManifestHash");
            Manifest manifest=JsonSerializer.Deserialize<Manifest>(bytes);
            Require(manifest!=null&&manifest.Format==2&&manifest.World==world&&world==1&&manifest.Sectors!=null&&manifest.Sectors.All(r=>r!=null&&Hex(r.Digest,64)&&Hex(r.Sha256,64))&&
                manifest.Sectors.Select(r=>r.Sector).Distinct().Count()==manifest.Sectors.Length,"ActiveInteriorManifestIdentity");return manifest;
        }
        internal static MissionInteriorEvidence LoadActive(string directory,ushort world,ushort sector)
        {
            return LoadActive(directory,world,sector,ActiveManifest(directory,world));
        }
        internal static MissionInteriorEvidence LoadActive(string directory,ushort world,ushort sector,Manifest manifest)
        {
            Require(manifest!=null&&manifest.World==world&&manifest.Format==2,"ActiveInteriorManifestIdentity");
            Declaration row=manifest.Sectors.SingleOrDefault(r=>r.Sector==sector);
            Require(row!=null,"UndeclaredActiveInteriorScene");
            MissionInteriorEvidence evidence=LoadVerified(File.ReadAllBytes(Path.Combine(directory,sector+".json.gz")),row.Sha256,world,sector);
            Require(evidence.document.Format==2&&evidence.Digest.Equals(row.Digest,StringComparison.OrdinalIgnoreCase)&&evidence.document.Regions.Length==row.Regions&&
                evidence.document.Portals.Length==row.Portals&&evidence.document.Groups.Length==row.Groups,"ActiveInteriorManifestCounts");return evidence;
        }
        internal static MissionInteriorEvidence LoadSaved(string active,string legacy,ushort world,ushort sector,string digest)
        {
            Require(Hex(digest,64),"InvalidSavedInteriorDigest");Manifest manifest=null;
            try {manifest=ActiveManifest(active,world);}
            catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is UnauthorizedAccessException||ex is JsonException) { }
            if(manifest?.Sectors.Any(r=>r.Sector==sector&&r.Digest.Equals(digest,StringComparison.OrdinalIgnoreCase))==true)return LoadActive(active,world,sector);
            return LoadForDigest(legacy,world,sector,digest);
        }
    }
}
