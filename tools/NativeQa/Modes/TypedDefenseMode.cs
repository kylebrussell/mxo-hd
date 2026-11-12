namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class TypedDefenseMode : NativeQaMode
{
    public override string Name => "typed-defense";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,1112,134,1101 }, 24800, -1498,
        new string[] {  },
        new string[] {  },
        new string[] { @"UPDATE characters SET healthC=5000,healthM=5000 WHERE charId=110",
            @"UPDATE characters SET innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)",
            $@"UPDATE characters SET level=20,exp={hds.PlayerProgression.ExperienceForLevel(20)} WHERE charId=6111" },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
