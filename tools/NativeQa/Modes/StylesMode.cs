namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class StylesMode : NativeQaMode
{
    public override string Name => "styles";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,132,133,1006,1008,1112,146,281,341,535,647,121,207,208,547,1022,1023,1024,267,412,198 }, 24800, -1498,
        new string[] {  },
        new string[] {  },
        new string[] {  },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
