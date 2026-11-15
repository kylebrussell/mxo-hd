using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using E=hds.MissionInteriorEvidence;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    // Compact fresh-selection evidence. It never authorizes a different committed site or a new root on replay.
    internal sealed partial class MissionStreetContexts
    {
        internal sealed class Context
        {
            public string Key {get;}public string Kind {get;}public string Refusal {get;}public string StreetDigest {get;}
            public MissionNpcInteriorEntry Floor {get;}public MissionNpcInteriorEntry Lobby {get;}public MissionNpcInteriorDoor Door {get;}
            private readonly MissionNpcInteriorNode[] outside,inside;
            public MissionNpcInteriorNode[] Outside=>(MissionNpcInteriorNode[])outside?.Clone();public MissionNpcInteriorNode[] Inside=>(MissionNpcInteriorNode[])inside?.Clone();
            [JsonConstructor]public Context(string key,string kind,string refusal,string streetDigest,MissionNpcInteriorEntry floor,MissionNpcInteriorEntry lobby,MissionNpcInteriorDoor door,MissionNpcInteriorNode[] outside,MissionNpcInteriorNode[] inside)
            {Key=key;Kind=kind;Refusal=refusal;StreetDigest=streetDigest;Floor=floor;Lobby=lobby;Door=door;this.outside=(MissionNpcInteriorNode[])outside?.Clone();this.inside=(MissionNpcInteriorNode[])inside?.Clone();}
            internal Context WithKey(string key)=>new Context(key,Kind,Refusal,StreetDigest,Floor,Lobby,Door,outside,inside);
        }
        internal sealed class Row {public string Site {get;set;}public string Npc {get;set;}public string Objects {get;set;}}
        internal sealed class Document
        {
            public int Version {get;set;}public ushort World {get;set;}public string Catalogue {get;set;}public string Inputs {get;set;}
            public string InteriorManifest {get;set;}public string PublicIndex {get;set;}public string NpcDomains {get;set;}
            public Row[] Sites {get;set;}public Context[] Contexts {get;set;}
        }
        private readonly Dictionary<MissionSite,Tuple<string,Context,Context>> sites;
        private MissionStreetContexts(Dictionary<MissionSite,Tuple<string,Context,Context>> sites){this.sites=sites;}
        private static string Hash(string value)=>MissionOpenFloorSite.Hash(value);
        private static string Fingerprint(Context c)=>Hash(JsonSerializer.Serialize(new{c.Kind,c.Refusal,c.StreetDigest,c.Floor,c.Lobby,c.Door,c.Outside,c.Inside}));
        private static bool Digest(string s)=>s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);
        private static void Need(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
        internal static MissionStreetContexts Read(byte[] json,IReadOnlyList<MissionSite> catalogue,string inputs,string publicIndex)
        {
            var doc=JsonSerializer.Deserialize<Document>(json);
            Need(doc!=null&&doc.Version==1&&doc.World==1&&doc.Catalogue==MissionNpcDomainCertificates.Catalogue(catalogue)&&doc.Inputs==inputs&&
                doc.InteriorManifest==E.ActiveManifestSha256&&doc.PublicIndex==publicIndex&&doc.NpcDomains==MissionNpcCoverage.DomainSha256,"StaleStreetContextCatalogue");
            Need(doc.Sites!=null&&doc.Contexts!=null&&doc.Sites.All(r=>r!=null&&Digest(r.Site)&&Digest(r.Npc)&&Digest(r.Objects))&&
                doc.Sites.Select(r=>r.Site).Distinct().Count()==doc.Sites.Length&&doc.Contexts.All(c=>c!=null&&Digest(c.Key)&&c.Key==Fingerprint(c)&&new[]{"Npc","Objects"}.Contains(c.Kind))&&
                doc.Contexts.Select(c=>c.Key).Distinct().Count()==doc.Contexts.Length,"StreetContextShape");
            var contexts=doc.Contexts.ToDictionary(c=>c.Key);var rows=doc.Sites.ToDictionary(r=>r.Site);var selected=new Dictionary<MissionSite,Tuple<string,Context,Context>>();
            foreach(Context c in contexts.Values)
            {
                if(c.Refusal!=null){Need(!string.IsNullOrWhiteSpace(c.Refusal)&&c.Floor==null&&c.Lobby==null&&c.Door==null&&c.Outside==null&&c.Inside==null,"StreetContextRefusalShape");continue;}
                Need(Digest(c.StreetDigest)&&Entry(c.Floor)&&Entry(c.Lobby)&&c.Door!=null&&MissionInteriorDoorTypes.IsSupported(c.Door.Goid)&&
                    MissionNpcInteriorPlanning.ReadRaw(c.Door.RawDoorJson).mxoStaticId==c.Door.Key&&c.Door.Position?.Length==3&&c.Door.Position.All(double.IsFinite)&&
                    c.Outside?.Length>0&&c.Inside?.Length>0&&c.Inside[c.Inside.Length-1].Equals(c.Lobby.Node),"StreetContextEntryShape");
            }
            foreach(MissionSite site in catalogue)
            {
                string fingerprint=MissionOpenFloorSite.CatalogFingerprint(site);
                Need(rows.TryGetValue(fingerprint,out Row row)&&contexts.TryGetValue(row.Npc,out Context npc)&&contexts.TryGetValue(row.Objects,out Context objects)&&npc.Kind=="Npc"&&objects.Kind=="Objects","MissingStreetContextSite");
                selected[site]=Tuple.Create(fingerprint,contexts[row.Npc],contexts[row.Objects]);
            }
            Need(rows.Count==catalogue.Select(MissionOpenFloorSite.CatalogFingerprint).Distinct().Count(),"UnexpectedStreetContextSite");return new MissionStreetContexts(selected);
        }
        private static bool Entry(MissionNpcInteriorEntry entry)=>entry!=null&&Digest(entry.EvidenceDigest)&&!string.IsNullOrWhiteSpace(entry.RawDoorJson)&&
            entry.Position?.Length==3&&entry.Standing?.Length==3&&entry.Position.All(double.IsFinite)&&entry.Standing.All(double.IsFinite)&&entry.Quaternion?.Length==32&&entry.Quaternion.All(Uri.IsHexDigit);
        internal bool TryGet(MissionSite site,bool npc,out Context context,out string refusal)
        {
            context=null;refusal="MissingStreetContextSite";
            if(site==null||!sites.TryGetValue(site,out var row)||row.Item1!=MissionOpenFloorSite.CatalogFingerprint(site))return false;
            context=npc?row.Item2:row.Item3;refusal=context.Refusal;return refusal==null;
        }
        internal static Context FromProof(string kind,MissionNpcInteriorEntry floor,MissionNpcInteriorEntry lobby,E.StreetAccess proof)
        {
            var door=proof.Door;var context=new Context(null,kind,null,proof.OutdoorDigest,floor,lobby,
                new MissionNpcInteriorDoor(door.Key,door.Goid,proof.RawDoor,door.Quaternion,new double[]{door.Position.X,door.Position.Y,door.Position.Z}),
                proof.OutsidePath.Select(n=>new MissionNpcInteriorNode(n.Region,n.Polygon)).ToArray(),proof.InsidePath.Select(n=>new MissionNpcInteriorNode(n.Region,n.Polygon)).ToArray());
            return context.WithKey(Fingerprint(context));
        }
        internal static Context Refused(string kind,string reason)
        {var context=new Context(null,kind,reason,null,null,null,null,null,null);return context.WithKey(Fingerprint(context));}
        internal static bool ValidateSelected(Context context,MissionSite site,MissionPublicDoors.Dataset dataset,Func<uint,StaticWorldObject> lookup,out string refusal)
        {
            refusal="InvalidSelectedStreetContext";if(context==null||context.Refusal!=null||site==null||lookup==null||
                context.Floor.RawDoorJson!=MissionNpcInteriorPlanning.RawJson(site.elevatorDoor)||context.Lobby.RawDoorJson!=MissionNpcInteriorPlanning.RawJson(site.lobbyDoor))return false;
            if(!dataset.TryScene(site.metrId,site.elevatorDoor.sectorID,out E floor,out _,out refusal)||
                !dataset.TryScene(site.metrId,site.lobbyDoor.sectorID,out E lobby,out var outdoor,out refusal))return false;
            try
            {
                MissionNpcInteriorCheckpoint.SavedEntry(floor,context.Floor);var saved=MissionNpcInteriorCheckpoint.SavedEntry(lobby,context.Lobby);
                if(!lobby.TryStreetAccess(outdoor,saved,site.lobbyDoor.mxoStaticId,lookup,out var proof,out refusal))return false;
                Context actual=FromProof(context.Kind,context.Floor,context.Lobby,proof);
                if(JsonSerializer.Serialize(actual)!=JsonSerializer.Serialize(context)){refusal="ChangedSelectedStreetContext";return false;}
                refusal=null;return true;
            }
            catch(Exception ex) when(ex is InvalidDataException||ex is MissionCheckpointException||ex is JsonException||ex is ArgumentException)
            {refusal="InvalidSelectedStreetContext:"+ex.Message;return false;}
        }
    }
}
