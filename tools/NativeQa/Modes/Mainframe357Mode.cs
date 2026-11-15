using hds;
using HeadlessClient;
namespace NativeQa.Modes;
public sealed class Mainframe357Mode:NativeQaMode
{
    public override string Name=>InteriorAdmissionFixture.Mainframe.Name;
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{InteriorAdmissionFixture.Seed(InteriorAdmissionFixture.Mainframe,110),"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:InteriorAdmissionFixture.Environment(InteriorAdmissionFixture.Mainframe).ToDictionary(x=>x.Key,x=>x.Value),
        extraQaMetadata:new(){["Mainframe357AdmissionOnly"]=true,["EarlierReceiptsOnly"]=true,["MissionId"]=301206,["EscortRadius"]=MissionRuntime.EscortArrivalUnits,["ForcedSourceOrPose"]=false},
        readyMessages:new[]{"CSR110 only. Flood Call > Critical Electromagnetic301206; only earlier receipts seeded, no selected progress/items/rewards. Ordinary saved street elevator to current Root; remain stationary and verify Mainframe357 draws at its actual published centre/quaternion. Walk the current route, do not use historical source coordinates. Talk Churyumov and Give his earned-on-entry EMP Bomb through actual radial/item menus. Escort the living follower to the mainframe; BOTH actor and follower must satisfy unchanged700 radius, not just initial usable400. Observe goal3 once and protect him. Inspect actual Mainframe state/Actions without assuming geometric ownership adds any action; its own hack timing/state remain unchanged. Ordinary elevator exit and actual one completion/receipt, then Hardline LA. No source reroll, forced pose or comfort radius. Headless test movement/StillNpcs are aids, not native walking/survival proof. See docs/MISSION-MAINFRAME-357.md."});
}
