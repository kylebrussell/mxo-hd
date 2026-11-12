using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using hds;
using NativeQa;

namespace hds.Tests;

public class MissionObjectiveReplayTests
{
    private static MissionProgress Progress()
    {
        var xml = new XmlDocument();
        xml.LoadXml("""
        <mission id="2202" title="Replay" description="D"><phase>
          <objective goal="1" type="Get" text="Same label" npc="1"/>
          <objective goal="2" type="Get" text="Same label" npc="1"/>
          <objective goal="3" type="Talk" text="Locked" after="2" npc="1"/>
        </phase><phase><objective goal="4" type="Talk" text="Next phase" npc="1"/></phase></mission>
        """);
        return new MissionProgress(MissionXml.Parse(xml.DocumentElement!));
    }
    private static (byte Goal, byte State)[] Send(MissionProgress progress, bool restoring)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData(); client.playerInstance = new PlayerCharacter();
        client.playerInstance.CharacterName.setValue(Encoding.ASCII.GetBytes("ObjectiveQA\0"));
        client.messageQueue = new MessageQueue();
        MissionRuntime.SendObjectives(client, progress, restoring);
        return client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m =>
        {
            var rpc = m.content[(m.content[0] >= 128 ? 2 : 1)..];
            Assert.Equal(new byte[] {0x80,0xa0}, rpc[..2]);
            return (rpc[2], rpc[5]);
        }).ToArray();
    }
    [Fact]
    public void CompletedRowExistsBeforeTickAndIdenticalTextDoesNotMergeGoalIds()
    {
        var progress = Progress(); progress.Complete(progress.Phase.missionObjectives[0]);
        Assert.Equal(new[] {((byte)1,(byte)0),((byte)2,(byte)0),((byte)1,(byte)1)}, Send(progress,true));
        Assert.True(progress.IsDone(1)); Assert.False(progress.IsDone(2));
    }
    [Fact]
    public void OrdinaryPhaseReplayStillShowsOnlyOpenGoals()
    {
        var progress = Progress(); progress.Complete(progress.Phase.missionObjectives[0]);
        Assert.Equal(new[] {((byte)2,(byte)0)}, Send(progress,false));
    }
    [Fact]
    public void FinishedRowsRemainVisibleButLaterPhaseAndLockedRowsNeverLeak()
    {
        var progress = Progress(); progress.Complete(progress.Phase.missionObjectives[0]);
        progress.Complete(progress.Phase.missionObjectives[1]);
        Assert.Equal(new[] {((byte)1,(byte)0),((byte)2,(byte)0),((byte)3,(byte)0),((byte)1,(byte)1),((byte)2,(byte)1)}, Send(progress,true));
        progress.NextPhase(); Assert.Equal(new[] {((byte)4,(byte)0)}, Send(progress,true));
    }
    [Fact]
    public void NativeModeUsesExistingAcceptanceWithoutGmOrForcedRolls()
    {
        var mode = NativeQaModes.Find("mission-objectives-relog");
        Assert.Equal("mission-objectives-relog",mode.Name); Assert.True(mode.Plan.DevCommands);
        Assert.False(mode.Plan.GmCharacter110); Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Empty(mode.Plan.EnvironmentOverrides); Assert.Equal(new ushort[] {2,17},mode.Plan.Abilities);
        Assert.Equal(2202,mode.Plan.ExtraQaMetadata["MissionId"].GetInt32());
        Assert.False(NativeQaModes.Find("unknown-objective-replay").Plan.DevCommands);
    }
}
