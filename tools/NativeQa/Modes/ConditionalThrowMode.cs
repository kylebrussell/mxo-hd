using HeadlessClient;
namespace NativeQa.Modes;
public sealed class ConditionalThrowMode:NativeQaMode
{
    public override string Name=>"conditional-thrown-damage";
    public override NativeQaModePlan Plan {get;}=new(ConditionalThrowFixture.Abilities,24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
        DevCommands:true,environmentOverrides:new(){["MXO_CONDITIONAL_THROW_QA"]="1"},
        extraQaMetadata:new(){["ConditionalThrowAbilities"]="279,282",["PreparationOnly"]="selected visible target NPCOffBalance885/200; no player ability grant",["RandomOverrides"]=false},
        readyMessages:new[]{"Normal Actions Deadly Throw279/Blinding Throw282 on QAEnemy: no Off Balance means no conditional hit. Daze alone also gives none. Select target then ?combatdepth offbalance 30; normal279 adds value45 (Spy Style29% gives58 before resistance),282 adds24 (30 after same influence). Stored health/DOT base remains current policy. These stored2s casts currently bypass the round queue, including while interlocked; no hit/base-damage/DOT/callback RNG override. See docs/ABILITY-CONDITIONAL-THROW.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var helper=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",helper);await helper.EnterWorldAsync();await ConcentrationFixture.PrepareEnemyAsync(helper);
        return Array.Empty<byte>();
    }
}
