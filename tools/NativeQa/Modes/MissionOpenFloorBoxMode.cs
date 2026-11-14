using hds;
namespace NativeQa.Modes;

public sealed class MissionOpenFloorBoxMode : NativeQaMode
{
    public override string Name=>"mission-open-floor-box";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.PosterHuntFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201202"},
        extraQaMetadata:new(){["OpenFloorWitnessRequired"]=true,["MissionId"]=201202,["ExpectedSourceKey"]="0d1003dc",["ExpectedFirstPhaseObjects"]=3},
        readyMessages:new[]{"Diagnostic open-floor candidate; native scene acceptance remains pending. CSR110 begins at original Mara17000/495/2300. Call Agent Gray > Critical > Poster Hunt > accept. Use the ordinary elevator; begin at published Root and walk each published Route to Standing, never Centre. All three normal Box548 proxies must retain their natural sources; object1 source0d1003dc. Observe visible Box/hover/radial Open3 and unchanged statics. Relog before Open: all three fixed witnesses must replay. Abort normally, return to Mara and reaccept in the same world. Open all three to tick their authored Use goals, then leave normally. These goals do not offer loot/Take; do not inject rewards. No static hide/bind/move/delete, pose override, GM or forced random outcomes. Native walking/picking remains pending. See docs/MISSION-OPEN-FLOOR-BOX.md."});
}
