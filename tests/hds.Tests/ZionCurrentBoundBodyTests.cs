using System.Text.Json;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class ZionCurrentBoundBodyTests
{
    const string Raw="010008AB054703A02D0ECDAB04C60100000000F20435BF00000000F304353F00000000A090F04000000000004887400000000000A4CFC004FF320002100000";
    const uint Source=0x2da00347;
    static MissionProgress.SavedState State()=>new(){Version=1,Mission=101216,Phase=0,Instance=50,Identity="a9b0c74b40cd4aafbe04f407fb155eab",Started=1794713521,
        Flags=new(){["entered"]=true},Objects=new(){new(){Id=1,Goid=1451,Spot=new(){metrId=1,sectorID=730,mxoStaticId=Source,type=BitConverter.GetBytes((ushort)1451),pos_x=67850,pos_y=745,pos_z=-16200,quat="00000000f20435bf00000000f304353f"}}}};
    static byte[] Own(byte spawn=1){var obj=new PlayerCharacter();obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=14,ushort view=16){var b=Convert.FromHexString(Raw);b[9]=spawn;BitConverter.GetBytes(view).CopyTo(b,b.Length-3);return b;}
    static byte[] Delete(ushort view)=>new byte[]{1,0,1,1,0}.Concat(BitConverter.GetBytes(view)).ToArray();
    static OwnViewLifeSnapshot Stream(params byte[][] records){var log=new OwnViewLifeLog();for(int i=0;i<records.Length;i++)log.Observe(false,records[i],i,WireFormat.FindCreations(records[i]));return log.Snapshot();}
    static ZionCurrentBoundBody.Result Observe(params byte[][] records){var state=State();return ZionCurrentBoundBody.Require(Stream(records),ZionCurrentBoundBody.Capture(state,1),state);}
    [Fact]public void Actual1451BoundHeaderAndMissionKeyQualifyAcrossPhaseCursor()
    {
        var result=Observe(Own(),Birth());Assert.Equal((ushort)16,result.View.ViewId);Assert.Equal((byte)14,result.View.SpawnId);
        Assert.Equal((ushort)1451,result.View.Goid);Assert.Equal(Source,result.StaticBinding);Assert.Equal("FF320002",Convert.ToHexString(result.View.Attributes["MissionKey"]));
        Assert.Equal(new[]{67850d,745d,-16200d},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(result.View.Attributes["Position"],8*i)));
        Assert.Equal("00000000F20435BF00000000F304353F",Convert.ToHexString(result.View.Attributes["Orientation"]));Assert.Equal(Raw,result.RawCreation);
    }
    [Fact]public void ExactOwnTeleportCounter16DoesNotMasqueradeAsFurnitureRecord()
    {
        var tp=PacketsUtils.createTeleportPacket(68000,745,-16200,730,1,1,16,0);Assert.Equal(28,tp.Length);
        Assert.Equal((ushort)16,Observe(Own(),Birth(),tp).View.ViewId);
    }
    [Fact]public void ActualCurrentStateOnlyUpdatePreservesBoundSourceQualification()
    {
        var update=BitConverter.GetBytes((ushort)16).Concat(ObjectStates.BuildStateUpdate(1451,1234)).ToArray();
        Assert.Equal((ushort)16,Observe(Own(),Birth(),update).View.ViewId);
    }
    [Fact]public void DeletionReplacementAndRetiredSpawnNeverFallBack()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Delete(16)));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Delete(16),Birth()));
        var wrong=Birth(15);wrong[5]++;Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),wrong));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),wrong,Birth()));
        Assert.Equal((byte)15,Observe(Own(),Birth(),Delete(16).Concat(Birth(15)).ToArray()).View.SpawnId);
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Birth(15).Concat(Delete(16)).ToArray()));
    }
    [Fact]public void TwoLiveBindingsAreAmbiguousEvenWhenOnlyOneLooksConvenient()
    {Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Birth(15,17)));}
    [Theory][InlineData("binding")][InlineData("position")][InlineData("quaternion")][InlineData("key")]
    public void LatestCreationWrongFieldCannotAdoptOldMatchingBody(string field)
    {
        byte[] wrong=Birth(15);var eventRow=WireFormat.FindCreationEvents(wrong).Single();
        var attributes=eventRow.Creation.Attributes;
        if(field=="binding")wrong[5]++;
        else{byte[] needle=attributes[field=="key"?"MissionKey":field=="position"?"Position":"Orientation"];int at=BytePattern.IndexOf(wrong,needle,12);Assert.True(at>=12);wrong[at]++;}
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),wrong));
    }
    [Fact]public void UnsupportedOrMovingNativePositionCannotUseOriginalPose()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),new byte[]{16,0,1,0xff}));
        byte[] moving=new byte[]{16,0,2,0x0e,0,0}.Concat(new[]{67850f,745f,-16200f}.SelectMany(BitConverter.GetBytes)).ToArray();
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),moving));
    }
    [Fact]public void FreeHeaderWithMatchingPoseAndKeyDoesNotProveNativeBinding()
    {byte[] native=Birth();var free=native.Take(2).Concat(new byte[]{0x0c}).Concat(native.Skip(3).Take(2)).Concat(native.Skip(9)).ToArray();Assert.Throws<InvalidDataException>(()=>Observe(Own(),free));}
    [Fact]public void ResetOldOwnLifeAndOwnReplacementRequireFreshBody()
    {
        var state=State();var scope=ZionCurrentBoundBody.Capture(state,1);var log=new OwnViewLifeLog();
        var own=Own();var birth=Birth();log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,WireFormat.FindCreations(birth));var old=log.Snapshot();
        Assert.Equal((ushort)16,ZionCurrentBoundBody.Require(old,scope,state).View.ViewId);log.Observe(true,null,2,Array.Empty<ViewCreation>());
        Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(old,scope,state));Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(log.Snapshot(),scope,state));
        own=Own(2);log.Observe(false,own,3,WireFormat.FindCreations(own));Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(log.Snapshot(),scope,state));
        birth=Birth(15);log.Observe(false,birth,4,WireFormat.FindCreations(birth));Assert.Equal((byte)15,ZionCurrentBoundBody.Require(log.Snapshot(),scope,state).View.SpawnId);
        Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(log.Snapshot() with{Events=old.Events},scope,state));
        log.EndLife();Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(log.Snapshot(),scope,state));
    }
    [Fact]public void WrongCurrentMissionPhaseInstanceIdentitySourceOrFailedScopeRefuses()
    {
        var original=State();var scope=ZionCurrentBoundBody.Capture(original,1);var stream=Stream(Own(),Birth());
        foreach(var change in new Action<MissionProgress.SavedState>[] {s=>s.Mission++,s=>s.Phase++,s=>s.Instance++,s=>s.Identity=new string('1',32),s=>s.Started++,s=>s.Objects[0].Spot.pos_x++,s=>s.Flags["failed"]=true})
        {var current=State();change(current);Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Require(stream,scope,current));}
        var free=State();free.Objects[0].Spot.type=BitConverter.GetBytes((ushort)1412);Assert.Throws<InvalidDataException>(()=>ZionCurrentBoundBody.Capture(free,1));
        Assert.Throws<InvalidDataException>(()=>Observe(Birth(),Own()));
    }
    [Fact]public void ObserverNeverMutatesInputSourceStateOrViewArrays()
    {
        var state=State();var source=JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true});var birth=Birth();var raw=(byte[])birth.Clone();
        _=ZionCurrentBoundBody.Require(Stream(Own(),birth),ZionCurrentBoundBody.Capture(state,1),state);
        Assert.Equal(raw,birth);Assert.Equal(source,JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true}));
    }
}
