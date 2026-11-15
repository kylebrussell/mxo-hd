namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class NpcMode : NativeQaMode
{
    public override string Name => "npc";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,18,52,128,137,155,1006,77,967,169,371,1064,1067,1089,383,315,333,254,1135,48,352,359,466,329,355,870,450,433,12,184,217,37,277,276,133,1112,147 }, 24800, -1498,
        new string[] {  },
        new string[] {  },
        new string[] {  },
        DevCommands: false, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
