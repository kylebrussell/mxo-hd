using HeadlessClient;
namespace NativeQa.Modes;
public sealed class GhostStrikeMode:NativeQaMode
{
    public override string Name=>"ghost-strike-callback";
    public override NativeQaModePlan Plan {get;}=new(GhostStrikeFixture.Abilities,GhostStrikeFixture.CsrX,GhostStrikeFixture.CsrZ,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)",
            "UPDATE characters SET x=24800,y=495,z=-1498,rotation=0 WHERE charId=110",
            "UPDATE characters SET x=25200,y=495,z=-1498,rotation=0 WHERE charId=6114",
            "UPDATE characters SET x=24800,y=495,z=-1098,rotation=0 WHERE charId=6111"},
        DevCommands:true,environmentOverrides:new(){["MXO_GHOST_STRIKE_QA"]="1",["MXO_GHOST_STRIKE_QA_ROLL"]=""},
        extraQaMetadata:new(){["GhostStrikeScope"]="365 Subject callback only; current cast/hit/damage policy; no retail Test timing claim",["CallbackOnlyRolls"]="CSR110 ?ghoststrike 8 / ?ghoststrike 9",["CsrPose"]="24800/495/-1498",["EnemyPose"]="QAEnemy 25200/495/-1498,4m east; separate from WinterFrost stack",["ObserverPose"]="QAFriend 24800/495/-1098,4m north; actually logged in",["ImpartedLimit"]="366 requires character99: unit-only active-state coverage, no forced native grant"},
        readyMessages:new[]{"Normal Actions: Spy Style1112, Invisibility1116, Ghost Strike365 on QAEnemy at25200/495/-1498 (4m east). QAFriend is logged in at24800/495/-1098 (4m north) as friendly observer. ?ghoststrike 8 forces callback success; ?ghoststrike 9 forces failure, only after accepted hit. Observe ordinary break during1.5s cast then1116 restored only for ORIGINAL remaining expiry on8, no restoration on9. Respect365 normal10s reuse AND1116 normal30s term cooldown. No whole-cast/throw/damage RNG control; no retail timing/complete365 parity claim. See docs/ABILITY-GHOST-STRIKE.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var helper=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",helper);await helper.EnterWorldAsync();await ConcentrationFixture.PrepareEnemyAsync(helper);
        var observer=await HeadlessPlayer.LoginAsync(context.Address,"QAFriend","QAFriend");
        context.Helpers.Add("QAFriend",observer);await observer.EnterWorldAsync();
        return Array.Empty<byte>();
    }
}
