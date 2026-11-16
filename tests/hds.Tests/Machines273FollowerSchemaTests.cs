using System.Collections;
using System.Reflection;
using System.Text.Json;
using HeadlessClient;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
 const string MachinesRetiredMoves="2E00020E00A800F4C9460080F743001056462F00020E00A800F4C9460080F74300105646";
 static IDictionary ReadFollowerFrames(OwnViewLifeSnapshot life,string handle)
 {
  try{return (IDictionary)typeof(ZionAfter1212FollowerReadiness).GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{life,handle})!;}
  catch(TargetInvocationException e) when(e.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
 }
 [Fact] public void Actual273Section479RetiredMovementFramesWithoutRevivingViews46And47()
 {
  var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"hds.sln")))root=root.Parent;Assert.NotNull(root);
  using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName,"tests/hds.Tests/Fixtures/Machines273-retired-follower-movement.json")));
  var views=doc.RootElement.GetProperty("Views").EnumerateArray().Select(v=>Convert.FromHexString(v.GetString()!)).ToArray();Assert.Equal(MachinesRetiredMoves,Convert.ToHexString(views[479]));
  var log=QaLife(views);var live=ReadFollowerFrames(log.Snapshot(),"MachinesAfter201206");Assert.False(live.Contains((ushort)46));Assert.False(live.Contains((ushort)47));Assert.True(live.Contains((ushort)2));
 }
 [Fact] public void FollowerHistoryFramesKnownDeletedMovesButKeepsOnlyCurrentLiveNpc()
 {
  var own=QaOwn();var handle=Assert.Single(WireFormat.FindCreations(own)).Name!;
  var log=QaLife(own,QaNpc(QaState()),QaNpc(QaState(),46,8),QaNpc(QaState(),47,9),Convert.FromHexString("01000101002E0001000101002F00"),Convert.FromHexString(MachinesRetiredMoves));
  var live=ReadFollowerFrames(log.Snapshot(),handle);Assert.True(live.Contains((ushort)7));Assert.False(live.Contains((ushort)46));Assert.False(live.Contains((ushort)47));
  var snapshot=log.Snapshot();log.EndLife();Assert.Throws<InvalidDataException>(()=>ReadFollowerFrames(snapshot,handle));
 }
 [Theory] [InlineData("unknown")] [InlineData("truncated")] [InlineData("reused")]
 public void FollowerSchemaHistoryCannotFrameUnknownTruncatedOrReusedLiveKind(string change)
 {
  var own=QaOwn();var handle=Assert.Single(WireFormat.FindCreations(own)).Name!;
  var sections=new List<byte[]>{own,QaNpc(QaState()),QaNpc(QaState(),46,8),Convert.FromHexString("01000101002E00")};
  if(change=="reused")sections.Add(QaDoor(view:46,spawn:10));
  sections.Add(change=="unknown"?Convert.FromHexString("6300020E00A800F4C9460080F74300105646"):change=="truncated"?Convert.FromHexString(MachinesRetiredMoves[..34]):Convert.FromHexString(MachinesRetiredMoves[..36]));
  Assert.Throws<InvalidDataException>(()=>ReadFollowerFrames(QaLife(sections.ToArray()).Snapshot(),handle));
 }
}
