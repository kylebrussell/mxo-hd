namespace NativeQa.Modes;
public sealed class LaHardlineRegistrationMode : NativeQaMode
{
    public override string Name => "la-hardline-registration";
    public override NativeQaModePlan Plan {get;} = new(new ushort[]{2,17},37700,31700,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='medium03',districtId=16,y=495,rotation=192 WHERE charId=110"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["HardlineRegistrationOurs"]=true,["WorldMap"]=16,["HardlineLogicalId"]=8,["Hardline"]=2},
        readyMessages:new[]{"OURS hardline registration: cities1..3 preserved; six validated testworld filenames appended4..9. CSR110 at source medium03 booth16/2, heading192. If unsynchronized, double-click or radial Synchronize first; verify full radial appears. Then Exit to Loading Area; inspect labels and choose medium03/logical8/HID2, then city. Native UI success pending; see LA-HARDLINE-REGISTRATION.md. No QA visited-row grants; existing default all-hardlines policy."});
}
