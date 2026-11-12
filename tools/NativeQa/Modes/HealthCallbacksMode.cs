namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class HealthCallbacksMode : NativeQaMode
{
    public override string Name => "health-callbacks";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,196,861,941,943,378,380,506,1137,54,481 }, 23828, 13984,
        new string[] {  },
        new string[] {  },
        new string[] { @"UPDATE characters SET innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)",
            $@"UPDATE characters SET level=20,exp={hds.PlayerProgression.ExperienceForLevel(20)} WHERE charId=6111" },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: true, SeedDisguiseTool: true,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: true, CompileItemsQa: false);
}
