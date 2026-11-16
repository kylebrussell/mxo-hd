using HeadlessClient;
namespace NativeQa.Modes;
public sealed class OrdinaryWithdrawalMode:NativeQaMode
{
    public override string Name=>WithdrawalFixture.Name;
    public override NativeQaModePlan Plan {get;}=new(WithdrawalFixture.Abilities,17000,2300,Array.Empty<string>(),
        new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147606528"},
        new[]{PvpOpponentsFixture.NativeSql,"UPDATE characters SET alignment=1,x=17000,y=495,z=2300,healthC=20000,healthM=20000 WHERE charId=110;UPDATE characters SET alignment=2,x=17120,y=495,z=2300,healthC=20000,healthM=20000 WHERE charId=6114"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(WithdrawalFixture.Environment),
        extraQaMetadata:new(){["WithdrawalPolicy"]="OURS ordinary44 raw1005+level-percent1045; recovered tactic factors; no TF6 change",["InitiallyLoaded120"]=false,["RandomOverrides"]=false},
        readyMessages:new[]{"Normal SpyStyle1112 and QAEnemy Block; enter CloseCombat using ordinaryPower, then selectBlock and normalWithdraw. Off120 gives direct1005=72/input72/base122. Withdraw both naturally, load onlyDisguise120 through Hardline/LA; fixedparents:1045=25/input84/base134. Unload120 and repeat122. Natural rolls, no forced hits or win-rate/damage claim. TF6 escapes unchanged. docs/ABILITY-ORDINARY-WITHDRAWAL.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",enemy);await PvpOpponentsFixture.EnterAsync(enemy);
        enemy.SkipReceived();enemy.SendRpc("42 03 00 00 00 00 00");return (await enemy.WaitForRpcAsync("80 bc ?? ?? f5 03 00 80 f4 03 00 80")).Bytes;
    }
}
