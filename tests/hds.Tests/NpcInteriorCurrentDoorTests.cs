using HeadlessClient;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    [Fact] public void ExactDoorResetInvalidatesOnlyItsOwnBirthAndDoesNotPoisonAnotherCurrentDoor()
    {
        // Literal retained Merv section51: old elevator6 reset to closed, followed by its delete.
        var reset=Convert.FromHexString("060001FF0000000000000000000000000003000000003508000001000101000600");
        Assert.Equal(reset.Take(26).Skip(2),DoorLifecycle.BuildResetUpdate());
        var log=QaLife(QaOwn(),QaDoor(view:6,key:0x2260056c),QaDoor(state:0x834),reset);
        Assert.Equal(0x834u,NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor()).State);
        var ownReset=BitConverter.GetBytes((ushort)9).Concat(DoorLifecycle.BuildResetUpdate()).ToArray();
        log=QaLife(QaOwn(),QaDoor(state:0x834),ownReset);
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor()));
        var malformed=(byte[])ownReset.Clone();malformed[5]^=1;
        log=QaLife(QaOwn(),QaDoor(view:6,key:0x2260056c),QaDoor(state:0x834),malformed);
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor()));
        log=QaLife(QaOwn(),QaDoor(state:0x834),ownReset[..^1]);
        Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor()));
    }
    static MissionNpcInteriorProof.Door QaExpectedDoor()=>new(0x2260056b,346,new[]{38500d,6145,25800},QaDoorQuaternion,null,"");
    static readonly double[] QaDestination={39810.81081081081,6095,25564.864864864863};
    // Literal producer contract: own2/011c,id,NAV3*u16,yaw,XYZ3*f32,simtimeu32 (PacketsUtils144).
    static byte[] QaTeleport()=>Convert.FromHexString("0200011C0426027D001E0000").Concat(QaDestination.SelectMany(x=>BitConverter.GetBytes((float)x))).Concat(new byte[4]).ToArray();
    [Fact] public void PreexistingTypedOpenDoorAndFreshSameTargetOwnTeleportQualifyWithoutNewBirth()
    {
        var log=QaLife(QaOwn(),QaDoor(state:0x834));var before=log.Snapshot();int requestCursor=2;var message=QaTeleport();log.Observe(false,message,2,WireFormat.FindCreations(message));
        var current=NpcInteriorComputerState.CurrentDoor(log.Snapshot(),QaExpectedDoor());var teleport=NpcInteriorComputerState.FreshTeleport(log.Snapshot(),before,requestCursor,QaDestination);
        Assert.Equal(1,current.Capability.Birth);Assert.True(current.Capability.Birth<requestCursor);Assert.Equal(0x834u,current.State);Assert.Equal(2,teleport.Section);Assert.Equal((byte)4,teleport.Id);Assert.Equal((ushort)550,teleport.Sector);Assert.Equal((ushort)125,teleport.Region);Assert.Equal((ushort)30,teleport.Polygon);Assert.Equal((float)QaDestination[0],teleport.X);
    }
    [Theory] [InlineData("key")] [InlineData("goid")] [InlineData("position")] [InlineData("quaternion")] [InlineData("closed")] [InlineData("deleted")] [InlineData("reused")] [InlineData("ambiguous")] [InlineData("oldLife")] [InlineData("unknown")]
    public void TypedCurrentDoorNeverFallsBackToHistoricalDeletedReusedOrWrongIdentity(string change)
    {
        var expected=QaExpectedDoor();var log=QaLife(QaOwn(),QaDoor(state:0x834));var delete=Convert.FromHexString("01000101000900");
        if(change=="key")expected=expected with{Key=expected.Key+1};
        else if(change=="goid")expected=expected with{Goid=343};
        else if(change=="position")expected=expected with{Position=new[]{38501d,6145,25800}};
        else if(change=="quaternion")expected=expected with{Quaternion="0000000000000000000000000000803F"};
        else if(change=="closed")log=QaLife(QaOwn(),QaDoor());
        else if(change=="deleted")log=QaLife(QaOwn(),QaDoor(state:0x834),delete);
        else if(change=="reused")log=QaLife(QaOwn(),QaDoor(state:0x834),delete,QaDoor(state:0x834));
        else if(change=="ambiguous")log=QaLife(QaOwn(),QaDoor(state:0x834),QaDoor(10,6,0x834));
        else if(change=="unknown")log=QaLife(QaOwn(),QaDoor(state:0x834),new byte[]{99,88},QaDoor(10,6,0x834));
        var snapshot=log.Snapshot();if(change=="oldLife")log.EndLife();Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.CurrentDoor(snapshot,expected));
    }
    [Theory] [InlineData("old")] [InlineData("target")] [InlineData("movement")] [InlineData("otherSession")] [InlineData("unknown")]
    public void ActualOwnTeleportMustBeFreshSameTargetAndSameObservationOwner(string change)
    {
        var log=QaLife(QaOwn(),QaDoor(state:0x834));var before=log.Snapshot();int cursor=2;double[] expected=QaDestination;var message=QaTeleport();
        if(change=="movement")
        {
            var npc=QaNpc(QaState());log.Observe(false,npc,2,WireFormat.FindCreations(npc));
            message=Convert.FromHexString("0700020E0198").Concat(QaDestination.SelectMany(x=>BitConverter.GetBytes((float)x))).ToArray();log.Observe(false,message,3,WireFormat.FindCreations(message));
        }
        else if(change=="unknown") {log.Observe(false,new byte[]{99,88},2,Array.Empty<ViewCreation>());log.Observe(false,message,3,WireFormat.FindCreations(message));}
        else {log.Observe(false,message,2,WireFormat.FindCreations(message));if(change=="old")cursor=3;if(change=="target")expected=new[]{QaDestination[0]+1,QaDestination[1],QaDestination[2]};}
        if(change=="otherSession")before=QaLife(QaOwn()).Snapshot();Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.FreshTeleport(log.Snapshot(),before,cursor,expected));
    }
    [Fact] public void KnownV1LobbyTypeUsesFirstRawIdentityForPersistedKeyWithoutPoseRecovery()
    {
        const string first="1,550,17286022,0,A3010000,0,40300,6145,25800,90,q",second="1,550,17286022,0,A1010000,0,39500,6145,25000,0,q";
        Assert.Equal((ushort)419,MissionNpcInteriorProof.RawGoidForKnownKey(new[]{first,second},0x22602817));
        Assert.Equal((ushort)417,MissionNpcInteriorProof.RawGoidForKnownKey(new[]{second,first},0x22602817));
        Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.RawGoidForKnownKey(new[]{first.Replace("A3010000","57010000"),second},0x22602817));Assert.Throws<InvalidDataException>(()=>MissionNpcInteriorProof.RawGoidForKnownKey(new[]{first},0x22602818));
    }
    [Theory] [InlineData(null)] [InlineData("bad")] [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void ActualCaseSourcePinIsMandatoryAndRejectsMissingMalformedAndMismatched(string? expected)=>
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.RequireSource(expected,new string('B',64)));
}
