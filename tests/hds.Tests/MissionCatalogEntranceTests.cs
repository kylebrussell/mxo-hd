using System.IO.Compression;
using System.Text.Json;
using HeadlessClient;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionCatalogEntranceTests
{
    [Theory][InlineData(1205,0)][InlineData(101210,1)][InlineData(301209,3)]
    public void CatalogWorkerUsesTheSameMissionSeedAsAnIndividualServer(int mission,int phase)
    {
        Assert.Equal(MissionStaging.QaSiteSeed("1","mxotest_sweep",mission.ToString(),mission,phase),MissionStaging.QaSiteSeed("1","mxotest_sweep","catalog",mission,phase));
        Assert.Null(MissionStaging.QaSiteSeed("1","realityhd","catalog",mission,phase));
        Assert.Null(MissionStaging.QaSiteSeed("0","mxotest_sweep","catalog",mission,phase));
    }
    static string Repo { get { string p=AppContext.BaseDirectory; while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!; return p; } }

    [Fact]
    public void Sector1129CarUsesClientModelCentreAndReadyMessagesNeverAdvertiseItsCsvAlias()
    {
        using var packed=File.OpenRead(Path.Combine(Repo,"data/mission_street_access/1/contexts.json.gz"));
        using var zip=new GZipStream(packed,CompressionMode.Decompress);
        using var doc=JsonDocument.Parse(zip);
        var context=JsonSerializer.Deserialize<MissionStreetContexts.Context>(doc.RootElement.GetProperty("Contexts").EnumerateArray().Single(c=>
            c.GetProperty("Kind").GetString()=="Npc" && c.GetProperty("Refusal").ValueKind==JsonValueKind.Null &&
            c.GetProperty("Floor").GetProperty("Position")[1].GetDouble()==545 &&
            c.GetProperty("Lobby").GetProperty("RawDoorJson").GetString()!.Contains("1183842843") &&
            c.GetProperty("Lobby").GetProperty("RawDoorJson").GetString()!.Contains("-2800")).GetRawText())!;
        var raw=MissionNpcInteriorPlanning.ReadRaw(context.Lobby.RawDoorJson);
        Assert.Equal(new double[]{-2800,145,-5700},new[]{raw.pos_x,raw.pos_y,raw.pos_z});
        Assert.Equal(new double[]{-3500,145,-6400},context.Lobby.Position);
        var evidence=MissionInteriorEvidence.LoadActive(Path.Combine(Repo,"data/mission_interior_evidence_v2/1"),1,1129);
        var actual=MissionNpcInteriorCheckpoint.SavedEntry(evidence,context.Lobby,raw);
        Assert.Equal(1183842843u,actual.Door.Key);
        Assert.Equal((ushort)419,actual.Door.Goid);
        var floor=MissionNpcInteriorPlanning.ReadRaw(context.Floor.RawDoorJson);
        var site=new MissionSite{metrId=1,elevatorDoor=floor,lobbyDoor=raw};
        var identity=Guid.NewGuid().ToString("N");
        var saved=new MissionProgress.SavedState{Version=2,Mission=1205,Identity=identity,Started=1,Phase=1,
            SiteDoors=new[]{floor},OpenFloorSites=new[]{MissionOpenFloorSite.From(site)},OpenFloorCurrentSiteIndex=0,
            Waypoints=new[]{new MissionWaypoint{x=context.Floor.Position[0],y=context.Floor.Position[1],z=context.Floor.Position[2],doorHex=MissionSites.DoorHex(floor)}},Exits=new[]{context.Lobby.Position},
            StreetAccess=JsonSerializer.SerializeToElement(new MissionStreetEnvelope(1,"StreetAccess",true,
                new[]{new MissionStreetSelection(1,1205,identity,1,1,MissionOpenFloorSite.From(site),"Npc",context,null!)}))};
        var lines=MissionCatalogEntrances.Describe(saved);
        Assert.Contains(lines,l=>l.Contains("phase 1 (area 2)")&&l.Contains("street Door 1183842312 at -4800,145,-6100"));
        Assert.Contains(lines,l=>l.Contains("Lobby car 1183842843 at -3500,145,-6400")&&l.Contains("stand -3500,95,-6200"));
        Assert.All(lines,l=>Assert.DoesNotContain("-2800",l));
        var mission=new Mission{id=1205,district=1};
        mission.phases.Add(new MissionPhase{site="Normal"});mission.phases.Add(new MissionPhase{site="Normal"});
        var progress=new MissionProgress(mission){phaseIndex=1,site=site};
        progress.streetWitnesses.Add(1,new MissionStreetSelection(1,1205,progress.persistenceId,progress.startedAt,1,MissionOpenFloorSite.From(site),"Npc",context,null!));
        NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        Assert.True(NavMesh.Locate(1,-3500,95,-6200,out var at));
        var waypoint=MissionStreetGuidance.Project(progress,at);
        Assert.Equal(MissionSites.DoorHex(raw),waypoint.doorHex);
        Assert.Equal(context.Lobby.Position,new[]{waypoint.x,waypoint.y,waypoint.z});
        Assert.True(NavMesh.Locate(1,-3500,495,-6200,out var upstairs));
        Assert.Equal(MissionSites.DoorHex(floor),MissionStreetGuidance.Project(progress,upstairs).doorHex);
        progress.waitingForExit=true;
        Assert.Equal(MissionSites.DoorHex(raw),MissionStreetGuidance.Project(progress,at).doorHex);
        progress.CommitGeneratedFloor(1,site,raw);
        Assert.Equal(1183842312u,BitConverter.ToUInt32(Convert.FromHexString(MissionStreetGuidance.Project(progress,at).doorHex)));
        progress.phaseIndex=2;
        Assert.Equal(1183842312u,BitConverter.ToUInt32(Convert.FromHexString(MissionStreetGuidance.Project(progress,at).doorHex)));
    }
}
