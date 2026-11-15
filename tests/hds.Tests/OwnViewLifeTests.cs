using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public class OwnViewLifeTests
{
    static byte[] Own(byte spawn=1)
    {var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(own,0,spawn).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static void Observe(OwnViewLifeLog log,byte[] bytes,int section)=>log.Observe(false,bytes,section,WireFormat.FindCreations(bytes));
    [Fact] public void ResetInvalidatesRetainedSnapshotBeforeNewOwnBirth()
    {
        var log=new OwnViewLifeLog();Observe(log,Own(),0);var old=log.Snapshot();Assert.True(old.Current);Assert.True(old.OwnViewReady);
        log.Observe(true,null,1,Array.Empty<ViewCreation>());Assert.False(old.Current);Assert.False(log.Snapshot().OwnViewReady);
        var reset=log.Snapshot();log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Equal(reset.Life,log.Snapshot().Life);
        Observe(log,Own(2),3);Assert.True(log.Snapshot().Current);Assert.True(log.Snapshot().OwnViewReady);Assert.Equal(3,log.Snapshot().OwnBirthSection);
    }
    [Fact] public void RepeatedSameOwnBirthDoesNotInventNewLifeButChangedSpawnDoes()
    {
        var log=new OwnViewLifeLog();Observe(log,Own(),0);var first=log.Snapshot();Observe(log,Own(),1);Assert.Equal(first.Life,log.Snapshot().Life);Assert.Equal(0,log.Snapshot().OwnBirthSection);
        Observe(log,Own(2),2);Assert.False(first.Current);Assert.Equal(2,log.Snapshot().OwnBirthSection);Assert.Single(log.Snapshot().Events);
    }
    [Fact] public void ActualOwnDeleteRetiresOldLifeAndRequiresFreshBirth()
    {
        var log=new OwnViewLifeLog();Observe(log,Own(),0);var old=log.Snapshot();Observe(log,new byte[]{1,0,1,1,0,2,0},1);
        Assert.False(old.Current);Assert.False(log.Snapshot().OwnViewReady);Observe(log,Own(),2);Assert.True(log.Snapshot().OwnViewReady);Assert.Equal(2,log.Snapshot().OwnBirthSection);
    }
    [Fact] public void EndLifeCannotBeReopenedByLaterObservedBirth()
    {
        var log=new OwnViewLifeLog();Observe(log,Own(),0);var old=log.Snapshot();log.EndLife();Observe(log,Own(2),1);
        Assert.False(old.Current);Assert.False(log.Snapshot().Current);Assert.False(log.Snapshot().OwnViewReady);Assert.Empty(log.Snapshot().Events);
    }
}