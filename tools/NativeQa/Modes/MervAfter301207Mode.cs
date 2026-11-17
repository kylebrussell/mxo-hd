using hds;
namespace NativeQa.Modes;
public sealed class MervAfter301207Mode : NativeQaMode
{
    public override string Name=>"merovingian-after-301207";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        new[]{HeadlessClient.MervAfter301207Fixture.SeedPrerequisites(110)},Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="301208"},
        extraQaMetadata:new(){["MervAfter301207Qa"]=true,["QaBaseMaster"]="500dc117afedb2d95cf1229558564d67982f79c1",["HistoricalQaBaseMaster"]="500dc117afedb2d95cf1229558564d67982f79c1",["FurnitureCoreDependency"]="PR274/aac33496ef2afe609eb17adc5b79f850737a8077 SOURCE INTEGRATION ONLY; local build and new gameplay unrun",["CombinedCoreDependency"]="PR274/aac33496 includes prior generic Street/bound-route runtime; qualified PR213/8001ceb2 shared QA and schema-history retained",["MissionLayoutSeedOnly"]=true,["SelectedMissions"]="301208,301209,301210,301211,301212"},
        readyMessages:new[]{"CSR110 only: normal Flood Call > Critical Necessary Action. Earn301208-301212 in actual contact order, then next offer Mod Box301214. Only earlier27 receipts seeded; no selected mission, progress, items, rewards or forced rolls. V3 furniture uses exact saved native-profile Standing/path and each required own-GOID ordinary door Open; no five-point fallback. Use actual current NPC radial Talk/Get/Give, normal corpse Loot, saved object Root/Route/Standing and real Hack/Take. Generated enemies are Machine Programs; named Agent NPCs retain their names. ComputerParts6827 carry/relog before final Alvaro Give. No authored escort/deadline/Upload/Put/LA step occurs in these five; generated enemies still require ordinary combat. No source reroll or centre teleport. Native walking/picking/combat remain pending. Checklist docs/MISSION-MERV-AFTER-301207.md."});
}
