using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MervBound326LegacyApproachTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static (MissionProgress.SavedState State,OwnViewLifeLog Log,MervBound326Approach.Arrival Arrival) Case()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Repo,"tests/hds.Tests/Fixtures/MervBound326-2de301205.json")));var f=doc.RootElement;
        var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(f.GetProperty("State").GetRawText(),Fields)!;
        var root=f.GetProperty("ActualRoot").EnumerateArray().Select(v=>v.GetDouble()).ToArray();var own=f.GetProperty("OwnBirthBase64").GetBytesFromBase64();
        SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));Assert.True(NavMesh.Locate(1,root[0],root[1],root[2],out var nav));
        var log=new OwnViewLifeLog();log.Observe(false,own,0,WireFormat.FindCreations(own));return(state,log,MervBound326Approach.Capture(state,root,nav,log.Snapshot()));
    }
    [Fact] public void Actual301205LegacyCabinetUsesCapturedArrivalWithoutInventingMeasuredAuthority()
    {
        var c=Case();Assert.Equal(301205,c.State.Mission);Assert.Equal(2,c.State.Phase);Assert.Equal(MervBound326Approach.ProofScope.Legacy,MervBound326Approach.Classify(c.State));Assert.False(c.Arrival.VerifiedMeasured);
        var obj=Assert.Single(c.State.Objects);Assert.Equal(933232854u,obj.Spot.mxoStaticId);Assert.Equal(new[]{63940d,2748,-9415},new[]{obj.Spot.pos_x,obj.Spot.pos_y,obj.Spot.pos_z});
        string exact=JsonSerializer.Serialize(c.State,Fields);var b=MervBound326Approach.Load(Repo,obj);
        Assert.Equal(new[]{63940d,2683,-9415},b.Centre);Assert.Equal(new[]{63910d,2603,-9440},b.Minimum);Assert.Equal(new[]{63970d,2763,-9390},b.Maximum);Assert.Equal(18,b.Region);
        Assert.Equal("00000000F304353F00000000F204353F",b.Quaternion);
        foreach(var d in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})Assert.False(NavMesh.Locate(1,b.Centre[0]+d.Item1,b.Centre[1],b.Centre[2]+d.Item2,out _));
        Assert.True(NavMesh.Locate(1,c.Arrival.Root[0],c.Arrival.Root[1],c.Arrival.Root[2],out var actual));Assert.Equal((ushort)291,actual.polygon);
        Assert.False(NavMesh.Project(1,c.Arrival.Root[0],c.Arrival.Root[1],c.Arrival.Root[2],out _,0,c.Arrival.Root,false)); // Raw arrival is legal, but not inside NPC SpawnInset20.
        var result=MervBound326Approach.ChooseLegacy(Repo,c.State,1,c.Arrival,c.Log.Snapshot());
        Assert.Equal(new[]{64051d,2695,-9399},result.Standing);Assert.Equal((ushort)890,result.Nav.Sector);Assert.Equal((ushort)18,result.Nav.Region);Assert.Equal((ushort)364,result.Nav.Polygon);Assert.Equal(2600,result.Nav.FloorY);
        Assert.Equal(6560.9976f,result.NativeDistanceSquared);Assert.True(ContainerActions.InReach(result.Standing.SelectMany(BitConverter.GetBytes).ToArray(),b.Centre[0],b.Centre[1],b.Centre[2]));
        Assert.False(result.DirectRouteProven);Assert.False(result.CollisionProven);Assert.False(result.NativePickingProven);Assert.False(c.Arrival.VerifiedMeasured);Assert.Equal(exact,JsonSerializer.Serialize(c.State,Fields));
        c.State.Flags["entered"]=false;MervBound326Approach.RequireLegacyScope(c.State,1,c.Arrival,c.Log.Snapshot()); // Content-zone narrative flag is not arrival authority.
    }
    [Theory][InlineData("missing")][InlineData("reset")][InlineData("other-life")][InlineData("mission")][InlineData("phase")][InlineData("instance")][InlineData("identity")][InlineData("site")][InlineData("source-key")][InlineData("source-pose")][InlineData("kind")][InlineData("floor")][InlineData("region")][InlineData("root")][InlineData("unknown")][InlineData("missing-adopted")]
    public void LegacyApproachCannotBorrowUnknownArrivalOwnerLifeRawSourceOrMeasuredState(string fault)
    {
        var c=Case();var s=c.State;MervBound326Approach.Arrival? a=c.Arrival;var life=c.Log.Snapshot();
        switch(fault){case "missing":a=null;break;case "reset":c.Log.Observe(true,null,1,Array.Empty<ViewCreation>());break;case "other-life":life=Case().Log.Snapshot();break;case "mission":s.Mission++;break;case "phase":s.Phase++;break;case "instance":s.Instance++;break;case "identity":s.Identity=new string('b',32);break;case "site":s.SiteDoors[^1].pos_x++;break;case "source-key":s.Objects[0].Spot.mxoStaticId++;break;case "source-pose":s.Objects[0].Spot.pos_x++;break;case "kind":s.Objects[0].Goid=548;break;case "floor":a=a with{FloorY=2000};break;case "region":a=a with{Region=19};break;case "root":a.Root[0]++;break;case "unknown":s.Version=3;break;case "missing-adopted":s.OpenFloorPhase=s.Phase;break;}
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.RequireLegacyScope(s,1,a,life));
    }
}
