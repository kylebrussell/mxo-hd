using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class MachinesStreetObjectsTests
{
    static MissionProgress.SavedState Captured()
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"tests/hds.Tests/Fixtures/MachinesStreetObjects-201203-35f87d9bca.json")))d=d.Parent;
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(d!.FullName,"tests/hds.Tests/Fixtures/MachinesStreetObjects-201203-35f87d9bca.json")),new JsonSerializerOptions{IncludeFields=true})!;
    }
    [Fact] public void ActualBoundOnlyHardTimeEntryUsesStreetAuthorityWithoutCsvFallback()
    {
        var s=Captured();Assert.Equal(201203,s.Mission);Assert.Equal(0,s.Phase);Assert.Equal(2,s.Version);
        Assert.Equal(JsonValueKind.Undefined,s.NpcInterior.ValueKind);Assert.Empty(s.ElevatorWitnesses??Array.Empty<MissionElevatorWitness>());
        string before=JsonSerializer.Serialize(s,new JsonSerializerOptions{IncludeFields=true});
        var up=ZionAfter1212RidePlan.Select(s,true,Array.Empty<string>());var down=ZionAfter1212RidePlan.Select(s,false,Array.Empty<string>());
        Assert.NotNull(up.Street);Assert.True(up.Measured);Assert.Null(up.Npc);Assert.Null(up.Witness);
        Assert.Equal(219152424u,up.Key);Assert.Equal(219152408u,down.Key);
        Assert.Equal(new[]{28500d,545,14400},up.Pose);Assert.Equal(new[]{28500d,2145,14400},down.Pose);
        Assert.Equal(new[]{28649.657413095472,495,14267.324988390537},up.Approach);
        Assert.Equal(new[]{28649.657413095472,2095,14267.324988390537},down.Approach);
        Assert.Equal((ushort)45,up.Street!.Raw.GetProperty("Context").GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetUInt16());
        Assert.Equal(before,JsonSerializer.Serialize(s,new JsonSerializerOptions{IncludeFields=true}));
    }
    [Theory] [InlineData("missing")] [InlineData("owner")] [InlineData("waypoint")]
    public void ActualCorrectedStreetEntryCannotDowngradeOrUseDifferentOwnership(string fault)
    {
        var s=Captured();if(fault=="missing")s.StreetAccess=default;
        if(fault=="owner"){var n=JsonNode.Parse(s.StreetAccess.GetRawText())!;n["Witnesses"]![0]!["Identity"]="another";s.StreetAccess=JsonSerializer.SerializeToElement(n);}
        if(fault=="waypoint")s.Waypoints[0].x+=1;
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RidePlan.Select(s,true,Array.Empty<string>()));
    }
}
