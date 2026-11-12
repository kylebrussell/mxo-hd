namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class DeathPresentationMode : NativeQaMode
{
    public override string Name => "death-presentation";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,325,326,375,416,967,10,1064,133 }, 23828, 13984,
        new string[] {  },
        new string[] {  },
        new string[] {  },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
