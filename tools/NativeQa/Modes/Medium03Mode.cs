namespace NativeQa.Modes;
public sealed class Medium03Mode : NativeQaMode
{
    public override string Name=>"medium03";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},37700,31700,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='medium03',districtId=16,y=495,rotation=192 WHERE charId=110"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["Medium03Partial"]=true,["WorldMap"]=16,["Hardline"]=2,["ExcludedStaticKeyCount"]=134,
            ["LoadingAreaUiReturnUnresolved"]=true,["CapturedNpcsOutsideFootprint"]=58},
        readyMessages:new[]{"Medium03 heavily PARTIAL: shipped hardline16/2;4491 primary-proven statics/60nav sectors;134 ambiguous keys omitted. Existing58NPC captures remain outside footprint, no usable population/subway claimed. Native LA tree rejects16; return UI UNRESOLVED. See WORLD-MEDIUM03.md."});
}
