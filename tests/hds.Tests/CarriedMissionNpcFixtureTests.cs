using System.Text.Json;
using HeadlessClient;
namespace hds.Tests;
public class CarriedMissionNpcFixtureTests
{
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
