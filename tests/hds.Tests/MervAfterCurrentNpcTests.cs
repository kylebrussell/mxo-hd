using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public sealed class MervAfterCurrentNpcTests
{
    static MissionProgress.SavedState State(bool friendly=false)=>new(){Version=2,Mission=301209,Phase=1,Instance=73,Identity=new string('a',32),Started=100,
        Flags=new(){["entered"]=true},Mobs=new(){new(){Npc=6,Phase=0,Identity=new string('b',32),Name="Agent",Friendly=friendly,Hostile=false,Health=100,X=100,Y=2095,Z=300}}};
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4,ushort view=9,bool friendly=false,double x=100)
    {
        var o=new AttributeClass3827("NPC_BASE",599);o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string name,byte[] b){var a=(hds.Attribute)o.GetType().GetField(name)!.GetValue(o)!;a.enable();a.setValue(b);}
        var attr=(hds.Attribute)o.GetType().GetField("CharacterName")!.GetValue(o)!;var name=new byte[attr.getSize()];System.Text.Encoding.ASCII.GetBytes("Agent").CopyTo(name,0);Set("CharacterName",name);
        Set("Position",new[]{x,2095d,300d}.SelectMany(BitConverter.GetBytes).ToArray());Set("MissionKey",new byte[]{0,73,0,2});
        if(friendly)Set("DontAttack",new byte[]{1});else{Set("IsEnemy",new byte[]{1});Set("TalkDefaultable",new byte[]{1});}
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static byte[] Move(float x=100,float y=2095)=>new byte[]{9,0,2,0x0e,1,0}.Concat(new[]{x,y,300f}.SelectMany(BitConverter.GetBytes)).ToArray();
    static byte[] Delete()=>new byte[]{1,0,1,1,0,9,0};
    static OwnViewLifeSnapshot Stream(params byte[][] rows){var log=new OwnViewLifeLog();for(int i=0;i<rows.Length;i++)log.Observe(false,rows[i],i,WireFormat.FindCreations(rows[i]));return log.Snapshot();}
    static MervAfterCurrentNpc.CurrentView Observe(MissionProgress.SavedState state,params byte[][] rows)=>MervAfterCurrentNpc.Require(Stream(rows),MervAfterCurrentNpc.Capture(state,6),state);
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
        var death=Convert.FromHexString("090007011D0D000100002B00250000").Concat(new[]{100f,2095f,300f}.SelectMany(BitConverter.GetBytes)).ToArray();
        Assert.Equal((ushort)9,Observe(state,Own(),Birth(),death).View.ViewId);
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth()));
        Assert.Throws<InvalidDataException>(()=>Observe(state,Own(),Birth(),death,Move(y:495)));
    }
}
