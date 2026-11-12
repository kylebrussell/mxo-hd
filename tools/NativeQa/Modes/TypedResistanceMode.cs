using HeadlessClient;
namespace NativeQa.Modes;
public sealed class TypedResistanceMode : NativeQaMode
{
    public override string Name=>"typed-resistance";
    public override NativeQaModePlan Plan {get;}=new(TypedResistanceFixture.Abilities,23828,13984,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
        DevCommands:true,environmentOverrides:new(){["MXO_TYPED_RESISTANCE_QA"]="1",["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"]="65"},
        readyMessages:new[]{"Typed resistance OURS: QAEnemy normal330 damage424 and1135 rawdraw65 (loaded1112 influence29 gives83 before resistance). Self8/5 provide75 viral/thrown for20s. No combat/hit/callback RNG override. See docs/ABILITY-TYPED-RESISTANCE.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in context.HelperNames)
        {
            var helper=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,helper);await helper.EnterWorldAsync();
        }
        await ConcentrationFixture.PrepareEnemyAsync(context.Helpers["QAEnemy"]);
        return Array.Empty<byte>();
    }
}
