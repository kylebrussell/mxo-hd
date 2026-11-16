using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using HeadlessClient;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private static MissionProgress.SavedState DtNpcRideState()
    {
        var p=new MissionProgress(Definition()){phaseIndex=1,persistenceId=new string('a',32),startedAt=123};var site=Site();var c=Client(p);
        Assert.True(MissionNpcInteriorPlanning.Prepare(c,p,2,site,new Provider(),out _,out var pending,out var why),why);
        p.NextPhase();p.site=site;p.sites.Add(site);p.openFloorCheckpoint=true;p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=pending.Witness;
        var floor=pending.Witness.Floor;var lobby=pending.Witness.Lobby;
        p.waypoints.Add(new(){x=floor.Position[0],y=floor.Position[1],z=floor.Position[2],doorHex=Convert.ToHexString(BitConverter.GetBytes(site.elevatorDoor.mxoStaticId))});p.exits.Add(lobby.Position);
        return p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{site});
    }
    [Fact] public void DisturbingTrendsUsesTheOwnedNpcLobbyWithoutRawPoseLookup()
    {
        var state=DtNpcRideState();string before=JsonSerializer.Serialize(state,MissionProgress.SaveJson);
        var plan=MissionNpcInteriorRide.ForSite(state)!;
        Assert.Equal(plan.Lobby.Key,DisturbingTrendsFixture.LobbyKey(state,Array.Empty<string>()));
        Assert.Equal(state.Exits.Single(),plan.Lobby.Position);Assert.Equal(state.Waypoints.Single().y,plan.Floor.Position[1]);
        Assert.Equal(before,JsonSerializer.Serialize(state,MissionProgress.SaveJson));
    }
    [Theory][InlineData("owner")][InlineData("missing")][InlineData("future")][InlineData("pose")]
    public void DisturbingTrendsNeverFallsBackFromInvalidDeclaredNpcAuthority(string change)
    {
        var state=DtNpcRideState();var e=JsonNode.Parse(state.NpcInterior.GetRawText())!.AsObject();var rows=e["Witnesses"]!.AsArray();var w=rows[0]!.AsObject();
        if(change=="owner")w["Selection"]!["Identity"]="foreign";
        if(change=="missing")rows.Clear();
        if(change=="future")w["Selection"]!["Phase"]=3;
        if(change=="pose")w["Lobby"]!["Position"]![0]=1;
        state.NpcInterior=JsonSerializer.SerializeToElement(e);
        Assert.Throws<InvalidDataException>(()=>DisturbingTrendsFixture.LobbyKey(state,Array.Empty<string>()));
    }
}
