using System.Text.Json;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public class CarriedMissionNpcFixtureTests
{
    [Fact] public void AReusedCurrentGenerationCannotFallBackToItsOldFriendlyBody()
    {
        var old=View(68,1);var replaced=View(68,2,false);
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedView(new[]{old,replaced},0,Token()));
    }
    [Fact] public void OptedInCarriedViewRequiresTheResolvedCurrentHeight()
    {
        var point=CarriedMissionNpcFixture.Resolve(Snapshot(phase:1),Token(),1,true);var view=View(68,2);
        var attrs=(Dictionary<string,byte[]>)view.Attributes;
        attrs["Position"]=new[]{point.X,point.Y+100,point.Z}.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedView(new[]{view},0,Token(),point));
        attrs["Position"]=new[]{point.X,point.Y,point.Z}.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.Same(view,CarriedMissionNpcFixture.RecreatedView(new[]{view},0,Token(),point));
    }
    private static byte[] Birth(double x=49050,double y=95,double z=7500,byte spawn=85,bool friendly=true)
    {
        var obj=new AttributeClass3827("NPC_BASE",599);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
        void Set(string name,byte[] bytes){var a=(hds.Attribute)obj.GetType().GetField(name)!.GetValue(obj)!;a.enable();a.setValue(bytes);}
        var name=(hds.Attribute)obj.GetType().GetField("CharacterName")!.GetValue(obj)!;var text=new byte[name.getSize()];System.Text.Encoding.ASCII.GetBytes("Sharmaine Tarn").CopyTo(text,0);Set("CharacterName",text);
        Set("Position",new[]{x,y,z}.SelectMany(BitConverter.GetBytes).ToArray());Set("DontAttack",new byte[]{1});Set("MissionKey",new byte[]{0,51,0,2});if(!friendly)Set("IsEnemy",new byte[]{1});
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(new byte[]{87,0,0}).ToArray();
    }
    private static byte[] Move(float x,float y,float z)=>new byte[]{87,0,2,0x0e,1,0x98}.Concat(new[]{x,y,z}.SelectMany(BitConverter.GetBytes)).ToArray();
    private static CarriedMissionNpcFixture.CurrentView Observe(params byte[][] sections)=>CarriedMissionNpcFixture.RecreatedCurrentView(sections.SelectMany(WireFormat.FindCreations).ToArray(),0,Token(),new(48349.99550902511,495,6677.731041764251),sections,0);
    [Fact] public void ActualFailedMovingSharmaineUsesLatestCurrentGenerationPosition()
    {
        byte[] movement=Convert.FromHexString("5700020e0198ffdd3c470080f743d9add045");var current=Observe(Birth(),movement);
        Assert.Equal((ushort)87,current.View.ViewId);Assert.Equal((byte)85,current.View.SpawnId);Assert.Equal(1,current.PositionSection);Assert.Equal(48349.99609375,current.Position.X);Assert.Equal(495,current.Position.Y);Assert.Equal(6677.73095703125,current.Position.Z);
        Assert.Equal(95,BitConverter.ToDouble(current.View.Attributes["Position"],8)); // immutable birth remains historical
    }
    [Fact] public void LatestWrongPositionCannotFallBackToEarlierMatchingUpdate()
    {
        var match=Move(48349.99609375f,495,6677.73095703125f);
        Assert.Throws<InvalidDataException>(()=>Observe(Birth(),match,Move(48349.99609375f,595,6677.73095703125f)));
        Assert.Throws<InvalidDataException>(()=>Observe(Birth(),match,new byte[]{1,0,1,1,0,87,0}));
        Assert.Throws<InvalidDataException>(()=>Observe(Birth(),match,Birth(spawn:86,friendly:false)));
        Assert.Throws<InvalidDataException>(()=>Observe(Birth(),match,new byte[]{87,0,2,0xff}));
        Assert.Throws<InvalidDataException>(()=>Observe(Birth(),match,Move(float.NaN,495,6677)));
    }
    [Fact] public void CurrentPhaseBirthIsMandatoryAndOnlyFullyFramedMovementCanRefreshPosition()
    {
        var birth=Birth();var move=Move(48349.99609375f,495,6677.73095703125f);var rows=WireFormat.FindCreations(birth);
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedCurrentView(rows,1,Token(),new(48349.9955,495,6677.731),new[]{birth,move},0));
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedCurrentView(rows,0,Token(),new(48349.9955,495,6677.731),new[]{birth,move},1));
        Assert.Throws<InvalidDataException>(()=>Observe(birth,new byte[]{9,9}.Concat(move).ToArray()));
        Assert.Equal(2,Observe(birth,new byte[]{87,0,2,0xff},move).PositionSection);
        Assert.Equal(1,Observe(birth,move,new byte[]{86,0,2,0xff}).PositionSection);
    }
    private const string Identity="d8c6dd0ceca741c28dbd85d1bba0c23a";
    private static string Snapshot(int phase=0,int mission=301207,int instance=51,ushort npc=1,string name="Sharmaine Tarn",bool friendly=true,bool giver=true,bool dead=false,string identity=Identity,bool following=true)=>JsonSerializer.Serialize(new{Version=1,Mission=mission,Instance=instance,Phase=phase,Following=following?new[]{1}:Array.Empty<int>(),Mobs=new[]{new{Npc=npc,Name=name,Identity=identity,Friendly=friendly,Giver=giver,Dead=dead,Health=1,X=47928.26170730894,Y=895d,Z=7711.280382300636}}});
    private static CarriedMissionNpcFixture.Token Token()=>CarriedMissionNpcFixture.Capture(Snapshot(),301207,0,1,"Sharmaine Tarn",true);
    private static ViewCreation View(ushort view,byte spawn,bool friendly=true)
    {
        var attributes=new Dictionary<string,byte[]>{{"CharacterName",System.Text.Encoding.ASCII.GetBytes("Sharmaine Tarn\0")},{"TalkDefaultable",new byte[]{1}},{"DontAttack",new byte[]{1}},{"MissionKey",new byte[]{0,51,0,2}}};
        if(!friendly)attributes["IsEnemy"]=new byte[]{1}; // hostile WITH dialogue/friendly-looking surface fields must still lose
        return new(view,599,spawn,attributes);
    }
    [Fact] public void ActualFailedOld64CannotWinAgainstRecreatedCurrent68()
    {
        var token=Token();var point=CarriedMissionNpcFixture.Resolve(Snapshot(phase:1,following:false),token,1,true);Assert.Equal(47928.26170730894,point.X);Assert.Equal(895,point.Y);Assert.Equal(7711.280382300636,point.Z);
        var selected=CarriedMissionNpcFixture.RecreatedView(new[]{View(64,62),View(68,66)},1,token);Assert.Equal((ushort)68,selected.ViewId);Assert.Equal((byte)66,selected.SpawnId);
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedView(new[]{View(64,62)},1,token));
        var hostile=View(68,66,false);Assert.True(hostile.Attributes.ContainsKey("TalkDefaultable"));
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedView(new[]{View(64,62),hostile},1,token));
        var wrongOwned=View(68,66);((Dictionary<string,byte[]>)wrongOwned.Attributes)["MissionKey"]=new byte[]{0,52,0,2};
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.RecreatedView(new[]{wrongOwned},0,token));
    }
    [Fact] public void WrongMissionPhaseNpcNameSideLifeGiverOrIdentityFailsClosed()
    {
        var token=Token();foreach(string bad in new[]{Snapshot(phase:1,mission:301206),Snapshot(phase:0),Snapshot(phase:1,npc:4),Snapshot(phase:1,name:"Other"),Snapshot(phase:1,friendly:false),Snapshot(phase:1,giver:false),Snapshot(phase:1,dead:true),Snapshot(phase:1,instance:52),Snapshot(phase:1,identity:"11111111111111111111111111111111")})Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.Resolve(bad,token,1,true));
        Assert.Throws<InvalidDataException>(()=>CarriedMissionNpcFixture.Capture(Snapshot(following:false),301207,0,1,"Sharmaine Tarn",true));
    }
}
