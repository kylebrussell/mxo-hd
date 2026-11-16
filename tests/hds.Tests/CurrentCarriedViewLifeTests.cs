using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;

public sealed class CurrentCarriedViewLifeTests
{
    const string Identity="d43409a020a34b1f89eb33219db751b4";
    static readonly byte[] Move81=Convert.FromHexString("5100020e0198ffdd3c470080f743d9add045");
    static readonly byte[] Move87=Convert.FromHexString("5700020e0198ffdd3c470080f743d9add045");
    static string Checkpoint(int phase=1,int mission=301207,int instance=51,string identity=Identity,bool friendly=true,bool dead=false,bool giver=true) =>
        JsonSerializer.Serialize(new { Version=1,Mission=mission,Instance=instance,Phase=phase,Following=new[]{1},Mobs=new[]{new{
            Npc=1,Name="Sharmaine Tarn",Identity=identity,Friendly=friendly,Dead=dead,Giver=giver,Health=1,
            X=48349.99550902511,Y=495d,Z=6677.731041764251 }} });
    static CarriedMissionNpcFixture.Token Token()=>CarriedMissionNpcFixture.Capture(Checkpoint(phase:0),301207,0,1,"Sharmaine Tarn",true);
    static byte[] Encode(GameObject obj,ushort view,byte spawn)=>new byte[]{1,0}
        .Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    static byte[] Own(byte spawn=1)
    {
        var obj=new PlayerCharacter();obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();return Encode(obj,2,spawn);
    }
    static byte[] Birth(ushort view=81,byte spawn=79,bool friendly=true,ushort instance=51,string name="Sharmaine Tarn")
    {
        var obj=new AttributeClass3827("NPC_BASE",599);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
        void Set(string field,byte[] bytes){var a=(hds.Attribute)obj.GetType().GetField(field)!.GetValue(obj)!;a.enable();a.setValue(bytes);}
        var nameAttribute=(hds.Attribute)obj.GetType().GetField("CharacterName")!.GetValue(obj)!;
        var text=new byte[nameAttribute.getSize()];Encoding.ASCII.GetBytes(name).CopyTo(text,0);Set("CharacterName",text);
        Set("Position",new[]{64450d,295d,11700d}.SelectMany(BitConverter.GetBytes).ToArray());
        Set("DontAttack",new byte[]{1});Set("MissionKey",new byte[]{0,(byte)instance,(byte)(instance>>8),2});
        if(!friendly)Set("IsEnemy",new byte[]{1});return Encode(obj,view,spawn);
    }
    static byte[] Delete(ushort view)=>new byte[]{1,0,1,1,0}.Concat(BitConverter.GetBytes(view)).ToArray();
    static OwnViewLifeSnapshot Stream(params byte[][] sections)
    {
        var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();
    }
    static CarriedMissionNpcFixture.CurrentView Observe(OwnViewLifeSnapshot stream,int phaseCursor=2,string? checkpoint=null)=>
        CarriedMissionNpcFixture.CurrentLiveView(stream,Token(),checkpoint??Checkpoint(),1,true,phaseCursor);

    [Fact]public void Retained153SecondCaseUsesCurrentLive81AndLatestActualPosition()
    {
        var current=Observe(Stream(Own(),Birth(),Move81));Assert.Equal((ushort)81,current.View.ViewId);
        Assert.Equal((byte)79,current.View.SpawnId);Assert.True(current.Retained);Assert.Equal(2,current.PositionSection);
        Assert.Equal(48349.99609375,current.Position.X);Assert.Equal(495,current.Position.Y);Assert.Equal(6677.73095703125,current.Position.Z);
    }
    [Fact]public void DeletedThenRecreated143SecondCaseUsesOnlyCurrent87Generation()
    {
        var current=Observe(Stream(Own(),Birth(),Delete(81),Birth(87,85),Move87),3);
        Assert.Equal((ushort)87,current.View.ViewId);Assert.Equal((byte)85,current.View.SpawnId);Assert.False(current.Retained);
        Assert.Equal(3,current.CreationSection);Assert.Equal(4,current.PositionSection);
    }
    [Fact]public void DeleteWithoutRecreationAndAmbiguousLiveCopiesRefuse()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,Delete(81),Move81)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,Birth(87,85),Move87)));
    }
    [Fact]public void SamePacketBirthDeletionAndReplacementOrderIsAuthoritative()
    {
        var deletedThenBorn=Delete(81).Concat(Birth(81,80)).ToArray();
        Assert.Equal((byte)80,Observe(Stream(Own(),Birth(),Move81,deletedThenBorn,Move81)).View.SpawnId);
        var bornThenDeleted=Birth(81,80).Concat(Delete(81)).ToArray();
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,bornThenDeleted,Move81)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Birth().Concat(Own()).ToArray(),Move81)));
    }
    [Theory][InlineData("side")][InlineData("same-spawn-side")][InlineData("key")][InlineData("name")]
    public void LatestReusedViewCannotFallBackToEarlierMatchingGeneration(string change)
    {
        byte[] replacement=Birth(81,change=="same-spawn-side"?(byte)79:(byte)80,friendly:change!="side"&&change!="same-spawn-side",instance:change=="key"?(ushort)52:(ushort)51,name:change=="name"?"Other":"Sharmaine Tarn");
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,replacement,Move81)));
    }
    [Fact]public void RetiredSpawnCannotBeResurrectedByOldBirthOrReplaceNewLiveGeneration()
    {
        var original=Birth();
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),original,Move81,Delete(81),original,Move81)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),original,Move81,Birth(81,80),Move81,original,Move81)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),original,Move81,Birth(81,79,friendly:false),original,Move81)));
    }
    [Fact]public void LatestWrongUnsupportedOrNonfinitePositionCannotSearchHistory()
    {
        var wrong=(byte[])Move81.Clone();BitConverter.GetBytes(595f).CopyTo(wrong,10);
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,wrong)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,new byte[]{81,0,2,0xff})));
        var nan=(byte[])Move81.Clone();BitConverter.GetBytes(float.NaN).CopyTo(nan,6);
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,nan)));
        Assert.Equal(3,Observe(Stream(Own(),Birth(),new byte[]{81,0,2,0xff},Move81)).PositionSection);
    }
    [Fact]public void SameBirthRetransmissionNeverRewindsLatestPosition()
    {
        var birth=Birth();var current=Observe(Stream(Own(),birth,Move81,birth));Assert.Equal(2,current.PositionSection);
    }
    [Fact]public void ResetAndNewOwnGenerationRequireNewCurrentLifeBodies()
    {
        var log=new OwnViewLifeLog();void Add(byte[] b,int i)=>log.Observe(false,b,i,WireFormat.FindCreations(b));
        Add(Own(),0);Add(Birth(),1);Add(Move81,2);var old=log.Snapshot();Assert.Equal((ushort)81,Observe(old).View.ViewId);
        log.Observe(true,null,3,Array.Empty<ViewCreation>());Assert.False(log.Snapshot().OwnViewReady);
        Assert.False(old.Current);Assert.Throws<InvalidDataException>(()=>Observe(old));
        Assert.Throws<InvalidDataException>(()=>Observe(log.Snapshot()));Add(Birth(),4);
        Assert.Throws<InvalidDataException>(()=>Observe(log.Snapshot()));Add(Own(2),5);
        Assert.Throws<InvalidDataException>(()=>Observe(log.Snapshot()));Add(Birth(81,80),6);Add(Move81,7);
        var current=log.Snapshot();Assert.True(current.OwnViewReady);Assert.True(current.Life>old.Life);Assert.Equal((byte)80,Observe(current).View.SpawnId);
        Assert.Throws<InvalidDataException>(()=>Observe(current with { Events=old.Events }));
        Assert.Equal(3,old.Events.Count); // the old snapshot was never cleared or mutated
        Add(Own(3),8);Assert.Throws<InvalidDataException>(()=>Observe(log.Snapshot()));
    }
    [Fact]public void EndedOwnSessionRejectsCapturedSnapshotAndCannotAdoptLaterRecords()
    {
        var log=new OwnViewLifeLog();var sections=new[]{Own(),Birth(),Move81};
        for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));
        var captured=log.Snapshot();Assert.True(captured.Current);log.EndLife();
        Assert.False(captured.Current);Assert.Throws<InvalidDataException>(()=>Observe(captured));
        log.Observe(false,Own(2),3,WireFormat.FindCreations(Own(2)));Assert.False(log.Snapshot().OwnViewReady);
    }
    [Fact]public void OwnBirthMustActuallyExistAndOldBodiesBeforeNewOwnBirthCannotWin()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Birth(),Move81)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Birth(),Own(),Move81)));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,Delete(2))));
        Assert.Throws<InvalidDataException>(()=>Observe(Stream(Own(),Birth(),Move81,Own(2))));
    }
    [Fact]public void CurrentCheckpointAuthorityMustStillResolveCapturedUuidMissionPhaseInstanceAndSide()
    {
        var stream=Stream(Own(),Birth(),Move81);
        foreach(var bad in new[]{Checkpoint(mission:301206),Checkpoint(phase:0),Checkpoint(instance:52),Checkpoint(identity:new string('1',32)),Checkpoint(friendly:false),Checkpoint(dead:true),Checkpoint(giver:false)})
            Assert.Throws<InvalidDataException>(()=>Observe(stream,checkpoint:bad));
    }
    [Fact]public void ObservationReusesParsedCreationsAndDoesNotMutatePayloadOrLegacySnapshotParsing()
    {
        var own=Own();var birth=Birth();var before=(byte[])birth.Clone();var parsed=WireFormat.FindCreations(birth);
        var log=new OwnViewLifeLog();log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,parsed);
        Assert.Equal(before,birth);Assert.Equal(JsonSerializer.Serialize(parsed),JsonSerializer.Serialize(WireFormat.FindCreationEvents(birth).Select(e=>e.Creation)));
        Assert.Equal(0,WireFormat.FindCreationEvents(birth).Single().Offset);Assert.Equal(birth.Length,WireFormat.FindCreationEvents(birth).Single().Length);
    }
}
