using HeadlessClient;
namespace NativeQa.Modes;
public sealed class DeflectCodeMode:NativeQaMode
{
    public override string Name=>"deflect-code-duration";
    public override NativeQaModePlan Plan {get;}=new(DeflectCodeFixture.DefenderAbilities,17000,2300,
        Array.Empty<string>(),new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147507200"},
        new[]{PvpOpponentsFixture.NativeSql,DeflectCodeFixture.CasterSql(6114)},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_PACKET_LOG"]="1",
            ["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]="32766",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="0"},
        extraQaMetadata:new(){["DeflectCodePolicy"]="OURS positive1047 percent shortening of new known hostile controls after raw575, total33% floor; retail coefficient unresolved",
            ["Native23InitiallyLoaded"]=false,["CasterAbilities"]=string.Join(",",DeflectCodeFixture.CasterAbilities),["StunAbility"]=315,["BaselineSeconds"]=7,["LoadedSeconds"]=6.3,["ImmunitySeconds"]=14},
        readyMessages:new[]{"OURS Deflect Code23: CSR110 normal Hardline/LA load/unload; keep parents fixed. QAEnemy normal Hacker1006 then315: off7s/on6.3s/off7s. Each cast retains real734 immunity14s and normal reuse; wait actual immunity removal before another. Never use ?combatdepth stun. See docs/ABILITY-DEFLECT-CODE.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var caster=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",caster);
        await PvpOpponentsFixture.EnterAsync(caster);caster.SendRpc("80 b9 ee 03 02 00");
        return (await caster.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 ee 03 00 00 ac 00 ?? ?? ?? ?? 00",TimeSpan.FromSeconds(10))).Bytes;
    }
}
