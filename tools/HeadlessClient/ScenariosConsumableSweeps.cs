namespace HeadlessClient;

public static partial class Scenarios
{
    // Actual 80 b9 inventory form: byte35 bag slot, byte36 IsItem.
    private static void CastSweepItem(HeadlessPlayer player, ushort ability, byte slot)
    {
        var body = new byte[41];
        BitConverter.GetBytes(ability).CopyTo(body, 0);
        body[2] = 2; body[34] = 8; body[35] = slot; body[36] = 1;
        player.SendRpc("80 b9 " + BitConverter.ToString(body).Replace('-', ' '));
    }

    private static async Task ConsumableSweepItems(ScenarioContext context)
    {
        var p = (await context.AddPlayersInWorldAsync(new CharacterSeed("SweepItems") {
            Level = 50, Items = new[] { (3,39410u,2), (4,39409u,1), (5,38539u,2) }
        }))[0];
        CastSweepItem(p, 678, 3);
        await p.WaitForRpcAsync("80 ac a6 02");
        // A sibling item cannot bypass the family reservation during the first cast.
        p.SendRpc("80 c6 04 00");
        await p.WaitForRpcAsync("80 bd 01 f2 99 00 00 00 00 41 01");
        await p.WaitForRpcAsync(ModPattern(678,678));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=3"),"Antibiotic consumes exactly one stack unit");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=4"),"Family reservation refuses sibling during cast");
        // 80 c6 normal right-click path reaches the same sweep executor.
        p.SendRpc("80 c6 05 00");
        await p.WaitForRpcAsync("80 ac 93 02");
        await p.WaitForRpcAsync(ModPattern(659,659));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=5"),"Condition sweep consumes exactly one stack unit");
        await p.WaitForRpcAsync(ModPattern(659,659,true),TimeSpan.FromSeconds(24));
        Check.Equal(0,p.World.Problems.Count,"Consumable sweep wire protocol");
    }

    private static async Task ConsumableSweepPermissions(ScenarioContext context)
    {
        var low = (await context.AddPlayersInWorldAsync(new CharacterSeed("LowSweep") {
            Level = 1, Items = new[] { (3,39410u,2) }
        }))[0];
        low.SendRpc("80 c6 03 00");
        await low.WaitForTextAsync("character level is too low");
        Check.Equal(2L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={low.CharId} AND slot=3"),"Character requirement refuses without consuming");
        Check.Equal(0,low.Received("80 ac a6 02").Count,"Level refusal starts no cast");
        Check.Equal(0,low.Received(ModPattern(678,678)).Count,"Level refusal adds no sweep");
        Check.Equal(0,low.World.Problems.Count,"Consumable refusal protocol");
    }
    private static async Task ConsumableSweepControlCancellation(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("PillController") { Level=50, Organization=1 },
            new CharacterSeed("PillVictim") { Level=50, Organization=2, X=17120, Items=new[] { (3,39410u,2) } });
        var caster=players[0]; var victim=players[1];
        foreach(var p in players) { p.SendChat("?pvpflag on"); await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn); }
        var view=await caster.WaitForViewNamedAsync(victim.Handle);
        Cast(caster,315,view.ViewId);
        await caster.WaitForRpcAsync("80 ac 3b 01");
        await Task.Delay(3500);
        victim.SendRpc("80 c6 03 00");
        await victim.WaitForRpcAsync("80 ac a6 02");
        await victim.WaitForRpcAsync(ModPattern(315,102),TimeSpan.FromSeconds(10));
        await victim.WaitUntilAsync(()=>context.Server.DebugLog().Contains("cast cancelled"),"Incapacitation cancels pending consumable",TimeSpan.FromSeconds(5));
        Check.Equal(2L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={victim.CharId} AND slot=3"),"Interrupted cast preserves stack");
        Check.Equal(0,victim.Received(ModPattern(678,678)).Count,"Interrupted cast creates no sweep");
        victim.SendRpc("80 c6 03 00");
        await victim.WaitForRpcAsync(ModPattern(315,102,true),TimeSpan.FromSeconds(10));
        Check.Equal(2L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={victim.CharId} AND slot=3"),"Stunned right-click cannot consume");
        victim.SendRpc("80 c6 03 00");
        await victim.WaitForRpcAsync(ModPattern(678,678));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={victim.CharId} AND slot=3"),"Cancelled reservation releases for legal retry");
        Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Consumable control cancellation protocol");
    }

}
