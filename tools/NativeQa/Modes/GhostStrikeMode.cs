using HeadlessClient;
namespace NativeQa.Modes;
public sealed class GhostStrikeMode:NativeQaMode
{
    public override string Name=>"ghost-strike-callback";
    public override NativeQaModePlan Plan {get;}=new(GhostStrikeFixture.Abilities,24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
        DevCommands:true,environmentOverrides:new(){["MXO_GHOST_STRIKE_QA"]="1",["MXO_GHOST_STRIKE_QA_ROLL"]=""},
        extraQaMetadata:new(){["GhostStrikeScope"]="365 Subject callback only; current cast/hit/damage policy; no retail Test timing claim",["CallbackOnlyRolls"]="CSR110 ?ghoststrike8/9",["ImpartedLimit"]="366 requires character99: unit-only active-state coverage, no forced native grant"},
        readyMessages:new[]{"Normal Actions: Spy Style1112, Invisibility1116, Ghost Strike365 on QAEnemy. ?ghoststrike8 forces callback success;9 forces failure, only after accepted hit. Observe ordinary break during1.5s cast then1116 restored only for ORIGINAL remaining expiry on8, no restoration on9. Wait normal10s reuse. No whole-cast/throw/damage RNG control; no retail timing/complete365 parity claim. See docs/ABILITY-GHOST-STRIKE.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var helper=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",helper);await helper.EnterWorldAsync();await ConcentrationFixture.PrepareEnemyAsync(helper);
        return Array.Empty<byte>();
    }
}
