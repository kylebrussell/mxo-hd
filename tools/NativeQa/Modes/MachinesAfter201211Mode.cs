using hds;
namespace NativeQa.Modes;
public sealed class MachinesAfter201211Mode:NativeQaMode
{
 public override string Name=>"machines-critical-after-201211";
 public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
  Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.MachinesAfter201211Fixture.SeedPrerequisites(110),
   $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
  DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
  environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201212"},
  extraQaMetadata:new(){["FirstMission"]=201212,["LastMission"]=201216,["NextExpectedMission"]=201217,["EarlierStoryReceiptsOnly"]=31,["StandardTrainingReceipt"]=986,["SeededReceiptCount"]=32,["LayoutSeedOnly"]=true,
   ["CoreProducer"]="34dc9169695f68006ced609e32202a7566716f9d",["RequiresPendingGuidance218"]=true,["PendingGuidanceSource"]="429c676575f82e3cbaba557028e18fa91a63e1f3",["QualifiedGenericQaSource"]="4c49fb3a3c762a2ddf41ed92d479c5bef8e13f46",["RetailLockedDoorImplemented"]=false,["TimedMaintenanceWavesImplemented"]=false,["RetailRewardsComplete"]=false},
  readyMessages:new[]{"CSR110. Missions/Phone > Agent Gray > Critical Weapons of Terror. Only31 earlier story receipts plus standard986; selected201212-201216 and next201217 uncompleted. Ordinary public Door/Open, lobby walk, car Ride; radial Talk/Get/Give, current item menus and ordinary combat/Loot. Earn the statue, carry it through normal Quit/relog/reservation cleanup, then Give it to Stephan. Every hardline Upload requires ordinary DOWN/public exit first; return normally to the current car/owned floor. Keep Stewart, Long and Norris alive through actual escort destinations. Activate three Computers in authored order; current content does not actually unlock security doors. Scheduled Maintenance is one current attacker wave, not the retail multi-minute three-wave repair. Police combat in Murder Mystery is optional; no forced kills/hostility transition. Five once-only completions and next Bombs and Betrayal. Locked key-door/Dallas persuasion/body stand-ins and borrowed201212 reward remain reconstructed. No native teleport as door/walk proof. Source-only unbuilt/unrun; see docs/MACHINES-AFTER-201211.md."});
}
