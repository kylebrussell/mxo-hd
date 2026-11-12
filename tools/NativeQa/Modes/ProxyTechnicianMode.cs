using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;

/// <summary>Actual pet/enemy fixture, with deterministic QA rolls restricted to Proxy pet attack callbacks.</summary>
public sealed class ProxyTechnicianMode : NativeQaMode
{
    public override string Name => "proxy-technician";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[]{2,17,594,589},23828,13984,
        new string[]{"UPDATE characters SET innerStrC=1000,innerStrM=1000 WHERE charId=110"},
        Array.Empty<string>(),Array.Empty<string>(),DevCommands:true,Bootstrap:"proxy-technician-helper-and-enemy",
        environmentOverrides:new Dictionary<string,string>{{hds.ProxyTechnician.QaRollVariable,"80"}},
        extraQaMetadata:new Dictionary<string,object?>{{"ProxyTechnicianQa",true},{"ProxyPetAttackRoll",80}},
        readyMessages:new[]{"Proxy Technician QA: QAFriend and QAEnemy own real Proxies; level50 Simulacra Dummy is prepared. Summon594 through actual Actions, then compare pet Attack before/after589. Only Proxy pet hit rolls are fixed80 in the disposable fixture."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in context.HelperNames)
        {
            var helper=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,helper);await helper.EnterWorldAsync();
        }
        foreach(string name in new[]{"QAFriend","QAEnemy"})
        {
            var helper=context.Helpers[name];helper.SendRpc("80 b9 52 02 02 00");
            await helper.WaitForRpcAsync("81 76",TimeSpan.FromSeconds(20));
        }
        var friend=context.Helpers["QAFriend"];
        friend.SendChat("?pet dummy 50");await friend.WaitForTextAsync("stands 8 m away");
        var enemy=await friend.WaitForViewNamedAsync("Simulacra Dummy");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"proxy-technician-ready.json"),JsonSerializer.Serialize(new {
            Owner="QAFriend",Summon=594,Enemy="Simulacra Dummy",EnemyLevel=50,EnemyView=enemy.ViewId,Prepared=DateTime.UtcNow}));
        return Array.Empty<byte>();
    }
}
