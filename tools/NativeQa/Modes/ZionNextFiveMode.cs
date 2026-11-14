using hds;
namespace NativeQa.Modes;
public sealed class ZionNextFiveMode : NativeQaMode
{
    public override string Name=>"zion-critical-next-five";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,45,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.ZionNextFiveFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101208"},
        extraQaMetadata:new(){["ZionNextFiveQa"]=true,["MissionSiteSeedOnly"]=true,["BoxMissionGeometryUnproven"]=true},
        readyMessages:new[]{"DRAFT native geometry gate: Tyndall Call > Critical. Earn missions 101208-101212 normally. Hardlined Learning: Talk/kill/loot CD/upload. Hardline Hunter: ordinary Open of each junction Box, secure second site. Carry Maynard to Lonny. Protect both witnesses. Talk/defeat Burdette, loot/upload black box. Prior27 only; selected5 uncompleted. Level/EXP50; no GM or RNG override. Existing dev weaken is assistance, not ordinary survival. Check Box physical approach/hover/Open in actual rooms, then relog/no duplicate payout. See docs/MISSION-ZION-CRITICAL-NEXT-FIVE.md."});
}
