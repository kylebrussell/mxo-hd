using hds;
namespace NativeQa.Modes;
public sealed class ZionAfter1212Mode : NativeQaMode
{
    public override string Name=>"zion-critical-after-101212";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.ZionAfter1212Fixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,FixedCallbackRolls:false,GmCharacter110:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101213"},
        extraQaMetadata:new(){["ZionAfter101212Qa"]=true,["QaBaseMaster"]="08d961eb47810f754d3ab4cd55cd23f7dcc8b897",["LegacyGeometryBaseline"]="c30569e1fc8437883b74b0b011e662c73fb14efb",["RequiresSharedRoomDoorRuntime"]=true,["NpcInteriorEvidenceFormat"]=1,["NpcInteriorSceneCount"]=4,["CurrentOwnLifeBoundBody"]=true,["EarlierReceiptsOnly"]=true,["NextMission"]=101301},
        readyMessages:new[]{"CSR110 only. Tyndall Call > Critical It's Hard To Find Good Help beside Standard Courier. Earn101213-101217 without progress/item/reward grants. End at Lord of Flies. Use published Root/Route/Standing for free proxies; inspect actual radial/item menus and Hardline Upload, Actions combat and normal elevator paths. Duplicate Agent Lee holders require their separate corpses. Fissure and AgentBrown/Park escorts must survive. Relog carrying actual earned items before Give/Upload; verify each receipt once. No native geometry or no-aids claim from headless. See docs/MISSION-ZION-AFTER-101212.md. Uses reviewed249/250/251/253/254/255/256 room/door/restore/escort runtime. Wait for current followers nearby before an ordinary downride. Fixture HP5000/IS2000 and weaken can affect nearby friendly followers; no unaided survival claim. Only four audited scenes are declared, not370. Native walking/picking remains unproved."});
}
