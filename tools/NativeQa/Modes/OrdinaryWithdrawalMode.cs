using HeadlessClient;
namespace NativeQa.Modes;
public sealed class OrdinaryWithdrawalMode:NativeQaMode
{
    public override string Name=>WithdrawalFixture.Name;
    public override NativeQaModePlan Plan {get;}=new(WithdrawalFixture.Abilities,17000,2300,Array.Empty<string>(),
        Array.Empty<string>(),
        new[]{PvpOpponentsFixture.NativeSql,"UPDATE characters SET alignment=1,x=17000,y=495,z=2300,healthC=20000,healthM=20000 WHERE charId=110;UPDATE characters SET alignment=2,x=17120,y=495,z=2300,healthC=20000,healthM=20000 WHERE charId=6114"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(WithdrawalFixture.Environment),
        extraQaMetadata:new(){["WithdrawalPolicy"]="Primary direct1005/recovered tactic factors; OURS ordinary44 caller/existinglevelbaseline; no TF6 change",["Linked1045Consumed"]=false,["RandomOverrides"]=false},
        readyMessages:new[]{"Seven normal parents stay loaded. Enter CloseCombat with ordinaryPower vs QAEnemyBlock. AFTER each engagement select/verify requested active style (radialCC may reset17):17 then1112 then17. Direct1005=65/72/65; chooseBlock then normalWithdraw; bases115/122/115. Use actual Actions/style hotbar and leftCloseCombat picker; do not assume pre-entrySpyStyle survives radialCC. No1045coefficient or producer changes, stats/reset/devcontrol/RNG override. docs/ABILITY-ORDINARY-WITHDRAWAL.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",enemy);await PvpOpponentsFixture.EnterAsync(enemy);
        enemy.SkipReceived();enemy.SendRpc("42 03 00 00 00 00 00");return (await enemy.WaitForRpcAsync("80 bc ?? ?? f5 03 00 80 f4 03 00 80")).Bytes;
    }
}
