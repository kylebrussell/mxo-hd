using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using HeadlessClient;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private static MissionProgress.SavedState NpcRideState()
    {
        var p=new MissionProgress(Definition()){phaseIndex=1,persistenceId=new string('a',32),startedAt=123};var site=Site();var c=Client(p);var provider=new Provider();
        Assert.True(MissionNpcInteriorPlanning.Prepare(c,p,2,site,provider,out _,out var pending,out var why),why);
        p.NextPhase();p.site=site;p.sites.Add(site);p.openFloorCheckpoint=true;p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=pending.Witness;
        var floor=pending.Witness.Floor;var lobby=pending.Witness.Lobby;
        p.waypoints.Add(new(){x=floor.Position[0],y=floor.Position[1],z=floor.Position[2],doorHex=Convert.ToHexString(BitConverter.GetBytes(site.elevatorDoor.mxoStaticId))});p.exits.Add(lobby.Position);
        return p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{site});
    }
    private static void ChangeNpc(MissionProgress.SavedState state,Action<JsonObject> change)
    {var node=JsonNode.Parse(state.NpcInterior.GetRawText())!.AsObject();change(node);state.NpcInterior=JsonSerializer.SerializeToElement(node);}
    [Fact] public void MervNpcRideUsesOwnedMeasuredKeysAndPosesWithoutAnyCsvLookup()
    {
        var state=NpcRideState();string raw=JsonSerializer.Serialize(state,MissionProgress.SaveJson);
        var npc=MissionNpcInteriorRide.ForSite(state)!;var entry=npc.Lobby;var exit=npc.Floor;
        Assert.Equal(state.Exits[0],entry.Position);Assert.Equal(BitConverter.ToUInt32(Convert.FromHexString(state.Waypoints[0].doorHex)),exit.Key);
        Assert.Equal(raw,JsonSerializer.Serialize(state,MissionProgress.SaveJson));
        Assert.NotEmpty(MissionNpcInteriorProof.Route(Repo,npc));Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.Route(Repo,npc,1));
    }
    [Theory][InlineData("world")][InlineData("owner")][InlineData("duplicate")][InlineData("missing")][InlineData("key")][InlineData("pose")][InlineData("site")][InlineData("future")]
    public void BadNpcAuthorityNeverFallsBackToRawCsv(string change)
    {
        var state=NpcRideState();
        if(change=="world")state.SiteDoors[0].metrId=2;
        else ChangeNpc(state,e=>{var witnesses=e["Witnesses"]!.AsArray();var w=witnesses[0]!.AsObject();switch(change)
        {
            case "owner":w["Selection"]!["Identity"]="foreign";break;
            case "duplicate":witnesses.Add(w.DeepClone());break;
            case "missing":witnesses.Clear();break;
            case "key":state.Waypoints[0].doorHex="01000000";break;
            case "pose":w["Floor"]!["Position"]![0]=1;break;
            case "site":w["Selection"]!["Site"]!["Fingerprint"]="other";break;
            case "future":w["Selection"]!["Phase"]=3;e["CurrentPhase"]=null;break;
        }});
        Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorRide.ForSite(state));
    }
    [Fact] public void HistoricalSameSiteNpcEntryDoesNotPretendToCertifyCurrentActors()
    {
        var state=NpcRideState();ChangeNpc(state,e=>{e["CurrentPhase"]=1;e["Witnesses"]![0]!["Selection"]!["Phase"]=1;});
        var plan=MissionNpcInteriorRide.ForSite(state)!;Assert.Equal(1,plan.Witness.GetProperty("Selection").GetProperty("Phase").GetInt32());
        Assert.NotNull(MissionNpcInteriorRide.ForSite(state));
        var other=NpcRideState();ChangeNpc(other,e=>e["Witnesses"]![0]!["Floor"]!["Position"]![0]=1);
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.SameEntry(state,other));
    }
}
