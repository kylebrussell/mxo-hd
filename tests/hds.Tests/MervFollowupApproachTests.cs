using HeadlessClient;
using hds;
namespace hds.Tests;
public class MervFollowupApproachTests
{
    [Fact] public void HistoricalWaitingLineCannotExitTheNewPhaseOrCompletedMission()
    {
        Assert.True(MervFollowupApproachFixture.OwnsWaitingPhase(new(){Mission=301203,Phase=1},301203,1));
        Assert.False(MervFollowupApproachFixture.OwnsWaitingPhase(new(){Mission=301203,Phase=2},301203,1));
        Assert.False(MervFollowupApproachFixture.OwnsWaitingPhase(new(){Mission=301204,Phase=1},301203,1));
        Assert.False(MervFollowupApproachFixture.OwnsWaitingPhase(null,301203,1));
    }
    [Fact] public void DepartureWaitsForOwnedPersistenceAndNeverExitsAnAlreadyAdvancedPhase()
    {
        var current=new MissionProgress.SavedState{Mission=301203,Phase=1};
        Assert.False(MervFollowupApproachFixture.CanDepart(current,301203,1));
        current.Flags["waitingForExit"]=true;Assert.True(MervFollowupApproachFixture.CanDepart(current,301203,1));
        Assert.False(MervFollowupApproachFixture.Transitioned(current,301203,1));
        current.Phase=2;Assert.False(MervFollowupApproachFixture.CanDepart(current,301203,1));Assert.True(MervFollowupApproachFixture.Transitioned(current,301203,1));
        current.Mission=301204;Assert.True(MervFollowupApproachFixture.Transitioned(current,301203,1));
        Assert.True(MervFollowupApproachFixture.Transitioned(null,301203,1));Assert.False(MervFollowupApproachFixture.CanDepart(null,301203,1));
    }
    [Theory][InlineData("valid")][InlineData("phase")][InlineData("mission")][InlineData("definition")][InlineData("goal")][InlineData("type")][InlineData("destination")][InlineData("done")][InlineData("prerequisite")][InlineData("not-following")][InlineData("dead")][InlineData("missing-npc")][InlineData("not-entered")][InlineData("failed")]
    public void OnlyTheExactPendingAuthoredEscortExitPermitsAPrecompletionDoor(string change)
    {
        var goal=new MissionObjective{goal=2,type=MissionGoalType.Escort,npcId=1,escortTo="exit",afterGoal=1};
        var phase=new MissionPhase();phase.missionObjectives.Add(goal);var authored=new Mission{id=301207};authored.phases.Add(phase);
        var saved=new MissionProgress.SavedState{Mission=301207,Phase=0};saved.Flags["entered"]=true;saved.Done=new[]{1};saved.Following=new ushort[]{1};saved.Mobs.Add(new(){Npc=1,Friendly=true,Dead=false});
        switch(change){case "phase":saved.Phase=1;break;case "mission":saved.Mission=301206;break;case "definition":authored.id=301206;break;case "goal":goal.goal=3;break;case "type":goal.type=MissionGoalType.Talk;break;case "destination":goal.escortTo="npc:4";break;case "done":saved.Done=new[]{1,2};break;case "prerequisite":saved.Done=Array.Empty<int>();break;case "not-following":saved.Following=Array.Empty<ushort>();break;case "dead":saved.Mobs[0].Dead=true;break;case "missing-npc":saved.Mobs.Clear();break;case "not-entered":saved.Flags["entered"]=false;break;case "failed":saved.Flags["failed"]=true;break;}
        Assert.Equal(change=="valid",MervFollowupApproachFixture.CanDepart(saved,301207,0,authored,2));
        Assert.False(MervFollowupApproachFixture.CanDepart(saved,301207,0));
        Assert.False(MervFollowupApproachFixture.CanDepart(null,301207,0,authored,2));
    }
    [Theory][InlineData("valid")][InlineData("old-ack")][InlineData("other-goal")][InlineData("identity")][InlineData("started")][InlineData("mission")][InlineData("wrong-next")][InlineData("uncommitted")]
    public void EscortExitOutcomeRequiresFreshScopedAckAndExactCommittedNextPhase(string change)
    {
        var m=new Mission{id=301207};var first=new MissionPhase();first.missionObjectives.Add(new(){goal=2,type=MissionGoalType.Escort,npcId=1,escortTo="exit",afterGoal=1});m.phases.Add(first);m.phases.Add(new());
        var before=new MissionProgress.SavedState{Mission=301207,Phase=0,Identity="scope",Started=100,Done=new[]{1}};
        var current=new MissionProgress.SavedState{Mission=301207,Phase=1,Identity="scope",Started=100,EverDone=new[]{1,2}};
        var rows=new[]{WireFormat.FromHex("80 a0 02 06 00 01")};int cursor=0;
        switch(change){case "old-ack":cursor=1;break;case "other-goal":rows=new[]{WireFormat.FromHex("80 a0 03 06 00 01")};break;case "identity":current.Identity="other";break;case "started":current.Started++;break;case "mission":current.Mission=301206;break;case "wrong-next":current.Phase=2;break;case "uncommitted":current.Phase=0;break;}
        Assert.Equal(change=="valid",MervFollowupApproachFixture.EscortExitAdvanced(before,current,m,2,rows,cursor,0));
        Assert.False(MervFollowupApproachFixture.EscortExitAdvanced(before,null,m,2,rows,cursor,1));
    }
    [Theory][InlineData("valid")][InlineData("no-summary")][InlineData("no-receipt")][InlineData("duplicate-receipt")][InlineData("old-ack")][InlineData("already-done")]
    public void TerminalEscortExitNeedsItsFreshGoalSummaryAndOneReceipt(string change)
    {
        var m=new Mission{id=301207};var phase=new MissionPhase();phase.missionObjectives.Add(new(){goal=2,type=MissionGoalType.Escort,npcId=1,escortTo="exit",afterGoal=1});m.phases.Add(phase);
        var before=new MissionProgress.SavedState{Mission=301207,Phase=0,Identity="scope",Started=100,Done=new[]{1}};
        var rows=new[]{WireFormat.FromHex("80 a0 02 06 00 01"),WireFormat.FromHex("80 a2 00")};long receipts=1;int cursor=0;
        switch(change){case "no-summary":rows=rows.Take(1).ToArray();break;case "no-receipt":receipts=0;break;case "duplicate-receipt":receipts=2;break;case "old-ack":cursor=1;break;case "already-done":before.Done=new[]{1,2};break;}
        Assert.Equal(change=="valid",MervFollowupApproachFixture.EscortExitAdvanced(before,null,m,2,rows,cursor,receipts));
    }
    [Fact] public void FinalCompletionRequiresFreshSummaryTerminalPayloadAndExactlyOneReceipt()
    {
        Assert.True(MervFollowupApproachFixture.CompletionReady(null,1,true));
        Assert.False(MervFollowupApproachFixture.CompletionReady(null,1,false));
        Assert.False(MervFollowupApproachFixture.CompletionReady(null,0,true));
        Assert.False(MervFollowupApproachFixture.CompletionReady(null,2,true));
        Assert.False(MervFollowupApproachFixture.CompletionReady(new(){Mission=301203,Phase=2},1,true));
    }
    [Fact] public void OmittedComputerCreationStateUsesItsShippedDefault()
    {
        var attrs=new Dictionary<string,byte[]>();var view=new ViewCreation(10,3,1,attrs);
        Assert.Equal(ObjectStates.DefaultState(3),MervFollowupApproachFixture.InitialState(view));
        attrs["CurrentState"]=BitConverter.GetBytes(123u);Assert.Equal(123u,MervFollowupApproachFixture.InitialState(view));
    }
    [Fact] public void FriendlyOmissionAndReusedGenerationAreHandledBeforeIdentityMatching()
    {
        var mob=new MissionProgress.SavedMob{Name="Friendly",X=1,Y=95,Z=2,Friendly=true};
        var attrs=new Dictionary<string,byte[]>{{"Position",new[]{1d,95d,2d}.SelectMany(BitConverter.GetBytes).ToArray()},{"CharacterName",System.Text.Encoding.UTF8.GetBytes("Friendly\0")},{"DontAttack",new byte[]{1}},{"MissionKey",new byte[]{1,42,0,2}}};
        var old=new ViewCreation(42,599,1,attrs);Assert.True(MervFollowupApproachFixture.NpcMatches(old,mob,42));
        var reused=new ViewCreation(42,599,2,new Dictionary<string,byte[]>());
        Assert.DoesNotContain(MervFollowupApproachFixture.CurrentGenerations(new[]{old,reused},0),v=>MervFollowupApproachFixture.NpcMatches(v,mob,42));
        Assert.False(MervFollowupApproachFixture.NpcMatches(old,mob,43));
        var hostile=new MissionProgress.SavedMob{Name="Friendly",X=1,Y=95,Z=2,Friendly=false,Hostile=false};
        attrs["IsEnemy"]=new byte[]{1};Assert.True(MervFollowupApproachFixture.NpcMatches(old,hostile,42));
        Assert.False(MervFollowupApproachFixture.NpcMatches(old,mob,42));
    }
    [Fact] public void CurrentDoorLookupRefusesHistoricalNearbyAndAmbiguousDoors()
    {
        string Row(string key,int x)=>$"1,0,{key},0,A301,0,{x},545,200,0,0";
        Assert.Equal(1u,MervFollowupApproachFixture.DoorAt(new[]{Row("01000000",100),Row("02000000",101)},new[]{100d,545,200}));
        Assert.Throws<InvalidDataException>(()=>MervFollowupApproachFixture.DoorAt(new[]{Row("01000000",101)},new[]{100d,545,200}));
        Assert.Throws<InvalidDataException>(()=>MervFollowupApproachFixture.DoorAt(new[]{Row("01000000",100),Row("02000000",100)},new[]{100d,545,200}));
    }
    [Theory] [InlineData(3,1412,true)] [InlineData(548,1410,true)] [InlineData(552,1410,true)] [InlineData(326,326,false)]
    public void AdoptedPropsCannotFallBackToUnqualifiedLegacyCoordinates(ushort concrete,ushort source,bool expected)
        =>Assert.Equal(expected,MervFollowupApproachFixture.RequiresWitness(new(){Goid=concrete,Spot=new(){type=BitConverter.GetBytes(source)}}));
    [Fact] public void ActualViewRequiresAllThreeCoordinatesAndQuaternion()
    {
        var centre=new[]{1d,2d,3d};var q="0000000000000000000000000000803F";
        var attrs=new Dictionary<string,byte[]>{{"Position",centre.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(q)}};
        Assert.True(MervFollowupApproachFixture.Matches(new(10,552,1,attrs),552,centre,q));
        attrs["Position"]=new[]{1d,99d,3d}.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.False(MervFollowupApproachFixture.Matches(new(10,552,1,attrs),552,centre,q));
        attrs["Position"]=centre.SelectMany(BitConverter.GetBytes).ToArray();attrs["Orientation"]=new byte[16];
        Assert.False(MervFollowupApproachFixture.Matches(new(10,552,1,attrs),552,centre,q));
    }
    [Fact] public void GroundEntryUsesAnActualOwnViewPositionRecordAtThePublishedRoot()
    {
        var wire=MervFollowupApproachFixture.ClientPosition(new[]{1d,95d,2d});
        Assert.Equal(Convert.FromHexString("020001080000803F0000BE42000000400000"),wire);
        Assert.True(SelfViewEntry.TryParse(wire,2,out var entry,out _));Assert.True(entry.State!.HasPosition);
        Assert.Equal((1f,95f,2f),(entry.State.X,entry.State.Y,entry.State.Z));
        Assert.Throws<ArgumentException>(()=>MervFollowupApproachFixture.ClientPosition(new[]{1d,double.NaN,2d}));
    }
    [Fact] public void CurrentLifeOwnedBodyMayPrecedePhaseCursorButForeignOrPreviousWorldCannotMatch()
    {
        var centre=new[]{1d,95d,2d};const string q="0000000000000000000000000000803F";
        var attrs=new Dictionary<string,byte[]>{{"Position",centre.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(q)},{"MissionKey",new byte[]{255,47,0,2}}};
        var body=new ViewCreation(9,548,1,attrs);var self=new ViewCreation(2,12,1,new Dictionary<string,byte[]>());
        Assert.Same(body,MervFollowupApproachFixture.CurrentBody(new[]{self,body},548,centre,q,47));
        Assert.Null(MervFollowupApproachFixture.CurrentBody(new[]{self,body},548,centre,q,48));
        Assert.Null(MervFollowupApproachFixture.CurrentBody(new[]{body,self},548,centre,q,47));
        var reused=new ViewCreation(9,548,2,new Dictionary<string,byte[]>());
        Assert.Null(MervFollowupApproachFixture.CurrentBody(new[]{self,body,reused},548,centre,q,47));
    }
}
