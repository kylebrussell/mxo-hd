using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class MervAfterCurrentNpcTests
{
    static MissionProgress.SavedState State(bool friendly=false)=>new(){Version=2,Mission=301209,Phase=1,Instance=73,Identity=new string('a',32),Started=100,
        Flags=new(){["entered"]=true},Mobs=new(){new(){Npc=6,Phase=0,Identity=new string('b',32),Name="Agent",Level=50,Rsi="35000024",Friendly=friendly,Hostile=false,Health=100,X=100,Y=2095,Z=300}}};
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4,ushort view=9,bool friendly=false,double x=100,bool missionKey=true,byte level=50,byte npcRank=1,bool hostileDontAttack=false)
    {
        var o=new AttributeClass3827("NPC_BASE",599);o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string name,byte[] b){var a=(hds.Attribute)o.GetType().GetField(name)!.GetValue(o)!;a.enable();a.setValue(b);}
        var attr=(hds.Attribute)o.GetType().GetField("CharacterName")!.GetValue(o)!;var name=new byte[attr.getSize()];System.Text.Encoding.ASCII.GetBytes("Agent").CopyTo(name,0);Set("CharacterName",name);
        Set("Position",new[]{x,2095d,300d}.SelectMany(BitConverter.GetBytes).ToArray());Set("Level",new[]{level});
        if(missionKey)Set("MissionKey",new byte[]{0,73,0,2});
        Set("IsFriendly",new byte[]{0});Set("TitleAbility",Convert.FromHexString("00100000"));Set("ConditionStateFlags",Convert.FromHexString("00001000"));
        Set("CombatantMode",new byte[]{0x22});Set("NPCRank",new[]{npcRank});Set("IsDead",new byte[]{0});Set("CurrentState",BitConverter.GetBytes(0x806u));Set("MoreInfoID",Convert.FromHexString("35000024"));
        if(friendly)Set("DontAttack",new byte[]{1});else{Set("IsEnemy",new byte[]{1});Set("TalkDefaultable",new byte[]{1});if(hostileDontAttack)Set("DontAttack",new byte[]{1});}
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static byte[] Move(float x=100,float y=2095)=>new byte[]{9,0,2,0x0e,1,0}.Concat(new[]{x,y,300f}.SelectMany(BitConverter.GetBytes)).ToArray();
    static byte[] Delete()=>new byte[]{1,0,1,1,0,9,0};
    static OwnViewLifeSnapshot Stream(params byte[][] rows){var log=new OwnViewLifeLog();for(int i=0;i<rows.Length;i++)log.Observe(false,rows[i],i,WireFormat.FindCreations(rows[i]));return log.Snapshot();}
    static MervAfterCurrentNpc.CurrentView Observe(MissionProgress.SavedState state,params byte[][] rows)=>MervAfterCurrentNpc.Require(Stream(rows),MervAfterCurrentNpc.Capture(state,6),state);
    [Fact]public void ApproachRefreshUsesNewEnteredCheckpointWithoutRelaxingOwnerPhaseOrActorIdentity()
    {
        var before=State();before.Flags["entered"]=false;var after=State();
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentNpc.Capture(before,6));
        var scope=MervAfterCurrentNpc.CaptureAfterApproach(before,after,6);
        Assert.Equal((ushort)9,MervAfterCurrentNpc.Require(Stream(Own(),Birth()),scope,after).View.ViewId);
        foreach(var kind in new[]{"mission","phase","instance","identity","started","actor","entered"})
        {
            after=State();switch(kind){case "mission":after.Mission++;break;case "phase":after.Phase++;break;case "instance":after.Instance++;break;
            case "identity":after.Identity=new string('c',32);break;case "started":after.Started++;break;case "actor":after.Mobs[0].Identity=new string('c',32);break;case "entered":after.Flags["entered"]=false;break;}
            Assert.Throws<InvalidDataException>(()=>MervAfterCurrentNpc.CaptureAfterApproach(before,after,6));
        }
    }    [Fact]public void CurrentStageHostilesUseMembershipOrAuthoredStageRatherThanWirePhase()
    {
        var state=new MissionProgress.SavedState{Phase=2,Mobs=new(){
            new(){Npc=1,Phase=0,Hostile=true,Health=10},new(){Npc=2,Phase=0,Hostile=false,Health=10},
            new(){Npc=3,Phase=0,Hostile=false,Health=10},new(){Npc=4,Phase=0,Hostile=true,Friendly=true,Health=10},new(){Npc=5,Phase=0,Hostile=true,Dead=true,Health=0}}};
        Assert.Equal(new ushort[]{1,2},MervAfterCurrentNpc.CurrentHostiles(state,new ushort[]{2}).Select(m=>m.Npc));
    }
    [Fact]public void ScriptedHostileWithHostileMembershipFalseAndFriendlyWithoutEnemyFlagAreValid()
    {
        var hostile=State();Assert.False(hostile.Mobs[0].Hostile);Assert.Equal((ushort)9,Observe(hostile,Own(),Birth()).View.ViewId);
        Assert.Equal((ushort)9,Observe(State(true),Own(),Birth(friendly:true)).View.ViewId);
        Assert.Throws<InvalidDataException>(()=>Observe(hostile,Own(),Birth(friendly:true)));
        hostile.Mobs[0].Health=0;Assert.Throws<InvalidDataException>(()=>Observe(hostile,Own(),Birth()));
    }
    [Fact]public void DuplicateAgentNamesUseCurrentPositionAndCurrentGenerationWithoutHistoryFallback()
    {
        Assert.Equal((ushort)9,Observe(State(),Own(),Birth(),Birth(5,10,x:500)).View.ViewId);
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Own(),Birth(),Birth(5,10)));
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Own(),Birth(),Birth(5,x:500)));
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Own(),Birth(),Move(),Move(y:495)));
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Own(),Birth(),Move(),new byte[]{9,0,2,255}));
    }
    [Fact]public void DeletedReusedPreviousLifeAndChangedCheckpointIdentityAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Own(),Birth(),Delete()));
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Own(),Birth(),Delete(),Birth()));
        Assert.Equal((byte)5,Observe(State(),Own(),Birth(),Delete(),Birth(5)).View.SpawnId);
        var state=State();var scope=MervAfterCurrentNpc.Capture(state,6);state.Mobs[0].Identity=new string('c',32);
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentNpc.Require(Stream(Own(),Birth()),scope,state));
        Assert.Throws<InvalidDataException>(()=>Observe(State(),Birth(),Own()));
    }
    [Fact]public void CurrentDeathRecordUsesLatestCorpsePositionAndCannotAdoptAliveBirth()
    {
        var state=State();state.Mobs[0].Dead=true;state.Mobs[0].Health=0;
        var death=Convert.FromHexString("090007011D0D000101002B00250000").Concat(new[]{100f,2095f,300f}.SelectMany(BitConverter.GetBytes)).Concat(Convert.FromHexString("c202008080e0010000808003fd07000000000000")).ToArray();
        Assert.Equal((ushort)9,Observe(state,Own(),Birth(),death).View.ViewId);
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth()));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(),death,Move(y:495)));
    }
    [Fact]public void ExactCapturedOwnTeleportCounter6DoesNotInvalidateNpcView6()
    {
        // 98c060d575 section13, CSEQ45/SSEQ124. The counter at offset4 is not an NPC view identifier.
        var raw=Convert.FromHexString("0200011C06000000000000000020E94600E0A14400705546177A0000");
        var state=State();var observed=Observe(state,Own(),Birth(view:6),raw);
        Assert.Equal((ushort)6,observed.View.ViewId);Assert.Equal(1,observed.PositionSection);
        Assert.Equal(new MervAfterCurrentNpc.Point(100,2095,300),observed.Position);
        var other=PacketsUtils.createTeleportPacket(29840,1295,13660,0,0,0,9,0);
        Assert.Equal((ushort)9,Observe(state,Own(),Birth(),other).View.ViewId);
    }
    [Fact]public void OwnTeleportMustBeCompleteAndAtKnownCursorNotInsideAnUnknownGap()
    {
        var state=State();var raw=Convert.FromHexString("0200011C06000000000000000020E94600E0A14400705546177A0000");
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(view:6),raw.Take(27).ToArray()));
        var wrong=(byte[])raw.Clone();wrong[3]=0x1d;
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(view:6),wrong));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(view:6),new byte[]{99,88}.Concat(raw).ToArray()));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(view:6),raw.Concat(new byte[]{6,0,2,255}).ToArray()));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(view:6),new byte[]{6,0,2,255}));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(view:6),raw,new byte[]{1,0,1,1,0,6,0}));
    }

    [Fact]public void ActualHostileDoraReplacementWithoutKeyUsesSqlAndCurrentViewCorroboration()
    {
        // bfc5bbadf0 section43, after the friendly view16 deletion at42; literal ordinary Mob producer.
        var raw=Convert.FromHexString("01000C57020FCDAB15AE446F726100000000000000000000000000000000000000000000000000000000001000002200001000D4006001005801A10001C1600100580000000040C6E24000000000805FC14000000000007ED840C001860100A8326608B860010058000000000000000000000066080608000081080A00000211110000");
        var state=State();state.Mission=301208;state.Phase=1;state.Instance=47;state.Identity="a9ee4f3b7d2847608605213d4195517c";state.Started=1794725566;
        var actor=state.Mobs[0];actor.Npc=4;actor.Identity="099ec6885aa845beaec225c4dceb8ea6";actor.Name="Dora";actor.Rsi="60010058";actor.X=38450;actor.Y=8895;actor.Z=25080;
        var observed=MervAfterCurrentNpc.Require(Stream(Own(),raw),MervAfterCurrentNpc.Capture(state,4),state);
        Assert.Equal((ushort)17,observed.View.ViewId);Assert.Equal((byte)15,observed.View.SpawnId);Assert.True(observed.WireMissionKeyAbsent);
        Assert.Contains("SQL",observed.IdentityAssociation);Assert.Contains("no wire ownership proof",observed.IdentityAssociation);
        var scope=MervAfterCurrentNpc.Capture(state,4);
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentNpc.Require(Stream(Own(),raw,new byte[]{1,0,1,1,0,17,0}),scope,state));
        state.Mobs[0].Identity=new string('c',32);Assert.Throws<InvalidDataException>(()=>MervAfterCurrentNpc.Require(Stream(Own(),raw),scope,state));
    }
    [Fact]public void AbsentHostileKeyNeverRelaxesFriendlyPresentKeyLevelOrProducerShape()
    {
        var state=State();Assert.True(Observe(state,Own(),Birth(missionKey:false)).WireMissionKeyAbsent);
        Assert.False(Observe(state,Own(),Birth()).WireMissionKeyAbsent);
        Assert.Throws<InvalidDataException>(()=>Observe(State(true),Own(),Birth(friendly:true,missionKey:false)));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(missionKey:false,level:49)));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(missionKey:false,npcRank:2)));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(missionKey:false,hostileDontAttack:true)));
        var wrong=Birth();var key=Assert.Single(WireFormat.FindCreations(wrong)).Attributes["MissionKey"];int at=BytePattern.IndexOf(wrong,key,12);Assert.True(at>=12);wrong[at+1]++;
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),wrong));
    }
    [Fact]public void KeyOmissionDoesNotAdmitDeletedRetiredOrAmbiguousHostileGenerations()
    {
        var state=State();var birth=Birth(missionKey:false);
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),birth,Delete()));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),birth,Delete(),birth));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),birth,Birth(5,10,missionKey:false)));
        Assert.Equal((byte)5,Observe(state,Own(),birth,Delete(),Birth(5,missionKey:false)).View.SpawnId);
        Assert.Throws<InvalidDataException>(()=>Observe(state,birth,Own()));
    }

}
