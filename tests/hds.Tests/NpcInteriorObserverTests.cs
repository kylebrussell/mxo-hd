using System.Text;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    static byte[] QaEncode(GameObject obj,ushort view,byte spawn)=>new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    static byte[] QaOwn(){var obj=new PlayerCharacter();obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();return QaEncode(obj,2,1);}
    static byte[] QaNpc(MissionProgress.SavedState s,ushort view=7,byte spawn=3)
    {
        var actor=s.Mobs.Single(m=>m.Npc==6);var obj=new AttributeClass3827("NPC_BASE",599);obj.UnsetRelatedStaticObjId();obj.DisableAllAttributes();
        void Set(string field,byte[] raw){var a=(hds.Attribute)obj.GetType().GetField(field)!.GetValue(obj)!;a.enable();a.setValue(raw);}
        var name=(hds.Attribute)obj.GetType().GetField("CharacterName")!.GetValue(obj)!;var text=new byte[name.getSize()];Encoding.ASCII.GetBytes(actor.Name).CopyTo(text,0);
        Set("CharacterName",text);Set("Position",new[]{actor.X,actor.Y,actor.Z}.SelectMany(BitConverter.GetBytes).ToArray());Set("DontAttack",new byte[]{1});Set("Level",new byte[]{50});Set("MissionKey",new byte[]{0,47,0,2});return QaEncode(obj,view,spawn);
    }
    static OwnViewLifeLog QaLife(params byte[][] sections)
    {var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log;}
    [Fact] public void CanonicalOwnIs9ByteFramePreservesCurrentFriendlyOphiuchi6AndEndsOnLifeChange()
    {
        var s=QaState();var log=QaLife(QaOwn(),QaNpc(s),Convert.FromHexString("02000280808010D807"));var scope=NpcInteriorCurrentNpc.Capture(s,6);var observed=NpcInteriorCurrentNpc.Require(log.Snapshot(),scope,s);
        Assert.Equal((ushort)7,observed.View.ViewId);Assert.False(observed.View.Attributes.ContainsKey("IsEnemy"));Assert.Equal(new byte[]{1},observed.View.Attributes["DontAttack"]);Assert.False(observed.WireMissionKeyAbsent);
        var old=log.Snapshot();log.EndLife();Assert.False(old.Current);Assert.Throws<InvalidDataException>(()=>NpcInteriorCurrentNpc.Require(old,scope,s));
    }
    [Fact] public void CurrentNpcCannotRecoverAfterUnknownBytesOrReuseDeletedBirth()
    {
        var s=QaState();var scope=NpcInteriorCurrentNpc.Capture(s,6);var log=QaLife(QaOwn(),QaNpc(s),new byte[]{99,88},QaNpc(s,8,4));Assert.Throws<InvalidDataException>(()=>NpcInteriorCurrentNpc.Require(log.Snapshot(),scope,s));
        var delete=Convert.FromHexString("01000101000700");log=QaLife(QaOwn(),QaNpc(s),delete,QaNpc(s));Assert.Throws<InvalidDataException>(()=>NpcInteriorCurrentNpc.Require(log.Snapshot(),scope,s));
        log=QaLife(QaOwn(),QaNpc(s));s.Mobs[0].Identity="44444444444444444444444444444444";Assert.Throws<InvalidDataException>(()=>NpcInteriorCurrentNpc.Require(log.Snapshot(),scope,s));
    }
    const string QaDoorQuaternion="00000000F30435B300000000FFFF7F3F";
    static byte[] QaDoor(ushort view=9,byte spawn=5,uint state=0x835)
    {
        var obj=new AttributeClass364("Door",346);obj.SetRelatedStaticObjId(0x2260056b);obj.DisableAllAttributes();obj.Position.enable();obj.Position.setValue(new[]{38500d,6145,25800}.SelectMany(BitConverter.GetBytes).ToArray());obj.Orientation.enable();obj.Orientation.setValue(Convert.FromHexString(QaDoorQuaternion));obj.CurrentState.enable();obj.CurrentState.setValue(BitConverter.GetBytes(state));return QaEncode(obj,view,spawn);
    }
    [Fact] public void CurrentCertifiedDoorStateUsesOwnSchemaAndCannotBorrowNpcOrUnknownSuffix()
    {
        var birth=QaDoor();var log=QaLife(QaOwn(),birth);var door=Assert.Single(WireFormat.FindCreations(birth));var scope=NpcInteriorComputerState.Capture(log.Snapshot(),door);
        Assert.Equal(0x835u,scope.State);var update=BitConverter.GetBytes((ushort)9).Concat(ObjectStates.BuildStateUpdate(346,0x834)).ToArray();log.Observe(false,update,2,WireFormat.FindCreations(update));Assert.Equal(0x834u,NpcInteriorComputerState.Read(log.Snapshot(),scope.Capability));
        log.Observe(false,new byte[]{99,88},3,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.Read(log.Snapshot(),scope.Capability));
        var npc=QaNpc(QaState());log=QaLife(QaOwn(),npc);Assert.Throws<InvalidDataException>(()=>NpcInteriorComputerState.Capture(log.Snapshot(),Assert.Single(WireFormat.FindCreations(npc))));
    }
}
