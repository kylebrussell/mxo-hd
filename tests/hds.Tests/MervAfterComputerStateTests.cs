using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class MervAfterComputerStateTests
{
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4)
    {
        var field=typeof(GameObjectDefinitions).GetField("Object3")!;var o=(GameObject)Activator.CreateInstance(field.FieldType,field.Name,(ushort)3)!;
        o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string name,byte[] b){var a=(hds.Attribute)o.GetType().GetField(name)!.GetValue(o)!;a.enable();a.setValue(b);}
        Set("Position",new[]{100d,200d,300d}.SelectMany(BitConverter.GetBytes).ToArray());Set("Orientation",Convert.FromHexString("0000000000000000000000000000803F"));Set("MissionKey",new byte[]{255,73,0,2});
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(new byte[]{9,0,0}).ToArray();
    }
    static byte[] Update(uint state)=>BitConverter.GetBytes((ushort)9).Concat(ObjectStates.BuildStateUpdate(3,state)).ToArray();
    static byte[] Delete()=>new byte[]{1,0,1,1,0,9,0};
    static OwnViewLifeSnapshot Stream(params byte[][] sections){var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();}
    static ViewCreation View(byte[] birth)=>Assert.Single(WireFormat.FindCreations(birth));
    [Fact]public void OldEnabledStateBeforeNewBirthCannotOverrideItsShippedDefault()
    {
        var birth=Birth(5);var stream=Stream(Own(),Birth(),Update(2401),Delete(),birth);
        var observation=MervAfterComputerState.Capture(stream,View(birth));
        Assert.Equal(ObjectStates.DefaultState(3),observation.State);Assert.Equal(4,observation.Capability.Birth);
        Assert.Equal(ObjectStates.DefaultState(3),MervAfterComputerState.Read(stream,observation.Capability));
    }
    [Fact]public void OnlyFramedUpdatesAfterCurrentBirthChangeItsObservedState()
    {
        var birth=Birth();var scope=MervAfterComputerState.Capture(Stream(Own(),birth),View(birth)).Capability;
        Assert.Equal(2401u,MervAfterComputerState.Read(Stream(Own(),birth,Update(2401)),scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,new byte[]{99,88}.Concat(Update(2401)).ToArray()),scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,new byte[]{9,0,1,255}),scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,Update(2401).Take(7).ToArray()),scope));
    }
    [Fact]public void DeleteReplacementAndRetiredSpawnInvalidateTheOldStateCapability()
    {
        var birth=Birth();var scope=MervAfterComputerState.Capture(Stream(Own(),birth),View(birth)).Capability;
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,Delete()),scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,Delete(),Birth(5)),scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Capture(Stream(Own(),birth,Delete(),birth),View(birth)));
    }
    [Fact]public void ResetInvalidatesPreviouslyCapturedLifeEvenWhenTheViewNumberMatches()
    {
        var log=new OwnViewLifeLog();var own=Own();var birth=Birth();log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,WireFormat.FindCreations(birth));var old=log.Snapshot();
        var scope=MervAfterComputerState.Capture(old,View(birth)).Capability;log.Observe(true,null,2,Array.Empty<ViewCreation>());
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(old,scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(log.Snapshot(),scope));
    }
}
