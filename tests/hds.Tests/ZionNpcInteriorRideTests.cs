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
    [Fact] public void ZionNpcRideUsesOwnedMeasuredKeysAndPosesWithoutAnyCsvLookup()
    {
        var state=NpcRideState();string raw=JsonSerializer.Serialize(state,MissionProgress.SaveJson);
        var npc=ZionNpcInteriorRide.ForSite(state)!;var entry=ZionAfter1212RidePlan.Select(state,true,Array.Empty<string>());var exit=ZionAfter1212RidePlan.Select(state,false,Array.Empty<string>());
        Assert.True(entry.Measured);Assert.NotNull(entry.Npc);Assert.Null(entry.Witness);Assert.Equal(npc.Lobby.Key,entry.Key);Assert.Equal(npc.Lobby.Position,entry.Pose);
        Assert.Equal(npc.Floor.Key,exit.Key);Assert.Equal(npc.Floor.Position,exit.Pose);
        var view=new ViewCreation(9,419,7,new Dictionary<string,byte[]>{{"Position",entry.Pose.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(entry.Quaternion!)}}){RelatedStaticObjectId=entry.Key};
        entry.RequireOverlay(view);Assert.Throws<InvalidDataException>(()=>entry.RequireOverlay(view with{RelatedStaticObjectId=entry.Key+1}));
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
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(state,true,Array.Empty<string>()));
    }
    [Fact] public void HistoricalSameSiteNpcEntryDoesNotPretendToCertifyCurrentActors()
    {
        var state=NpcRideState();ChangeNpc(state,e=>{e["CurrentPhase"]=1;e["Witnesses"]![0]!["Selection"]!["Phase"]=1;});
        var plan=ZionNpcInteriorRide.ForSite(state)!;Assert.Equal(1,plan.Witness.GetProperty("Selection").GetProperty("Phase").GetInt32());
        Assert.NotNull(ZionAfter1212RidePlan.Select(state,true,Array.Empty<string>()).Npc);
        var other=NpcRideState();ChangeNpc(other,e=>e["Witnesses"]![0]!["Floor"]!["Position"]![0]=1);
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.SameEntry(state,other));
    }
}
