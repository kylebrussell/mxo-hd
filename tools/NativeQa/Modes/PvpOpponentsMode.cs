using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public sealed class PvpOpponentsMode:NativeQaMode
{
    public override string Name=>"pvp-opponents";
    public override NativeQaModePlan Plan {get;}=new(PvpOpponentsFixture.Abilities,PvpOpponentsFixture.X,PvpOpponentsFixture.Z,
        Array.Empty<string>(),Array.Empty<string>(),new[]{PvpOpponentsFixture.NativeSql},DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15"},
        extraQaMetadata:new(){["PvpOpponentFixture"]=true,["NativeCsrEntryProven"]=false,["SeededPvpFlag"]=0,["OrdinaryServerMode"]=6},
        readyMessages:new[]{"CSR110 alone: existing hostile-server entry should auto-flag level50; helpers already entered and have recorded flags/settings. QAFriend same-org must not be offered as a legal opponent; the client may suppress its attack action (headless checks4d24). QAEnemy is different-org and should allow ordinary close combat/opening HP loss without a duel. For deterministic NORMAL withdrawal, press Withdraw and use the existing helper request {\"actor\":\"QAEnemy\",\"action\":\"stop\"}; both ordinary44 requests end on the next round. No /duel, flag/dev command or fixed RNG. CSR entry/native targetability remain unproven until observed. This does not prove205 Powerless on players. See docs/NATIVEQA-PVP-OPPONENTS.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var entries=new List<object>();var observers=new List<object>();
        foreach(string name in context.HelperNames)
        {
            var player=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,player);entries.Add(await PvpOpponentsFixture.EnterAsync(player));
        }
        //Helpers only: waiting for CSR before Ready would deadlock the native startup workflow.
        foreach(var observer in context.Helpers.Values)foreach(var target in context.Helpers.Values.Where(p=>p!=observer))
            observers.Add(new{Observer=observer.Handle,Target=target.Handle,View=PvpOpponentsFixture.Observe(await observer.WaitForViewNamedAsync(target.Handle),(byte)(target.Handle=="QAEnemy"?2:1),target.CharId)});
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"pvp-opponents-helpers.json"),JsonSerializer.Serialize(new{Entries=entries,Observers=observers,NativeCsrEntryProven=false},new JsonSerializerOptions{WriteIndented=true}));
        return Array.Empty<byte>();
    }
}
