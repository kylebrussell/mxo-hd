using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    // Only original public-portal candidates are declared here. Mission elevators remain owner-scoped.
    internal static class MissionPublicDoors
    {
        internal enum Kind { Legacy,Resolved,Refused }
        internal sealed class Result
        {
            internal readonly Kind State;internal readonly MissionInteriorEvidence.DoorPose Pose;internal readonly string Refusal,RawSignature;
            internal Result(Kind state,MissionInteriorEvidence.DoorPose pose=null,string refusal=null,string rawSignature=null){State=state;Pose=pose;Refusal=refusal;RawSignature=rawSignature;}
        }
        // Request lifetime only, not a mission/public-door ownership lease. Capture BEFORE lazy scene resolution.
        internal sealed class Request
        {
            private readonly WorldClient client;private readonly ClientData owner;private readonly int world,life;private readonly uint character,district;private readonly long transport;
            internal Request(WorldClient client)
            {
                this.client=client;owner=client?.playerData;world=owner?.worldGeneration??-1;life=owner?.lifeGeneration??-1;
                character=owner?.getCharID()??0;district=owner?.getDistrictId()??0;transport=client?.TransportFence?.Capture()??-1;
            }
            internal bool Current=>owner!=null&&ReferenceEquals(client.playerData,owner)&&client.Alive&&owner.getOnWorld()&&!owner.isDead&&!owner.worldChangePending&&
                !owner.waitForRPCShutDown&&owner.worldGeneration==world&&owner.lifeGeneration==life&&owner.getCharID()==character&&owner.getDistrictId()==district&&client.TransportFence!=null&&client.TransportFence.IsCurrent(transport);
            private void RequireCurrent(){if(!Current)throw new MissionCheckpointException("Stale public door request lifetime");}
            internal bool Publish(StaticWorldObject raw,Result result,Action publish)
            {
                if(owner==null||raw==null||result?.State!=Kind.Resolved||result.Pose==null||raw.metrId!=district||raw.mxoStaticId!=result.Pose.Key||
                    result.RawSignature!=MissionNpcInteriorPlanning.RawJson(raw))return false;
                lock(owner.missionSync)
                {
                    if(!Current)return false;
                    if(MissionOutput.Buffered(client.messageQueue))
                    {
                        if(!MissionOutput.BindCurrent(client.messageQueue,client.TransportFence,transport,world))return false;
                        publish();RequireCurrent();return true;
                    }
                    using(var output=MissionOutput.Begin(client.messageQueue))
                    {
                        if(!MissionOutput.BindCurrent(client.messageQueue,client.TransportFence,transport,world))return false;
                        publish();RequireCurrent();output.Commit();return true;
                    }
                }
            }
        }
        internal static Request CaptureRequest(WorldClient client)=>new Request(client);
        internal sealed class Declaration
        {public ushort Sector {get;set;}public string Digest {get;set;}public string Sha256 {get;set;}public uint[] Keys {get;set;}}
        internal sealed class Index
        {public int Format {get;set;}public ushort World {get;set;}public Declaration[] Sectors {get;set;}}
        private static Index ReadIndex()
        {
            using(var stream=typeof(MissionPublicDoors).Assembly.GetManifestResourceStream("hds.MissionPublicDoors.Index"))
            {
                var index=JsonSerializer.Deserialize<Index>(stream??throw new InvalidDataException("MissingPublicDoorIndex"));
                if(index==null||index.Format!=1||index.World!=1||index.Sectors==null||index.Sectors.Select(r=>r?.Sector).Distinct().Count()!=index.Sectors.Length||
                    index.Sectors.Any(r=>r==null||r.Keys==null||r.Keys.Distinct().Count()!=r.Keys.Length||r.Keys.Any(k=>(k>>20)!=r.Sector)||
                    !Hash(r.Digest)||!Hash(r.Sha256)))throw new InvalidDataException("InvalidPublicDoorIndex");
                return index;
            }
        }
        private static bool Hash(string s)=>s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);
        private static readonly Index committed=ReadIndex();
        private static readonly HashSet<uint> committedKeys=new HashSet<uint>(committed.Sectors.SelectMany(row=>row.Keys));
        internal sealed class Dataset
        {
            private readonly string street,interior;private readonly Index index;private readonly Dictionary<uint,Declaration> keys;
            private readonly ConcurrentDictionary<ushort,Lazy<Tuple<MissionInteriorEvidence,MissionStreetAccessData>>> scenes=new ConcurrentDictionary<ushort,Lazy<Tuple<MissionInteriorEvidence,MissionStreetAccessData>>>();
            internal Dataset(string street,string interior,Index index=null){this.street=Path.GetFullPath(street);this.interior=Path.GetFullPath(interior);this.index=index??committed;keys=this.index.Sectors.SelectMany(row=>row.Keys.Select(key=>new KeyValuePair<uint,Declaration>(key,row))).ToDictionary(pair=>pair.Key,pair=>pair.Value);}
            internal Result Resolve(StaticWorldObject raw)
            {
                if(raw==null||raw.metrId!=index.World)return new Result(Kind.Legacy);
                if(!keys.TryGetValue(raw.mxoStaticId,out Declaration row))return new Result(Kind.Legacy);
                try
                {
                    var scene=scenes.GetOrAdd(row.Sector,sector=>new Lazy<Tuple<MissionInteriorEvidence,MissionStreetAccessData>>(()=>
                    {
                        var evidence=MissionInteriorEvidence.LoadActive(interior,index.World,sector);
                        var data=MissionStreetAccessData.Read(File.ReadAllBytes(Path.Combine(street,sector+".json.gz")),row.Sha256,row.Digest,evidence);
                        return Tuple.Create(evidence,data);
                    })).Value;
                    return scene.Item1.TryCanonicalPublicDoor(scene.Item2,raw,out var pose,out string why)?new Result(Kind.Resolved,pose,rawSignature:MissionNpcInteriorPlanning.RawJson(raw)):new Result(Kind.Refused,refusal:why);
                }
                catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is JsonException||ex is UnauthorizedAccessException||ex is ArgumentException||ex is FormatException||ex is OverflowException)
                {return new Result(Kind.Refused,refusal:"UnavailableDeclaredPublicDoor:"+ex.GetType().Name);}
            }
        }
        private static readonly ConcurrentDictionary<string,Dataset> datasets=new ConcurrentDictionary<string,Dataset>(StringComparer.OrdinalIgnoreCase);
        internal static Result Resolve(StaticWorldObject raw)
        {
            if(raw==null||raw.metrId!=committed.World||!committedKeys.Contains(raw.mxoStaticId))return new Result(Kind.Legacy);
            string street=DataLoader.DataPath("mission_street_access","1"),interior=DataLoader.DataPath("mission_interior_evidence_v2","1");
            return datasets.GetOrAdd(Path.GetFullPath(street)+"|"+Path.GetFullPath(interior),_=>new Dataset(street,interior)).Resolve(raw);
        }
    }
}
