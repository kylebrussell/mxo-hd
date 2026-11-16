using System;
using System.IO;
using System.Linq;
using HeadlessClient;
using hds;
using hds.resources.gameobjects;
using Xunit;
public class OpenWorldCurrentNpcTests
{
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4,ushort view=9,bool friendly=false,bool dead=false,byte level=1,string rsi="5E010088",bool mission=false)
    {
        var o=new AttributeClass3827("NPC_BASE",599);o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string key,byte[] value){var a=(hds.Attribute)o.GetType().GetField(key)!.GetValue(o)!;a.enable();a.setValue(value);}
        var name=new byte[((hds.Attribute)o.GetType().GetField("CharacterName")!.GetValue(o)!).getSize()];System.Text.Encoding.ASCII.GetBytes("Chopper Runner").CopyTo(name,0);Set("CharacterName",name);
        Set("Position",new[]{19403d,-705,60701}.SelectMany(BitConverter.GetBytes).ToArray());Set("Level",new[]{level});Set("MoreInfoID",Convert.FromHexString(rsi));Set("IsEnemy",new byte[]{friendly?(byte)0:(byte)1});Set("IsFriendly",new byte[]{friendly?(byte)1:(byte)0});Set("IsDead",new byte[]{dead?(byte)1:(byte)0});if(mission)Set("MissionKey",new byte[]{0,1,0,2});
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static OwnViewLifeSnapshot Stream(params byte[][] sections){var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();}
    static OpenWorldCurrentNpc.Current Read(params byte[][] sections)=>OpenWorldCurrentNpc.Require(Stream(sections),19403,-705,60701);
    [Fact] public void LatestMovementKeepsExactLiveGeneration()
    {
        byte[] move=new byte[]{9,0,2,0x0e,1,0}.Concat(new[]{19410f,-705f,60720f}.SelectMany(BitConverter.GetBytes)).ToArray();var current=Read(Own(),Birth(),move);Assert.Equal(new[]{19410d,-705,60720},current.Position);Assert.Equal((byte)4,current.View.SpawnId);
    }
    [Fact] public void RetiredReusedAmbiguousAndUnknownFramesRefuse()
    {
        byte[] deleted={1,0,1,1,0,9,0};Assert.Throws<InvalidDataException>(()=>Read(Own(),Birth(),deleted));Assert.Throws<InvalidDataException>(()=>Read(Own(),Birth(),deleted,Birth()));Assert.Throws<InvalidDataException>(()=>Read(Own(),Birth(),Birth(5,10)));Assert.Throws<InvalidDataException>(()=>Read(Own(),Birth(),new byte[]{9,0,2,255}));
    }
    [Fact] public void WrongPopulationSideLevelAndMissionCannotMasqueradeAsAmbient()
    {
        foreach(var birth in new[]{Birth(friendly:true),Birth(dead:true),Birth(level:2),Birth(rsi:"5D010088"),Birth(mission:true)})Assert.Throws<InvalidDataException>(()=>Read(Own(),birth));
    }
    [Fact] public void OldOwnLifeSnapshotNeverRevivesTarget()
    {
        var log=new OwnViewLifeLog();var own=Own();var birth=Birth();log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,WireFormat.FindCreations(birth));var old=log.Snapshot();log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>OpenWorldCurrentNpc.Require(old,19403,-705,60701));
    }
}
