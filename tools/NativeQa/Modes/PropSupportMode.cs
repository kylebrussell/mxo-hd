using hds;
namespace NativeQa.Modes;
public sealed class PropSupportMode:NativeQaMode
{
    public override string Name=>"prop-support-diagnostic";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},-4243,-5943,Array.Empty<string>(),Array.Empty<string>(),
        new[]{$"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},x=-4242.57080078125,y=1695,z=-5942.9,rotation=0 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){[PropSupportProbe.EnvironmentKey]="1"},
        extraQaMetadata:new(){["PropSupportDiagnosticOnly"]=true,["MissionExecutor"]=false,["NativeRenderingProven"]=false,["HeightDeltas"]="0,25,50,75,100",["AssetControls"]="3,548,552"},
        readyMessages:new[]{"CSR110 only, observed office Standing -4242.5708/1695/-5942.9. First compare ?propsupport 3 0, ?propsupport 548 0 and ?propsupport 552 0 at the same XZ, with centres1625.0038/1640/1650 so measured asset bases all equal1600. Then ?propsupport 3 0 creates private free GO3 at exact failedCentre -4242.57080078125/1625.003776550293/-5901.43408203125, quat00000000F30435B300000000FFFF7F3F, explicit CurrentState0x961 (Box/Safe retain raw default0x835). Capture normal camera side profile, floor contact and Computer hover/radial. Repeat ?propsupport 3 25, 3 50, 3 75, 3 100 in order with the same camera; each replaces only its private lease. Record if NONE draws. ?propsupport off removes only its lease. No model resource/HalfExtents override, mission/loot/reward executor, collision/height fix or parity claim. See docs/PROP-SUPPORT-DIAGNOSTIC.md."});
}
