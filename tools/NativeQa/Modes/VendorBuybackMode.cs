namespace NativeQa.Modes;
/// <summary>Normal sell/Buy Back through the existing tools-vendor QA opener. No rendered NPC claim.</summary>
public sealed class VendorBuybackMode : NativeQaMode
{
    public override string Name=>"vendor-buyback";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17},17000,2300,Array.Empty<string>(),new[]{@"
DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
(110,11666,11,0,872415232),(110,7332,12,8,0),(110,2147490819,13,0,0);
UPDATE characters SET cash=10000,y=495 WHERE charId=110;
"},Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new Dictionary<string,object?>{{"VendorBuybackQa",true},{"VendorFixtureId",98}},
        readyMessages:new[]{"Buyback QA: use ?clientactions vendor to open the existing tools-vendor window (no rendered NPC assumed). Appraise shirt slot11, then Sell. Check the one Buy Back row and its 680 price. Click Buy Back: the shirt returns and the row clears. Repeat with Code Bits slot12 and leveled ability slot13. A second sale replaces the last row. Close/reopen the same vendor does not replay a row; Buy Back is available only if the client still offers it. World/relog expires this server receipt; no retail lifetime or price policy claim. See VENDOR-BUYBACK.md."});
}
