using hds;
using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MissionPropVisibilityMode:NativeQaMode
{
    public override string Name=>"mission-prop-visibility";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{InteriorAdmissionFixture.Seed(InteriorAdmissionFixture.Machines,110),"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new Dictionary<string,string>(InteriorAdmissionFixture.Environment(InteriorAdmissionFixture.Machines)){[MissionPropProbe.EnvironmentKey]="1"},
        extraQaMetadata:new(){["MissionPropVisibilityDiagnosticOnly"]=true,["EarlierReceiptsBeforeLogin"]=20,["CurrentMissionSeeded"]=false,["CurrentOriginalPoseOnly"]=true,["GeometryChanged"]=false,["Prior230Producer"]="ad81d3be",["LiteralAssets"]="3,548,552",["LiteralDeltas"]="0,25,50,75,95,100"},
        readyMessages:new[]{"CSR110 only. Normal Agent Gray Call > Critical Disturbing Trends201201; only20 earlier receipts before login, no selected progress/items/reward. Talk Ophiuchi and use normal saved elevators to the actual current Computer floor. BEFORE Take, keep camera fixed with avatar clear and capture original body/source/state/key. Compare ?missionprop zero 3 0 then ?missionprop same 3 0 at EXACT original pose/state; each replaces only one private lease. Then compare literal3/548/552 at0,25,50,75,95,100 with zero/same keys and the SAME camera. Box/Safe delta0 support floor derives from original CentreY plus measured GO3 support minimum; not a global95 correction. ?missionprop off removes only its clone. Do not touch original/statics, reroll source/site, or claim mission authority from same key. Ordinary clone Take5 may open only generic zero-item loot; no Map Disk/goal/reward. Log NO DRAW too. Root appends one-pass checklist; Claude owns client/reset/lock. Prior230 static NAV1600 delta0 draw remains historical, not current-site proof. See docs/MISSION-PROP-VISIBILITY.md."});
}
