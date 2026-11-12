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
        Cast(p, 1111);
        await p.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 01");
        // A forged spawn identifier must not pick it up or break an exclusive ability.
        p.SendRpc($"80 c7 {Le(item.ViewId)} {(byte)(item.SpawnId+1):x2} 00 02 00");
        p.SendRpc("81 48"); await p.WaitForRpcAsync("81 49");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=596"),"forged handle ignored");
        Check.That(context.Server.DebugLog().Contains("outcome=denied_handle db=unchanged"),"Structured diagnostic identifies forged handle rejection");
        Check.Equal(0, p.Received("80 bd 05 57 04 00 00 00 00 00 00").Count, "Forged pickup preserves the exclusive Sneak");
        p.SendRpc($"80 c7 {MissionNpcId(item)} 02 00");
        var added=await p.WaitForRpcAsync("5e ?? ?? 54 02 00 00");
        await p.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 00"); // successful pickup breaks the exclusive Sneak
        ushort slot=BitConverter.ToUInt16(added.Bytes,1);
        Check.Equal(0L,await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=596"),"picked ground row removed");
        Check.That(context.Server.DebugLog().Contains("outcome=success db=committed ground="),"Native pickup diagnostics identify committed bag slot");
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
        p.SendRpc("5d 5f 00 01 01"); await p.WaitForRpcAsync("5f 5f 00 01");
        p.SendRpc("5d 5e 00 01 01"); await p.WaitForRpcAsync("5f 5e 00 01");
        var exclusive=await UtilityFixture(p,47277);
        p.SendRpc($"80 c7 {MissionNpcId(exclusive)} 04 00"); await p.WaitForRpcAsync("5e ?? ?? ad b8 00 00");
        var refused=await UtilityFixture(p,47276);
        p.SendRpc($"80 c7 {MissionNpcId(refused)} 04 00"); await p.WaitForTextAsync("exclusive luggable");
        Check.Equal(1L,await context.Database.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=47276"),"GOB PickupExclusive prevents a second datamine tap without consuming it");
    }
    private static async Task DataMiningLocator(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Miner") {Level=50,Items=new[]{(30,422u,1)}});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES ({p.CharId},300,{AbilityGoid(621)},50,1),({p.CharId},301,{AbilityGoid(540)},1,1),({p.CharId},302,{AbilityGoid(541)},1,1),({p.CharId},303,{AbilityGoid(215)},5,1)");
        await p.EnterWorldAsync();
        p.SendChat("?utilityobject 606"); await p.WaitForTextAsync("Utility fixture 606 placed.");
        var item=await UtilityFixture(p,596);
        Cast(p,621); await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 01"); await p.WaitForRpcAsync(AbilityLine(0x7c000011));
        await p.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        await p.WaitForRpcAsync("80 cb"); await p.WaitForRpcAsync("80 c5 01 00 0a");
        Cast(p,621); await p.WaitForRpcAsync("80 ce 00 00"); await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 00"); await p.WaitForRpcAsync(AbilityLine(0x7c000012));
        await context.Database.ExecuteAsync($"UPDATE inventory SET goid=423 WHERE charId={p.CharId} AND slot=30");
        Cast(p,621); await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 01"); await p.WaitForRpcAsync(AbilityLine(0x7c000011));
        await p.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        await p.WaitForRpcAsync("80 cd 80 00 f9 04 00 28"); // See Luggables world aura
        p.SendRpc($"80 c7 {MissionNpcId(item)} 02 00");
        await p.WaitForRpcAsync("5e ?? ?? 54 02 00 00");
        await p.WaitForRpcAsync("80 ce 80 00");
        await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 00");
        await p.WaitForRpcAsync(AbilityLine(0x7c000012)); // pickup also ends the exclusive Find Data Node
        await UtilityFixture(p, 562);
        Cast(p, 621);
        await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 01");
        await p.WaitForRpcAsync("80 cd 80 00 f9 04 00 28");
        await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=30");
        await p.WaitForRpcAsync("80 ce 80 00"); // Removing the tool cleans up the goggles
        await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 00");
        Cast(p,621); await p.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60");
        Check.Equal(0,p.World.Problems.Count,"locator protocol");
    }
    private static async Task DataMiningKnownBaseLocator(ScenarioContext context)
    {
        var seed=await context.AddPlayerAsync(new CharacterSeed("NativeMiner") {Level=50,Items=new[]{(30,422u,1)}});
        ushort[] ids={2,17,44,215,538,539,540,541,621,52,537};
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={seed.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES "+
            string.Join(",",ids.Select((id,slot)=>$"({seed.CharId},{slot},{AbilityGoid(id)},{(id==215?5:id==44?10:50)},{(id==52||id==537?0:1)})")));
        string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={seed.CharId}");
        seed.Dispose(); context.Server.Dispose();
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);
        using var p=await HeadlessPlayer.LoginAsync(server.Address,user,seed.Handle);
        Dictionary<ushort,uint> Unpack(byte[] block) => Enumerable.Range(0,block.Length/6).Select(i=>BitConverter.ToUInt32(block,i*6+2)).ToDictionary(v=>hds.AbilityCatalog.IdOf(v));
        var known=Unpack(p.CharacterData[7]); var loaded=Unpack(p.CharacterData[6]);
        Check.That(known.ContainsKey(52)&&known.ContainsKey(537),"Native margin known list includes Hacker/Data Miner folder ownership");
        Check.That(!loaded.ContainsKey(52)&&!loaded.ContainsKey(537),"UI folder ownership does not load unwanted passive stats");
        Check.Equal(5u,known[215]&1023,"Base locator retains its real level cap in packed margin ID");
        Check.Equal(10u,known[44]&1023,"Download Map retains cap10");
        Check.That(loaded.ContainsKey(215)&&loaded.ContainsKey(621),"Both distinct native search IDs are loaded");
        await p.EnterWorldAsync();
        p.SendChat("?utilityobject 606"); await p.WaitForTextAsync("Utility fixture 606 placed.");
        Cast(p,215); await p.WaitForRpcAsync("80 bd 05 d7 00 00 00 00 00 00 01");
        await p.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        await p.WaitForRpcAsync("80 bd 05 d7 00 00 00 00 00 00 00",TimeSpan.FromSeconds(15)); // source handheld Duration=10, not an indefinite polling toggle
        p.SendChat("?utilitylocator 423"); await p.WaitForTextAsync("Locator fixture changed.");
        Cast(p,621); await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 01");
        await p.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        Cast(p,215); await p.WaitForRpcAsync("80 bd 05 6d 02 00 00 00 00 00 00"); // another locator use ends the active ID, never emits a false215-off
        SetAbilityLoaded(p,3,215,5,false); p.SendRpc("81 48"); await p.WaitForRpcAsync("81 49");
        Cast(p,621); await p.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60");
        p.SendChat("?utilitystatus"); await p.WaitForTextAsync("search=0, loaded215=0, loaded621=50");
        Check.Equal(0,p.World.Problems.Count,"native-margin/base-locator protocol");
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
        p.SendChat("?utilitymap 7"); await p.WaitForTextAsync("Utility map fixture flags=7"); // deterministic presentation; pure retail roll has separate boundary tests
        var reveal=await p.WaitForRpcAsync("80 c5 01 00 03");
        Check.Equal(9,reveal.Bytes.Length,"native map header plus one nested four-byte position");
        Check.Equal((byte)0,reveal.Bytes[8],"objective uses yellow relationship");
        Check.Equal(0,p.World.Problems.Count,"mission map protocol");
    }
}
