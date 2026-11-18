using System.Text.Json;
using HeadlessClient;
using hds;
using hds.resources.gameobjects;
namespace hds.Tests;
public class ZionCurrentNpcWireNameTests
{
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 public static JsonDocument Retained()
 {
  var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"tests/hds.Tests/Fixtures/CurrentNpc-retained-name32.json")))d=d.Parent;
  return JsonDocument.Parse(File.ReadAllText(Path.Combine(d!.FullName,"tests/hds.Tests/Fixtures/CurrentNpc-retained-name32.json")));
 }
 static byte[] Encode(GameObject obj,ushort view,byte spawn)=>new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
 static void Add(OwnViewLifeLog log,byte[] b,int section)=>log.Observe(false,b,section,WireFormat.FindCreations(b));
 static OwnViewLifeLog Own(JsonElement f){var l=new OwnViewLifeLog();Add(l,Convert.FromHexString(f.GetProperty("Own").GetString()!),0);return l;}
 static byte[] Born(MissionProgress.SavedMob m,string name,ushort view=31,byte spawn=29,byte side=1,ushort instance=51)
 {
  var o=new AttributeClass3827("NPC_BASE",599);o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
  o.CharacterName.setValue(name);o.Position.setValue(new[]{m.X,m.Y,m.Z}.SelectMany(BitConverter.GetBytes).ToArray());o.Level.setValue((byte)m.Level);
  o.IsEnemy.setValue(side);if(m.Friendly)o.DontAttack.setValue((byte)1);o.IsDead.setValue((byte)0);o.MissionKey.setValue(new[]{m.Phase,(byte)instance,(byte)(instance>>8),(byte)2});
  return Encode(o,view,spawn);
 }
 [Fact] public void ActualRetained33CharacterNameMatchesExactCurrent32ByteWireView()
 {
  using var f=Retained();var mob=JsonSerializer.Deserialize<MissionProgress.SavedMob>(f.RootElement.GetProperty("Mob"),Fields)!;Assert.Equal(33,mob.Name.Length);
  var log=Own(f.RootElement);Assert.Null(ZionAfter1212FollowerReadiness.CurrentNpc(log.Snapshot(),"MachinesAfter201206",mob,51));
  Add(log,Convert.FromHexString(f.RootElement.GetProperty("Birth").GetString()!),1);
  var view=ZionAfter1212FollowerReadiness.CurrentNpc(log.Snapshot(),"MachinesAfter201206",mob,51);Assert.NotNull(view);Assert.Equal((ushort)31,view.ViewId);Assert.Equal((byte)29,view.SpawnId);
  Assert.Equal("Merovingian Martial Arts Initiat",view.Name);Assert.Equal("Merovingian Martial Arts Initiate",mob.Name);
 }
 [Theory][InlineData("abc")][InlineData("01234567890123456789012345678901")][InlineData("Merovingian Martial Arts Initiate")][InlineData("René雪")][InlineData("A\0B")]
 public void ExpectedBytesMatchActualPrimaryAttributeAsciiTruncateAndPadding(string name)
 {
  var o=new AttributeClass3827("NPC_BASE",599);o.CharacterName.setValue(name);var expected=ZionAfter1212FollowerReadiness.CharacterNameBytes(name);
  Assert.Equal(32,expected.Length);Assert.Equal(o.CharacterName.getValue(),expected);
  if(name=="abc")Assert.Equal(new byte[]{97,98,99}.Concat(new byte[29]),expected);
  if(name=="René雪")Assert.Equal(new byte[]{82,101,110,63,63}.Concat(new byte[27]),expected);
  if(name=="A\0B")Assert.Equal(new byte[]{65,0,66}.Concat(new byte[29]),expected);
 }
 [Theory][InlineData("abc")][InlineData("01234567890123456789012345678901")][InlineData("René雪")][InlineData("A\0B")]
 public void CurrentNpcComparesAllCharacterBytesIncludingAfterEmbeddedNull(string name)
 {
  using var f=Retained();var mob=JsonSerializer.Deserialize<MissionProgress.SavedMob>(f.RootElement.GetProperty("Mob"),Fields)!;mob.Name=name;var log=Own(f.RootElement);Add(log,Born(mob,name),1);
  Assert.NotNull(ZionAfter1212FollowerReadiness.CurrentNpc(log.Snapshot(),"MachinesAfter201206",mob,51));
  mob.Name=name=="A\0B"?"A\0C":"Wrong";Assert.Null(ZionAfter1212FollowerReadiness.CurrentNpc(log.Snapshot(),"MachinesAfter201206",mob,51));
 }
 [Theory][InlineData("side")][InlineData("key")][InlineData("pose")][InlineData("life")][InlineData("reuse")]
 public void ExactNameDoesNotWaiveOtherCurrentAuthority(string change)
 {
  using var f=Retained();var mob=JsonSerializer.Deserialize<MissionProgress.SavedMob>(f.RootElement.GetProperty("Mob"),Fields)!;var log=Own(f.RootElement);
  Add(log,Born(mob,mob.Name,side:change=="side"?(byte)0:(byte)1,instance:change=="key"?(ushort)52:(ushort)51),1);var life=log.Snapshot();
  if(change=="pose")mob.X++;
  if(change=="life"){log.EndLife();Assert.Throws<InvalidDataException>(()=>ZionAfter1212FollowerReadiness.CurrentNpc(life,"MachinesAfter201206",mob,51));return;}
  if(change=="reuse"){var p=new PlayerCharacter();p.UnsetRelatedStaticObjId();p.DisableAllAttributes();Add(log,Encode(p,31,30),2);}
  Assert.Null(ZionAfter1212FollowerReadiness.CurrentNpc(log.Snapshot(),"MachinesAfter201206",mob,51));
 }
 [Fact] public void EscortWireNameUsesSameCodecWithoutNormalizingSavedIdentity()
 {
  using var f=Retained();var mob=JsonSerializer.Deserialize<MissionProgress.SavedMob>(f.RootElement.GetProperty("Mob"),Fields)!;mob.Friendly=true;
  var state=new MissionProgress.SavedState{Version=1,Mission=201211,Phase=0,Identity=new string('a',32),Started=100,Instance=51,Following=new ushort[]{1},Mobs=new(){mob}};
  var log=Own(f.RootElement);Add(log,Born(mob,mob.Name,side:0),1);var life=log.Snapshot();
  Assert.Single(ZionAfter1212FollowerReadiness.Require(life,life.Life,"MachinesAfter201206",state,state,new ushort[]{1},new[]{mob.X,mob.Y,mob.Z}));
  var after=JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(state,Fields),Fields)!;after.Mobs[0].Name+="Suffix";
  Assert.Throws<InvalidDataException>(()=>ZionAfter1212FollowerReadiness.Require(life,life.Life,"MachinesAfter201206",state,after,new ushort[]{1},new[]{mob.X,mob.Y,mob.Z}));
 }
 [Fact] public void SameTruncatedWireNameDoesNotChooseBetweenTwoCurrentCandidates()
 {
  using var f=Retained();var mob=JsonSerializer.Deserialize<MissionProgress.SavedMob>(f.RootElement.GetProperty("Mob"),Fields)!;var log=Own(f.RootElement);
  Add(log,Born(mob,mob.Name),1);Add(log,Born(mob,mob.Name+"OtherSuffix",32,30),2);
  Assert.Throws<InvalidDataException>(()=>ZionAfter1212FollowerReadiness.CurrentNpc(log.Snapshot(),"MachinesAfter201206",mob,51));
 }
}
