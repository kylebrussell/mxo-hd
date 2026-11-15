namespace NativeQa.Modes;
public sealed class HyperjumpResourceRecordsMode:NativeQaMode
{
    public override string Name=>"hyperjump-resource-records";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,184,11},17000,2300,
        Array.Empty<string>(),new[]{"UPDATE char_abilities SET level=CASE WHEN ability_id=2147672064 THEN 1 ELSE 10 END WHERE char_id=110"},
        new[]{$"UPDATE characters SET level=10,exp={hds.PlayerProgression.ExperienceForLevel(10)},innerStrC=100,innerStrM=100 WHERE charId=110"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        readyMessages:new[]{"Hyper-Jump records: level10/matching EXP; normal loaded2 at10,184 at1,11 at10. Outdoors put Hyper-Jump on the hotbar, activate, aim and click a nearby visible landing point. Optional shipped default CTRL+Space jumps forward; user remapping may differ. Cancel normally, repeat after relog, retain actual80c2/80c4/80c3 and IS history. One start clock echo, none fabricated on cancel. No new fee/physics/range parity claim. See docs/HYPERJUMP-RESOURCE-RECORDS.md."});
}
