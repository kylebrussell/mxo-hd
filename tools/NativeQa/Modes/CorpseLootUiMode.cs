namespace NativeQa.Modes;
public sealed class CorpseLootUiMode:NativeQaMode
{
    public override string Name=>"corpse-loot-ui";
    public override NativeQaModePlan Plan {get;}=MissionCatalogSampleMode.BuildPlan("301210");
}
