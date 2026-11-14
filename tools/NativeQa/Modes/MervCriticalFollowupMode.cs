using hds;
namespace NativeQa.Modes;
public sealed class MervCriticalFollowupMode:NativeQaMode
{
    public override string Name=>"merovingian-critical-followup";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,45,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.MervFollowupFixture.SeedPrerequisites(110),
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="301101"},
        extraQaMetadata:new(){["MervCriticalFollowupQa"]=true,["MissionLayoutSeedOnly"]=true},
        readyMessages:new[]{"DRAFT depends197 Safe native/merge. Flood Call > Critical: earn301203-301207 normally, only22 earlier receipts seeded. Use ordinary talk/combat/loot/object/escort/upload paths. Existing weaken/test movement are diagnostic preparation, not ordinary strength/survival/walk proof. Generic1412 computer/mainframe and bound furniture approach/picking remain native unproven. See docs/MISSION-MERV-CRITICAL-FOLLOWUP.md."});
}
