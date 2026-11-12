namespace NativeQa.Modes;
/// <summary>Real data-loaded vendor82837505 near19155,495,5313; deterministic item quotes need no random override.</summary>
public sealed class VendorEconomyMode : NativeQaMode
{
    public override string Name=>"vendor-economy";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17},19155,5313,Array.Empty<string>(),new[]{@"
DELETE FROM inventory WHERE charId=110 AND slot<97;
INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
(110,11666,11,1,0),(110,7332,12,8,0),(110,2147490819,13,1,0),(110,39406,14,4,0);
UPDATE characters SET cash=10000,y=495 WHERE charId=110;
"},Array.Empty<string>(),
        extraQaMetadata:new Dictionary<string,object?>{{"VendorEconomyQa",true},{"VendorStaticId",82837505}},
        readyMessages:new[]{"Economy QA: right-click the nearby data-loaded vendor (19155,495,5313), open vendor UI, Appraise shirt slot11 then Sell. Appraise must not consume/pay. In inventory use observed Recycle buttons/confirm/cancel on Code Bits8 and leveled ability code; compare base recycle500 vs full-price leveled buyback12600. No dev or random override enabled."});
}
