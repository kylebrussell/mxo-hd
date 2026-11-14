using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using hds;
using NativeQa;

namespace hds.Tests;
public class MissionObjectiveUiReplayTests
{
    private static (WorldClient Client,MissionProgress Progress) Fixture()
    {
        var xml=new XmlDocument();
        xml.LoadXml("<mission id='2202' title='Replay' description='D'><phase><objective goal='1' type='Get' text='Same' npc='1'/><objective goal='2' type='Get' text='Same' npc='1'/><objective goal='3' type='Talk' text='Locked' after='2' npc='1'/></phase><phase><objective goal='4' type='Talk' text='Next' npc='1'/></phase></mission>");
        var progress=new MissionProgress(MissionXml.Parse(xml.DocumentElement!));
        progress.Complete(progress.Phase.missionObjectives[0]);
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.CharacterName.setValue(Encoding.ASCII.GetBytes("UiReplayQA\0"));
        c.playerData.missionProgress=progress;c.playerData.setDistrictId(progress.mission.district);c.playerData.setOnWorld(true);
        MissionObjectiveReplay.Arm(c,progress);return(c,progress);
    }
    private static (byte Goal,byte State)[] Drain(WorldClient c)
    {
        var rows=c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>
        {
            var b=m.content[(m.content[0]>=128?2:1)..];Assert.Equal(new byte[]{0x80,0xa0},b[..2]);return(b[2],b[5]);
        }).ToArray();c.messageQueue.RPCMessagesQueue.Clear();return rows;
    }
    [Fact]
    public void RealLoadingMarkerCreatesRowsThenSeparateCompletionWithoutChangingProgress()
    {
        var(c,p)=Fixture();var complete=MissionObjectiveReplay.Begin(c,new byte[]{1});Assert.NotNull(complete);
        Assert.Equal(new[]{((byte)1,(byte)0),((byte)2,(byte)0)},Drain(c));
        Assert.True(complete!());Assert.Equal(new[]{((byte)1,(byte)1)},Drain(c));
        Assert.True(p.IsDone(1));Assert.False(p.IsDone(2));Assert.Equal(0,p.phaseIndex);
        Assert.Null(MissionObjectiveReplay.Begin(c,new byte[]{1}));Assert.Empty(Drain(c));
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(255)]
    public void WrongStatusDoesNotConsumePendingReplay(byte status)
    {
        var(c,_)=Fixture();Assert.Null(MissionObjectiveReplay.Begin(c,new[]{status}));Assert.Empty(Drain(c));
        Assert.NotNull(MissionObjectiveReplay.Begin(c,new byte[]{1}));
    }
    [Fact]
    public void TruncatedOrExtendedMarkerAndOrdinaryLoadingHaveNoEffect()
    {
        var(c,_)=Fixture();Assert.Null(MissionObjectiveReplay.Begin(c,Array.Empty<byte>()));
        Assert.Null(MissionObjectiveReplay.Begin(c,new byte[]{1,0}));Assert.Empty(Drain(c));
        c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();
        Assert.Null(MissionObjectiveReplay.Begin(c,new byte[]{1}));
    }
    [Theory]
    [InlineData("abort")] [InlineData("phase")] [InlineData("world")] [InlineData("death")]
    [InlineData("pending")] [InlineData("district")] [InlineData("offworld")]
    public void LateCompletionCannotWriteIntoAChangedMissionOrWorld(string change)
    {
        var(c,p)=Fixture();var complete=MissionObjectiveReplay.Begin(c,new byte[]{1});Assert.NotNull(complete);Drain(c);
        switch(change)
        {
            case "abort":c.playerData.missionProgress=null;break;
            case "phase":p.NextPhase();break;
            case "world":c.playerData.worldGeneration++;break;
            case "death":c.playerData.lifeGeneration++;c.playerData.isDead=true;break;
            case "pending":c.playerData.worldChangePending=true;break;
            case "district":c.playerData.setDistrictId((uint)p.mission.district+1);break;
            case "offworld":c.playerData.setOnWorld(false);break;
        }
        Assert.False(complete!());Assert.Empty(Drain(c));
    }
    [Fact]
    public void ANewRestoreCancelsThePreviousPendingCompletion()
    {
        var(c,p)=Fixture();var old=MissionObjectiveReplay.Begin(c,new byte[]{1});Drain(c);
        MissionObjectiveReplay.Arm(c,p);Assert.False(old!());Assert.Empty(Drain(c));
        Assert.NotNull(MissionObjectiveReplay.Begin(c,new byte[]{1}));
    }
    [Fact]
    public void ChangedWorldBeforeLoadingCannotCreateRows()
    {
        var(c,_)=Fixture();c.playerData.worldGeneration++;
        Assert.Null(MissionObjectiveReplay.Begin(c,new byte[]{1}));Assert.Empty(Drain(c));
    }
    [Fact]
    public void DiscoveredModeKeepsOnlyExistingMissionDevPreparation()
    {
        var m=NativeQaModes.Find("mission-objective-ui-replay");Assert.Equal("mission-objective-ui-replay",m.Name);
        Assert.True(m.Plan.DevCommands);Assert.False(m.Plan.GmCharacter110);Assert.False(m.Plan.FixedCallbackRolls);
        Assert.Empty(m.Plan.EnvironmentOverrides);Assert.Equal(new ushort[]{2,17},m.Plan.Abilities);
        Assert.Equal(2202,m.Plan.ExtraQaMetadata["MissionId"].GetInt32());
    }
}
