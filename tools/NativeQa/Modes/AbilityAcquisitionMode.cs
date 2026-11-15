using hds;
namespace NativeQa.Modes;
public sealed class AbilityAcquisitionMode:NativeQaMode
{
    public override string Name=>"ability-acquisition";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},17000,2300,Array.Empty<string>(),new[]{
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;",
        "DELETE FROM char_abilities WHERE char_id=110 AND slot<520 AND (ability_id & 0xFC000000)=0x80000000 AND (((ability_id >> 10) & 4095) IN(1,5));",
        "UPDATE characters SET cash=200000,y=495,rotation=0 WHERE charId=110;"},Array.Empty<string>(),DevCommands:true,
        environmentOverrides:new(){["MXO_ABILITY_ACQUISITION_QA"]="1"},
        extraQaMetadata:new(){["AbilityAcquisitionQa"]=true,["RealVendorId"]=AbilityAcquisitionQa.Vendor},
        readyMessages:new[]{"CSR110: ?abilityacquisition vendor opens unchanged real vendor277874480 stock, not a rendered/reachable NPC claim. Buy unowned Calm Mind Calm Body (40000), then Adrenaline Booster (40000), using normal Buy. Observe bag item46 and cash. Use the Mara hardline's normal Upload; verify both now owned/unloaded in Loading Area archive and after relog. Buy another Calm Mind code and upload: duplicate learning must keep its bag code. No QA buy/upload/grant operation exists. See docs/ABILITY-ACQUISITION.md. Known-list upload5e is a native-compatible existing inference, not a recovered retail ability-upload capture."});
}
