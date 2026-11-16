using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
namespace hds.Tests;
public sealed partial class MissionStreetAccessTests
{
    static MissionProgress.SavedState StreetQaSaved()
    {
        var (site,c)=StreetContext();var identity=new string('a',32);var selected=MissionOpenFloorSite.From(site);
        var row=new MissionStreetSelection(1,101213,identity,123,0,selected,"Npc",c,null);
        return new(){Version=2,Mission=101213,Identity=identity,Started=123,Phase=0,OpenFloorSites=new[]{selected},OpenFloorCurrentSiteIndex=0,SiteDoors=new[]{site.elevatorDoor},
            Waypoints=new[]{new MissionWaypoint{x=c.Floor.Position[0],y=c.Floor.Position[1],z=c.Floor.Position[2],doorHex=Convert.ToHexString(BitConverter.GetBytes(site.elevatorDoor.mxoStaticId))}},Exits=new[]{c.Lobby.Position},
            StreetAccess=JsonSerializer.SerializeToElement(new MissionStreetEnvelope(1,"StreetAccess",true,new[]{row}),MissionProgress.SaveJson)};
    }
    [Fact]public void QaReadExercisesActual209CountedPublicRouteWithoutChooser()
    {
        var saved=StreetQaSaved();SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        var plan=MissionPublicStreetProof.Read(Repo,saved);Assert.NotEmpty(plan.Outside);Assert.NotEmpty(plan.Inside);Assert.NotEqual(saved.SiteDoors[0].mxoStaticId,plan.Door.Key);
        Assert.True(NavMesh.Locate(1,plan.Outside[^1][0],plan.Outside[^1][1],plan.Outside[^1][2],out var outdoor));Assert.False(outdoor.indoors);
        Assert.True(NavMesh.Locate(1,plan.Inside[0][0],plan.Inside[0][1],plan.Inside[0][2],out var inside));Assert.True(inside.indoors);Assert.Equal(outdoor.floorY,inside.floorY);Assert.Equal(plan.Lobby,plan.Inside[^1]);
    }
    [Theory][InlineData("owner")][InlineData("duplicate")][InlineData("refused")][InlineData("world")][InlineData("pose")][InlineData("index")]
    public void QaStreetSelectionCannotFallbackAcrossScopeOrRefusal(string change)
    {
        var saved=StreetQaSaved();var json=JsonNode.Parse(saved.StreetAccess.GetRawText())!;var row=json["Witnesses"]![0]!;
        switch(change){case "owner":row["Identity"]=new string('b',32);break;case "duplicate":json["Witnesses"]!.AsArray().Add(row.DeepClone());break;case "refused":row["Context"]!["Refusal"]="UnprovedOriginalDoorlessSemantics";break;case "world":row["Site"]!["District"]=2;break;case "pose":row["Context"]!["Floor"]!["Position"]![0]=999;break;case "index":saved.OpenFloorCurrentSiteIndex=1;break;}
        saved.StreetAccess=JsonSerializer.Deserialize<JsonElement>(json.ToJsonString());Assert.Throws<InvalidDataException>(()=>MissionPublicStreetProof.Select(saved));
    }
    [Fact]public void QaOutdoorMidpointUsesOnlyTheCountedReciprocalSubsegment()
    {
        Assert.True(MissionPublicStreetProof.TrySharedSegment(new[]{0d,0,0},new[]{110d,0,0},new[]{85d,0,0},new[]{25d,0,0},out var shared));Assert.Equal(new[]{25d,0,0},shared[0]);Assert.Equal(new[]{85d,0,0},shared[1]);
        Assert.False(MissionPublicStreetProof.TrySharedSegment(new[]{0d,0,0},new[]{110d,0,0},new[]{29d,0,0},new[]{0d,0,0},out _));Assert.False(MissionPublicStreetProof.TrySharedSegment(new[]{0d,0,0},new[]{110d,0,0},new[]{85d,0,1},new[]{25d,0,1},out _));
    }
    [Fact]public void QaCloseNodeCentreStillHasItsOwnGeometricSideSample()
    {
        Assert.Equal(new[]{1d,95,0},MissionPublicStreetProof.SideMidpoint(new[]{0d,0,0},new[]{2d,95,0}));
        Assert.Throws<InvalidDataException>(()=>MissionPublicStreetProof.SideMidpoint(new[]{0d,0,0},new[]{0d,95,0}));
    }
    [Fact]public void QaArrivalReceiptMustBeTheSameCharacterAfterTheActualSend()
    {
        byte[] raw=Convert.FromHexString("0200011C000100020003000000000000000000000000000000000000");var arrival=new NpcInteriorComputerState.Teleport(1,1,0,0,1,2,3,0,0,0,Convert.ToHexString(raw));
        string packet=Convert.ToHexString(new byte[]{1,3}.Concat(raw).Concat(new byte[]{0,0}).ToArray());
        string server=$"[Server -> Client now PSS :0 CSEQ: 0 SSEQ: 4095 ]\n[Receiver |CharId: 1001|]\n{WireFormat.ToHex(Convert.FromHexString(packet))}\n";
        string client="[Client->Server now PSS :1 CSEQ: 0 SSEQ: 0 ]\n[Receiver |CharId: 1001|]\n01\n";
        Assert.Equal((ushort)4095,MissionPublicStreetWire.Ack(server+client,1001,0,arrival).TeleportSequence);
        Assert.Throws<InvalidDataException>(()=>MissionPublicStreetWire.Ack(server,1001,0,arrival));Assert.Throws<InvalidDataException>(()=>MissionPublicStreetWire.Ack(server+client,1002,0,arrival));Assert.Throws<InvalidDataException>(()=>MissionPublicStreetWire.Ack(client+server,1001,0,arrival));
    }
}
