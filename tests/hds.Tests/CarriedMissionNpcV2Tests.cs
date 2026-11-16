using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public class CarriedMissionNpcV2Tests
{
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static JsonObject Actual()
    {
        using var d=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Repo,"tests/hds.Tests/Fixtures/CarriedSharmaine-8cd301207.json")));
        Assert.Equal("7ED53522077F651049916B3EF5965C9EB26E34F5BF782F7A868C1F9D244DD239",d.RootElement.GetProperty("RawProofSha256").GetString());
        return JsonNode.Parse(d.RootElement.GetProperty("State").GetRawText())!.AsObject();
    }
    static byte[] Encode(GameObject obj,ushort view,byte spawn)=>new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    static OwnViewLifeLog Life()
    {
        var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();byte[] b=Encode(own,2,1);
        var log=new OwnViewLifeLog();log.Observe(false,b,0,WireFormat.FindCreations(b));return log;
    }
    static CarriedMissionNpcFixture.Token Capture(JsonObject state,OwnViewLifeLog log)=>CarriedMissionNpcFixture.Capture(state.ToJsonString(),301207,0,1,"Sharmaine Tarn",true,log.Snapshot());
    static byte[] Birth(bool hostile=false)
    {
        var obj=new AttributeClass3827("NPC_BASE",599);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
        void Set(string name,byte[] bytes){var a=(hds.Attribute)obj.GetType().GetField(name)!.GetValue(obj)!;a.enable();a.setValue(bytes);}
        var name=(hds.Attribute)obj.GetType().GetField("CharacterName")!.GetValue(obj)!;var text=new byte[name.getSize()];Encoding.ASCII.GetBytes("Sharmaine Tarn").CopyTo(text,0);Set("CharacterName",text);
        Set("Position",new[]{65750d,2695,-10400}.SelectMany(BitConverter.GetBytes).ToArray());Set("DontAttack",new byte[]{1});Set("MissionKey",new byte[]{0,51,0,2});if(hostile)Set("IsEnemy",new byte[]{1});
        return Encode(obj,87,85);
    }
    [Fact] public void ActualV2FollowingSharmaineBindsAcceptedOwnerAndCurrentLifeWithoutRewritingCheckpoint()
    {
        var s=Actual();string before=s.ToJsonString();var log=Life();var token=Capture(s,log);
        Assert.Equal(2,token.CheckpointVersion);Assert.Equal("58e93f06e97749438f822859316dbffb",token.AcceptedIdentity);Assert.Equal(s["Started"]!.GetValue<long>(),token.AcceptedStarted);Assert.Equal(51,token.Instance);
        Assert.Equal("4dff45092f8c4b3988cc54a076c38a35",token.Identity);Assert.Equal(before,s.ToJsonString());Assert.True(token.OwnerLife!.Current);Assert.Equal(log.Snapshot().Life,token.OwnerLife.Life);
        // A unit-only later-phase projection tests identity continuity; it is not an earned phase or seeded server.
        s["Phase"]=1;var point=CarriedMissionNpcFixture.Resolve(s.ToJsonString(),token,1,true);Assert.Equal(new CarriedMissionNpcFixture.Point(65750,2695,-10400),point);
        byte[] birth=Birth();log.Observe(false,birth,1,WireFormat.FindCreations(birth));var view=CarriedMissionNpcFixture.CurrentLiveView(log.Snapshot(),token,s.ToJsonString(),1,true,1);Assert.Equal((ushort)87,view.View.ViewId);
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedView(new[]{view.View},0,token));
    }
    [Theory] [InlineData("unknown-version")] [InlineData("missing-owner")] [InlineData("bad-owner")] [InlineData("missing-started")] [InlineData("no-following")] [InlineData("dead")] [InlineData("hostile")] [InlineData("wrong-npc")] [InlineData("wrong-mission")] [InlineData("wrong-phase")] [InlineData("bad-uuid")] [InlineData("no-life")]
    public void V2CaptureRefusesUnknownMalformedForeignOrUnrecruitedData(string fault)
    {
        var s=Actual();var log=Life();var mob=s["Mobs"]![0]!;
        if(fault=="unknown-version")s["Version"]=3;if(fault=="missing-owner")s.Remove("Identity");if(fault=="bad-owner")s["Identity"]="not-a-guid";if(fault=="missing-started")s.Remove("Started");
        if(fault=="no-following")s["Following"]=new JsonArray();if(fault=="dead")mob["Dead"]=true;if(fault=="hostile")mob["Friendly"]=false;if(fault=="wrong-npc")mob["Npc"]=99;
        if(fault=="wrong-mission")s["Mission"]=301208;if(fault=="wrong-phase")s["Phase"]=1;if(fault=="bad-uuid")mob["Identity"]="invalid";
        if(fault=="no-life"){Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.Capture(s.ToJsonString(),301207,0,1,"Sharmaine Tarn",true));return;}
        Assert.Throws<InvalidDataException>(()=>Capture(s,log));
    }
    [Theory] [InlineData("acceptance")] [InlineData("started")] [InlineData("instance")] [InlineData("version")] [InlineData("uuid")] [InlineData("reset")] [InlineData("other-life")] [InlineData("dead")] [InlineData("side")]
    public void V2ContinuationCannotBorrowAnotherAcceptanceActorOrOwnLife(string fault)
    {
        var s=Actual();var log=Life();var token=Capture(s,log);s["Phase"]=1;
        if(fault=="acceptance")s["Identity"]=new string('b',32);if(fault=="started")s["Started"]=s["Started"]!.GetValue<long>()+1;if(fault=="instance")s["Instance"]=52;if(fault=="version")s["Version"]=1;if(fault=="uuid")s["Mobs"]![0]!["Identity"]=new string('c',32);
        if(fault=="dead")s["Mobs"]![0]!["Dead"]=true;if(fault=="side")s["Mobs"]![0]!["Friendly"]=false;
        if(fault=="reset")log.Observe(true,null,1,Array.Empty<ViewCreation>());
        if(fault=="other-life"){var other=Life();byte[] birth=Birth();other.Observe(false,birth,1,WireFormat.FindCreations(birth));Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.CurrentLiveView(other.Snapshot(),token,s.ToJsonString(),1,true,1));return;}
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.Resolve(s.ToJsonString(),token,1,true));
    }
    [Fact] public void V1TokenRetainsLegacySerializedShape()
    {
        var s=Actual();s["Version"]=1;var token=CarriedMissionNpcFixture.Capture(s.ToJsonString(),301207,0,1,"Sharmaine Tarn",true);
        using var d=JsonDocument.Parse(JsonSerializer.Serialize(token));Assert.Null(token.CheckpointVersion);Assert.False(d.RootElement.TryGetProperty("AcceptedIdentity",out _));Assert.False(d.RootElement.TryGetProperty("CheckpointVersion",out _));
        s["Phase"]=1;Assert.NotNull(CarriedMissionNpcFixture.Resolve(s.ToJsonString(),token,1,true));
    }
}
