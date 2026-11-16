using System.Text.Json;
using hds;
using E=hds.MissionInteriorEvidence;
namespace hds.Tests;
public sealed partial class MissionStreetAccessTests
{
    static (MissionSite Site,MissionStreetContexts.Context Context) StreetContext(string kind="Npc")
    {
        var c=Load(209);var floorRaw=Raw(c.Raw,0x0d100020);var lobbyRaw=Raw(c.Raw,0x0d100028);
        var floor=Lobby(c.Evidence,c.Interior,0x0d100020,47,193,28500,1295,14200);var lobby=Lobby(c.Evidence,c.Interior,0x0d100028,49,283,28500,495,14200);
        Assert.True(c.Evidence.TryStreetAccess(c.Outdoor,lobby,0x0d100028,key=>Raw(c.Raw,key),out var proof,out var why),why);
        var site=new MissionSite{metrId=1,elevatorDoor=floorRaw,lobbyDoor=lobbyRaw,minX=27000,maxX=30000,minZ=11000,maxZ=14900,floorY=1346};site.shaft.AddRange(new[]{lobbyRaw,floorRaw});
        return(site,MissionStreetContexts.FromProof(kind,MissionNpcDomainCertificates.Entry(floorRaw,c.Evidence,floor),MissionNpcDomainCertificates.Entry(lobbyRaw,c.Evidence,lobby),proof));
    }
    static MissionStreetContexts.Document ContextDocument(MissionSite site,MissionStreetContexts.Context context,string inputs)
    {
        var negative=MissionStreetContexts.Refused("Objects","NoCertifiedOrdinaryObjectEntry");
        return new(){Version=1,World=1,Catalogue=MissionNpcDomainCertificates.Catalogue(new[]{site}),Inputs=inputs,InteriorManifest=E.ActiveManifestSha256,PublicIndex=MissionPublicDoors.IndexIdentity,
            NpcDomains=MissionNpcCoverage.DomainSha256,Sites=new[]{new MissionStreetContexts.Row{Site=MissionOpenFloorSite.CatalogFingerprint(site),Npc=context.Key,Objects=negative.Key}},Contexts=new[]{context,negative}};
    }
    [Fact]public void CompactStreetContextRoundTripsAndBindsItsExactSelectedNativeEntryWithoutReplanning()
    {
        var (site,context)=StreetContext();string before=MissionOpenFloorSite.CatalogFingerprint(site);string inputs=MissionNpcDomainCertificates.Inputs(new[]{site.elevatorDoor,site.lobbyDoor});
        var doc=ContextDocument(site,context,inputs);var cache=MissionStreetContexts.Read(JsonSerializer.SerializeToUtf8Bytes(doc),new[]{site},inputs,MissionPublicDoors.IndexIdentity);
        Assert.True(cache.TryGet(site,true,out var selected,out var why),why);Assert.False(cache.TryGet(site,false,out _,out why));Assert.Equal("NoCertifiedOrdinaryObjectEntry",why);
        var c=Load(209);var dataset=new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2"));
        Assert.True(MissionStreetContexts.ValidateSelected(selected,site,dataset,key=>Raw(c.Raw,key),out why),why);
        var copied=selected.Outside;copied[0]=new MissionNpcInteriorNode(65535,65535);Assert.NotEqual(copied[0],selected.Outside[0]);
        Assert.Equal(before,MissionOpenFloorSite.CatalogFingerprint(site));site.minX++;
        Assert.False(cache.TryGet(site,true,out _,out _));
    }
    [Theory][InlineData("catalogue")][InlineData("index")][InlineData("policy")][InlineData("context")]
    public void CompactStreetContextRejectsStaleCataloguePolicyOrAlteredProof(string change)
    {
        var (site,context)=StreetContext();string inputs=MissionNpcDomainCertificates.Inputs(new[]{site.elevatorDoor,site.lobbyDoor});var doc=ContextDocument(site,context,inputs);
        switch(change){case "catalogue":doc.Catalogue=new string('A',64);break;case "index":doc.PublicIndex=new string('A',64);break;case "policy":doc.Version=2;break;
            case "context":doc.Contexts[0]=new MissionStreetContexts.Context(context.Key,context.Kind,null,new string('A',64),context.Floor,context.Lobby,context.Door,context.Outside,context.Inside);break;}
        Assert.Throws<InvalidDataException>(()=>MissionStreetContexts.Read(JsonSerializer.SerializeToUtf8Bytes(doc),new[]{site},inputs,MissionPublicDoors.IndexIdentity));
    }
    [Fact]public void AlteredFixedStreetStandingRefusesRatherThanCalculatingANewRoot()
    {
        var (site,context)=StreetContext();double[] standing=context.Lobby.Standing;standing[1]++;
        var lobby=new MissionNpcInteriorEntry(context.Lobby.RawDoorJson,context.Lobby.EvidenceDigest,context.Lobby.Quaternion,context.Lobby.Node,context.Lobby.Position,standing);
        var changed=new MissionStreetContexts.Context(context.Key,context.Kind,null,context.StreetDigest,context.Floor,lobby,context.Door,context.Outside,context.Inside);
        var c=Load(209);var dataset=new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2"));
        Assert.False(MissionStreetContexts.ValidateSelected(changed,site,dataset,key=>Raw(c.Raw,key),out _));Assert.Equal(context.Lobby.Standing[1]+1,changed.Lobby.Standing[1]);
    }
}
