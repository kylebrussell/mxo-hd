using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
using Xunit;
namespace hds.Tests;
public sealed class ZionAfter101217OptionalProofTests
{
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 static JsonObject Fixture()=>JsonNode.Parse(File.ReadAllText(Path.Combine(NativeBoundFurnitureFixture.FindRepository(),"tests/hds.Tests/Fixtures/ZionOptional-101302-794cc131a6.json")))!.AsObject();
 static MissionProgress.SavedState State(JsonNode n)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(n.ToJsonString(),Fields)!;
 static byte[][] Records(JsonObject f)=>f["Rpc"]!.AsArray().Select(n=>Convert.FromHexString((string)n!)).ToArray();
 [Fact] public void ActualUse3DoneThenTalkThenEscortAdvanceRetainsOnlyEarnedOptional()
 {
  var f=Fixture();var before=State(f["Before"]!);var after=State(f["After"]!);var records=Records(f);var scope=DisturbingTrendsProgressProof.Capture(before);var session=new object();var receipts=new List<ZionAfter101217OptionalProof.Receipt>();var proof=new ZionAfter101217OptionalProof(scope,3,4,receipts.Add);
  Assert.Contains(3,before.Done);Assert.Equal(4,after.Phase);Assert.Empty(after.Objects);
  Assert.True(proof.Done(3,true,before,null,0,records.Take(14).ToArray(),0,0,session));var receipt=Assert.Single(receipts);Assert.Equal(7,receipt.AckIndex);Assert.Equal(3,receipt.Phase);Assert.Equal(scope.Identity,receipt.Identity);
  Assert.False(DisturbingTrendsProgressProof.GoalDone(scope,3,4,3,true,after,null,0,records,0,0));
  Assert.True(proof.Done(3,true,after,null,0,records,0,0,session));Assert.Single(receipts);
 }
 [Theory][InlineData("identity")][InlineData("mission")][InlineData("instance")][InlineData("started")][InlineData("jump")][InlineData("oldPhase")][InlineData("failed")][InlineData("session")][InlineData("cursor")][InlineData("replacedAck")][InlineData("samePhaseLost")]
 public void EarnedProofCannotMaskOwnerSessionCursorOrPersistenceChanges(string fault)
 {
  var f=Fixture();var before=State(f["Before"]!);var scope=DisturbingTrendsProgressProof.Capture(before);var records=Records(f);var session=new object();var proof=new ZionAfter101217OptionalProof(scope,3,4);Assert.True(proof.Done(3,true,before,null,0,records,0,0,session));var n=f["After"]!.DeepClone();int cursor=0;object currentSession=session;
  switch(fault){case"identity":n["Identity"]=new string('a',32);break;case"mission":n["Mission"]=101303;break;case"instance":n["Instance"]=999;break;case"started":n["Started"]=1;break;case"jump":n["Phase"]=5;break;case"oldPhase":n["Phase"]=2;break;case"failed":n["Flags"]!["failed"]=true;break;case"session":currentSession=new object();break;case"cursor":cursor=1;break;case"replacedAck":records[7]=new byte[]{0};break;case"samePhaseLost":n=f["Before"]!.DeepClone();n["Done"]=new JsonArray(1);break;}
  Assert.Throws<InvalidDataException>(()=>proof.Done(3,true,State(n),null,0,records,cursor,0,currentSession));
 }
 [Fact] public void AdvancedEverDoneAndRepeatedNumericAckWithoutPriorProofRefuse()
 {var f=Fixture();var before=State(f["Before"]!);var proof=new ZionAfter101217OptionalProof(DisturbingTrendsProgressProof.Capture(before),3,4);Assert.Throws<InvalidDataException>(()=>proof.Done(3,true,State(f["After"]!),null,0,Records(f),0,0,new object()));}
 [Fact] public void OldAckBeforeCurrentCursorCannotCreateProof()
 {var f=Fixture();var before=State(f["Before"]!);var proof=new ZionAfter101217OptionalProof(DisturbingTrendsProgressProof.Capture(before),3,4);var records=Records(f);var session=new object();Assert.False(proof.Done(3,true,before,null,0,records,8,8,session));Assert.Throws<InvalidDataException>(()=>proof.Done(3,true,State(f["After"]!),null,0,records,8,8,session));}
 [Fact] public void CachedOptionalCannotAuthorizeTerminalCompletionOrReward()
 {var f=Fixture();var before=State(f["Before"]!);var proof=new ZionAfter101217OptionalProof(DisturbingTrendsProgressProof.Capture(before),3,4);var records=Records(f);var session=new object();Assert.True(proof.Done(3,true,before,null,0,records,0,0,session));Assert.Throws<InvalidDataException>(()=>proof.Done(3,true,null,before,1,records,0,0,session));}
}
