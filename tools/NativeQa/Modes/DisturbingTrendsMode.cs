using hds;
using HeadlessClient;
namespace NativeQa.Modes;
public sealed class DisturbingTrendsMode : NativeQaMode
{
    public override string Name=>"machines-disturbing-trends";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{DisturbingTrendsFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201101"},
        extraQaMetadata:new(){["DisturbingTrendsQa"]=true,["RequiredComputerParent"]="4dbb2067672e806aa31a04d9e3aba496385342b5",["CurrentPolicyXp"]=DisturbingTrendsFixture.ExpectedXp,["CurrentPolicyInfo"]=DisturbingTrendsFixture.ExpectedInfo,["NativePickingHold"]=true},
        readyMessages:new[]{"DRAFT HOLD on PR 196 native Computer picking; do not force Take. Once cleared: CSR 110, Agent Gray Call > Critical > Disturbing Trends. Talk to the first Ophiuchi; exit and enter the operations post normally. Source 1407, key 0d1001ed, creates GO3 at (28740,881,13200). Use native Take for Map Disk 6783, relog while carrying, then exit/enter normally and Give to the new phase-3 Ophiuchi. Current default reference 50 pays 54,871 XP / 45,339 information; archive HUD 17 and pending PR 202 are separate caveats. No item/progress/reward injection. See docs/MACHINES-DISTURBING-TRENDS.md."});
}
