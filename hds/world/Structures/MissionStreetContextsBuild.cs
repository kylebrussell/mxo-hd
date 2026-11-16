using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using E=hds.MissionInteriorEvidence;
namespace hds
{
    internal sealed partial class MissionStreetContexts
    {
        // Offline construction only. Runtime eligibility reads the compact result, never scans these scenes per candidate.
        internal static Document Build(IReadOnlyList<MissionSite> catalogue,IEnumerable<StaticWorldObject> world,MissionPublicDoors.Dataset dataset,byte[] npcDomains)
        {
            var objects=world.Where(o=>o.metrId==1).ToArray();string inputs=MissionNpcDomainCertificates.Inputs(objects);
            var npc=MissionNpcDomainCertificates.Load(npcDomains,MissionNpcCoverage.DomainSha256,catalogue,inputs,E.ActiveManifestSha256);
            var source=objects.GroupBy(o=>o.mxoStaticId).ToDictionary(g=>g.Key,g=>g.First());
            StaticWorldObject Lookup(uint key)=>source.TryGetValue(key,out var raw)?raw:null;
            var doc=new Document{Version=1,World=1,Catalogue=MissionNpcDomainCertificates.Catalogue(catalogue),Inputs=inputs,InteriorManifest=E.ActiveManifestSha256,
                PublicIndex=MissionPublicDoors.IndexIdentity,NpcDomains=MissionNpcCoverage.DomainSha256};
            var rows=new Dictionary<string,Row>();var contexts=new Dictionary<string,Context>();var memo=new Dictionary<string,Context>();
            var routes=new Dictionary<string,Tuple<E.StreetAccess,string>>();
            foreach(MissionSite site in catalogue)
            {
                string id=MissionOpenFloorSite.CatalogFingerprint(site);if(rows.ContainsKey(id))continue;
                string Add(Context c){if(!contexts.ContainsKey(c.Key))contexts.Add(c.Key,c);return c.Key;}
                Context Prepare(string kind)
                {
                    if(site.elevatorDoor==null||site.lobbyDoor==null||site.metrId!=1)return Refused(kind,"MissingInteriorStreetSite");
                    MissionNpcInteriorEntry floorEntry=null,lobbyEntry=null;
                    if(kind=="Npc")
                    {
                        if(!npc.TryGet(site,1,out var binding,out string npcWhy))return Refused(kind,npcWhy);
                        floorEntry=binding.FloorEntry;lobbyEntry=binding.LobbyEntry;
                    }
                    string key=Hash(kind+"|"+MissionNpcInteriorPlanning.RawJson(site.elevatorDoor)+"|"+MissionNpcInteriorPlanning.RawJson(site.lobbyDoor)+"|"+
                        (kind=="Npc"?JsonSerializer.Serialize(new[]{floorEntry,lobbyEntry}):site.CentreX.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+site.CentreZ.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
                    if(memo.TryGetValue(key,out Context cached))return cached;
                    Context result;
                    try
                    {
                        if(!dataset.TryScene(1,site.elevatorDoor.sectorID,out E floor,out _,out string why)||
                            !dataset.TryScene(1,site.lobbyDoor.sectorID,out E lobby,out var outside,out why))result=Refused(kind,why);
                        else
                        {
                            E.Entry actualLobby;
                            if(kind=="Objects")
                            {
                                bool Into(StaticWorldObject raw,E evidence,out E.Entry entry,out string refusal)=>evidence.TryEntry(raw,
                                    native=>MissionOpenFloorEntry.Calculate(1,native,site.CentreX,site.CentreZ,"one ordinary entry for fresh street certification"),out entry,out refusal);
                                if(!Into(site.elevatorDoor,floor,out E.Entry actualFloor,out why)||!Into(site.lobbyDoor,lobby,out actualLobby,out why))return memo[key]=Refused(kind,why);
                                floorEntry=MissionNpcDomainCertificates.Entry(site.elevatorDoor,floor,actualFloor);lobbyEntry=MissionNpcDomainCertificates.Entry(site.lobbyDoor,lobby,actualLobby);
                            }
                            else {MissionNpcInteriorCheckpoint.SavedEntry(floor,floorEntry);actualLobby=MissionNpcInteriorCheckpoint.SavedEntry(lobby,lobbyEntry);}
                            string routeKey=Hash(JsonSerializer.Serialize(lobbyEntry));
                            if(!routes.TryGetValue(routeKey,out var route))
                            {
                                bool accepted=lobby.TryStreetAccess(outside,actualLobby,site.lobbyDoor.mxoStaticId,Lookup,out var proof,out why);
                                route=Tuple.Create(accepted?proof:null,why);routes.Add(routeKey,route);
                            }
                            result=route.Item1==null?Refused(kind,route.Item2):FromProof(kind,floorEntry,lobbyEntry,route.Item1);
                        }
                    }
                    catch(Exception ex) when(ex is InvalidDataException||ex is IOException||ex is MissionCheckpointException||ex is JsonException||ex is ArgumentException)
                    {result=Refused(kind,"UnavailableStreetContext:"+ex.Message);}
                    memo[key]=result;return result;
                }
                rows[id]=new Row{Site=id,Npc=Add(Prepare("Npc")),Objects=Add(Prepare("Objects"))};
            }
            doc.Sites=rows.Values.ToArray();doc.Contexts=contexts.Values.OrderBy(c=>c.Key,StringComparer.Ordinal).ToArray();return doc;
        }
    }
}
