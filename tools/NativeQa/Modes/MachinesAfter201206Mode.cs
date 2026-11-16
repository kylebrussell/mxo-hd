using hds;
namespace NativeQa.Modes;
public sealed class MachinesAfter201206Mode:NativeQaMode
{
 public override string Name=>"machines-critical-after-201206";
 public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
  Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.MachinesAfter201206Fixture.SeedPrerequisites(110),
   $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
  DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
  environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201207"},
  extraQaMetadata:new(){["FirstMission"]=201207,["LastMission"]=201211,["NextExpectedMission"]=201212,["EarlierReceiptsOnly"]=26,["LayoutSeedOnly"]=true,
   ["QaBaseMaster"]="500dc117afedb2d95cf1229558564d67982f79c1",["RequiresUnmerged241BoundRoutes"]=true,["StakeoutTimedWavesImplemented"]=false,["CorpseTracerGiveImplemented"]=false,["RetailRewardsComplete"]=false},
  readyMessages:new[]{"Agent Gray Call > Critical Coroner's Report. Earn201207-201211 normally; only26 earlier receipts are seeded. Walk the actual public door/lobby/car, then use ordinary radial Talk/Get/Defeat and item menus. Keep BOTH Gaffey and Butterfly following through ordinary exit rides and the Sijthoff handoff. Just the Facts uses searchable furniture rather than Tonisha's dead body; inspect native-bound cabinet and required own-GOID doors before crossing. Carry the earned dossier through normal Quit/relog, then ordinary DOWN/public exit before hardline Upload. Rescue Rudolph and Vennie alive. Stakeout currently uses Box Open/Use plus all initial attackers, NOT the retail seven-minute/four-wave defense. Coroner's corpse tagging, the locked key door and Witness Protection delay are not implemented. Do not claim retail level50 payout parity;201209 reward is reconstructed. Five once-only receipts and next Weapons of Terror. No native teleport as walking/door proof. See docs/MACHINES-AFTER-201206.md. PR264 is merged; PR241 bound-route core remains an explicit unmerged dependency."});
}
