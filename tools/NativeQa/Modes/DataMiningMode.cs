namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class DataMiningMode : NativeQaMode
{
    public override string Name => "data-mining";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,44,215,538,539,540,541,621 }, 23828, 13984,
        new string[] {  },
        new string[] { @"DELETE FROM inventory WHERE charId=110 AND slot<97; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,422,1,1,0)" },
        new string[] {  },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
