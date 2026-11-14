using System.Text.Json;
using hds;
namespace HeadlessClient;

// Exact RNG inputs and source identities retained from failed mxotest_run_1_98d89a144c.
public static class Rollback842Fixture
{
    public const int InitialSeed=1821579883,Phase1Seed=1821541235;
    public const string Phase0Fingerprint="EE14642429DDB504E87EB5DA29BCCF7C368B045FAAFE2CBE89F56D7EBE31F7A0";
    public const string Phase1Fingerprint="B35F28E2919D869A7844C6F4C04A4519803C7440F196F1EE23260B591C7BF1FA";
    public const string ChoiceHash="18EE2EB80B361613983F46FB5B4932A94153F93320B85C4578413886784F97BD";
    public static void PreGet(double[] position)
    {Check.That(position.SequenceEqual(new double[]{27480,895,14650}),"Exact failed pre-Get position preserved");}
    public static void Phase0(MissionProgress.SavedState state)
    {
        Check.Equal(399905,state.Mission,"Exact failed mission");Check.Equal(0,state.Phase,"Still phase0 before Get");
        var door=state.SiteDoors.Single();Check.Equal(219152420u,door.mxoStaticId,"Exact initial site door");
        Check.That(new[]{door.pos_x,door.pos_y,door.pos_z}.SequenceEqual(new double[]{28800,945,14700}),"Exact initial site pose");
        var courier=state.Mobs.Single(m=>m.Name=="Checkpoint Courier");
        PreGet(new[]{courier.X+100,courier.Y,courier.Z});Check.Equal(0,state.Done.Length,"No goal earned before ordinary Get");
    }
    public static void Phase1(MissionProgress.SavedState state)
    {
        Check.Equal(1,state.Phase,"Exact reproduced phase1 admitted");
        var site=state.OpenFloorSites!.Single(s=>s.DoorKey==882901607);
        Check.Equal(Phase1Fingerprint,site.Fingerprint,"Exact failed selected site fingerprint");
        var obj=state.Objects.Single();Check.Equal((ushort)552,obj.Goid,"Same generated Safe");
        Check.Equal(882902114u,obj.Spot.mxoStaticId,"Exact failed source, never seed-hop");
        var choices=new[]{new MissionOpenFloorChoice(obj.Id,obj.Goid,JsonSerializer.Serialize(obj.Spot,new JsonSerializerOptions{IncludeFields=true}))};
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(choices,new JsonSerializerOptions{IncludeFields=true}))));
        Check.Equal(ChoiceHash,hash,"Exact failed canonical source choices");
    }
}
