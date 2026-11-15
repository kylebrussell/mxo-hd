using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public sealed class InvisibilityDurationMode:NativeQaMode
{
    public override string Name=>"invisibility-duration";
    public override NativeQaModePlan Plan {get;}=new(InvisibilityDurationFixture.Abilities,17000,2300,Array.Empty<string>(),Array.Empty<string>(),new[]{
        "UPDATE characters SET level=50,exp="+hds.PlayerProgression.ExperienceForLevel(50)+",x=CASE WHEN charId=6111 THEN 17200 ELSE 17000 END,y=495,z=2300,alignment=1,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN(110,6111)",
        "DELETE FROM inventory WHERE charId IN(110,6111) AND (slot=40 OR goid=460); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0),(6111,460,40,1,0)"},
        environmentOverrides:InvisibilityDurationFixture.Environment.ToDictionary(x=>x.Key,x=>x.Value),
        extraQaMetadata:new(){["DurationStat"]=644,["ShadowBuff"]=1117,["ShadowGrantParent"]=290,["InvisibilityGrantParent"]=175,["OwnedBelt"]=460,["BaselineSeconds"]=130,["WithShadowSeconds"]=133,["ExpectedMemory"]=46,["RequiresNativeShadowActionsVisibilityGate"]=true,["NativeActionsVisibilityProven"]=false,["NoRngControls"]=true},
        readyMessages:new[]{"First verify actual granted Shadow1117 is visible in native Actions after loading normal290. If absent, STOP/HOLD; do not invoke hidden1117 directly or claim native proof. Read-only --fixture-plan does not reset. CSR110: use the Inventory belt GO460 bag40 via Actions > Tools > double-click Invisibility Belt; require actual1116 cast/modifiers before proceeding. Bag Activate Item is not authored for this tool;8151 only changes target. If Tools omits the belt or no1116 is sent, STOP/HOLD. Baseline no1117:130s natural OFF. Then actual visible Actions > Shadow class buff1117 (20IS), observe644+3; normal same belt:133s natural OFF. Observe actual1116 own timer ready before second belt activation; current30s starts on use and normally ends during130s baseline, no term30 timer claimed. Preserve1116 50IS/3s cast/30s reuse. QAFriend logged in2m east as friendly observer, no NPC/RNG/state injection.629/explicit remaining-time overrides remain unchanged. See docs/ABILITY-INVISIBILITY-DURATION.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer=await HeadlessPlayer.LoginAsync(context.Address,"QAFriend","QAFriend");context.Helpers.Add("QAFriend",observer);await observer.EnterWorldAsync();
        byte slot=GhostStrikeInvisibilityTool.OwnedSlot(observer.CharacterData[5]);var archived=GhostStrikeInvisibilityTool.AbilityIds(observer.CharacterData[7]);var loaded=GhostStrikeInvisibilityTool.AbilityIds(observer.CharacterData[6]);
        Check.That(new ushort[]{175,290}.All(loaded.Contains),"Normal prerequisite parents loaded");Check.That(!archived.Contains((ushort)1116)&&!archived.Contains((ushort)1117),"Hidden grants are not archived");
        foreach(var grant in new[]{"80 b2 5c 04 00 00 08 02","80 b2 5d 04 00 00 08 02"})await observer.WaitUntilAsync(()=>observer.World.RpcSnapshot().Any(r=>r.Bytes.SequenceEqual(WireFormat.FromHex(grant))),"actual normal hidden grant "+grant,TimeSpan.FromSeconds(10));
        return JsonSerializer.SerializeToUtf8Bytes(new{Mode=Name,Slot=slot,Margin05=Convert.ToHexString(observer.CharacterData[5]),Margin06=Convert.ToHexString(observer.CharacterData[6]),Margin07=Convert.ToHexString(observer.CharacterData[7]),Rpc=observer.World.RpcSnapshot(),Views=observer.World.ViewSnapshot().Select(Convert.ToHexString),NativeActionsVisibilityProven=false,NoActivationBeforeNativeVisibilityGate=true});
    }
}
