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
    // Separate from key-only static lookup. Legacy membership does NOT establish region ownership.
    internal sealed class MissionElevatorAnchors
    {
        internal class Signature { public ushort Goid {get;set;} public double[] Position {get;set;} public string Quaternion {get;set;} }
        internal sealed class Legacy { public Signature Signature {get;set;} public string File {get;set;} public int Line {get;set;} }
        internal sealed class Occurrence : Signature { public ushort Region {get;set;} public int Placement {get;set;} public uint SourceRid {get;set;} public uint PrefabRid {get;set;} public int RecordOffset {get;set;} }
        internal sealed class Group { public uint StaticId {get;set;} public uint Key {get;set;} public Legacy[] Legacy {get;set;} public Occurrence[] Occurrences {get;set;} }
        internal sealed class Document { public int Format {get;set;} public ushort World {get;set;} public ushort Sector {get;set;} public int OriginalOutdoorRegionCount {get;set;} public string SidiSha256 {get;set;} public string NavSha256 {get;set;} public Group[] Groups {get;set;} public Group[] Sources {get;set;} }
        internal sealed class Pose
        {
            internal readonly double X,Y,Z;internal readonly string Quaternion,Evidence;internal readonly ushort Region;
            internal Pose(double x,double y,double z,string quaternion,ushort region,string evidence){X=x;Y=y;Z=z;Quaternion=quaternion;Region=region;Evidence=evidence;}
            // Exporter verifies each417/419 bound midpoint (6231ae00) is logical origin+(0,145,0).
            internal MissionOpenFloorResolver.Position Presentation=>new MissionOpenFloorResolver.Position(X,Y+145,Z);
        }
        private readonly Document data;
        private readonly Dictionary<uint,Group> groups,sources;
        internal readonly string Digest;
        private static readonly ConcurrentDictionary<string,Lazy<MissionElevatorAnchors>> cache=new ConcurrentDictionary<string,Lazy<MissionElevatorAnchors>>(StringComparer.OrdinalIgnoreCase);
        private MissionElevatorAnchors(Document d,string digest){data=d;Digest=digest;groups=d.Groups.ToDictionary(g=>g.Key);sources=d.Sources.ToDictionary(g=>g.Key);}
        internal static MissionElevatorAnchors Read(Stream input,ushort sector)
        {
            using var bytes=new MemoryStream();input.CopyTo(bytes);var raw=bytes.ToArray();
            var d=JsonSerializer.Deserialize<Document>(raw);
            bool Hex(string s,int n)=>s!=null&&s.Length==n&&s.All(Uri.IsHexDigit);
            bool Valid(Signature s)=>s!=null&&s.Goid!=0&&s.Position?.Length==3&&s.Position.All(double.IsFinite)&&Hex(s.Quaternion,32)&&Enumerable.Range(0,4).All(i=>float.IsFinite(BitConverter.ToSingle(Convert.FromHexString(s.Quaternion),i*4)));
            if(d==null||d.Format!=1||d.World!=1||d.Sector!=sector||sector>0x7ff||d.OriginalOutdoorRegionCount<0||!Hex(d.SidiSha256,64)||!Hex(d.NavSha256,64)||d.Groups==null||d.Sources==null)throw new InvalidDataException("ElevatorAnchorHeader");
            foreach(var list in new[]{d.Groups,d.Sources})
            {
                if(list.Any(g=>g==null)||list.Select(g=>g.Key).Distinct().Count()!=list.Length)throw new InvalidDataException("DuplicateElevatorAnchorGroup");
                foreach(var g in list)
                    if(g.StaticId>0xfffff||g.Key!=((uint)sector<<20|g.StaticId)||g.Legacy==null||g.Legacy.Length==0||g.Legacy.Any(l=>l==null||!Valid(l.Signature)||string.IsNullOrWhiteSpace(l.File)||l.Line<2)||
                        g.Occurrences==null||g.Occurrences.Length==0||g.Occurrences.Any(o=>!Valid(o)||o.Placement<0||o.Region!=d.OriginalOutdoorRegionCount+o.Placement||o.SourceRid==0||o.PrefabRid==0||o.RecordOffset<0))throw new InvalidDataException("ElevatorAnchorIdentity");
            }
            if(d.Groups.Any(g=>g.Legacy.Any(l=>!Door(l.Signature.Goid))||g.Occurrences.Any(o=>!Door(o.Goid))))throw new InvalidDataException("ElevatorAnchorFamily");
            return new MissionElevatorAnchors(d,Convert.ToHexString(SHA256.HashData(raw)));
        }
        private static bool Door(ushort goid)=>goid==417||goid==419;
        private static bool Match(StaticWorldObject raw,Signature s)=>raw!=null&&MissionSites.Goid(raw)==s.Goid&&raw.pos_x==s.Position[0]&&raw.pos_y==s.Position[1]&&raw.pos_z==s.Position[2]&&string.Equals(raw.quat,s.Quaternion,StringComparison.OrdinalIgnoreCase);
        private bool Identity(StaticWorldObject raw)=>raw!=null&&raw.metrId==data.World&&!raw.exterior&&raw.sectorID==data.Sector&&raw.staticId<=0xfffff&&raw.mxoStaticId==((uint)raw.sectorID<<20|raw.staticId)&&raw.type!=null&&(raw.type.Length==2||raw.type.Length==4)&&(raw.type.Length==2||raw.type[2]==0&&raw.type[3]==0);
        internal bool HasDoor(StaticWorldObject raw)=>Identity(raw)&&groups.ContainsKey(raw.mxoStaticId);
        internal bool SourceRegion(StaticWorldObject raw,ushort region)=>Identity(raw)&&sources.TryGetValue(raw.mxoStaticId,out var group)&&group.Legacy.Any(l=>Match(raw,l.Signature))&&
            group.Occurrences.Where(o=>o.Goid==MissionSites.Goid(raw)).Select(o=>o.Region).Distinct().SequenceEqual(new[]{region});
        internal bool NeedsCorrection(StaticWorldObject raw,out bool changed,out string refusal)
        {
            changed=false;refusal=null;
            if(!Identity(raw)||!groups.TryGetValue(raw.mxoStaticId,out var group))return true;
            if(!group.Legacy.Any(l=>Match(raw,l.Signature))){refusal="UnlistedElevatorLegacySignature";return false;}
            // A raw pose already coincident with an original occurrence retains legacy behavior.
            // Unused conflicting occurrences must not invalidate that existing witness.
            changed=!group.Occurrences.Any(o=>raw.pos_x==o.Position[0]&&raw.pos_y==o.Position[1]+145&&raw.pos_z==o.Position[2]);
            return true;
        }
        internal bool TryPosition(StaticWorldObject raw,out MissionOpenFloorResolver.Position position,out string refusal)
        {
            position=default;refusal="MissingElevatorAnchorGroup";
            if(!Identity(raw)||!Door(MissionSites.Goid(raw))||!groups.TryGetValue(raw.mxoStaticId,out var group))return false;
            if(!group.Legacy.Any(l=>Match(raw,l.Signature))){refusal="UnlistedElevatorLegacySignature";return false;}
            var first=group.Occurrences[0].Position;
            if(group.Occurrences.Any(o=>!o.Position.SequenceEqual(first))){refusal="ElevatorNativePositionDisagreement";return false;}
            position=new MissionOpenFloorResolver.Position(first[0],first[1],first[2]);refusal=null;return true;
        }
        internal bool TryResolve(StaticWorldObject raw,ushort validatedRegion,out Pose pose,out string refusal)
        {
            pose=null;refusal="MissingElevatorAnchorGroup";
            if(!Identity(raw)||!Door(MissionSites.Goid(raw))||!groups.TryGetValue(raw.mxoStaticId,out var group))return false;
            if(!group.Legacy.Any(l=>Match(raw,l.Signature))){refusal="UnlistedElevatorLegacySignature";return false;}
            if(!TryPosition(raw,out _,out refusal))return false;
            var occurrence=group.Occurrences.Where(o=>o.Region==validatedRegion).ToArray();
            if(occurrence.Length!=1){refusal="MissingOrAmbiguousElevatorRegion";return false;}
            var chosen=occurrence[0];pose=new Pose(chosen.Position[0],chosen.Position[1],chosen.Position[2],chosen.Quaternion,validatedRegion,
                "original-elevator-group:"+group.Key.ToString("x8")+":legacy:"+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(group.Legacy.Single(l=>Match(raw,l.Signature)).Signature)))+
                ":selected-record:"+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(chosen))));refusal=null;return true;
        }
        internal static bool TryCurrent(StaticWorldObject raw,out MissionElevatorAnchors index)
        {
            index=null;if(raw==null||raw.metrId!=1)return false;string path=DataLoader.DataPath("mission_elevators","1",raw.sectorID+".json.gz");
            try{index=cache.GetOrAdd(path,p=>new Lazy<MissionElevatorAnchors>(()=>{using var file=File.OpenRead(p);using var gz=new GZipStream(file,CompressionMode.Decompress);return Read(gz,raw.sectorID);})).Value;return true;}
            catch(Exception ex) when(ex is IOException||ex is JsonException||ex is ArgumentException||ex is OverflowException){return false;}
        }
    }
}
