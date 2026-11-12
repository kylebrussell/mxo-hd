namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class ScaledMode : NativeQaMode
{
    public override string Name => "scaled";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,54,124,170,173,417,419,421,373,374 }, 23828, 13984,
        new string[] {  },
        new string[] {  },
        new string[] { $@"UPDATE characters SET level=20,exp={hds.PlayerProgression.ExperienceForLevel(20)} WHERE charId=6111" },
        DevCommands: false, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
