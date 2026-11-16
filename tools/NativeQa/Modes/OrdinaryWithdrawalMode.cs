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
        var server=context.ActualServer??throw new InvalidOperationException("Actual owned server required for Block state proof");
        if(context.OwnedDatabase==null||server.DatabaseName!=context.OwnedDatabase.Name||!context.OwnedDatabase.Name.StartsWith(ThrowawayDatabase.Prefix,StringComparison.Ordinal)||server.ConfiguredAddress!=context.Address||server.HasExited)
            throw new InvalidOperationException("Actual Block helper server/schema/address differs");
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");context.Helpers.Add("QAEnemy",enemy);Check.Equal(6114u,enemy.CharId,"Actual owned QAEnemy identity");
        await PvpOpponentsFixture.EnterAsync(enemy);int cursor=enemy.World.RpcSnapshot().Count,log=server.DebugLog().Length;bool passed=false;Exception? failure=null;byte[]? ack=null;
        try
        {
            enemy.SkipReceived();enemy.SendRpc("42 03 00 00 00 00 00");
            ack=(await enemy.WaitForRpcAsync(r=>WithdrawalFixture.BlockRecord(r.Bytes),"Fresh complete1013→1012=Block modifier",TimeSpan.FromSeconds(10))).Bytes;
            await enemy.WaitUntilAsync(()=>server.DebugLog()[log..].Contains("[COMBAT] Tactic changed to Block (Normal)"),"Actual server assigned ordinaryBlock after42",TimeSpan.FromSeconds(10));
            Check.Equal(0,enemy.World.Problems.Count,"Actual helper parser healthy");passed=true;return ack;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Directory.CreateDirectory(context.OutputDirectory);
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"ordinary-block-hook-proof.json"),System.Text.Json.JsonSerializer.Serialize(new{Passed=passed,Failure=failure?.ToString(),Character=enemy.CharId,RpcCursor=cursor,Request="42030000000000",Ack=ack==null?null:Convert.ToHexString(ack),
                Modifier=ack==null?null:InvisibilityDurationFixture.Read(ack),ServerStateLog=server.DebugLog()[log..],ServerPid=server.ProcessId,Schema=server.DatabaseName,server.SourceAssemblySha256,Rpc=enemy.World.RpcSnapshot().Select(r=>r.Hex),Problems=enemy.World.Problems},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
