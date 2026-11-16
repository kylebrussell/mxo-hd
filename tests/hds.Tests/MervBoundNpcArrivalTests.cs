using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class MervBoundNpcArrivalTests
{
    static string Repo{get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static MissionProgress.SavedState State()=>JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/MervNpcOnlyArrival-3328bc6d23.json")),new JsonSerializerOptions{IncludeFields=true})!;
    static OwnViewLifeSnapshot Life(){var p=new PlayerCharacter();p.UnsetRelatedStaticObjId();p.DisableAllAttributes();var bytes=new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(p,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();var log=new OwnViewLifeLog();log.Observe(false,bytes,0,WireFormat.FindCreations(bytes));return log.Snapshot();}
    [Fact]public void ActualNpcOnlyArrivalIsMeasuredButNeverRequestsABound326Sampler()
    {
        var s=State();Assert.Equal(301203,s.Mission);Assert.Equal(0,s.Phase);Assert.Empty(s.Objects);Assert.Null(s.ElevatorWitnesses);Assert.False(s.Flags["entered"]);
        Assert.Equal(MervBound326Approach.ProofScope.Measured,MervBound326Approach.Classify(s));Assert.False(MervBound326Approach.NeedsArrival(s));
        var ride=MissionNpcInteriorRide.ForSite(s)!;Assert.Equal(0x0d1003bbu,ride.Floor.Key);Assert.Equal(new[]{25700d,2095,13600},ride.Floor.Standing);
        var arrival=MervBound326Approach.Capture(s,ride.Floor.Standing!,new NavLocation{sector=209,region=70,floorY=2000},Life(),true);Assert.True(arrival.VerifiedMeasured);
    }
    [Theory][InlineData("owner")][InlineData("floor")][InlineData("duplicate")][InlineData("future")]
    public void NpcAuthorityCannotDowngradeWhenRecognizedDataIsWrong(string fault)
    {
        var s=State();var e=JsonNode.Parse(s.NpcInterior.GetRawText())!;var w=e["Witnesses"]![0]!;
        switch(fault){case "owner":w["Selection"]!["Identity"]=new string('b',32);break;case "floor":w["Floor"]!["Position"]![0]=1;break;case "duplicate":e["Witnesses"]!.AsArray().Add(w.DeepClone());break;case "future":w["Selection"]!["Phase"]=s.Phase+1;break;}
        s.NpcInterior=JsonSerializer.Deserialize<JsonElement>(e.ToJsonString());Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Classify(s));
    }
    [Fact]public void NpcCarCannotMaskMissingCurrentFreeObjectAuthority()
    {
        var s=State();s.OpenFloorPhase=s.Phase;Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Classify(s));
    }
    [Fact]public void OnlyAnActualGenuinelyBound326RequestsTheSampler()
    {
        var s=State();var raw=s.SiteDoors[0];var other=JsonSerializer.Deserialize<StaticWorldObject>(JsonSerializer.Serialize(raw,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true})!;other.type=BitConverter.GetBytes((ushort)326);s.Objects=new[]{new MissionProgress.SavedObject{Id=1,Goid=326,Spot=other}};Assert.True(MervBound326Approach.NeedsArrival(s));s.Objects[0].Goid=3;Assert.False(MervBound326Approach.NeedsArrival(s));
    }
}
