namespace NativeQa.Modes;
/// <summary>Normal Agent Gray mission2201; no object, prototype, progress or reward injection.</summary>
public sealed class MissionSafeAnchorsMode : NativeQaMode
{
    public override string Name=>"mission-safe-anchors";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.MachinesChainFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="220110"},
        extraQaMetadata:new(){["MissionSafeAnchors"]=true,["OpenFloorWitnessRequired"]=true,["MissionId"]=2201,["ExpectedSourceKey"]="0d10003e",["SafePrototypePairing"]="qualified unique SpawnType6 inference"},
        readyMessages:new[]{"Diagnostic shared open-floor candidate; native scene acceptance remains pending. CSR110 starts at the original Mara offer origin17000/495/2300, not at the Safe or an old approach. Agent Gray remote Missions > Critical > The End of the Beginning > accept; ordinary elevator. Object1 must remain naturally selected source0d10003e/Safe552. After ordinary Ride, begin at its published Root and follow the published open-floor Route to Standing; never use Centre as a player start or destination; do not reuse historical source-centre coordinates. Confirm visible Safe/hover/radial Open3 and unchanged static furniture. Relog BEFORE Open; the fixed centre/quaternion/standing witness must replay without replanning. Abort normally, confirm statics unchanged, return to the original Mara offer origin and reaccept in the same world. Finally Open > take dossier6799 > leave normally. No static hide/bind/move/delete or object/progress injection. Native walking/picking remains pending. See docs/MISSION-SAFE-ANCHORS.md."});
}
