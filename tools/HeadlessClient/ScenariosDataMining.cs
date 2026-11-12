namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task<ViewCreation> UtilityFixture(HeadlessPlayer p, ushort goid)
    {
        int before = p.ViewsOfGoid(goid).Count;
        p.SendChat("?utilityobject " + goid);
        await p.WaitForTextAsync("Utility fixture " + goid + " placed.");
        await p.WaitUntilAsync(() => p.ViewsOfGoid(goid).Count > before,"fixture spawn",TimeSpan.FromSeconds(10));
        return p.ViewsOfGoid(goid).Last();
    }
    private static async Task DataMiningLuggables(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(new CharacterSeed("Lugger") {Level=50, Items=new[]{(20,2399u,1)}});
        var watcher = await context.AddPlayerAsync(new CharacterSeed("LugWatcher") {Level=50,X=17040});
        await p.EnterWorldAsync(); await watcher.EnterWorldAsync();
        var item = await UtilityFixture(p,596);
        await watcher.WaitUntilAsync(() => watcher.ViewsOfGoid(596).Count>0,"shared luggable view",TimeSpan.FromSeconds(10));
        // A forged spawn identifier must not pick it up.
        p.SendRpc($"80 c7 {Le(item.ViewId)} {(byte)(item.SpawnId+1):x2} 00 02 00");
        p.SendRpc("81 48"); await p.WaitForRpcAsync("81 49");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=596"),"forged handle ignored");
        p.SendRpc($"80 c7 {MissionNpcId(item)} 02 00");
        var added=await p.WaitForRpcAsync("5e ?? ?? 54 02 00 00");
        ushort slot=BitConverter.ToUInt16(added.Bytes,1);
        Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=596"),"picked ground row removed");
        var contested=await UtilityFixture(p,562);
        await watcher.WaitUntilAsync(()=>watcher.ViewsOfGoid(562).Count>0,"contested shared view",TimeSpan.FromSeconds(10));
        p.SendRpc($"80 c7 {MissionNpcId(contested)} 02 00");
        watcher.SendRpc($"80 c7 {MissionNpcId(watcher.ViewsOfGoid(562).Last())} 02 00");
        p.SendRpc("81 48"); watcher.SendRpc("81 48");
        await p.WaitForRpcAsync("81 49"); await watcher.WaitForRpcAsync("81 49");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId IN ({p.CharId},{watcher.CharId}) AND goid=562"),"competing players receive exactly one item");
        Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=562"),"contested object removed once");
        // Replayed pickup and the observer's old handle cannot duplicate it.
        p.SendRpc($"80 c7 {MissionNpcId(item)} 02 00");
        p.SendRpc("5c 14 00 01"); // nonluggable item cannot be dropped
        p.SendRpc($"5c {Le(slot)} 00"); // storage flag rejected
        p.SendRpc("81 48"); await p.WaitForRpcAsync("81 49");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=596"),"one persistent carried luggable");
        p.SendRpc($"5c {Le(slot)} 01"); await p.WaitForRpcAsync($"5f {Le(slot)} 01");
        Check.Equal(1L,await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=596"),"drop persisted on floor");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=596"),"dropped bag item removed");
        string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
        p.Dispose(); watcher.Dispose(); context.Server.Dispose();
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);
        using var again=await HeadlessPlayer.LoginAsync(server.Address,user,p.Handle);
        await again.EnterWorldAsync();
        await again.WaitUntilAsync(() => again.ViewsOfGoid(596).Count>0,"dropped item survives a fresh server",TimeSpan.FromSeconds(10));
        var restored=again.ViewsOfGoid(596).Last();
        again.SendRpc($"80 c7 {MissionNpcId(restored)} 02 00"); await again.WaitForRpcAsync("5e ?? ?? 54 02 00 00");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=596"),"restart pickup persisted once");
        Check.Equal(0,again.World.Problems.Count,"restarted luggable protocol");
    }
    private static async Task DataMiningFullBag(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("FullLugger") {Level=50,Items=Enumerable.Range(1,96).Select(s=>(s,2399u,1)).ToArray()});
        await p.EnterWorldAsync(); var item=await UtilityFixture(p,596);
        p.SendRpc($"80 c7 {MissionNpcId(item)} 02 00"); await p.WaitForTextAsync("inventory is full");
        Check.Equal(1L,await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables"),"full bag preserves ground object");
        p.SendRpc("5d 60 00 01 01"); await p.WaitForRpcAsync("5f 60 00 01");
        p.SendRpc($"80 c7 {MissionNpcId(item)} 02 00"); await p.WaitForRpcAsync("5e 60 00 54 02 00 00");
        Check.Equal(96L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId}"),"retry fills only freed slot");
    }
    private static async Task DataMiningLocator(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Miner") {Level=50,Items=new[]{(30,422u,1)}});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES ({p.CharId},300,{AbilityGoid(621)},50,1),({p.CharId},301,{AbilityGoid(540)},1,1),({p.CharId},302,{AbilityGoid(541)},1,1)");
        await p.EnterWorldAsync();
        p.SendChat("?utilityobject 606"); await p.WaitForTextAsync("Utility fixture 606 placed.");
        var item=await UtilityFixture(p,596);
        Cast(p,621); await p.WaitForRpcAsync(AbilityLine(0x7c000011));
        await p.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        await p.WaitForRpcAsync("80 cb"); await p.WaitForRpcAsync("80 c5 01 00 0a");
        Cast(p,621); await p.WaitForRpcAsync("80 ce 00 00"); await p.WaitForRpcAsync(AbilityLine(0x7c000012));
        await context.Database.ExecuteAsync($"UPDATE inventory SET goid=423 WHERE charId={p.CharId} AND slot=30");
        Cast(p,621); await p.WaitForRpcAsync(AbilityLine(0x7c000011));
        await p.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        await p.WaitForRpcAsync("80 cd 80 00 f9 04 00 28"); // See Luggables world aura
        await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=30");
        await p.WaitForRpcAsync("80 ce 80 00"); // Removing the tool cleans up the goggles
        Cast(p,621); await p.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60");
        Check.Equal(0,p.World.Problems.Count,"locator protocol");
    }
    private static async Task DataMiningMissionMap(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("MapMiner") {Level=50});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES ({p.CharId},300,{AbilityGoid(44)},50,1)");
        await p.EnterWorldAsync();
        p.SendRpc("80 94 05 00 03"); await p.WaitForTextAsync("Locked Safe Fixture"); await p.WaitForRpcAsync("80 97 05 00");
        p.SendRpc("80 98 06 00 00 00 01 02 00"); await p.WaitForRpcAsync("80 99");
        p.SendRpc("80 9b 07 00 01"); await p.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context,p);
        p.SendChat("?missionobject 1"); await p.WaitForTextAsync("Object moved");
        var reveal=await p.WaitForRpcAsync("80 c5 01 00 03");
        Check.Equal(9,reveal.Bytes.Length,"native map header plus one nested four-byte position");
        Check.Equal((byte)0,reveal.Bytes[8],"objective uses yellow relationship");
        Check.Equal(0,p.World.Problems.Count,"mission map protocol");
    }
}
