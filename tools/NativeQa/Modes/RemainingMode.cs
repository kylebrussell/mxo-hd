namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class RemainingMode : NativeQaMode
{
    public override string Name => "remaining";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,9,15,13,16,442,49,1114,42,485,489,63,436,68,319,481,488,327,438,129,147,158,123,1101,1102,868 }, 23828, 13984,
        new string[] {  },
        new string[] {  },
        new string[] { @"UPDATE characters SET innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114); DELETE FROM inventory WHERE charId IN (110,6111,6112,6113,6114) AND slot=1; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,615,1,1,0),(6111,615,1,1,0),(6112,615,1,1,0),(6113,615,1,1,0),(6114,615,1,1,0)",
            $@"UPDATE characters SET level=20,exp={hds.PlayerProgression.ExperienceForLevel(20)} WHERE charId=6111" },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
