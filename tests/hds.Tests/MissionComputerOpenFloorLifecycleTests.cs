using hds;
using hds.resources.gameobjects;
using System.Text.Json;
namespace hds.Tests;
public class MissionComputerOpenFloorLifecycleTests
{
    [Theory][InlineData(1407)][InlineData(1415)]
    public void FreeComputerNeverBindsOrMutatesItsSource(int source)
    {
        var spot=new StaticWorldObject{type=new[]{(byte)source,(byte)(source>>8)},mxoStaticId=0x07f005a3,pos_x=40931,pos_y=3430,pos_z=-88280,quat="00000000f204353f00000000f304353f"};
        var options=new JsonSerializerOptions{IncludeFields=true};string raw=JsonSerializer.Serialize(spot,options);
        var state=new MissionObjectState{goid=3,spot=spot};var obj=new AttributeClass20("Computer",3);
        // Even an accidentally pre-bound instance must be reset to the independent proxy role.
        obj.SetRelatedStaticObjId(spot.mxoStaticId);
        MissionStaging.ConfigureSourceBinding(obj,state);
        Assert.Equal(uint.MaxValue,obj.GetRelatedStaticObjId());
        Assert.Equal(raw,JsonSerializer.Serialize(spot,options));
        var bytes=new ObjectManager().GenerateCreationPacket(obj,0,1).getBytes();
        Assert.Equal((byte)0x0c,bytes[0]);
    }
    [Fact] public void UnrelatedSamePrototypeFurnitureBindingIsPreserved()
    {
        var obj=new AttributeClass20("Computer",3);var state=new MissionObjectState{goid=3,spot=new StaticWorldObject{type=new byte[]{3,0},mxoStaticId=123}};
        MissionStaging.ConfigureSourceBinding(obj,state);Assert.Equal(123u,obj.GetRelatedStaticObjId());
    }
    [Fact] public void SourceVisibilityAndManualMutationCapabilitiesAreAbsent()
    {
        var assembly=typeof(MissionStaging).Assembly;
        Assert.Null(assembly.GetType("hds.MissionComputerSourceVisibility"));
        foreach(string method in new[]{"BoundSource","HiddenUpdate","Dev","Cleanup","ForgetWorld"})
            Assert.Null(typeof(MissionComputer1407Qa).GetMethod(method));
        var source=typeof(MissionComputer1407Qa).GetFields(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Assert.Empty(source); // Read-only logger owns no views or source-lifecycle state.
    }
}
