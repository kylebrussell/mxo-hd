namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class EmpBlastMode : NativeQaMode
{
    public override string Name => "emp-blast";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,1025 }, 24800, -1498,
        new string[] {  },
        new string[] {  },
        new string[] {  },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
