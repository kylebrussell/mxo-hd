using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static void CompilerTool(HeadlessPlayer player, byte slot, bool activate)
    {
        byte[] body = new byte[41];
        BitConverter.GetBytes((ushort)969).CopyTo(body, 0);
        BitConverter.GetBytes((ushort)2).CopyTo(body, 2);
        body[34] = 8; body[35] = slot; body[36] = activate ? (byte)1 : (byte)0;
        player.SendRpc("80 b9 " + BitConverter.ToString(body).Replace('-', ' '));
    }

    private static async Task CompileSessionAuthority(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(new CharacterSeed("CompilerSession") { Level=50, Info=100000,
            InnerStrength=0, Items=new[] {(3,1271u,1),(4,1271u,1),(5,7336u,20),(8,2565u,1),(9,38538u,1)} });
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES" +
            $"({p.CharId},30,{AbilityGoid(277)},50,1),({p.CharId},400,{CodeStorage.BaseValue(7332)},1,0)," +
            $"({p.CharId},401,{2399u << 7},1,0)");
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
        // This is the real native CompileUI 60 request for its ordinary item-code row, not an injected bit row.
        p.SendRpc($"60 {Le(2399u)} 91 01 00 03 01"); await p.WaitForRpcAsync("61 01");
        await p.WaitForTextAsync("compilation is not reconstructed");
        Check.Equal((long)(2399u << 7), await context.Database.LongAsync($"SELECT ability_id FROM char_abilities WHERE char_id={p.CharId} AND slot=401"), "Unsupported native item code preserved");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={p.CharId} AND slot=401"), "Native item code charge preserved");
        Check.Equal(100000L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"), "Native refusal has no cash cost");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=2399"), "No unsupported native item materialized");
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
