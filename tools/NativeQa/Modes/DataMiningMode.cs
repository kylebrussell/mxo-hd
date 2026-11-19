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
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false,
        readyMessages: new[]
        {
            "Use ?utilityobject 606 for a node and ?utilityobject 596 for a light luggable. Capture their distinct search presentation with the handheld locator.",
            "Use Locate Data Node 215, then independently Find Data Node 621. Check each activation ID, locator FX and handheld expiry after 10 s.",
            "With Detect Luggables 541 loaded, record the nearest light object's yellow minimap ping. Use ?utilitylocator 423 to switch to goggles.",
            "With See Luggables 540 loaded, record the visible aura around the light object. Pick it up and check the aura and locator clear."
        });
}
