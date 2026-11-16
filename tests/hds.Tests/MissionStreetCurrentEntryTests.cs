using System.IO.Compression;
using System.Text.Json;
using hds;
namespace hds.Tests;
public sealed partial class MissionStreetAccessTests
{
    static (MissionSite Site,MissionStreetContexts.Context Context,MissionStreetContexts Cache,StaticObjectIndex Current) Audited665Entries()
    {
        using var input=File.OpenRead(Path.Combine(DirectoryFor("mission_street_access"),"contexts.json.gz"));using var zip=new GZipStream(input,CompressionMode.Decompress);
        var document=JsonSerializer.Deserialize<MissionStreetContexts.Document>(zip)!;
        var context=document.Contexts.Single(c=>c.Key=="3FCAD1F41EC059A78A4D308EDD02D10C4C494BBCB95615BB9CECCF77098479D0");
        var floor=MissionNpcInteriorPlanning.ReadRaw(context.Floor.RawDoorJson);var lobby=MissionNpcInteriorPlanning.ReadRaw(context.Lobby.RawDoorJson);
        Assert.Equal(697304355u,floor.mxoStaticId);Assert.Equal(697304375u,lobby.mxoStaticId);Assert.Equal((ushort)419,MissionSites.Goid(floor));
        var site=new MissionSite{metrId=1,elevatorDoor=floor,lobbyDoor=lobby};
        var denied=MissionStreetContexts.Refused("Npc","Objects-only test context");string inputs=MissionNpcDomainCertificates.Inputs(new[]{floor,lobby});
        var doc=new MissionStreetContexts.Document{Version=1,World=1,Catalogue=MissionNpcDomainCertificates.Catalogue(new[]{site}),Inputs=inputs,
            InteriorManifest=MissionInteriorEvidence.ActiveManifestSha256,PublicIndex=MissionPublicDoors.IndexIdentity,NpcDomains=MissionNpcCoverage.DomainSha256,
            Sites=new[]{new MissionStreetContexts.Row{Site=MissionOpenFloorSite.CatalogFingerprint(site),Npc=denied.Key,Objects=context.Key}},Contexts=new[]{context,denied}};
        var cache=MissionStreetContexts.Read(JsonSerializer.SerializeToUtf8Bytes(doc),new[]{site},inputs,MissionPublicDoors.IndexIdentity);
        // The real loader's first-row policy, preserving BOTH original CSV records at each key.
        var current=new StaticObjectIndex();
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data","staticObjects_slums_1.csv")).Where(l=>l.Contains(",23059029,")||l.Contains(",37059029,")))
        {
            string[] fields=line.Split(',');var raw=new StaticWorldObject{metrId=ushort.Parse(fields[0]),sectorID=ushort.Parse(fields[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(fields[2])),
                type=Convert.FromHexString(fields[4]),exterior=bool.Parse(fields[5]),
                pos_x=double.Parse(fields[6],System.Globalization.CultureInfo.InvariantCulture),pos_y=double.Parse(fields[7],System.Globalization.CultureInfo.InvariantCulture),pos_z=double.Parse(fields[8],System.Globalization.CultureInfo.InvariantCulture),
                rot=double.Parse(fields[9],System.Globalization.CultureInfo.InvariantCulture),quat=fields[10]};
            // staticId is redundant in this comparison; derive its normal key component explicitly.
            raw.staticId=raw.mxoStaticId&0xfffff;current.Add(raw);
        }
        Assert.Equal((ushort)417,MissionSites.Goid(current.Find(floor.mxoStaticId,1)));Assert.Equal((ushort)417,MissionSites.Goid(current.Find(lobby.mxoStaticId,1)));
        return(site,context,cache,current);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void Actual665Default417VersusSelected419RefusesBothCarSidesBeforeChoose(bool floorSide)
    {
        var f=Audited665Entries();var floor=f.Site.elevatorDoor;var lobby=f.Site.lobbyDoor;
        StaticWorldObject Lookup(uint key)=>key==(floorSide?floor.mxoStaticId:lobby.mxoStaticId)?f.Current.Find(key,1):key==floor.mxoStaticId?floor:lobby;
        Assert.True(f.Cache.Allows(f.Site,false)); // Old compact-only filter admitted this same context.
        Assert.False(f.Cache.AllowsCurrent(f.Site,false,Lookup));
        Assert.False(MissionStreetContexts.CurrentEntryIdentities(f.Context,Lookup,out var why));Assert.Equal("ChangedStreet"+(floorSide?"Floor":"Lobby")+"RawIdentityOrGoid",why);
        var dataset=new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2"));
        Assert.False(MissionStreetContexts.ValidateSelected(f.Context,f.Site,dataset,Lookup,out why));Assert.Equal("ChangedStreet"+(floorSide?"Floor":"Lobby")+"RawIdentityOrGoid",why);
    }
    [Fact] public void MatchingSelectedRawIdentitiesRemainEligibleAndFullyValidated()
    {
        var f=Audited665Entries();var c=Load(665);
        StaticWorldObject Lookup(uint key)=>key==f.Site.elevatorDoor.mxoStaticId?f.Site.elevatorDoor:key==f.Site.lobbyDoor.mxoStaticId?f.Site.lobbyDoor:Raw(c.Raw,key);
        Assert.True(f.Cache.AllowsCurrent(f.Site,false,Lookup));Assert.True(MissionStreetContexts.CurrentEntryIdentities(f.Context,Lookup,out var why),why);
        Assert.True(MissionStreetContexts.ValidateSelected(f.Context,f.Site,new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2")),Lookup,out why),why);
    }
    [Theory][InlineData("missing")][InlineData("world")][InlineData("sector")][InlineData("key")][InlineData("goid")]
    public void CurrentDispatchIdentityCannotBeSubstituted(string change)
    {
        var f=Audited665Entries();var wrong=MissionNpcInteriorPlanning.ReadRaw(f.Context.Floor.RawDoorJson);
        if(change=="world")wrong.metrId=2;if(change=="sector")wrong.sectorID++;if(change=="key")wrong.mxoStaticId++;if(change=="goid")wrong.type=BitConverter.GetBytes((ushort)417);
        Assert.False(MissionStreetContexts.CurrentEntryIdentities(f.Context,key=>key==f.Site.elevatorDoor.mxoStaticId?change=="missing"?null!:wrong:f.Site.lobbyDoor,out _));
    }
    [Theory][InlineData("position")][InlineData("rotation")][InlineData("quaternion")]
    public void NecessaryIdentityDoesNotReplaceExactSavedEntryValidation(string change)
    {
        var f=Audited665Entries();var wrong=MissionNpcInteriorPlanning.ReadRaw(f.Context.Floor.RawDoorJson);
        if(change=="position")wrong.pos_x++;if(change=="rotation")wrong.rot++;if(change=="quaternion")wrong.quat="0000000000000000000000000000803f";
        StaticWorldObject Lookup(uint key)=>key==f.Site.elevatorDoor.mxoStaticId?wrong:f.Site.lobbyDoor;
        Assert.True(f.Cache.AllowsCurrent(f.Site,false,Lookup)); // Necessary identity is deliberately not a geometry certificate.
        Assert.False(MissionStreetContexts.ValidateSelected(f.Context,f.Site,new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2")),Lookup,out _));
    }
    [Fact] public void RefusedDurableContextRetainsExactPayloadChoicesAndItemsWithoutReroll()
    {
        var f=Audited665Entries();var selection=new MissionStreetSelection(1,101216,"9aa5d1f8c21643849871440c37883ea2",1794812627,2,MissionOpenFloorSite.From(f.Site),"Objects",f.Context,null);
        var pending=new MissionStreetPending(selection,Array.Empty<MissionOpenFloorChoice>());
        var state=new MissionProgress.SavedState{Version=2,Mission=101216,Phase=1,Items=new(){{6831,6}}};
        string before=JsonSerializer.Serialize(new{Pending=pending,State=state},MissionProgress.SaveJson);
        Assert.False(MissionStreetContexts.ValidateSelected(pending.Selection.Context,f.Site,new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2")),key=>f.Current.Find(key,1),out _));
        Assert.Same(selection,pending.Selection);Assert.Empty(pending.Choices);Assert.Equal((ushort)6,state.Items[6831]);
        Assert.Equal(before,JsonSerializer.Serialize(new{Pending=pending,State=state},MissionProgress.SaveJson));
    }
}
