using HeadlessClient;
namespace NativeQa.Modes;

public sealed class TransmitCodeMode:NativeQaMode
{
    public override string Name=>"transmit-code-resistance";
    public override NativeQaModePlan Plan {get;}=new(TransmitCodeFixture.DefenderAbilities,17000,2300,
        Array.Empty<string>(),new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2148090880"},
        new[]{"UPDATE characters SET level=50,pvpflag=0,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,x=CASE WHEN charId=6114 THEN 17120 ELSE 17000 END,y=495,z=2300,healthC=20000,healthM=20000,innerStrC=2000,innerStrM=2000 WHERE charId IN(110,6111,6112,6113,6114)",TransmitCodeFixture.CasterSql(6114)},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_PACKET_LOG"]="1",
            ["MXO_TYPED_RESISTANCE_QA"]="1",["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"]="65",["MXO_FREECAST_VIRAL_QA"]="1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"]="32766",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="0"},
        extraQaMetadata:new(){["TransmitCodePolicy"]="OURS1146/1148 percent to points on4*level+20, once through existing typed curve; no retail coefficient claim",
            ["Native593InitiallyLoaded"]=false,["CasterLoadout"]=string.Join(",",TypedResistanceFixture.Abilities),["ScopedControls"]="Existing1135raw65 and eligible TF2attack32766/defense0 only; no global combat/callback RNG"},
        readyMessages:new[]{"OURS Transmit Code593: normal Hardline/LA load/unload; all prerequisite parents remain fixed. QAEnemy normal Hacker-style330 baseline424, loaded593401; Spy-style1135 baseline83, loaded59378. Loaded50 gives1146/1148+10 each =>22points. Helper474 DOT also uses current bonus. Existing scoped controls only. See docs/ABILITY-TRANSMIT-CODE.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var caster=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",caster);
        await PvpOpponentsFixture.EnterAsync(caster);caster.SendRpc("80 b9 ee 03 02 00");
        var style=await caster.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 ee 03 00 00 ac 00 ?? ?? ?? ?? 00",TimeSpan.FromSeconds(10));
        return style.Bytes;
    }
}
