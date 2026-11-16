using hds;
namespace NativeQa.Modes;
public sealed class ZionNextFiveMode : NativeQaMode
{
    public override string Name=>"zion-critical-next-five";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,45,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.ZionNextFiveFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101208"},
        extraQaMetadata:new(){["ZionNextFiveQa"]=true,["QaBaseMaster"]="500dc117afedb2d95cf1229558564d67982f79c1",["HistoricalQaBaseMaster"]="e79906f0d493f631637a5052881e718bea6bb126",["RequiresPublic269"]=true,["RequiresMidphase271"]=true,["SharedInteriorAdmissionRequired"]=true,["RequiresPr249"]=true,["RequiresPr251"]=true,["RequiresPr255"]=true,["HistoricalGameplayProofProducer"]="4bb27ecb6ba0f598663f96d3873f7e8febc6d040",["MissionSiteSeedOnly"]=true,["BoxMissionGeometryUnproven"]=true},
        readyMessages:new[]{"Uses reviewed PR249/251/255 runtime dependencies; Tyndall Call > Critical. Earn missions 101208-101212 normally. Hardlined Learning: Talk/kill/loot CD/upload. Hardline Hunter: ordinary Open of each junction Box, secure second site. Carry Maynard to Lonny; wait until he is nearby and following before the ordinary floor door downride. Protect both witnesses. Talk/defeat Burdette, loot/upload black box. Prior27 only; selected5 uncompleted. Level/EXP50, fixture HP5000/IS2000; no GM or RNG override. Dev weaken affects nearby NPCs including followers, so no unaided survival claim. Check Box physical approach/hover/Open in actual rooms, carry item/follower relog before upload/escort completion, then completed relog/no duplicate payout. Current raw sources/Root/Route/Standing are dynamic, never force historical coordinates. See docs/MISSION-ZION-CRITICAL-NEXT-FIVE.md."});
}
