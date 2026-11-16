using HeadlessClient;
namespace NativeQa.Modes;
public sealed class ThrownAccuracyMode:NativeQaMode
{
    public override string Name=>"thrown-accuracy-bonus";
    public override NativeQaModePlan Plan {get;}=new(ThrownAccuracyFixture.Abilities,17000,2300,Array.Empty<string>(),
        new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147651584"},
        new[]{PvpOpponentsFixture.NativeSql,"UPDATE characters SET healthC=20000,healthM=20000 WHERE charId IN(110,6114)"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["ThrownAccuracyPolicy"]="OURS level*1145/100 truncated once plus separately capped direct1032; native coefficient unresolved",["WeakPointsInitiallyLoaded"]=false,["RandomOverrides"]=false,["HelperPolicy"]="QAEnemy ordinaryBlock; natural combat RNG"},
        readyMessages:new[]{"Use normal Actions SpyStyle1112, interlock QAEnemy (ordinaryBlock), queue LegSweep134. WeakPoints164 off/on/off via ordinary Hardline/LA loadout with parents unchanged: typed score66/71/66 at50, direct1032 remains66. No required hit or damage difference; actual queued input logs and RPC47 prove wiring. Optional read-only ?combatdepth specialattack134 is not live RNG. Do not seed stats/force hits. See docs/ABILITY-THROWN-ACCURACY-BONUS.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);enemy.SendRpc("42 03 00 00 00 00 00");
        return(await enemy.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 f5 03 00 00 f4 03 ?? ?? ?? ?? 00")).Bytes;
    }
}
