namespace NativeQa.Modes;

/// <summary>Native-PASS130 fixture: only Desperation owns the disposable health-hold override.</summary>
public sealed class DesperationMode : NativeQaMode
{
    public override string Name => "desperation";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {1149},23828,13984,
        Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),DevCommands:true,
        environmentOverrides:new Dictionary<string,string> { ["MXO_DESPERATION_QA_HOLD_HEALTH"]="1" });
}
