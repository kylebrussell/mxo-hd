using hds;
namespace NativeQa.Modes;
public sealed class SafeFloorMode:NativeQaMode
{
    public override string Name=>"safe-floor";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},25100,14020,Array.Empty<string>(),Array.Empty<string>(),
        new[]{$"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=2095,rotation=0 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){[SafeFloorProbe.EnvironmentKey]="1"},
        extraQaMetadata:new(){["SafeFloorDiagnosticOnly"]=true,["MissionExecutor"]=false,["NativePickingProven"]=false},
        readyMessages:new[]{"CSR110 standalone Safe asset diagnostic at QA room floor 2000, standing 25100/2095/14020. Inspect the space first; ?safefloor on creates actual free GO552 at 25100/2050/14100, explicit identity, raw default Closed835 (CurrentState omitted), UseCount1/MissionKey0. Look down/turn normally; inspect model, hover and radial Open. Ordinary Open3 is logged only: no mission/loot/door-animation executor. ?safefloor off removes only the private diagnostic view. Repeat on/off, retain actual03/80c7 and log. No source197 placement/model/bounds/range fix or native walking/scene-clearance claim. See docs/SAFE-FLOOR-DIAGNOSTIC.md."});
}
