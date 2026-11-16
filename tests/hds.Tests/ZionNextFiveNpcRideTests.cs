using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using HeadlessClient;
namespace hds.Tests;

public class ZionNextFiveNpcRideTests
{
    static MissionProgress.SavedState Captured()
    {
        string root=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(root,"tests","hds.Tests","Fixtures","ZionNextFiveNpcRide-b510b466.json")),new JsonSerializerOptions{IncludeFields=true})!;
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void Actual101210CheckpointUsesItsNpcEntryWithoutLegacyCsv(bool enter)
    {
        var state=Captured();string before=JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true});
        Assert.Equal(101210,state.Mission);Assert.Equal(0,state.Phase);Assert.Null(state.ElevatorWitnesses);
        var noCsv=Enumerable.Range(0,1).Select<int,string>(_=>throw new InvalidOperationException("NPC authority must not consult a CSV alias"));
        var selected=MervFollowupElevatorFixture.Select(state,enter,noCsv);var npc=MissionNpcInteriorRide.ForSite(state)!;
        var door=enter?npc.Lobby:npc.Floor;
        Assert.True(selected.Measured);Assert.Null(selected.Witness);Assert.NotNull(selected.Npc);
        Assert.Equal(door.Key,selected.Key);Assert.Equal(door.Position,selected.Pose);Assert.Equal(door.Quaternion,selected.Quaternion);Assert.Equal(door.Standing,selected.Approach);
        Assert.Equal(before,JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true}));
        var destination=enter?npc.Floor:npc.Lobby;ushort region=npc.Witness.GetProperty(enter?"Floor":"Lobby").GetProperty("Node").GetProperty("Region").GetUInt16();
        MervFollowupElevatorFixture.RequireRoot(selected,enter,destination.Standing!,checked((ushort)(destination.Key>>20)),region,destination.Standing![1]-95,Array.Empty<double[]>());
        double[] wrong=(double[])destination.Standing!.Clone();wrong[0]++;
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.RequireRoot(selected,enter,wrong,checked((ushort)(destination.Key>>20)),region,wrong[1]-95,Array.Empty<double[]>()));
    }
    [Theory][InlineData("owner")][InlineData("missing")][InlineData("future")][InlineData("pose")][InlineData("world")]
    public void InvalidNpcAuthorityCannotFallBackToRawDoor(string change)
    {
        var state=Captured();var envelope=JsonNode.Parse(state.NpcInterior.GetRawText())!;var witnesses=envelope["Witnesses"]!.AsArray();
        if(change=="owner")witnesses[0]!["Selection"]!["Identity"]="foreign";
        if(change=="missing")witnesses.Clear();
        if(change=="future")witnesses[0]!["Selection"]!["Phase"]=99;
        if(change=="pose")witnesses[0]!["Lobby"]!["Position"]![0]=1;
        if(change=="world")state.SiteDoors[0].metrId=2;
        state.NpcInterior=JsonSerializer.SerializeToElement(envelope);
        Assert.Throws<InvalidDataException>(()=>MervFollowupElevatorFixture.Select(state,true,Array.Empty<string>()));
    }
    [Fact]public void SameEntryIncludesTheActualNpcWitness()
    {
        var before=Captured();var after=Captured();Assert.True(MervFollowupElevatorFixture.SameEntry(before,after));
        var envelope=JsonNode.Parse(after.NpcInterior.GetRawText())!;
        envelope["Witnesses"]![0]!["Lobby"]!["Standing"]![0]=53501;
        after.NpcInterior=JsonSerializer.SerializeToElement(envelope);
        Assert.False(MervFollowupElevatorFixture.SameEntry(before,after));
    }
}
