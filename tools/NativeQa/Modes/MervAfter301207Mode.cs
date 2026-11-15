using hds;
namespace NativeQa.Modes;
public sealed class MervAfter301207Mode : NativeQaMode
{
    public override string Name=>"merovingian-after-301207";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.MervAfter301207Fixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="301208"},
        extraQaMetadata:new(){["MervAfter301207Qa"]=true,["QaBaseMaster"]="c30569e1fc8437883b74b0b011e662c73fb14efb",["CombinedCoreDependency"]="PR235/3bcdc6aa",["MissionLayoutSeedOnly"]=true,["SelectedMissions"]="301208,301209,301210,301211,301212"},
        readyMessages:new[]{"CSR110 only: normal Flood Call > Critical Necessary Action. Earn301208-301212 in actual contact order, then next offer Mod Box301214. Only earlier27 receipts seeded; no selected mission, progress, items, rewards or forced rolls. Use actual current NPC radial Talk/Get/Give, normal corpse Loot, saved object Root/Route/Standing and real Hack/Take. ComputerParts6827 carry/relog before final Alvaro Give. No authored escort/wave/deadline/Upload/Put/LA path occurs in these five; do not invent one. No source reroll or centre teleport. Native walking/picking/combat remain pending. Checklist docs/MISSION-MERV-AFTER-301207.md."});
}
