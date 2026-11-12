namespace NativeQa.Modes;

/// <summary>Deterministic selected-target daze preparation; production conditional throws remain unchanged.</summary>
public sealed class ConditionalControlsMode : NativeQaMode
{
    public override string Name => "conditional-controls";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,347,1135,1112 }, 24800, -1498,
        new string[] { }, new string[] { }, new string[] { }, DevCommands: true);
}
