using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace hds
{
    // Immutable versioned DTOs. Raw source JSON is independent of resolved world pose.
    public sealed class MissionOpenFloorChoice
    {
        public ushort ObjectId { get; }
        public ushort Goid { get; }
        public string RawSpotJson { get; }
        [JsonConstructor] public MissionOpenFloorChoice(ushort objectId,ushort goid,string rawSpotJson) {ObjectId=objectId;Goid=goid;RawSpotJson=rawSpotJson;}
        internal StaticWorldObject ReadSpot() => RawSpotJson==null?null:JsonSerializer.Deserialize<StaticWorldObject>(RawSpotJson,MissionProgress.SaveJson);
        internal static MissionOpenFloorChoice From(MissionStaging.ObjectChoice c) => new MissionOpenFloorChoice(c.Definition.id,c.Goid,
            c.Spot==null?null:JsonSerializer.Serialize(c.Spot,MissionProgress.SaveJson));
    }
    public sealed class MissionOpenFloorSite
    {
        public ushort District { get; }
        public uint DoorKey { get; }
        public double DoorX { get; }
        public double DoorY { get; }
        public double DoorZ { get; }
        public string Fingerprint { get; }
        [JsonConstructor] public MissionOpenFloorSite(ushort district,uint doorKey,double doorX,double doorY,double doorZ,string fingerprint)
        {District=district;DoorKey=doorKey;DoorX=doorX;DoorY=doorY;DoorZ=doorZ;Fingerprint=fingerprint;}
        internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        internal static string SpotFingerprint(StaticWorldObject spot) => Hash(JsonSerializer.Serialize(spot,MissionProgress.SaveJson));
        internal static string CatalogFingerprint(MissionSite s) => Hash(JsonSerializer.Serialize(new {
            s.style,s.metrId,s.floorY,s.minX,s.maxX,s.minZ,s.maxZ,
            Door=SpotFingerprint(s.elevatorDoor),Lobby=SpotFingerprint(s.lobbyDoor),
            Npcs=s.npcSpots,Containers=s.containers.Select(SpotFingerprint).ToArray(),Spawners=s.objectSpots.Select(SpotFingerprint).ToArray(),
            Real=s.realObjects.Select(SpotFingerprint).ToArray(),Shaft=s.shaft.Select(SpotFingerprint).ToArray()}));
        internal static MissionOpenFloorSite From(MissionSite s) => new MissionOpenFloorSite(s.metrId,s.elevatorDoor.mxoStaticId,
            s.elevatorDoor.pos_x,s.elevatorDoor.pos_y,s.elevatorDoor.pos_z,CatalogFingerprint(s));
        internal MissionSite Resolve(IEnumerable<MissionSite> sites)
        {
            var matching=sites.Where(s=>s?.elevatorDoor!=null&&s.metrId==District&&s.elevatorDoor.mxoStaticId==DoorKey&&
                s.elevatorDoor.pos_x==DoorX&&s.elevatorDoor.pos_y==DoorY&&s.elevatorDoor.pos_z==DoorZ&&CatalogFingerprint(s)==Fingerprint).Take(2).ToArray();
            return matching.Length==1?matching[0]:null;
        }
    }
    public sealed class MissionOpenFloorPendingAdmission
    {
        public int Policy { get; }
        public int Mission { get; }
        public string Identity { get; }
        public int Phase { get; }
        public long Started { get; }
        public MissionOpenFloorSite Site { get; }
        private readonly MissionOpenFloorChoice[] choices;
        public MissionOpenFloorChoice[] Choices => (MissionOpenFloorChoice[])choices.Clone();
        public string Refusal { get; }
        private FailureMemo failureMemo=new FailureMemo();
        private sealed class FailureMemo { internal string Key,Refusal; }
        internal bool TryFailedGeometry(string key,out string refusal)
        {refusal=failureMemo.Key==key?failureMemo.Refusal:null;return refusal!=null;}
        internal void RememberFailedGeometry(string key,string refusal)
        {if(!string.IsNullOrEmpty(key)&&!string.IsNullOrEmpty(refusal)){failureMemo.Key=key;failureMemo.Refusal=refusal;}}
        [JsonConstructor] public MissionOpenFloorPendingAdmission(int policy,int mission,string identity,int phase,long started,MissionOpenFloorSite site,MissionOpenFloorChoice[] choices,string refusal)
        {Policy=policy;Mission=mission;Identity=identity;Phase=phase;Started=started;Site=site;this.choices=(MissionOpenFloorChoice[])(choices??Array.Empty<MissionOpenFloorChoice>()).Clone();Refusal=refusal;}
        internal MissionOpenFloorPendingAdmission WithRefusal(string reason) => new MissionOpenFloorPendingAdmission(Policy,Mission,Identity,Phase,Started,Site,choices,reason){failureMemo=failureMemo};
    }

}
