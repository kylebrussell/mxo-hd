using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static void WriterTool(HeadlessPlayer player, byte slot, bool activate)
    {
        byte[] body = new byte[41]; BitConverter.GetBytes((ushort)970).CopyTo(body, 0);
        BitConverter.GetBytes((ushort)2).CopyTo(body, 2);
        body[34]=8; body[35]=slot; body[36]=activate ? (byte)1 : (byte)0;
        player.SendRpc("80 b9 " + BitConverter.ToString(body).Replace('-', ' '));
    }

    private static async Task WriterSessionAuthority(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(new CharacterSeed("WriterSession") { Level=50, InnerStrength=0, Health=500,
            Info=100000, Items=new[] {(2,531u,1),(3,531u,1),(4,1271u,1),(5,7336u,20)} });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId} AND ability_id IN ({AbilityGoid(37)},{AbilityGoid(970)},{AbilityGoid(277)});" +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES" +
            $"({p.CharId},30,{AbilityGoid(37)},15,1),({p.CharId},31,{AbilityGoid(277)},50,1)");
        await p.EnterWorldAsync(); p.SkipReceived();
        Cast(p,970); await p.WaitForTextAsync("Activate a Code Analyser Tool");
        WriterTool(p,4,true); await p.WaitForTextAsync("requires its loaded ability");
        foreach (uint ability in new uint[] {37,970})
        { p.SendRpc($"81 31 {Le(ability)} 02 02 01"); await p.WaitForTextAsync("That inventory ability is not available."); }
        p.SendChat("?writersession"); await p.WaitForTextAsync("Writer session inactive");
        WriterTool(p,2,true); await p.WaitForRpcAsync("80 bd 05 ca 03 00 00 00 00 00 01");
        await p.WaitForRpcAsync(ModPattern(970,968));
        await p.WaitForViewUpdateAsync(2,"01 28 ?? 40 03 00 00 10");
        WriterTool(p,2,true); p.SendChat("?writersession"); await p.WaitForTextAsync("active; tool slot 2");
        Check.Equal(1,p.Received(ModPattern(970,968)).Count,"Duplicate activation is idempotent");
        WriterTool(p,3,false); p.SendChat("?writersession"); await p.WaitForTextAsync("active; tool slot 2");
        CompilerTool(p,4,false); p.SendChat("?writersession"); await p.WaitForTextAsync("active; tool slot 2");
        // Writer authority cannot authorize the compiler recipe path or inventory-target shortcut.
        p.SendRpc($"81 2e {Le(7458u << 7)} 09 00 02 01 00 {Le(2u)} {Le(7336u)}");
        await p.WaitForRpcAsync($"81 2f 00 00 {Le(7458u)}");
        p.SendRpc($"81 31 {Le(970u)} 05 02 01"); await p.WaitForTextAsync("That inventory ability is not available.");
        WriterTool(p,2,false); await p.WaitForRpcAsync(ModPattern(970,968,true));
        await p.WaitForViewUpdateAsync(2,"01 28 ?? 40 00 00 00 10");
        p.SkipReceived(); WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(970,968));
        p.SendRpc("81 1a"); await p.WaitForRpcAsync(ModPattern(970,968,true));
        p.SkipReceived(); WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(970,968));
        CompilerTool(p,4,true); await p.WaitForRpcAsync(ModPattern(970,968,true));
        await p.WaitForRpcAsync(ModPattern(969,968));
        p.SendRpc("81 1a"); p.SendChat("?compilesession"); await p.WaitForTextAsync("active; tool slot 4");
        WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(969,968,true));
        await p.WaitForRpcAsync(ModPattern(970,968));
        p.SkipReceived(); p.SendRpc("33"); await p.WaitForRpcAsync(ModPattern(970,968,true));
        WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(970,968));
        p.SkipReceived(); p.SendChat("?health 400"); await p.WaitForRpcAsync(ModPattern(970,968,true));
        WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(970,968));
        p.SkipReceived(); Cast(p,17); await p.WaitForRpcAsync(ModPattern(970,968,true));
        WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(970,968));
        p.SkipReceived(); await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=2");
        await p.WaitForRpcAsync(ModPattern(970,968,true));
        WriterTool(p,3,true); await p.WaitForRpcAsync(ModPattern(970,968));
        p.SkipReceived(); await context.Database.ExecuteAsync($"UPDATE char_abilities SET is_loaded=0 WHERE char_id={p.CharId} AND slot=30");
        await p.WaitForRpcAsync(ModPattern(970,968,true));
        p.SendChat("?writersession"); await p.WaitForTextAsync("inactive");
        Check.Equal(20L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=5"),"No writer activation or spoofed inventory command consumes ingredients");
        Check.Equal(100000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Activation does not run a crafting economy");
        Check.Equal(0,p.World.Problems.Count,"Writer lifecycle packets decode");
    }

    private static async Task WriterSessionDevDisabled(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(new CharacterSeed("WriterNoDev") { Level=50, Items=new[] {(2,531u,1)} });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId} AND ability_id IN ({AbilityGoid(37)},{AbilityGoid(970)});" +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES" +
            $"({p.CharId},30,{AbilityGoid(37)},15,1)");
        await p.EnterWorldAsync(); p.SkipReceived();
        WriterTool(p,2,true); await p.WaitForRpcAsync(ModPattern(970,968));
        p.SendChat("?writersession off"); await p.WaitForTextAsync("?writersession is a dev command.");
        Check.Equal(0,p.Received(ModPattern(970,968,true)).Count,"Disabled dev helper cannot end a legitimate session");
        WriterTool(p,2,false); await p.WaitForRpcAsync(ModPattern(970,968,true));
        Check.Equal(0,p.World.Problems.Count,"Retail tool activation works without dev commands");
    }
}
