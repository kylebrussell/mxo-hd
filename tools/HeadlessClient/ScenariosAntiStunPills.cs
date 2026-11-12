namespace HeadlessClient;
public static partial class Scenarios
{
    private static void AntiStunItemUse(HeadlessPlayer player, ushort ability, byte slot)
    {
        byte[] body = new byte[43];
        body[0] = 0x80; body[1] = 0xb9;
        BitConverter.GetBytes(ability).CopyTo(body, 2);
        body[36] = 0x08; body[37] = slot; body[38] = 1;
        player.SendRpc(WireFormat.ToHex(body));
    }

    private static async Task AntiStunPillUse(ScenarioContext context)
    {
        foreach (var pair in new[] { (Ability: (ushort)971, Item: 42763u), (Ability: (ushort)972, Item: 42764u) })
        {
            var p = await context.AddPlayerAsync(new CharacterSeed("AntiStun" + pair.Ability)
            { Level = 50, Items = new[] { (1, pair.Item, 2), (2, pair.Ability == 971 ? 42764u : 42763u, 1), (3, 2399u, 1) } });
            await p.EnterWorldAsync();
            p.SendChat("?combatdepth stun");
            await p.WaitForRpcAsync(ModPattern(315, 102));
            await p.WaitForTextAsync("Combat depth level 5 stun applied");
            Cast(p, pair.Ability); // owning a pill does not authorize a free ability request
            await p.WaitForRpcAsync($"80 ba {Le(pair.Ability)}");
            p.SendRpc("80 c6 03 00"); // an ordinary health pill is still blocked while stunned
            await Task.Delay(350);
            Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=3"), "Ordinary pill remains blocked");
            int castBars = p.Received($"80 ac {Le(pair.Ability)}").Count;
            if (pair.Ability == 971) AntiStunItemUse(p, pair.Ability, 1);
            else p.SendRpc("80 c6 01 00"); // canonical pill action also respects CanUseWhileStunned
            await p.WaitUntilAsync(() => p.Received($"80 ac {Le(pair.Ability)}").Count > castBars, "Owned anti-stun pill casts while stunned", TimeSpan.FromSeconds(3));
            Check.Equal(2L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=1"), "Cast start does not consume");
            await p.WaitUntilAsync(() => p.Received(ModPattern(315, 102, true)).Count > 0 &&
                p.Received($"80 bd 01 {Le(pair.Ability)}").Count > 0 && p.Received("80 bd 01 cd 03").Count > 0,
                "Stun removed and own/shared cooldowns started", TimeSpan.FromSeconds(5));
            Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=1"), "Exactly one pill consumed");
            Check.That(p.Received(ModPattern(734, 681)).Count >= 2, "Immunity refreshed after halving its remaining time");
            int used = context.Server.DebugLog().Split('\n').Count(l => l.Contains("[ANTISTUN] Used"));
            p.SendRpc("80 c6 02 00");
            await p.WaitForRpcAsync($"80 ba {Le(pair.Ability == 971 ? (ushort)972 : (ushort)971)}");
            await Task.Delay(1200);
            Check.Equal(used, context.Server.DebugLog().Split('\n').Count(l => l.Contains("[ANTISTUN] Used")), "Shared cooldown blocks other variant");
            Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=2"), "Blocked pill not consumed");
            Check.Equal(0, p.World.Problems.Count, "Anti-stun protocol");
            p.Dispose();
        }

        var moved = await context.AddPlayerAsync(new CharacterSeed("MovedPill") { Items = new[] { (1, 42763u, 1) } });
        await moved.EnterWorldAsync();
        AntiStunItemUse(moved, 971, 1);
        await moved.WaitForRpcAsync("80 ac cb 03");
        await context.Database.ExecuteAsync($"UPDATE inventory SET slot=2 WHERE charId={moved.CharId} AND slot=1");
        await Task.Delay(1300);
        Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={moved.CharId} AND slot=2"), "Moved pill retained");
        Check.Equal(0, moved.Received("80 bd 01 cb 03").Count, "Moved pill starts no timer");
        Check.That(context.Server.DebugLog().Contains("[ANTISTUN] Cast cancelled"), "Cast lifecycle revalidation reached");

        var zoning = await context.AddPlayerAsync(new CharacterSeed("ZoningPill") { Items = new[] { (1, 42764u, 1) } });
        await zoning.EnterWorldAsync();
        AntiStunItemUse(zoning, 972, 1);
        await zoning.WaitForRpcAsync("80 ac cc 03");
        await GoToLoadingAreaAsync(zoning);
        await Task.Delay(1200);
        Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={zoning.CharId} AND slot=1"), "Zoning cancels without consuming");
        Check.Equal(0, zoning.Received("80 bd 01 cc 03").Count, "Zoning starts no cooldown");
    }
}
