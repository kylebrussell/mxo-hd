using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public sealed class DebuffRadiusMode : NativeQaMode
{
    public override string Name => "debuff-radius";
    public override NativeQaModePlan Plan {get;} = new(DebuffRadiusFixture.Abilities,DebuffRadiusFixture.NativeCasterX,DebuffRadiusFixture.NativeZ,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,alignment=2,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,y=495,rotation=0 WHERE charId=110",
            DebuffRadiusFixture.NativeTargetsSql(6111,6112,6113,6114)},
        DevCommands:true,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_DEBUFF_RADIUS_QA"]="1",["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]="32766",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="15636"},
        extraQaMetadata:new(){["FreeCastViralContestQa"]=true,["FreeCastViralAttackRand15"]=32766,["FreeCastViralDefenseRand15"]=15636},
        readyMessages:new[]{"Isolated radius fixture: CSR17000/495/2300, QAEnemy17500/495/2300; QAFriend3.8m/QAFar4.4m/QAOther4.99m. Existing PvP server mode auto-flags level50 on world entry; no dev flag injection. Dev is enabled only for strict disposable contest draws; generic callback/combat/damage RNG remain normal. Helpers logged in.333 base4m,345 captured+21 gives4.84m. Check [HOSTILE][AREA-QA] for actual org/flag/team and recipient reasons. No optional WinterFrost target. See docs/ABILITY-DEBUFF-RADIUS.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var evidence=new List<object>();
        foreach(string name in context.HelperNames)
        {
            var p=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,p);evidence.Add(await DebuffRadiusFixture.EnterFlaggedAsync(p));
        }
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"debuff-radius-helpers-ready.json"),JsonSerializer.Serialize(new{PvpServer=true,MaxSafeLevel=15,Helpers=evidence,DevCommands=true,ForcedCallbackRolls=false,ForcedViralContest=true,ViralAttackRand15=32766,ViralDefenseRand15=15636},new JsonSerializerOptions{WriteIndented=true}));
        return Array.Empty<byte>();
    }
}
