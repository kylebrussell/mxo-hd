namespace NativeQa.Modes;

/// <summary>Deterministic radius fixture; dev health-reset commands, no GM or forced rolls.</summary>
public sealed class ResistDamageMode : NativeQaMode
{
    public override string Name => "resist-damage";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {2,17,74,371,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{"UPDATE characters SET level=50,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            HeadlessClient.ResistDamageFixture.TargetsSql(6111,6112,6113,6114,17000,2300) },DevCommands:true);
}
