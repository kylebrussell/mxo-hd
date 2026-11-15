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
        extraQaMetadata:new(){["DisturbingTrendsQa"]=true,["QaBaseMaster"]="0490bff8fd8c75264be5ec8c9242e9e72fb0bfbe",["SharedInteriorAdmissionRequired"]=true,["CurrentPolicyXp"]=DisturbingTrendsFixture.ExpectedXp,["CurrentPolicyInfo"]=DisturbingTrendsFixture.ExpectedInfo,["NativePickingPending"]=true,["SharedOpenFloorRouteAdmission"]=true},
        readyMessages:new[]{"Shared admission and first-introduction core is merged. Native whole-chain QA remains pending. CSR110: Agent Gray > Call > Critical > Disturbing Trends. Use actual Door > Operator elevator rides, Talk to Ophiuchi, then enter the operations post normally. Record its dynamically admitted raw source separately from fixed Root/Route/Standing and Computer DTO; no historical source or coordinates are selected. Verify Wong/Park and Computer are inside, walk the route, and use actual hover/radial Take. Carry earned Map Disk6783 through ordinary exit/relog, then Give to current phase3 Ophiuchi. Level50 current quote:168,042 XP/133,350 information; final cash134,350 includes initial1,000. No selected progress/item/reward injection. See docs/MACHINES-DISTURBING-TRENDS.md; native camera/picking remains Claude's gate."});
}
