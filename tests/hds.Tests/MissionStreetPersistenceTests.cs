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
    [Fact]public void PublicApproachProjectionDoesNotRewriteOwnedCarOrExitAuthority()
    {
        var m=StreetMission();var p=new MissionProgress(m){phaseIndex=1,streetPolicy=true,persistenceId="street-owner",startedAt=123};var (site,c)=StreetContext("Objects");
        p.streetWitnesses.Add(1,new MissionStreetSelection(1,m.id,p.persistenceId,p.startedAt,1,MissionOpenFloorSite.From(site),"Objects",c,null));
        var car=new MissionWaypoint{x=c.Floor.Position[0],y=c.Floor.Position[1],z=c.Floor.Position[2],doorHex="2000100d"};p.waypoints.Add(car);p.exits.Add(c.Lobby.Position);
        string before=JsonSerializer.Serialize(new{p.waypoints,p.exits},MissionProgress.SaveJson);
        var shown=MissionStreetGuidance.Project(p,(NavLocation?)null);
        Assert.Equal(c.Door.Position,new[]{shown.x,shown.y,shown.z});Assert.NotEqual(car.doorHex,shown.doorHex);
        Assert.Equal(before,JsonSerializer.Serialize(new{p.waypoints,p.exits},MissionProgress.SaveJson));
        p.restoreRefusal="quarantined";Assert.Null(MissionStreetGuidance.Project(p,(NavLocation?)null));
    }

    [Fact]public void DiscoveredPublicModeUsesNormalRoomProfileWithoutSelectedProgressOrHiddenOpen()
    {
        var plan=NativeQa.NativeQaModes.RequireKnown("mission-public-street-access").Plan;
        Assert.Equal("101213",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal("1",plan.EnvironmentOverrides["MXO_PACKET_LOG"]);
        Assert.Equal((17000,2300),(plan.X,plan.Z));Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        string receipts=Assert.Single(plan.BeforeLoadout);Assert.Contains("(110,101212)",receipts);Assert.DoesNotContain("(110,101213)",receipts);
        Assert.Equal(new NativeQa.Modes.MissionNpcRoomRolesMode().Plan.Abilities,plan.Abilities);
        Assert.All(plan.AfterLoadout,sql=>Assert.DoesNotContain("char_active_missions",sql,StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.ReadyMessages,m=>m.Contains("WALK through")&&m.Contains("car waypoint")&&m.Contains("Stop on brick"));
    }

}

