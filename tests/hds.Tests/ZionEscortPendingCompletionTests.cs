using System.Text.Json;
using System.Xml;
using HeadlessClient;
using hds;
namespace hds.Tests;

public sealed class ZionEscortPendingCompletionTests
{
    const string Raw="""
    {"Version":1,"Identity":"98e5f19efa624a58890441429e112508","Mission":101217,"Phase":2,"Instance":51,"Started":1794717142,"Done":[1,2,3],"EverDone":[1,2,3],"Following":[8,7],"Flags":{"waitingForExit":true,"entered":true,"failed":false,"goalsCompleteSent":true,"leaveNotNeeded":true},"Mobs":[{"Npc":7,"Name":"Agent Park","Health":1,"Dead":false},{"Npc":8,"Name":"Agent Brown","Health":1,"Dead":false}]}
    """;
    static MissionProgress.SavedState Current()=>JsonSerializer.Deserialize<MissionProgress.SavedState>(Raw,new JsonSerializerOptions{IncludeFields=true})!;
    static MissionProgress.SavedState Before()
    {
        var s=Current();s.Done=new[]{1,2};s.Flags["waitingForExit"]=false;s.Flags["goalsCompleteSent"]=false;s.Flags["leaveNotNeeded"]=false;return s;
    }
    static Mission Mission()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        var xml=new XmlDocument();xml.Load(Path.Combine(root,"data","missions","zion_1_2_17_bombs_and_betrayal.xml"));return MissionXml.Parse(xml.DocumentElement!);
    }
    [Fact]public void Raw349SecondStateIsOnlyPendingOutcomeAndNeverDoorPermissionOrCompletedAdvance()
    {
        var before=Before();var current=Current();var mission=Mission();
        Assert.True(ZionAfter1212EscortProof.CanApproach(before,mission,3));
        Assert.False(ZionAfter1212EscortProof.CanApproach(current,mission,3));
        Assert.True(ZionAfter1212EscortProof.CompletedPendingAdvance(before,current,mission,3));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,current,mission,3,Array.Empty<byte[]>(),0));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,current,mission,3,new[]{Convert.FromHexString("80A003060001")},0));
    }
    [Theory]
    [InlineData("mission")][InlineData("phase")][InlineData("identity")][InlineData("start")][InlineData("instance")]
    [InlineData("not-done")][InlineData("not-history")][InlineData("failed")]
    [InlineData("not-waiting")][InlineData("not-all-complete")][InlineData("leave-required")][InlineData("dead")][InlineData("not-following")]
    [InlineData("incomplete-mandatory")][InlineData("wrong-goal")][InlineData("not-exit")][InlineData("missing-prerequisite")][InlineData("already-done-before")]
    public void PendingOutcomeRejectsEveryWrongScopeIncompleteOrUnsafeState(string change)
    {
        var before=Before();var current=Current();var mission=Mission();int goal=3;
        switch(change)
        {
            case "mission":current.Mission++;break;case "phase":current.Phase++;break;case "identity":current.Identity="other";break;
            case "start":current.Started++;break;case "instance":current.Instance++;break;case "not-done":current.Done=new[]{1,2};break;
            case "not-history":current.EverDone=new[]{1,2};break;
            case "failed":current.Flags["failed"]=true;break;case "not-waiting":current.Flags["waitingForExit"]=false;break;
            case "not-all-complete":current.Flags["goalsCompleteSent"]=false;break;case "leave-required":current.Flags["leaveNotNeeded"]=false;break;
            case "dead":current.Mobs[0].Dead=true;break;case "not-following":current.Following=new ushort[]{8};break;
            case "incomplete-mandatory":current.Done=new[]{2,3};break;case "wrong-goal":goal=4;break;
            case "not-exit":mission.phases[2].missionObjectives.Single(o=>o.goal==3).escortTo="hardline";break;
            case "missing-prerequisite":before.Done=new[]{1};break;case "already-done-before":before.Done=new[]{1,2,3};break;
        }
        Assert.False(ZionAfter1212EscortProof.CompletedPendingAdvance(before,current,mission,goal));
    }
    [Fact]public void EnteredFlagClearingDoesNotTurnPendingObservationIntoDoorOrAdvanceAuthority()
    {
        var before=Before();var current=Current();var mission=Mission();
        current.Flags["entered"]=false;
        Assert.True(ZionAfter1212EscortProof.CompletedPendingAdvance(before,current,mission,3));
        Assert.False(ZionAfter1212EscortProof.CanApproach(current,mission,3));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,current,mission,3,Array.Empty<byte[]>(),0));
        Assert.False(current.Flags["entered"]);
    }
    [Fact]public void PendingCannotSatisfyStrictCompletionWithoutFreshAckAndCommittedExactNextPhase()
    {
        var before=Before();var next=Current();next.Phase=3;var mission=Mission();var ack=Convert.FromHexString("80A003060001");
        Assert.False(ZionAfter1212EscortProof.CompletedPendingAdvance(before,next,mission,3));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,next,mission,3,Array.Empty<byte[]>(),0));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,next,mission,3,new[]{ack},1));
        Assert.False(ZionAfter1212EscortProof.Advanced(before,next,mission,3,new[]{Convert.FromHexString("80A002060001")},0));
        Assert.True(ZionAfter1212EscortProof.Advanced(before,next,mission,3,new[]{ack},0));
        next.Phase=4;Assert.False(ZionAfter1212EscortProof.Advanced(before,next,mission,3,new[]{ack},0));
        next.Phase=3;next.Identity="other";Assert.False(ZionAfter1212EscortProof.Advanced(before,next,mission,3,new[]{ack},0));
    }
}
