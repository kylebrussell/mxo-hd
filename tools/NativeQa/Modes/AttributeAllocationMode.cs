using HeadlessClient;
using hds;
namespace NativeQa.Modes;
public sealed class AttributeAllocationMode:NativeQaMode
{
    public override string Name=>"attribute-allocation";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},17000,2300,Array.Empty<string>(),
        new[]{AttributeAllocationFixture.Seed(110)},
        new[]{"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",cash=1000000,y=495,rotation=0 WHERE charId=110"},
        extraQaMetadata:new(){["AttributeAllocationQa"]=true,["AttributeAvailablePoints"]=49},
        readyMessages:new[]{"CSR110: Level/EXP50, all five existing base attribute rows,49 available points. Open Character > Upgrade > Attributes, queue one Focus and one Reason point, click Apply All once, verify47 points left and those values+1; Info unchanged. Relog and verify all five values and remaining points. No dev/GM/RNG. See docs/ATTRIBUTE-ALLOCATION.md."});
}
