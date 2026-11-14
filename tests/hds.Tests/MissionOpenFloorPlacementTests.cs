using System.Text.Json;
using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorPlacementTests
{
    private const string Q="0000000000000000000000000000803f";
    private static (G.Request Request,MissionObjectState State) Input()
    {
        var raw=new StaticWorldObject{metrId=1,sectorID=127,mxoStaticId=133169153,staticId=1,type=BitConverter.GetBytes((ushort)1415),pos_y=145,quat=Q};
        var rect=new[]{new G.Point(-500,-500),new G.Point(500,-500),new G.Point(500,500),new G.Point(-500,500)};
        var request=new G.Request(1,G.SourceModelPresence.Model6Absent,"literal positive absent",new(new(0,145,0),Q,raw.mxoStaticId,1415),new(new(0,0,0),Q,raw.mxoStaticId,1415),"literal source",new OpenFloorModelBounds(3,new(-10,-10,-10),new(10,10,10),"literal model"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal initial400"),new[]{new G.Surface(new(127,1,1),rect,0,0,0)},new(new(127,1,1),new(-300,95,0),"literal normal root"),new(-500,500,-500,500),new(1,true,new(-500,500,-500,500),-100,1000,Array.Empty<G.StaticObstacle>(),"literal complete domains"));
        return(request,new MissionObjectState{def=new MissionObjectDef{id=1},spot=raw,goid=3});
    }
    [Fact]
    public void UntouchedSavedObjectRetainsTheExactVersionOneJsonShape()
    {
        var saved=new MissionProgress.SavedObject{Id=1,Goid=548};
        Assert.Equal("{\"Id\":1,\"Spot\":null,\"Goid\":548,\"Unlocked\":false,\"State\":0}",JsonSerializer.Serialize(saved,MissionProgress.SaveJson));
    }
    [Fact]
    public void FixedCheckpointRoundTripsAndReplaysWithoutChangingRawSourceOrExposingArrays()
    {
        var (request,state)=Input();var result=G.Plan(request);Assert.True(result.HasPlan,result.Refusal);
        var pose=MissionOpenFloorPlacement.From(3082,0,1,state.spot,result);state.OpenFloor=pose;
        Assert.True(pose.Basic(state.spot,3,1));Assert.True(pose.Replay(request,3082,0,state,out string refusal),refusal);
        var centre=pose.Centre;centre[0]=9999;Assert.NotEqual(9999,pose.Centre[0]);Assert.Equal(145,state.spot.pos_y);
        string json=JsonSerializer.Serialize(pose,MissionProgress.SaveJson);var restored=JsonSerializer.Deserialize<MissionOpenFloorPlacement>(json,MissionProgress.SaveJson)!;
        Assert.True(restored.Replay(request,3082,0,state,out refusal),refusal);Assert.False(restored.Replay(request,3083,0,state,out _));Assert.False(restored.Replay(request,3082,1,state,out _));
        var badRoute=JsonSerializer.Deserialize<MissionOpenFloorPlacement>(json.Replace("\"RouteSurfaces\":[127,1,1]","\"RouteSurfaces\":[127,1,99]"),MissionProgress.SaveJson)!;
        Assert.False(badRoute.Replay(request,3082,0,state,out _));
        Assert.Equal(result.Centre.Y,MissionObjectPoses.For(state).Y);Assert.Equal(145,state.spot.pos_y);
        state.spot.pos_x=1;Assert.False(restored.Basic(state.spot,3,1));Assert.Throws<InvalidOperationException>(()=>MissionObjectPoses.For(state));
    }
}
