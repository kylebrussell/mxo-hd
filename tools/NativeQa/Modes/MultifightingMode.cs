using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MultifightingMode:NativeQaMode
{
    public override string Name=>"multifighting-interlock";
    public override NativeQaModePlan Plan {get;}=new(MultifightingFixture.Abilities,17000,2300,Array.Empty<string>(),
        new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147627008"},
        new[]{PvpOpponentsFixture.NativeSql,"UPDATE characters SET alignment=2,x=17000,y=495,z=2300,healthC=20000,healthM=20000 WHERE charId=110;UPDATE characters SET alignment=1,x=17090,y=495,z=2300,healthC=20000,healthM=20000 WHERE charId IN(6111,6112,6113,6114)"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["MultifightingPolicy"]="Primary penalty math; OURS threshold1/distinct current direct PvP links; arbitrary multi-NPC absent",["InitiallyLoaded140"]=false,["RandomOverrides"]=false},
        readyMessages:new[]{"Multifighting140: helpers are ordinary flagged players. Have QAFriend then QAFar melee CSR_Tester through supported helper requests. Two direct opponents: off-10/on0; add QAOther for three: off-15/on-5. Withdraw all normally before loading/unloading140 through Hardline/LA; keep all parents unchanged. Natural hit/damage RNG, deterministic input logs only. docs/ABILITY-MULTIFIGHTING.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in context.HelperNames){var p=await HeadlessPlayer.LoginAsync(context.Address,name,name);context.Helpers.Add(name,p);await PvpOpponentsFixture.EnterAsync(p);}
        return Array.Empty<byte>();
    }
}
