using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public abstract class ConcentrationFixtureMode : NativeQaMode
{
    protected ConcentrationFixtureMode(bool loaded)
    {
        Plan=new(ConcentrationFixture.Abilities(loaded),23828,13984,Array.Empty<string>(),Array.Empty<string>(),
            new[]{"UPDATE characters SET alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=1000,healthM=1000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
            DevCommands:true,Bootstrap:"concentration-damage-helper",
            environmentOverrides:new(){["MXO_CAST_INTERRUPT_QA_ROLL"]="85",["MXO_CONCENTRATION_QA_DAMAGE"]="1"},
            extraQaMetadata:new(){["ConcentrationQa"]=true,["ConcentrateLoaded"]=loaded,["ConcentrationRoll"]=85,
                ["ConcentrationCastAbility"]=360,["ConcentrationCastSeconds"]=4,["ConcentrationPreparationCommand"]="?pvpflag on",["ConcentrationDamageControl"]="?combatdepth hurtme 100 next",["ConcentrationDamageAmount"]=100,["ConcentrationDamageAfterCastSeconds"]=1,["ConcentrationCastTargetHelper"]="QAEnemy"},
            hotbarAbilities:new ushort[] { 1006, 360 },
            readyMessages:new[]{"Select Hacker Style on page 2, then QAEnemy. Turn PvP on with ?pvpflag on, arm ?combatdepth hurtme 100 next, and use Insidious Code on slot 2 or ?combatdepth cast 360. The arm waits for that cast and applies 100 self damage after one second."});
    }
    public override NativeQaModePlan Plan {get;}
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in context.HelperNames)
        {
            var helper=await HeadlessPlayer.LoginAsync(context.Address,name,name);
            context.Helpers.Add(name,helper);await helper.EnterWorldAsync();
        }
        await ConcentrationFixture.PrepareEnemyAsync(context.Helpers["QAEnemy"]);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"concentration-ready.json"),JsonSerializer.Serialize(new {
            CastTargetHelper="QAEnemy",PreparationCommand="?pvpflag on",DamageControl="?combatdepth hurtme 100 next",DamageAmount=100,DamageAfterCastSeconds=1,CastAbility=360,CastSeconds=4,Roll=85,ConcentrateLoaded=Plan.ExtraQaMetadata["ConcentrateLoaded"].GetBoolean(),At=DateTime.UtcNow}));
        return Array.Empty<byte>();
    }
}
public sealed class ConcentrationMode : ConcentrationFixtureMode
{
    public ConcentrationMode():base(true){}
    public override string Name=>"concentration";
}
