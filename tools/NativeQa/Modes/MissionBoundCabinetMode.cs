using hds;
using HeadlessClient;
namespace NativeQa.Modes;

public sealed class MissionBoundCabinetMode:NativeQaMode
{
    public override string Name=>MissionBoundCabinetFixture.Name;
    public override NativeQaModePlan Plan {get;}=new(MissionBoundCabinetFixture.Abilities,17000,2300,Array.Empty<string>(),Array.Empty<string>(),new[]{
        MissionBoundCabinetFixture.SeedPrerequisites(110),
        $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},alignment=2,y=495,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"
    },DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201202"},
        extraQaMetadata:new(){["BoundPoseScope"]="GO326 only; native factory authored-bounds centre, not145",["OriginalSource"]="0d1001a2; no forced choice",["Standing"]="28160/1295/11435; native walk unproved",["NativeEntryLimit"]="Separate elevator-native-pose dependency; stop if ordinary door cannot be picked"},
        readyMessages:new[]{"CSR110 only, level50/5000HP/2000IS disclosed QA profile. Agent Gray > Call > Missions > Critical > Hard Time; only22 earlier introduction/critical receipts plus standard tutorial986 seeded. Normal selected first-phase object must remain bound Filing Cabinet326/key0d1001a2. Ordinary current elevator/Root entry; stop and report if the separate native door-pose issue blocks its hover, do not force a use. Defeat the actual crew and Phage, Take the earned Flyer6796, then walk to the original cabinet near28040/1282/11435. From grounded28160/1295/11435 clear the avatar/camera; BEFORE Open inspect the original cabinet, ordinary hover/radial Open, then Take the Folder6795. It must remain the same native static, not a relocated/free clone. Normal carry logout/relog must retain identity and pose. No full-mission reward claim. Any test movement/weaken aid must be disclosed: weaken1 affects all nearby NPCs including friends; no unaided combat/survival claim. See docs/MISSION-BOUND-FILING-CABINET.md. Standard shutdown resets CSR110 to Mara/fullhealth/pvp0/mission986; Codex never uses the native client/lock."});
}
