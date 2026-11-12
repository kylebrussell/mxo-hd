using System.Text.RegularExpressions;
using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task TypedSpecialQueued(ScenarioContext context)
    {
        var attacker=await context.AddPlayerAsync(NearNpc("TypedAttacker") with {Level=50,Organization=1,Health=5000,MaxHealth=5000});
        var defender=await context.AddPlayerAsync(NearNpc("TypedTarget",100) with {Level=50,Organization=2,Health=5000,MaxHealth=5000});
        await LoadAbilitiesAsync(context,attacker,1112,134);
        await LoadAbilitiesAsync(context,defender,1101);
        await attacker.EnterWorldAsync();await defender.EnterWorldAsync();
        var mob=await attacker.WaitForViewNamedAsync("WinterFrost");
        attacker.SendRpc($"40 {HandleOf(mob)} 00 6b");await WaitForCombatView(attacker);
        Cast(attacker,134,mob.ViewId);
        await attacker.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[COMBAT-DEFENCE] NPC special 134 stat 1011"),"NPC queued throw selects thrown defence",TimeSpan.FromSeconds(25));
        Check.That(ViewUpdateValues(attacker,mob.ViewId,NpcHealthUpdate).Count>0,"NPC combat changes HP");
        attacker.SendRpc("44 00");
        await attacker.WaitForRpcAsync("46");
        var target=await attacker.WaitForViewNamedAsync(defender.Handle);
        Cast(defender,1101);await defender.WaitForRpcAsync(ModPattern(1101,1011));
        defender.SendChat("?pvpflag on");await defender.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        attacker.SendRpc($"40 {HandleOf(target)} 00 6b");
        await attacker.WaitForRpcAsync("46");
        // Family timer from the NPC special expires before this second cast.
        await Task.Delay(12500);
        Cast(attacker,134,target.ViewId);
        await attacker.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[COMBAT-DEFENCE] PvP special 134 stat 1011"),"PvP queued throw selects HyperSense defence",TimeSpan.FromSeconds(25));
        Check.That(defender.Received("45").Count>0,"PvP resolves real combat rounds");
        await defender.WaitUntilAsync(()=>OwnHealth(defender)>0 && OwnHealth(defender)<5000,"PvP combat updates authoritative HP",TimeSpan.FromSeconds(15));
        attacker.SendRpc("44 00");
        Check.Equal(0,attacker.World.Problems.Count+defender.World.Problems.Count,"Typed queued defence protocol");
    }

    private static async Task TypedSpecialDefense(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("TypedDefender") { Level=50, Organization=1, InnerStrength=500, MaxInnerStrength=500, Health=5000, MaxHealth=5000 });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId}");
        await LoadAbilitiesAsync(context,player,1101);
        await player.EnterWorldAsync();
        player.SendChat("?combatdepth specialdefense");
        var before = await player.WaitForTextAsync("Special defence 281");
        Cast(player,1101); await player.WaitForRpcAsync(ModPattern(1101,1011));
        player.SendChat("?combatdepth specialdefense");
        var after = await player.WaitForTextAsync("Special defence 281 stat1011 total 47");
        int Landed(RpcRecord message) => int.Parse(Regex.Match(Encoding.UTF8.GetString(message.Bytes), @"seeded landed (\d+)").Groups[1].Value);
        Check.That(Landed(after) < Landed(before), "HyperSense changes production thrown-special hit contests");
        foreach (var record in new[]{before,after})
        {
            var hp=Regex.Match(Encoding.UTF8.GetString(record.Bytes), @"diagnostic HP (\d+)->(\d+)");
            Check.Equal(Landed(record),int.Parse(hp.Groups[1].Value)-int.Parse(hp.Groups[2].Value),"Production damage changes authoritative HP for landed diagnostic contests");
        }
        Check.Equal(0,player.World.Problems.Count,"Typed defence protocol");
    }
}
