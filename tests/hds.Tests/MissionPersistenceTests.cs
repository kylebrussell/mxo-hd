using System.Text.Json;
using System.Xml;
using hds;

namespace hds.Tests;

public class MissionPersistenceTests
{
    private static Mission Definition()
    {
        var doc=new XmlDocument();doc.LoadXml("""
        <mission id="901" title="Persist" description="D" plot="1" exp="50" info="60">
          <npc id="1" handle="Friend" type="FRIENDLY" x="100" y="5" z="25" />
          <phase><objective goal="0" text="Talk" type="Talk" npc="1"/>
            <objective goal="1" text="Wait" type="Survive" seconds="20" after="0"/>
            <near x="1" y="2" z="3" radius="600"><say>Once</say></near>
          </phase>
          <phase><objective goal="2" text="Give" type="Give" npc="1" item="1266"/></phase>
        </mission>
        """);return MissionXml.Parse(doc.DocumentElement!);
    }
    private static MissionProgress.SavedState RoundTrip(MissionProgress p,params Mob[] mobs) =>
        JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(p.Capture(23,mobs),MissionProgress.SaveJson),MissionProgress.SaveJson)!;
    [Fact]
    public void RestoresCompletedGoalsTriggerIdentityItemsAndIndependentObjectState()
    {
        var mission=Definition();var p=new MissionProgress(mission);
        p.Complete(p.Phase.missionObjectives[0]);Assert.True(p.Fire(p.Phase.triggers[0]));
        p.itemSlots[1266]=7;p.following.Add(1);p.killed.Add(2);p.waitingForExit=true;p.entered=true;p.entryItemExchanged=true;
        var saved=RoundTrip(p);var restored=MissionProgress.Restore(mission,saved);
        Assert.True(restored.IsDone(0));Assert.True(restored.WasDoneInAnyPhase(0));Assert.False(restored.Fire(restored.Phase.triggers[0]));
        Assert.Equal((ushort)7,restored.itemSlots[1266]);Assert.Contains((ushort)1,restored.following);Assert.Contains((ushort)2,restored.killed);
        Assert.True(restored.waitingForExit);Assert.True(restored.entered);Assert.True(restored.entryItemExchanged);
        restored.itemSlots.Clear();Assert.Single(p.itemSlots);
    }
    [Fact]
    public void RestoresCurrentPhaseWithoutReplayingEarlierGoals()
    {
        var mission=Definition();var p=new MissionProgress(mission);p.Complete(p.Phase.missionObjectives[0]);p.NextPhase();
        var restored=MissionProgress.Restore(mission,RoundTrip(p));
        Assert.Equal(1,restored.phaseIndex);Assert.True(restored.WasDoneInAnyPhase(0));Assert.False(restored.IsDone(0));Assert.Equal(2,Assert.Single(restored.OpenObjectives()).goal);
    }
    [Fact]
    public void RestoresElapsedTimersAgainstNewProcessClockAndPausesOfflineTime()
    {
        var mission=Definition();double time=10;var p=new MissionProgress(mission){clock=()=>time};
        p.Complete(p.Phase.missionObjectives[0]);time=17;
        var saved=RoundTrip(p);double newTime=1000;var restored=MissionProgress.Restore(mission,saved,()=>newTime);
        Assert.NotEmpty(saved.GoalElapsed);
        Assert.Equal(saved.GoalElapsed,restored.Capture(23,Array.Empty<Mob>()).GoalElapsed);
        newTime+=5;Assert.All(restored.Capture(23,Array.Empty<Mob>()).GoalElapsed,x=>Assert.Equal(saved.GoalElapsed[x.Key]+5,x.Value));
    }
    [Fact]
    public void CapturesDeadDamagedAndWaveNpcsWithoutEntityIds()
    {
        var p=new MissionProgress(Definition());var mob=new Mob();mob.setName("Wave");mob.setRsiHex("01020304");mob.setLevel(9);mob.setHealthM(500);mob.setHealthC(123);mob.setXPos(8);mob.setYPos(9);mob.setZPos(10);mob.setWeapon(2568);mob.missionNpcId=12;
        p.hostiles.Add(mob);p.hostileWave[mob]=4;
        var record=Assert.Single(RoundTrip(p,mob).Mobs);Assert.Equal((ushort)123,record.Health);Assert.Equal(4,record.Wave);Assert.True(record.Hostile);Assert.Equal(8,record.X);
        mob.setHealthC(0);mob.setIsDead(true);Assert.True(Assert.Single(RoundTrip(p,mob).Mobs).Dead);
    }
    [Fact]
    public void EveryAcceptanceHasItsOwnDurableIdentityEvenWithSameMissionKeyAndSecond()
    {
        var mission=Definition();var a=new MissionProgress(mission);var b=new MissionProgress(mission){startedAt=a.startedAt};
        Assert.NotEqual(a.Capture(1,Array.Empty<Mob>()).Identity,b.Capture(1,Array.Empty<Mob>()).Identity);
        Assert.Equal(a.persistenceId,MissionProgress.Restore(mission,RoundTrip(a)).persistenceId);
    }
    [Fact]
    public void SavedObjectRetainsPlacementUnlockAndComputerState()
    {
        var mission=Definition();mission.phases[0].objects.Add(new MissionObjectDef{id=7,kind="Safe",locked=true});
        var p=new MissionProgress(mission);
        p.objects[7]=new MissionObjectState{def=mission.phases[0].objects[0],goid=1416,spot=new StaticWorldObject{pos_x=123,pos_y=456,pos_z=789,mxoStaticId=42},unlockedOnce=true,shownState=0x841,lootOpen=true};
        var q=MissionProgress.Restore(mission,RoundTrip(p));var o=q.objects[7];
        Assert.Equal(123,o.spot.pos_x);Assert.Equal(42u,o.spot.mxoStaticId);Assert.True(o.unlockedOnce);Assert.Equal(0x841u,o.shownState);Assert.False(o.lootOpen);
    }
    [Fact]
    public void SavedSiteLookupSkipsUnboundFloorsAndMatchesFullDoorIdentity()
    {
        var door=new StaticWorldObject{mxoStaticId=10,sectorID=20,metrId=1};
        var wrong=new MissionSite{elevatorDoor=new StaticWorldObject{mxoStaticId=10,sectorID=21,metrId=1}};
        var right=new MissionSite{elevatorDoor=door};
        Assert.Same(right,MissionProgress.FindSavedSite(new[]{new MissionSite(),wrong,right},door));
        Assert.Null(MissionProgress.FindSavedSite(new[]{right},null!));
    }
    [Theory]
    [InlineData("1","mxotest_m1",110,true,true,false,"Tester",true)]
    [InlineData("true","mxotest_m1",110,true,true,false,"Tester",false)]
    [InlineData("1","reality_hd",110,true,true,false,"Tester",false)]
    [InlineData("1","mxotest_m1",2,true,true,false,"Tester",false)]
    [InlineData("1","mxotest_m1",110,true,true,false,"TheNeo",false)]
    [InlineData("1","mxotest_m1",110,false,true,false,"Tester",false)]
    [InlineData("1","mxotest_m1",110,true,false,false,"Tester",false)]
    public void DevControlRequiresDisposableLiveUnprotectedCharacter(string dev,string db,uint character,bool live,bool world,bool dead,string handle,bool expected)
        => Assert.Equal(expected,MissionPersistence.DevAllowed(dev,db,character,live,world,dead,handle));
    [Theory]
    [InlineData(-1,1,901)] [InlineData(2,1,901)] [InlineData(0,2,901)] [InlineData(0,1,902)]
    public void RejectsInvalidOrOtherMissionCheckpoints(int phase,int version,int id)
    {
        Assert.Throws<InvalidOperationException>(()=>MissionProgress.Restore(Definition(),new MissionProgress.SavedState{Phase=phase,Version=version,Mission=id}));
    }
}
