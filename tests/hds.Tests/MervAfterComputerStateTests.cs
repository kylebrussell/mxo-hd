using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class MervAfterComputerStateTests
{
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4,ushort view=9)
    {
        var field=typeof(GameObjectDefinitions).GetField("Object3")!;var o=(GameObject)Activator.CreateInstance(field.FieldType,field.Name,(ushort)3)!;
        o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string name,byte[] b){var a=(hds.Attribute)o.GetType().GetField(name)!.GetValue(o)!;a.enable();a.setValue(b);}
        Set("Position",new[]{100d,200d,300d}.SelectMany(BitConverter.GetBytes).ToArray());Set("Orientation",Convert.FromHexString("0000000000000000000000000000803F"));Set("MissionKey",new byte[]{255,73,0,2});
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static byte[] Update(uint state,ushort view=9)=>BitConverter.GetBytes(view).Concat(ObjectStates.BuildStateUpdate(3,state)).ToArray();
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
    [Fact]public void CapturedShotSpawnAliasCannotPoisonTheCurrentComputerState()
    {
        // c979f597ae section87 offsets25..68: target VIEW29/SPAWN27; XYZ belongs to own shooter.
        var shot=Convert.FromHexString("0200032802010c3881001d001b0000e24c4700f0344500da2c471127010080843280808080030000100021");
        Assert.Equal(43,shot.Length);Assert.Equal((ushort)29,BitConverter.ToUInt16(shot,10));
        Assert.Equal((ushort)27,BitConverter.ToUInt16(shot,12));
        // This is the old raw-gap predicate: it mistakes the embedded spawn for computer view27.
        Assert.True(BytePattern.IndexOf(shot,BitConverter.GetBytes((ushort)27),0)>=0);
        var birth=Birth(view:27);var scope=MervAfterComputerState.Capture(Stream(Own(),birth),View(birth)).Capability;
        Assert.True(MervAfterCombatFrames.TryRead(shot,0,12,out var frame));Assert.Equal((ushort)2,frame.View);
        Assert.Equal(ObjectStates.DefaultState(3),MervAfterComputerState.Read(Stream(Own(),birth,shot),scope));
        Assert.Equal(2401u,MervAfterComputerState.Read(Stream(Own(),birth,shot.Concat(Update(2401,27)).ToArray()),scope));
        Assert.Equal(ObjectStates.DefaultState(3),MervAfterComputerState.Read(Stream(Own(),birth,Birth(),Update(2401)),scope));
    }
    [Fact]public void UnknownBeforeLaterComputerBirthOrStateCannotResynchronize()
    {
        var birth=Birth();var scope=MervAfterComputerState.Capture(Stream(Own(),birth),View(birth)).Capability;
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Capture(Stream(Own(),new byte[]{99,88},birth),View(birth)));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,new byte[]{99,88},Update(2401)),scope));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Capture(Stream(Own(),Birth(5)),View(birth)));
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,Birth(5)),scope));
        var malformed=Update(2401);malformed[3]=0xff;
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,malformed),scope));
        var movement=new byte[]{9,0,2,0x0e,1,0}.Concat(new[]{100f,200f,300f}.SelectMany(BitConverter.GetBytes)).ToArray();
        Assert.Throws<InvalidDataException>(()=>MervAfterComputerState.Read(Stream(Own(),birth,movement),scope));
    }

}
