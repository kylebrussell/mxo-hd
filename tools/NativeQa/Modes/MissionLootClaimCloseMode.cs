using hds;
namespace NativeQa.Modes;
/// <summary>Normal mission2201 Safe, with disposable bag filler to test failed and successful Take All.</summary>
public sealed class MissionLootClaimCloseMode : NativeQaMode
{
    public override string Name => "mission-loot-claim-close";
    public override NativeQaModePlan Plan { get; } = new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[] {
            HeadlessClient.MachinesChainFixture.SeedPrerequisites(110),
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110; INSERT INTO inventory(charId,slot,goid,count,purity) VALUES "+string.Join(",",Enumerable.Range(1,96).Select(s=>$"(110,{s},2568,1,0)"))
        },DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="220110"},
        extraQaMetadata:new(){["MissionLootClaimClose"]=true,["MissionId"]=2201,["BagFillerSlots"]=96,["NativeUiClosureProven"]=false},
        readyMessages:new[]{"CSR110 only, normal Agent Gray > Critical > The End of the Beginning. Ordinary elevator, follow persisted Root/Route/Standing to Safe552/source0d10003e. Hover/radial Open, then Take All with full bag: dossier icon/window must remain; no item/goal. Close/reopen retains dossier. Delete one ordinary filler item using bag UI, then Take All: one dossier6799, icon removed and window closes; verify goal advancement separately. Reopen/retry cannot duplicate item. Native picking/walking still required; no force-use or mission object injection. See docs/MISSION-LOOT-CLAIM-CLOSE.md; standard shutdown resets mainCSR110 to Mara/fullhealth/pvp0/mission986."});
}
