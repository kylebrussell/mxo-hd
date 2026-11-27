using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    public static void CompilerTool(HeadlessPlayer player, byte slot, bool activate)
    {
        byte[] body = new byte[41];
        BitConverter.GetBytes((ushort)969).CopyTo(body, 0);
        BitConverter.GetBytes((ushort)2).CopyTo(body, 2);
        body[34] = 8; body[35] = slot; body[36] = activate ? (byte)1 : (byte)0;
        player.SendRpc("80 b9 " + BitConverter.ToString(body).Replace('-', ' '));
    }

    private static async Task CompileSessionAuthority(ScenarioContext context)
    {
        const uint nonCraftablePass = 38538;
        var p = await context.AddPlayerAsync(new CharacterSeed("CompilerSession") { Level=50, Info=100000,
            InnerStrength=0, Items=new[] {(3,1271u,1),(4,1271u,1),(5,7336u,20),(8,2565u,1),(9,nonCraftablePass,1)} });
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES" +
            $"({p.CharId},30,{AbilityGoid(277)},50,1),({p.CharId},400,{CodeStorage.BaseValue(7332)},1,0)," +
            $"({p.CharId},401,{nonCraftablePass << 7},1,0)");
        await p.EnterWorldAsync(); p.SkipReceived();
        string recipe = $"81 2e {Le(7458u << 7)} 09 00 03 01 00 {Le(2u)} {Le(7336u)}";
        // Native double-click is equip 63, not tool activation. A refusal must unlock inventory without a move.
        foreach (string request in new[] { "63 03 6b", "63 03 69", "63 03 6a", "63 03", "63 03 6b 00", "63 ff 6b", "63", "64" })
        {
            p.SkipReceived();
            p.SendRpc(request);
            await p.WaitForRpcAsync(request.StartsWith("63 03") ? "65 03 00 03 00 00" : "65 00 00 00 00 00");
            await p.WaitForTextAsync("That item cannot be equipped there");
            Check.Equal(1271L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={p.CharId} AND slot=3"), "Refused tool equip preserves original bag slot");
        }
        p.SendChat("?compilesession"); await p.WaitForTextAsync("inactive");
        // The ordinary weapon double-click still resolves its equipment slot through the normal equip path.
        p.SendRpc("63 08 6b"); await p.WaitForRpcAsync("65 08 00 69 00 00");
        p.SendRpc("64 69 08"); await p.WaitForRpcAsync("65 69 00 08 00 00");
        p.SendRpc("63 09 6a"); await p.WaitForRpcAsync("65 09 00 6a 00 00");
        p.SendRpc("64 6a 09"); await p.WaitForRpcAsync("65 6a 00 09 00 00");
        Check.Equal(38538L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={p.CharId} AND slot=9"), "Archive book normal equipment routing preserved");
        p.SkipReceived();
        // Merely carrying a tool and loading the parent grant no longer permits either executor.
        p.SendRpc(recipe); await p.WaitForRpcAsync($"81 2f 00 00 {Le(7458u)}");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 03 01"); await p.WaitForRpcAsync("61 01");
        Cast(p,969); await p.WaitForTextAsync("Activate a Code Compiler Tool");
        CompilerTool(p,5,true); await p.WaitForTextAsync("requires its loaded ability");
        Check.Equal(20L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=5"),"No unauthorised ingredient consumption");
        CompilerTool(p,3,true); await p.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 01");
        await p.WaitForRpcAsync(ModPattern(969,968));
        p.SendChat("?compilesession"); await p.WaitForTextAsync("active; tool slot 3");
        // The shipped Zion pass is marked CantBeCrafted, so a stored charge cannot produce another copy.
        p.SendRpc($"60 {Le(nonCraftablePass)} 91 01 00 03 01"); await p.WaitForRpcAsync("61 01");
        await p.WaitForTextAsync("This compile requires an owned supported item code");
        Check.Equal((long)(nonCraftablePass << 7), await context.Database.LongAsync($"SELECT ability_id FROM char_abilities WHERE char_id={p.CharId} AND slot=401"), "Unsupported native item code preserved");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot=401"), "Native item code charge preserved");
        Check.Equal(100000L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"), "Native refusal has no cash cost");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid={nonCraftablePass}"), "Only the original Zion pass remains");
        CompilerTool(p,3,true); // idempotent start, not a toggle-off
        p.SendChat("?compilesession"); await p.WaitForTextAsync("active; tool slot 3");
        Check.Equal(1,p.Received(ModPattern(969,968)).Count,"One session record despite duplicate tool activation");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=7458"),"Ability activation never compiles a recipe");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 04 01"); await p.WaitForRpcAsync("61 01");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 03 01"); await p.WaitForRpcAsync("61 00");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={p.CharId} AND goid=7332"),"Matching session materializes one owned stored bit");
        CompilerTool(p,4,false); // stale close for another tool cannot close the current session
        p.SendChat("?compilesession"); await p.WaitForTextAsync("active; tool slot 3");
        CompilerTool(p,3,false); await p.WaitForRpcAsync(ModPattern(969,968,true));
        await p.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 00");
        p.SendRpc(recipe); await p.WaitForRpcAsync($"81 2f 00 00 {Le(7458u)}");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 03 01"); await p.WaitForRpcAsync("61 01");

        // A position echo leaves the active tool open; accepted displacement ends it without 33 or a UI close RPC.
        p.World.SendViewList(DisturbingTrendsFixture.ClientPosition(new double[] { 17000, 495, 6400 }));
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},402,{2399u << 7},1,0)");
        long movementInfo = await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
        CompilerTool(p,3,true); await p.WaitForRpcAsync(ModPattern(969,968));
        p.World.SendViewList(DisturbingTrendsFixture.ClientPosition(new double[] { 17000, 495, 6400 }));
        p.World.SendViewList(WireFormat.FromHex("02 00 01 04 10 00 00"));
        p.SendChat("?compilesession"); await p.WaitForTextAsync("active; tool slot 3");
        p.SendRpc("60 5f 09 00 00 92 01 00 03 01");
        p.SendChat("?compilesession"); await p.WaitForTextAsync("active; tool slot 3");
        int movement = p.World.ViewSnapshot().Count;
        p.World.SendViewList(DisturbingTrendsFixture.ClientPosition(new double[] { 17020, 495, 6400 }));
        await p.WaitForRpcAsync("61 01");
        await p.WaitForRpcAsync(ModPattern(969,968,true));
        await p.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 00");
        var standing = BytePattern.Parse("02 00 01 28 ?? 40 00 00 00 10");
        await p.WaitUntilAsync(() => p.World.ViewSnapshot().Skip(movement).Any(standing.Occurs),
            "Movement publishes a new standing pose", HeadlessPlayer.DefaultTimeout);
        await Task.Delay(TimeSpan.FromSeconds(3.2));
        Check.Equal(movementInfo, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"), "Walking cancels a pending compile with no Info debit");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot=402"), "Walking preserves the pending code charge");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=2399"), "Walking prevents the pending pill from materializing");

        CompilerTool(p,3,true); await p.WaitForRpcAsync(ModPattern(969,968));
        await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=3");
        await p.WaitForRpcAsync(ModPattern(969,968,true));
        CompilerTool(p,4,true); await p.WaitForRpcAsync(ModPattern(969,968));
        await context.Database.ExecuteAsync($"UPDATE char_abilities SET is_loaded=0 WHERE char_id={p.CharId} AND slot=30");
        await p.WaitForRpcAsync(ModPattern(969,968,true));
        p.SendChat("?compilesession"); await p.WaitForTextAsync("inactive");
        Check.Equal(20L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=5"),"Closed requests preserve all recipe ingredients");
        Check.Equal(0,p.World.Problems.Count,"Compiler lifecycle packets decode and remain ordered");
    }
}
