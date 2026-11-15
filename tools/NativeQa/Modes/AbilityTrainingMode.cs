using hds;
namespace NativeQa.Modes;
public sealed class AbilityTrainingMode:NativeQaMode
{
    public override string Name=>"ability-training";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,52},17000,2300,Array.Empty<string>(),
        new[]{HeadlessClient.AbilityTrainingFixture.Seed(110)},
        new[]{"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",cash=20000000,y=495,rotation=0 WHERE charId=110"},
        extraQaMetadata:new(){["AbilityTrainingQa"]=true,["RetailBackendAtomicityProven"]=false},
        readyMessages:new[]{"CSR110: at hardline Abilities, load the archived training candidates first (parents load automatically). Character > Upgrade lists ONLY equipped abilities. CHECK QUOTES BEFORE SPENDING: Sneak25->26 costs67600 (was135200) and Spy Style5->6 costs3600 (was7200); Write Code14->15 costs22500 and stops at15; Download Mission Map is capped10. Improved Sneak1->2 now costs260900 (was800); Find Data Node30->31 costs199400 (was192200), directly from current native mastery pricing, not a retail capture. Check those two quotes before accepting. Normal upgrade must change Info/level once and survive relog; loaded Hacker49->50 costs500000, replaces its196 record with200 and keeps unrelated Execute Program loaded. No GM, dev or RNG override. See docs/ABILITY-TRAINING-AUTHORITY.md."});
}
