using System.Text.Json;
using hds;
using HeadlessClient;
namespace hds.Tests;
public class MissionOutsideContinuationTests
{
    static MissionProgress.SavedState State()=>new MissionProgress.SavedState {Version=2,Mission=301203,Identity="owned",Started=1,Instance=2,Phase=1,Done=new[]{1},EverDone=new[]{1},Items=new(){{6783,6}},Following=new ushort[]{6},StreetAccess=JsonSerializer.SerializeToElement(new{Version=1,Context="saved"})};
    [Fact] public void NarrativeDepartureFlagsCanChangeWithoutChangingEarnedCurrentContext()
    {var a=State();var b=State();b.Flags["entered"]=false;b.Flags["waitingForExit"]=false;MissionOutsideContinuation.Require(a,b);}
    [Theory] [InlineData("owner")] [InlineData("phase")] [InlineData("items")] [InlineData("goals")] [InlineData("street")]
    public void OrdinaryDownCannotChangeEarnedOwnerOrSavedContext(string fault)
    {
        var a=State();var b=State();if(fault=="owner")b.Identity="new";if(fault=="phase")b.Phase++;
        if(fault=="items")b.Items.Clear();if(fault=="goals")b.Done=Array.Empty<int>();if(fault=="street")b.StreetAccess=default;
        Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(a,b));
    }
    [Fact] public void RepeatedOutsideUploadPermitsEarnedChangesButNeverDifferentSiteContext()
    {
        var a=State();var b=State();b.Items.Clear();b.Done=new[]{1,2};MissionOutsideContinuation.Require(a,b,false);
        b.StreetAccess=JsonSerializer.SerializeToElement(new{Version=1,Context="replacement"});Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(a,b,false));
    }
    static (OwnViewLifeLog Log,OwnViewLifeSnapshot Life) CurrentLife()
    {
        var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();
        byte[] bytes=new byte[]{1,0}.Concat(new hds.resources.gameobjects.ObjectManager().GenerateCreationPacket(own,0,1).getBytes()).Concat(BitConverter.GetBytes((ushort)2)).Concat(new byte[]{0}).ToArray();
        var log=new OwnViewLifeLog();log.Observe(false,bytes,0,WireFormat.FindCreations(bytes));return(log,log.Snapshot());
    }
    [Fact] public void OutsideTokenCannotCrossResetOrAnotherOwnLife()
    {
        var first=CurrentLife();var state=State();var token=new MissionOutsideUploadProof.Token(state,first.Life);
        MissionOutsideUploadProof.RequireCurrent(token,State(),first.Life);
        var other=CurrentLife();Assert.Throws<InvalidDataException>(()=>MissionOutsideUploadProof.RequireCurrent(token,State(),other.Life));
        first.Log.Observe(true,null,1,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>MissionOutsideUploadProof.RequireCurrent(token,State(),first.Log.Snapshot()));
    }

    [Fact] public void NoRequestOrDuplicateRequestCannotSupplyTransportAuthority()
    {
        var b=new MissionOutsideUploadProof.RequestBoundary();Assert.Throws<InvalidDataException>(b.RequireMarked);
        int captures=0;b.Mark(()=>captures++);b.RequireMarked();Assert.Throws<InvalidDataException>(()=>b.Mark(()=>captures++));Assert.Equal(1,captures);
    }
    [Fact] public void ApproachTeleportOrGroundDoorNoOpIsNotAnOrdinaryMissionTrip()
    {
        var target=new[]{3700d,95,13100};
        Assert.Throws<InvalidDataException>(()=>MissionOutsideUploadProof.RequirePhysicalTrip("[TELEPORT] Teleported to 3700,95,13100",target));
        Assert.Throws<InvalidDataException>(()=>MissionOutsideUploadProof.RequirePhysicalTrip("[MISSION] Elevator: to95",target));
        Assert.Throws<InvalidDataException>(()=>MissionOutsideUploadProof.RequirePhysicalTrip("[MISSION] Teleported to 3700,1295,13100",target));
        MissionOutsideUploadProof.RequirePhysicalTrip("[MISSION] Teleported to 3700,95,13100",target);
    }

}
