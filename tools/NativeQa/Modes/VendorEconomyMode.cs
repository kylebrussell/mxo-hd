namespace NativeQa.Modes;
/// <summary>Mara street fixture with the existing dev-only tools-vendor opener; no rendered NPC is assumed.</summary>
public sealed class VendorEconomyMode : NativeQaMode
{
    public override string Name=>"vendor-economy";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17},17000,2300,Array.Empty<string>(),new[]{@"
DELETE FROM inventory WHERE charId=110 AND slot<97;
INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
(110,11666,11,1,0),(110,7332,12,8,0),(110,2147490819,13,1,0),(110,39406,14,4,0);
UPDATE characters SET cash=10000,y=495 WHERE charId=110;
"},Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new Dictionary<string,object?>{{"VendorEconomyQa",true},{"VendorFixtureId",98},{"VendorUiOpener","?clientactions vendor"}},
        readyMessages:new[]{"Economy QA: type ?clientactions vendor in chat to open the existing Mara tools-vendor window (fixture98). Select shirt slot11, click Appraise (680, no item/cash change), then Sell. Appraise leveled ability slot13 (12600) without selling it. Close the window; inventory Recycle Code Bits8 (cancel/confirm40), then ability (500); Antibiotic Activate Item. Dev is enabled only for the disposable mode; no GM or random override. A rendered vendor NPC is not assumed."});
}
