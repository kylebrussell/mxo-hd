using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class ZionCarryRelogFixtureTests
{
    static (OwnViewLifeLog Log,OwnViewLifeSnapshot Life) CurrentLife()
    {
        var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();
        byte[] bytes=new byte[]{1,0}.Concat(new hds.resources.gameobjects.ObjectManager().GenerateCreationPacket(own,0,1).getBytes()).Concat(BitConverter.GetBytes((ushort)2)).Concat(new byte[]{0}).ToArray();
        var log=new OwnViewLifeLog();log.Observe(false,bytes,0,WireFormat.FindCreations(bytes));return(log,log.Snapshot());
    }
    static MissionProgress.SavedState State()=>new(){Version=2,Mission=101210,Identity="11111111111111111111111111111111",Started=123,Instance=7,Phase=1,Following=new ushort[]{1},Mobs=new(){new(){Npc=1,Identity="22222222222222222222222222222222",Name="Maynard",Friendly=true,Health=100}}};
    [Fact] public void V2CarryAcceptsFreshNewOwnerLifeWithoutNormalizingItsVersion()
    {var old=CurrentLife();var current=CurrentLife();old.Log.Observe(true,null,1,Array.Empty<ViewCreation>());var before=State();var after=State();ZionCarryRelogFixture.Require(before,after,current.Life);Assert.Equal(2,after.Version);}
    [Theory] [InlineData("owner")][InlineData("instance")][InlineData("uuid")][InlineData("version")][InlineData("following")][InlineData("dead")]
    public void RestoredFollowerMustRetainExactAcceptedIdentity(string fault)
    {var before=State();var after=State();if(fault=="owner")after.Identity="other";if(fault=="instance")after.Instance++;if(fault=="uuid")after.Mobs[0].Identity="33333333333333333333333333333333";if(fault=="version")after.Version=1;if(fault=="following")after.Following=Array.Empty<ushort>();if(fault=="dead")after.Mobs[0].Dead=true;Assert.Throws<InvalidDataException>(()=>ZionCarryRelogFixture.Require(before,after,CurrentLife().Life));}
    [Fact] public void ResetOldOwnLifeCannotQualifyRestoredCarry()
    {var old=CurrentLife();old.Log.Observe(true,null,1,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>ZionCarryRelogFixture.Require(State(),State(),old.Log.Snapshot()));}
}
