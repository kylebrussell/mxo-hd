using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using E = hds.MissionInteriorEvidence;
namespace hds
{
    // A fresh-selection domain, never persistence or permission to stage actors.
    internal sealed class MissionNpcDomainCertificates
    {
        internal const int Policy = 1, RolePlan = 1;
        internal sealed class Row { public string Site { get; set; } public string Domain { get; set; } }
        internal sealed class Domain
        {
            public string Key { get; set; } public string FloorDigest { get; set; } public string LobbyDigest { get; set; }
            public int Capacity { get; set; } public string Refusal { get; set; }
            public MissionNpcInteriorEntry Floor { get; set; } public MissionNpcInteriorEntry Lobby { get; set; }
            internal bool Allows(int roles) => Refusal==null && roles>0 && roles<=Capacity;
            internal bool Matches(MissionSite site,int roles,E floor,E.Entry floorEntry,E lobby,E.Entry lobbyEntry) => Allows(roles) &&
                FloorDigest==floor.Digest && LobbyDigest==lobby.Digest && SameEntry(Floor,Entry(site.elevatorDoor,floor,floorEntry)) && SameEntry(Lobby,Entry(site.lobbyDoor,lobby,lobbyEntry));
        }
        internal sealed class Document
        {
            public int Format { get; set; } public int Policy { get; set; } public int RolePlan { get; set; } public ushort World { get; set; }
            public string ProfilesManifest { get; set; } public string Catalogue { get; set; } public string Inputs { get; set; }
            public Row[] Sites { get; set; } public Domain[] Domains { get; set; }
        }
        internal sealed class Binding
        {
            private readonly Domain domain; private readonly string fingerprint;
            internal Binding(string fingerprint,Domain domain) {this.fingerprint=fingerprint;this.domain=domain;}
            internal MissionNpcInteriorEntry FloorEntry=>domain.Floor; internal MissionNpcInteriorEntry LobbyEntry=>domain.Lobby;
            internal string Refusal(int roles) => domain.Refusal??(domain.Allows(roles)?null:"InsufficientCertifiedNpcCapacity");
            internal bool Matches(MissionSite site,int roles,E floor,E.Entry floorEntry,E lobby,E.Entry lobbyEntry) => MissionOpenFloorSite.CatalogFingerprint(site)==fingerprint&&domain.Matches(site,roles,floor,floorEntry,lobby,lobbyEntry);
        }
        private readonly Dictionary<MissionSite,Binding> sites;
        internal readonly int Count;
        private MissionNpcDomainCertificates(Dictionary<MissionSite,Binding> sites) { this.sites=sites;Count=sites.Count; }
        private static string Hash(string value) => MissionOpenFloorSite.Hash(value);
        internal static string Catalogue(IEnumerable<MissionSite> sites) => Hash(string.Join("\n",sites.Select(MissionOpenFloorSite.CatalogFingerprint)));
        internal static string Inputs(IEnumerable<StaticWorldObject> world) => Hash(string.Join("\n",world.Where(o=>o.metrId==1&&o.type!=null&&o.type.Length>=2&&
            (MissionSites.Goid(o)==19||MissionSites.Goid(o)==417||MissionSites.Goid(o)==419||MissionInteriorDoorTypes.IsSupported(MissionSites.Goid(o)))).Select(MissionNpcInteriorPlanning.RawJson)));
        internal static MissionNpcInteriorEntry Entry(StaticWorldObject raw,E evidence,E.Entry entry) => new MissionNpcInteriorEntry(MissionNpcInteriorPlanning.RawJson(raw),evidence.Digest,entry.Door.Quaternion,
            new MissionNpcInteriorNode(entry.Node.Region,entry.Node.Polygon),new double[]{entry.Door.Position.X,entry.Door.Position.Y,entry.Door.Position.Z},new double[]{entry.Standing.X,entry.Standing.Y,entry.Standing.Z});
        private static bool SameEntry(MissionNpcInteriorEntry a,MissionNpcInteriorEntry b) => JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b);
        private static bool Digest(string value) => value!=null&&value.Length==64&&value.All(Uri.IsHexDigit);
        private static void Require(bool value,string reason) { if(!value)throw new InvalidDataException(reason); }
        internal static MissionNpcDomainCertificates Read(byte[] json,IReadOnlyList<MissionSite> catalogue,string inputs,string profilesManifest)
        {
            Document doc=JsonSerializer.Deserialize<Document>(json);
            Require(doc!=null&&doc.Format==1&&doc.Policy==Policy&&doc.RolePlan==RolePlan&&doc.World==1,"NpcDomainPolicy");
            Require(doc.ProfilesManifest==profilesManifest&&Digest(doc.ProfilesManifest),"NpcDomainProfiles");
            string[] fingerprints=catalogue.Select(MissionOpenFloorSite.CatalogFingerprint).ToArray();
            Require(doc.Catalogue==Hash(string.Join("\n",fingerprints))&&doc.Inputs==inputs,"StaleNpcDomainCatalogue");
            Require(doc.Sites!=null&&doc.Domains!=null&&doc.Sites.All(r=>r!=null&&Digest(r.Site)&&Digest(r.Domain))&&doc.Domains.All(d=>d!=null&&Digest(d.Key)),"NpcDomainShape");
            Require(doc.Sites.Select(r=>r.Site).Distinct().Count()==doc.Sites.Length&&doc.Domains.Select(d=>d.Key).Distinct().Count()==doc.Domains.Length,"DuplicateNpcDomain");
            var domains=doc.Domains.ToDictionary(d=>d.Key);
            foreach(Domain d in domains.Values)
            {
                Require(d.Capacity>=0&&(d.Refusal==null?d.Capacity>0&&d.Floor!=null&&d.Lobby!=null&&Digest(d.FloorDigest)&&Digest(d.LobbyDigest):!string.IsNullOrWhiteSpace(d.Refusal)&&d.Capacity==0),"NpcDomainCapacity");
                if(d.Refusal==null)Require(EntryShape(d.Floor,d.FloorDigest)&&EntryShape(d.Lobby,d.LobbyDigest),"NpcDomainEntry");
            }
            var rows=doc.Sites.ToDictionary(r=>r.Site,r=>r.Domain);var selected=new Dictionary<MissionSite,Binding>();
            for(int i=0;i<catalogue.Count;i++)
            {
                Require(rows.TryGetValue(fingerprints[i],out string key)&&domains.ContainsKey(key),"MissingNpcDomainSite");selected[catalogue[i]]=new Binding(fingerprints[i],domains[key]);
            }
            Require(rows.Count==fingerprints.Distinct().Count(),"UnexpectedNpcDomainSite");return new MissionNpcDomainCertificates(selected);
        }
        private static bool EntryShape(MissionNpcInteriorEntry entry,string digest) => entry.EvidenceDigest==digest&&entry.RawDoorJson!=null&&entry.Quaternion!=null&&entry.Quaternion.Length==32&&
            entry.Position!=null&&entry.Position.Length==3&&entry.Position.All(double.IsFinite)&&entry.Standing!=null&&entry.Standing.Length==3&&entry.Standing.All(double.IsFinite);
        internal static MissionNpcDomainCertificates Load(byte[] compressed,string hash,IReadOnlyList<MissionSite> catalogue,string inputs,string profilesManifest)
        {
            Require(Digest(hash)&&Convert.ToHexString(SHA256.HashData(compressed)).Equals(hash,StringComparison.OrdinalIgnoreCase),"NpcDomainPayloadHash");
            using var stream=new MemoryStream(compressed);using var gzip=new GZipStream(stream,CompressionMode.Decompress);using var output=new MemoryStream();gzip.CopyTo(output);
            return Read(output.ToArray(),catalogue,inputs,profilesManifest);
        }
        internal bool TryGet(MissionSite site,int roles,out Binding domain,out string refusal)
        {
            if(!sites.TryGetValue(site,out domain)){refusal="MissingNpcDomainSite";return false;}
            refusal=domain.Refusal(roles);return refusal==null;
        }
        internal sealed class BuildResult
        {
            public Document Document { get; set; } public int SceneLoads { get; set; } public long ParseMilliseconds { get; set; } public double Seconds { get; set; }
        }
        private sealed class OfflineProvider : IMissionNpcInteriorEvidenceProvider
        {
            private readonly string directory;
            private readonly Dictionary<ushort,StaticWorldObject[]> markers;
            private readonly Dictionary<uint,StaticWorldObject> doors;
            private readonly Dictionary<ushort,E> cache=new Dictionary<ushort,E>();
            private readonly Queue<ushort> order=new Queue<ushort>();
            internal int Loads; internal long ParseMilliseconds;
            internal OfflineProvider(string directory,IEnumerable<StaticWorldObject> world)
            {
                this.directory=directory;var rows=world.Where(o=>o.metrId==1).ToArray();
                markers=rows.Where(o=>MissionSites.Goid(o)==19).GroupBy(o=>o.sectorID).ToDictionary(g=>g.Key,g=>g.ToArray());
                doors=rows.GroupBy(o=>o.mxoStaticId).ToDictionary(g=>g.Key,g=>g.First());
            }
            public E Evidence(ushort world,ushort sector)
            {
                if(cache.TryGetValue(sector,out E value))return value;
                var timer=System.Diagnostics.Stopwatch.StartNew();value=E.LoadActive(directory,world,sector);ParseMilliseconds+=timer.ElapsedMilliseconds;Loads++;
                if(order.Count==4)cache.Remove(order.Dequeue());order.Enqueue(sector);cache.Add(sector,value);return value;
            }
            public StaticWorldObject[] Markers(ushort world,ushort sector) => markers.TryGetValue(sector,out StaticWorldObject[] rows)?rows:Array.Empty<StaticWorldObject>();
            public StaticWorldObject Door(ushort world,uint key) => doors.TryGetValue(key,out StaticWorldObject value)?value:null;
            public MissionOpenFloorEntry.Result Entry(ushort world,MissionOpenFloorResolver.Position door,double x,double z,string evidence) => MissionOpenFloorEntry.Calculate(world,door,x,z,evidence);
        }
        internal static List<StaticWorldObject> ReadOfflineWorld(string directory)
        {
            var rows=new List<StaticWorldObject>();
            foreach(string name in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
                foreach(string line in File.ReadLines(Path.Combine(directory,name)).Skip(1))
                {
                    string[] d=line.Split(',');if(d.Length<11)continue;
                    rows.Add(new StaticWorldObject{metrId=ushort.Parse(d[0]),sectorID=ushort.Parse(d[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(d[2]),0),staticId=BitConverter.ToUInt32(Convert.FromHexString(d[3]),0),
                        type=Convert.FromHexString(d[4].Substring(0,4)),exterior=bool.Parse(d[5]),pos_x=double.Parse(d[6],System.Globalization.CultureInfo.InvariantCulture),pos_y=double.Parse(d[7],System.Globalization.CultureInfo.InvariantCulture),
                        pos_z=double.Parse(d[8],System.Globalization.CultureInfo.InvariantCulture),rot=double.Parse(d[9],System.Globalization.CultureInfo.InvariantCulture),quat=d[10]});
                }
            return rows;
        }
        // Offline exporter API. No mission, player, mutation or publication entry point.
        internal static BuildResult BuildOffline(IReadOnlyList<MissionSite> catalogue,IEnumerable<StaticWorldObject> world,string directory)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();var provider=new OfflineProvider(directory,world);
            Document document=Build(catalogue,world,provider,E.ActiveManifestSha256);
            return new BuildResult{Document=document,SceneLoads=provider.Loads,ParseMilliseconds=provider.ParseMilliseconds,Seconds=timer.Elapsed.TotalSeconds};
        }
        internal static Document Build(IReadOnlyList<MissionSite> catalogue,IEnumerable<StaticWorldObject> world,IMissionNpcInteriorEvidenceProvider provider,string profilesManifest)
        {
            var doc=new Document{Format=1,Policy=Policy,RolePlan=RolePlan,World=1,ProfilesManifest=profilesManifest,Catalogue=Catalogue(catalogue),Inputs=Inputs(world)};
            var domains=new Dictionary<string,Domain>();var rows=new Dictionary<string,Row>();
            foreach(MissionSite site in catalogue.OrderBy(s=>s.elevatorDoor?.sectorID??ushort.MaxValue).ThenBy(s=>s.lobbyDoor?.sectorID??ushort.MaxValue))
            {
                string siteId=MissionOpenFloorSite.CatalogFingerprint(site);if(rows.ContainsKey(siteId))continue;
                string fallback=Hash("Invalid:"+siteId);Domain domain;
                try
                {
                    if(!MissionOpenFloorAdmission.ValidSite(site))throw new InvalidDataException("InvalidNpcSite");
                    StaticWorldObject[] markers=provider.Markers(1,site.elevatorDoor.sectorID).Where(m=>site.npcSpots.Any(s=>s.SequenceEqual(new double[]{m.pos_x,m.pos_y-50,m.pos_z,m.rot}))).ToArray();
                    E floor=provider.Evidence(1,site.elevatorDoor.sectorID),lobby=provider.Evidence(1,site.lobbyDoor.sectorID);
                    string key=Hash(floor.Digest+"|"+lobby.Digest+"|"+MissionNpcInteriorPlanning.RawJson(site.elevatorDoor)+"|"+MissionNpcInteriorPlanning.RawJson(site.lobbyDoor)+"|"+
                        string.Join("|",markers.Select(MissionNpcInteriorPlanning.RawJson).Distinct().OrderBy(v=>v,StringComparer.Ordinal)));
                    if(!domains.TryGetValue(key,out domain))
                    {
                        domain=new Domain{Key=key,FloorDigest=floor.Digest,LobbyDigest=lobby.Digest};
                        if(!floor.TryBuildingEntry(site.elevatorDoor,markers,1,(native,x,z)=>provider.Entry(1,native,x,z,"certified NPC building entry"),out E.Entry floorEntry,out uint car,out string why)||
                            !lobby.TryLobbyEntry(site.lobbyDoor,car,(native,x,z)=>provider.Entry(1,native,x,z,"certified NPC street entry"),out E.Entry lobbyEntry,out why))domain.Refusal=why;
                        else
                        {
                            domain.Capacity=MissionNpcRoomRoles.Allocate(MissionNpcRoomRoles.Certify(site,markers,floor,floorEntry,provider),int.MaxValue).Length;
                            if(domain.Capacity==0)domain.Refusal="NoCertifiedNpcRoutes";
                            else {domain.Floor=Entry(site.elevatorDoor,floor,floorEntry);domain.Lobby=Entry(site.lobbyDoor,lobby,lobbyEntry);}
                        }
                        domains.Add(key,domain);
                    }
                }
                catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is UnauthorizedAccessException||ex is JsonException)
                {domain=new Domain{Key=fallback,Refusal="NpcDomainUnavailable:"+ex.Message};domains[fallback]=domain;}
                rows[siteId]=new Row{Site=siteId,Domain=domain.Key};
            }
            doc.Sites=rows.Values.ToArray();doc.Domains=domains.Values.ToArray();return doc;
        }
    }
}
