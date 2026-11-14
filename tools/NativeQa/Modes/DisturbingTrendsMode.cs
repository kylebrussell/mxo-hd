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
        extraQaMetadata:new(){["DisturbingTrendsQa"]=true,["RequiredComputerParent"]="9cdc49b5fa7c1f7d2b97afd8e036700e0624d9b2",["CurrentPolicyXp"]=DisturbingTrendsFixture.ExpectedXp,["CurrentPolicyInfo"]=DisturbingTrendsFixture.ExpectedInfo,["NativePickingHold"]=false,["SharedOpenFloorRouteAdmission"]=true},
        readyMessages:new[]{"CSR 110, Agent Gray Call > Critical > Disturbing Trends. Talk to the first Ophiuchi, leave via the elevator and verify the next area is admitted without refusal. Enter the operations post normally. Source 1407/key 0d1001ed remains unchanged; read the current checkpoint's Root/Route/Standing and Computer centre, not the historical source-anchor coordinates. Verify the free Computer renders and native hover/radial Take works from the reachable standing point; the conservative closed-door graph does not prove native walking or line of sight. Take Map Disk 6783, leave the operations post normally, relog while carrying before Give, then enter the current phase-3 Ophiuchi area normally. Current default reference 50 pays 54,871 XP / 45,339 information; pending PR202's metadata is separate. No item/progress/reward injection. See docs/MACHINES-DISTURBING-TRENDS.md."});
}
