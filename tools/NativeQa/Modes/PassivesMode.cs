namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class PassivesMode : NativeQaMode
{
    public override string Name => "passives";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,45,269,71,233,370,95,52,77,54,169,1098,359,474,327,1064 }, 24800, -1498,
        new string[] {  },
        new string[] {  },
        new string[] { @"DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)" },
        DevCommands: false, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
