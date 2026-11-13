using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public abstract class UiLagAreaMode:NativeQaMode
{
    protected static NativeQaModePlan Fixture(bool hit)=>new(UiLagAreaFixture.Abilities,UiLagAreaFixture.X,UiLagAreaFixture.Z,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,alignment=2,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,y=495,rotation=0 WHERE charId=110",
            UiLagAreaFixture.HelpersSql(6111,6112,6113,6114)},DevCommands:true,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_DEBUFF_RADIUS_QA"]="1",["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]=hit?"32766":"0",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]=hit?"0":"32766"},
        extraQaMetadata:new(){["UiLagCasterAreaQa"]=true,["UiLagRadiusMetres"]=3,["UiLagForcedHit"]=hit},
        readyMessages:new[]{"CSR110 only: ?qaload 97 refreshes the owned ordinary loadout through80ae. Use Actions UI Lag1.0 without selecting an enemy. QAEnemy is2m; QAFar3.01m; QAFriend same-org1m; QAOther is2m above. Existing PvP-server auto-flags level50. "+(hit?"Only QAEnemy should receive the four27s debuffs.":"QAEnemy should deflect, with no debuffs/HP change.")+" Source radius/floor/relationship policy stays ordinary; selected QAFar must not move the centre. Contest-only draws, no other RNG. See docs/ABILITY-UI-LAG-AREA.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var evidence=new List<object>();
        foreach(string name in context.HelperNames)
        {
            var p=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,p);evidence.Add(await DebuffRadiusFixture.EnterFlaggedAsync(p));
        }
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"ui-lag-helpers.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
        return Array.Empty<byte>();
    }
}
public sealed class UiLagAreaHitMode:UiLagAreaMode
{
    public override string Name=>"ui-lag-area-hit";
    public override NativeQaModePlan Plan {get;}=Fixture(true);
}
public sealed class UiLagAreaDeflectMode:UiLagAreaMode
{
    public override string Name=>"ui-lag-area-deflect";
    public override NativeQaModePlan Plan {get;}=Fixture(false);
}
