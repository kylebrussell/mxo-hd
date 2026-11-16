using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public sealed class MissionStreetLobbyGuidanceTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static MissionProgress.SavedState Captured()=>JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllBytes(Path.Combine(Repo,"tests/hds.Tests/Fixtures/Native218-0940-101210-phase1.json")),Fields)!;
    static readonly Lazy<List<MissionSite>> Catalogue=new(()=>
    {
        var raw=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Where(l=>l.StartsWith("1,47,")||l.StartsWith("1,879,")))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            raw.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(raw,1);
    });
    static MissionProgress Progress(MissionProgress.SavedState s)
    {
        var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data/missions/zion_1_2_10_interview_with_a_bluepill.xml"));var m=MissionXml.Parse(xml.DocumentElement!);m.district=1;
        var p=new MissionProgress(m){phaseIndex=s.Phase,persistenceId=s.Identity,startedAt=s.Started,streetPolicy=true};
        foreach(var site in s.OpenFloorSites!)p.sites.Add(site.Resolve(Catalogue.Value)??throw new InvalidDataException("Captured exact site missing"));
        p.site=p.sites[s.OpenFloorCurrentSiteIndex!.Value];p.waypoints.AddRange(s.Waypoints);p.exits.AddRange(s.Exits);
        var npc=JsonSerializer.Deserialize<MissionNpcInteriorEnvelope>(s.NpcInterior.GetRawText(),Fields)!;
        var street=MissionStreetCheckpoint.PrepareRestore(p,s,Catalogue.Value,npc)!;
        foreach(var selection in street.Witnesses)p.streetWitnesses.Add(selection.Phase,selection);
        return p;
    }
    static NavLocation Lobby()=>new(){sector=879,region=28,polygon=15,floorY=0,indoors=true};
    static NavLocation Floor()=>new(){sector=879,region=25,polygon=26,floorY=2800,indoors=true};
    static uint Key(MissionWaypoint w)=>BitConverter.ToUInt32(Convert.FromHexString(w.doorHex));
    [Fact] public void Actual0940LobbyShowsPhysicalLobbyCarWithoutChangingInternalFloorAuthority()
    {
        Assert.Equal("20784EF7E2BAAFBF4EB38F765F9BE8D48F5B0B28CFB746475569AEED8D2AE872",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Repo,"tests/hds.Tests/Fixtures/Native218-0940-101210-phase1.json")))));
        var s=Captured();var p=Progress(s);string before=JsonSerializer.Serialize(new{s,p.waypoints,p.exits},Fields);
        var shown=MissionStreetGuidance.Project(p,Lobby());Assert.NotNull(shown);
        Assert.Equal(921699406u,Key(shown));Assert.Equal(new[]{-2800d,145,-8300},new[]{shown.x,shown.y,shown.z});
        byte[] actual=ServerPackets.BuildMissionWaypoint(shown.x,shown.y,shown.z,0,shown.doorHex);
        // Literal809e framing, independently encoded XYZ145 and raw1102 key. Old capturedRPC109 names floor1088/Y2945.
        Assert.Equal("809E000000000000E0A5C00000000000206240000000000036C0C0010000004E04F036",Convert.ToHexString(actual));
        Assert.Equal(921699392u,Key(p.waypoints.Single()));Assert.Equal(2945,p.waypoints.Single().y);
        Assert.Equal(before,JsonSerializer.Serialize(new{s,p.waypoints,p.exits},Fields));
    }
    [Fact] public void CurrentFloorStillNamesDownCarAndOnlyCommittedDepartureNamesPublicExit()
    {
        var p=Progress(Captured());Assert.Equal(921699392u,Key(MissionStreetGuidance.Project(p,Floor())));
        p.waitingForExit=true;Assert.Equal(921699406u,Key(MissionStreetGuidance.Project(p,Lobby())));
        p.CommitGeneratedFloor(p.phaseIndex,p.site,p.site.lobbyDoor);Assert.True(p.HasGeneratedDeparture);
        Assert.Equal(921698318u,Key(MissionStreetGuidance.Project(p,Lobby())));
        p.CommitGeneratedFloor(p.phaseIndex,p.site,p.site.elevatorDoor);
        Assert.Equal(921699392u,Key(MissionStreetGuidance.Project(p,Floor())));
    }
    [Fact] public void HistoricalLobbyRemainsPublicAndUnrelatedSectorCannotGainCurrentCarGuidance()
    {
        var p=Progress(Captured());p.phaseIndex=2;Assert.Equal(921698318u,Key(MissionStreetGuidance.Project(p,Lobby())));
        p.phaseIndex=1;var wrong=Lobby();wrong.sector=880;Assert.Equal(921698318u,Key(MissionStreetGuidance.Project(p,wrong)));
        p.restoreRefusal="quarantined";Assert.Null(MissionStreetGuidance.Project(p,Lobby()));
    }
    [Theory][InlineData("world")][InlineData("site")][InlineData("owner")][InlineData("phase")]
    public void ExistingCheckpointBoundaryStillRejectsForeignCurrentGuidanceAuthority(string fault)
    {
        var s=Captured();var raw=JsonNode.Parse(s.StreetAccess.GetRawText())!;var current=raw["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==1)!;
        switch(fault){case"world":current["Site"]!["District"]=2;break;case"site":current["Site"]!["Fingerprint"]=new string('0',64);break;case"owner":current["Identity"]=new string('b',32);break;case"phase":current["Phase"]=2;break;}
        s.StreetAccess=JsonSerializer.SerializeToElement(raw);Assert.Throws<MissionPlacementRestoreException>(()=>Progress(s));
    }
}
