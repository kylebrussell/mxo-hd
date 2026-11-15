using HeadlessClient;
namespace NativeQa.Modes;

public sealed class CombatInsightMode:NativeQaMode
{
    public override string Name=>"combat-insight-engagement";
    public override NativeQaModePlan Plan {get;}=new(CombatInsightFixture.Abilities,17000,2300,
        Array.Empty<string>(),new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147599360"},
        new[]{"UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=0,x=CASE WHEN charId=6114 THEN 17120 ELSE 17000 END,y=495,z=2300,healthC=20000,healthM=20000,innerStrC=2000,innerStrM=2000 WHERE charId IN(110,6111,6112,6113,6114)"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["EngagementPolicy"]="OURS deterministic scores, only new player interlock against current217; no shield HP/retail probability",
            ["Native113InitiallyLoaded"]=false,["CombatRngOverride"]=false,["Gaussian49Control"]="Normal helper self-cast, direct520+25 raises evade110->135"},
        readyMessages:new[]{"OURS113/49 entry: choose Power against Speed QAEnemy (normal217 already on). With113unloaded force100<110 refuses; load113 via Hardline/LA then110==110 admits. Withdraw with normal helper stop too. Request QAEnemy normal49self-cast;110<135 refuses. Choose Grab;220>=135 admits. No RNG/cost/timer override or retail contest claim. See docs/ABILITY-COMBAT-INSIGHT.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var target=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",target);
        await PvpOpponentsFixture.EnterAsync(target);
        target.SendRpc("80 b9 d9 00 02 00");
        var state=await target.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 d9 00 00 00 70 02 ?? ?? ?? ?? 00",TimeSpan.FromSeconds(10));
        return state.Bytes;
    }
}
