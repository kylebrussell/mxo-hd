namespace NativeQa.Modes;

/// <summary>Shipped small03 hardline and existing captured NPCs; no fabricated population.</summary>
public sealed class Small03Mode : NativeQaMode
{
    public override string Name => "small03";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[]{2,17},17700,34100,
        Array.Empty<string>(),
        new[]{"UPDATE characters SET district='small03',districtId=17,y=-503,rotation=180 WHERE charId=110"},
        Array.Empty<string>(),
        extraQaMetadata:new Dictionary<string,object?>{{"Small03Partial",true},{"WorldMap",17},{"Hardline",1},{"ExcludedStaticKeys","13/13 13/15 13/16"}},
        readyMessages:new[]{"Small03 PARTIAL: start at shipped hardline1 (358 Evertt Estates). Walk, test doors and captured NPC views, use booth Exit to Loading Area and return. Three ambiguous shared-boundary static keys are intentionally unavailable. No subway exists in this resource."});
}
