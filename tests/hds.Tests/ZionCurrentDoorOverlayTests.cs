using hds;
using HeadlessClient;
namespace hds.Tests;
public class ZionCurrentDoorOverlayTests
{
    const string Q="0000000000000000000000000000803F";
    static ZionAfter1212RidePlan Plan()=>new(1,99,new double[]{111,112,113},Q,
        new MissionElevatorWitness(1,"fp",47,new double[]{101,102,103},new double[]{111,112,113},Q,Q,"audited",Array.Empty<string>(),10,99,1));
    static byte[] Door(ushort view=5,byte spawn=2,uint key=99)
    {
        var values=new Dictionary<string,byte[]>{{"Position",new[]{111d,112d,113d}.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(Q)}};
        var definition=ObjectDefinitions.CreationAttributes(417)!;var bytes=new List<byte>{1,0,8,0xa1,1};bytes.AddRange(BitConverter.GetBytes(key));bytes.AddRange(new byte[]{spawn,0xcd,0xab,2});
        int last=Enumerable.Range(0,definition.Count).Where(i=>values.ContainsKey(definition[i].getName())).Max()/7;
        for(int group=0;group<=last;group++)
        {
            var present=Enumerable.Range(group*7,Math.Min(7,definition.Count-group*7)).Where(i=>values.ContainsKey(definition[i].getName())).ToArray();
            byte flags=(byte)(group<last?0x80:0);foreach(int i in present)flags|=(byte)(1<<(i%7));bytes.Add(flags);
            foreach(int i in present)bytes.AddRange(values[definition[i].getName()]);
        }
        bytes.AddRange(BitConverter.GetBytes(view));bytes.Add(0);return bytes.ToArray();
    }
    static OwnViewLifeLog Stream(params byte[][] sections)
    {
        var log=new OwnViewLifeLog();byte[] own=Convert.FromHexString("01000C0C0001CDAB00020000");
        log.Observe(false,own,0,WireFormat.FindCreations(own));int n=0;
        foreach(var section in sections)log.Observe(false,section,++n,WireFormat.FindCreations(section));return log;
    }
    [Fact] public void AlreadyLiveExactDoorNeedsNoSecondCreationFromRepeatedOpen()
    {var log=Stream(Door());Assert.Equal(99u,ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()).RelatedStaticObjectId);}
    [Fact] public void DeletedDoorCannotBeBorrowedFromCreationHistory()
    {var log=Stream(Door(),Convert.FromHexString("01000101000500"));Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void ReusedViewWithDifferentStaticIdentityDoesNotRetainOldBinding()
    {var log=Stream(Door(),Door(spawn:3,key:100));Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void RetiredGenerationCannotBeReintroducedAsCurrent()
    {var log=Stream(Door(),Convert.FromHexString("01000101000500"),Door());Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void DuplicateLiveBindingsAreAmbiguousEvenAtIdenticalPose()
    {var log=Stream(Door(),Door(view:6));Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void UnknownUpdateTouchingDoorRefusesItsPreviouslyKnownPose()
    {var log=Stream(Door(),Convert.FromHexString("0500FE"));Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void OldOwnerSnapshotCannotPassAfterActualLifeReset()
    {var log=Stream(Door());var old=log.Snapshot();log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(old,Plan()));}
    [Fact] public void UnknownGapBeforeLaterValidBirthNeverResynchronizes()
    {var log=Stream(new byte[]{0xfe,0xed}.Concat(Door()).ToArray());Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void UnknownGapWithoutDoorIdBytesStillRefuses()
    {var log=Stream(Door(),new byte[]{0xfe,0xed});Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}
    [Fact] public void TruncatedTypedOwnTeleportCannotBeSkippedBeforeDoorBirth()
    {var log=Stream(Convert.FromHexString("0200011C"),Door());Assert.Throws<InvalidDataException>(()=>ZionCurrentDoorOverlay.Require(log.Snapshot(),Plan()));}

}
