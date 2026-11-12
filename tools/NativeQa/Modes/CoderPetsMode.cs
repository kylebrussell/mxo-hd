namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class CoderPetsMode : NativeQaMode
{
    public override string Name => "coder-pets";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,27,594,616,617,20,25,603,31,34,19,609,619 }, 23828, 13984,
        new string[] { @"UPDATE characters SET innerStrC=1000,innerStrM=1000 WHERE charId=110" },
        new string[] {  },
        new string[] {  },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
