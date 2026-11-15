using HeadlessClient;
namespace NativeQa.Modes;
public sealed class GhostStrikeMode:NativeQaMode
{
    public override string Name=>"ghost-strike-callback";
    public override NativeQaModePlan Plan {get;}=new(GhostStrikeFixture.Abilities,GhostStrikeFixture.CsrX,GhostStrikeFixture.CsrZ,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)",
            "UPDATE characters SET x=24800,y=495,z=-1498,rotation=0 WHERE charId=110",
            "UPDATE characters SET x=25200,y=495,z=-1498,rotation=0 WHERE charId=6114",
            "UPDATE characters SET x=24800,y=495,z=-1098,rotation=0 WHERE charId=6111",
            "DELETE FROM inventory WHERE charId IN (110,6111) AND slot=40; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0),(6111,460,40,1,0)"},
        DevCommands:true,environmentOverrides:new(){["MXO_GHOST_STRIKE_QA"]="1",["MXO_GHOST_STRIKE_QA_ROLL"]=""},
        extraQaMetadata:new(){["GhostStrikeScope"]="365 Subject callback only; current cast/hit/damage policy; no retail Test timing claim",["CallbackOnlyRolls"]="CSR110 ?ghoststrike 8 / ?ghoststrike 9",["CsrPose"]="24800/495/-1498",["EnemyPose"]="QAEnemy 25200/495/-1498,4m east; separate from WinterFrost stack",["ObserverPose"]="QAFriend 24800/495/-1098,4m north; actually logged in",["InvisibilityPath"]="Loaded175 normal prerequisite closure; Actions > Tools > double-click owned Invisibility Belt460 (bag40);1116 hidden, never directly archived",["ImpartedLimit"]="366 requires character99: unit-only active-state coverage, no forced native grant"},
        readyMessages:new[]{"Normal Actions: Spy Style1112 and Ghost Strike365. For Invisibility use Actions > Tools > double-click Invisibility Belt GO460 (owned bag40); require actual1116 activation,50IS debit and observed130s modifier before testing365 (normal loaded175 grants hidden1116); no standalone1116 Actions entry is claimed. Ghost Strike365 on QAEnemy at25200/495/-1498 (4m east). QAFriend is logged in at24800/495/-1098 (4m north) as friendly observer. ?ghoststrike 8 forces callback success; ?ghoststrike 9 forces failure, only after accepted hit. Observe ordinary break during1.5s cast then1116 restored only for ORIGINAL remaining expiry on8, no restoration on9. Respect365 normal10s reuse AND1116 normal30s reuse (wait for the actual timer-ready indication). No whole-cast/throw/damage RNG control; no retail timing/complete365 parity claim. See docs/ABILITY-GHOST-STRIKE.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        // Prove the identical ordinary owned-tool path on QAFriend before a hostile logs in.
        // CSR remains reserved for native login; the observer is toggled off before ready.
        var observer=await HeadlessPlayer.LoginAsync(context.Address,"QAFriend","QAFriend");
        context.Helpers.Add("QAFriend",observer);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"QAFriend-margin-before-validation.json"),System.Text.Json.JsonSerializer.Serialize(
            observer.CharacterData.ToDictionary(k=>(int)k.Key,v=>Convert.ToHexString(v.Value)),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        await observer.EnterWorldAsync();
        var grant=await GhostStrikeInvisibilityTool.ActivateAsync(observer);
        await GhostStrikeInvisibilityTool.DeactivateAsync(observer);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"invisibility-belt-ready.json"),System.Text.Json.JsonSerializer.Serialize(new{
            Observer="QAFriend",Grant=grant,Off=true,CsrActivated=false,Scope="Actual helper ordinary belt activation, not native UI proof"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        var helper=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",helper);await helper.EnterWorldAsync();await ConcentrationFixture.PrepareEnemyAsync(helper);
        return Array.Empty<byte>();
    }
}
