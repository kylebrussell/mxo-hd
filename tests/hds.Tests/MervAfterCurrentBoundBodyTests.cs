using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class MervAfterCurrentBoundBodyTests
{
    const uint Source=0x12345678;
    static MissionProgress.SavedState State()=>new(){Version=1,Mission=301210,Phase=1,Instance=73,Identity=new string('a',32),Started=100,Flags=new(){["entered"]=true},Objects=new(){new(){Id=1,Goid=326,Spot=new(){metrId=1,sectorID=209,mxoStaticId=Source,type=BitConverter.GetBytes((ushort)326),pos_x=100,pos_y=200,pos_z=300,quat="0000000000000000000000000000803f"}}}};
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4,ushort view=9,bool bound=true)
    {
        var field=typeof(GameObjectDefinitions).GetField("Object326")!;var o=(GameObject)Activator.CreateInstance(field.FieldType,field.Name,(ushort)326)!;
        if(bound)o.SetRelatedStaticObjId(Source);else o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string name,byte[] b){var a=(hds.Attribute)o.GetType().GetField(name)!.GetValue(o)!;a.enable();a.setValue(b);}
        Set("Position",new[]{100d,200d,300d}.SelectMany(BitConverter.GetBytes).ToArray());Set("Orientation",Convert.FromHexString("0000000000000000000000000000803F"));Set("MissionKey",new byte[]{255,73,0,2});
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static byte[] Delete(ushort view=9)=>new byte[]{1,0,1,1,0}.Concat(BitConverter.GetBytes(view)).ToArray();
    static OwnViewLifeSnapshot Stream(params byte[][] sections){var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();}
    static MervAfterCurrentBoundBody.Result Observe(params byte[][] sections){var s=State();return MervAfterCurrentBoundBody.Require(Stream(sections),MervAfterCurrentBoundBody.Capture(s,1),s);}
    [Fact]public void Actual326SerializerHeaderRequiresItsExactNativeBindingPoseAndInstance()
    {
        var birth=Birth();Assert.Equal((byte)8,birth[2]);var result=Observe(Own(),birth);
        Assert.Equal((ushort)326,result.View.Goid);Assert.Equal(Source,result.StaticBinding);Assert.Equal("FF490002",Convert.ToHexString(result.View.Attributes["MissionKey"]));
        Assert.Equal((byte)4,result.View.SpawnId);Assert.Equal((ushort)9,result.View.ViewId);
    }
    [Fact]public void DeletedReusedAndAmbiguousBindingsCannotAdoptHistoricalBirth()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Delete()));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Delete(),Birth()));
        Assert.Equal((byte)5,Observe(Own(),Birth(),Delete(),Birth(5)).View.SpawnId);
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Birth(5,10)));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(bound:false)));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),new byte[]{9,0,2,255}));
    }
    [Fact]public void ExactOwnTeleportAndKnownStateOnlyUpdatesDoNotReplaceNativePose()
    {
        var tp=PacketsUtils.createTeleportPacket(100,295,300,209,1,1,9,0);
        Assert.Equal((ushort)9,Observe(Own(),Birth(),tp).View.ViewId);
        var update=BitConverter.GetBytes((ushort)9).Concat(ObjectStates.BuildStateUpdate(326,123)).ToArray();
        Assert.Equal((ushort)9,Observe(Own(),Birth(),update).View.ViewId);
    }
    [Fact]public void ResetAndChangedOwnedSourceMissionScopeInvalidateTheCapability()
    {
        var state=State();var scope=MervAfterCurrentBoundBody.Capture(state,1);var log=new OwnViewLifeLog();var own=Own();var birth=Birth();
        log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,WireFormat.FindCreations(birth));var old=log.Snapshot();
        Assert.Equal(Source,MervAfterCurrentBoundBody.Require(old,scope,state).StaticBinding);
        state.Phase++;Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(old,scope,state));state.Phase--;
        state.Objects[0].Spot.pos_y++;Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(old,scope,state));state.Objects[0].Spot.pos_y--;
        log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(old,scope,state));
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(log.Snapshot(),scope,state));
    }
}
