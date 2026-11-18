using HeadlessClient;
using hds;
using hds.resources.gameobjects;
namespace hds.Tests;
[Collection("DelayedActions")]
public sealed class MervAfterFurnitureBodyTests
{
    static byte[] Creation(GameObject obj,byte spawn,ushort view)=>new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    static byte[] Own(){var own=new PlayerCharacter();own.UnsetRelatedStaticObjId();own.DisableAllAttributes();return Creation(own,0,2);}
    static byte[] Body(MissionProgress.SavedState state,MissionBoundFurnitureRouteProof.Plan plan)
    {
        var source=state.Objects.Single(o=>o.Id==plan.ObjectId);var field=typeof(GameObjectDefinitions).GetField("Object1450")!;
        var obj=(GameObject)Activator.CreateInstance(field.FieldType,field.Name,(ushort)1450)!;obj.SetRelatedStaticObjId(source.Spot.mxoStaticId);obj.DisableAllAttributes();
        void Set(string name,byte[] bytes){var attr=(hds.Attribute)obj.GetType().GetField(name)!.GetValue(obj)!;attr.enable();attr.setValue(bytes);}
        Set("Position",plan.Centre.SelectMany(BitConverter.GetBytes).ToArray());Set("Orientation",Convert.FromHexString(plan.Quaternion));Set("MissionKey",new byte[]{255,(byte)state.Instance,(byte)(state.Instance>>8),2});
        return Creation(obj,4,9);
    }
    static OwnViewLifeSnapshot Stream(params byte[][] sections){var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();}
    [Fact] public void Typed1450PlanQualifiesExistingCurrentBirthButNeverRevivesDelete()
    {
        string repo=NativeBoundFurnitureFixture.FindRepository();var s=NativeBoundFurnitureFixture.Prepare(repo).State;s.Flags["entered"]=true;
        var plan=MissionBoundFurnitureRouteProof.Read(repo,s,1)!;var scope=MervAfterCurrentBoundBody.Capture(s,1);var own=Own();var body=Body(s,plan);
        var stream=Stream(own,body);Assert.Equal((ushort)1450,MervAfterCurrentBoundBody.Require(stream,scope,s,plan).View.Goid);
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(stream,scope,s));
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(stream,scope,s,plan with{ObjectId=2}));
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(Stream(own,body,new byte[]{1,0,1,1,0,9,0}),scope,s,plan));
    }
}
