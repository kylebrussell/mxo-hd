using hds;
namespace NativeQa.Modes;
public sealed class MachinesCriticalNextFiveMode : NativeQaMode
{
    public override string Name => "machines-critical-next-five";
    public override NativeQaModePlan Plan {get;} = new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[] {
            HeadlessClient.MachinesCriticalNextFiveFixture.SeedPrerequisites(110),
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"
        },DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201202"},
        extraQaMetadata:new(){["MachinesCriticalNextFive"]=true,["FirstMission"]=201202,["LastMission"]=201206,["NextExpectedMission"]=201207,["LayoutSeedOnly"]=true,["QaBaseMaster"]="08d961eb47810f754d3ab4cd55cd23f7dcc8b897",["NpcInteriorEvidenceFormat"]=2,["NpcInteriorSceneCount"]=370,["CurrentLiveBodies"]=true,["QualifiedSharedRoomDoorRuntime"]=true},
        readyMessages:new[]{"CSR110 only. Agent Gray Call > Critical: Poster Hunt, Hard Time, No Allies Here, Under Warranty, Gathering Evidence. All five completions must be earned; only21 earlier receipts (seven introduction plus14 Machine contact) are seeded. Return to original offer origin17000/495/2300 before each acceptance for the published source context. Walk the ordinary public street door into its lobby before using the car; never teleport into the building as native entry proof. Before any outside hardline Upload, use the ordinary car DOWN from the unfinished mission floor, walk the lobby/public street exit, then reach the hardline. If objectives remain inside, return UP through the ordinary public door/lobby/car without restaging. Use normal elevator and current Root/Route/Standing for every open-floor proxy, ordinary hover/radial actions, no missionobject relocation or forced use. Preserve primary source choices, carried items/followers and one receipt per mission; next Critical must be Coroner's Report. Poster Hunt uses merged reference17 metadata: level50 earns404250XP/175000Info. Remaining unknown source levels retain current default50 policy. Use actual measured saved doors and wait for current followers nearby; inspect every prop before use. Sheldon is authored friendly from entry (retail capture-side switching remains a documented gap). Fixture HP/IS and weaken1 can affect nearby followers; no unaided survival claim. Native accessibility/LOS remains an acceptance gate. See docs/MACHINES-CRITICAL-NEXT-FIVE.md; standard shutdown resets mainCSR110 to Mara/fullhealth/pvp0/mission986."});
}
