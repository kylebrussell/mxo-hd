using System.Text.Json;
using hds;
namespace hds.Tests;
public sealed partial class MissionStreetAccessTests
{
    static Mission StreetMission()
    {
        var m=new Mission{id=201203,district=1};m.phases.Add(new MissionPhase{site="Normal"});m.phases.Add(new MissionPhase{site="Normal"});
        m.phases[1].objects.Add(new MissionObjectDef{id=1,kind="Furniture"});return m;
    }
    [Fact]public void EmptyEnrollmentRoundTripsWithoutPoseAndOldCheckpointStaysUnenrolled()
    {
        var m=StreetMission();var fresh=new MissionProgress(m){streetPolicy=true,persistenceId="street-owner",startedAt=123};fresh.itemSlots[6783]=40;
        var saved=fresh.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),Array.Empty<MissionSite>());Assert.Equal(2,saved.Version);Assert.Empty(saved.StreetAccess.GetProperty("Witnesses").EnumerateArray());
        Assert.False(saved.StreetAccess.TryGetProperty("Pending",out _));Assert.Empty(saved.Waypoints);Assert.Empty(saved.Exits);
        string before=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        var restored=MissionProgress.RestoreFromCatalog(m,JsonSerializer.Deserialize<MissionProgress.SavedState>(before,MissionProgress.SaveJson)!,Array.Empty<MissionSite>());
        Assert.True(restored.streetPolicy);Assert.Equal((ushort)40,restored.itemSlots[6783]);Assert.Equal(before,JsonSerializer.Serialize(restored.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),Array.Empty<MissionSite>()),MissionProgress.SaveJson));
        var old=new MissionProgress(m){streetPolicy=false,persistenceId="old",startedAt=123};var legacy=old.Capture(47,Array.Empty<Mob>());
        var oldRestore=MissionProgress.RestoreFromCatalog(m,legacy,Array.Empty<MissionSite>());Assert.False(oldRestore.streetPolicy);Assert.Equal(JsonValueKind.Undefined,oldRestore.Capture(47,Array.Empty<Mob>()).StreetAccess.ValueKind);
    }
    [Theory][InlineData("null")][InlineData("[]")][InlineData("{\"Version\":1}")][InlineData("{\"Version\":1,\"Kind\":\"StreetAccess\",\"Enabled\":false,\"Witnesses\":[]}")]
    public void RecognizedMalformedStreetMetadataQuarantinesAfterRestoringOwnedItems(string raw)
    {
        var m=StreetMission();var p=new MissionProgress(m){streetPolicy=true,persistenceId="street-owner",startedAt=123};p.itemSlots[6783]=40;var saved=p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),Array.Empty<MissionSite>());
        saved.StreetAccess=JsonSerializer.Deserialize<JsonElement>(raw);string exact=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        var ex=Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreFromCatalog(m,saved,Array.Empty<MissionSite>()));
        Assert.Equal((ushort)40,ex.Progress.itemSlots[6783]);Assert.Equal(exact,JsonSerializer.Serialize(saved,MissionProgress.SaveJson));Assert.Empty(ex.Progress.streetWitnesses);
    }
    [Fact]public void DurableOrdinaryRefusalReplaysExactSelectionWithoutProviderOrChoices()
    {
        var m=StreetMission();var p=new MissionProgress(m){streetPolicy=true,persistenceId="street-owner",startedAt=123};var (site,_)=StreetContext("Objects");
        var selected=new MissionStreetSelection(1,m.id,p.persistenceId,p.startedAt,1,MissionOpenFloorSite.From(site),"Objects",null,"NoCertifiedPublicRoute");
        p.streetPending=new MissionStreetPending(selected,Array.Empty<MissionOpenFloorChoice>());p.itemSlots[6783]=40;var saved=p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{site});
        string exact=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        var restored=MissionProgress.RestoreFromCatalog(m,JsonSerializer.Deserialize<MissionProgress.SavedState>(exact,MissionProgress.SaveJson)!,new[]{site});
        Assert.NotNull(restored.streetPending);Assert.Equal("NoCertifiedPublicRoute",restored.streetPending.Selection.Refusal);Assert.Empty(restored.streetPending.Choices);
        Assert.Equal(exact,JsonSerializer.Serialize(restored.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{site}),MissionProgress.SaveJson));
        var envelope=JsonSerializer.Deserialize<MissionStreetEnvelope>(saved.StreetAccess.GetRawText(),MissionProgress.SaveJson)!;
        MissionStreetCheckpoint.ValidateGeometry(restored,envelope,null!,null!,new[]{site}); // Refusal has no geometry/provider authority.
    }
}

