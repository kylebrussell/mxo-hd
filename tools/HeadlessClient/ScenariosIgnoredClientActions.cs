using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task IgnoredActionsWindowClose(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("CloseElevator"){X=60300,Y=273,Z=33390}))[0];
        p.SendRpc("80 c8 b8 03 50 19 00 00");await p.WaitForRpcAsync("81 3f");
        p.SendRpc("81 41 01 00");p.SendRpc("81 40 02 00");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
        Check.Equal(0,p.Received("81 42 2a 06 00 22").Count,"Closed elevator cannot teleport from stale floor choice");
        p.SendRpc("80 c8 b8 03 50 19 00 00");await p.WaitForRpcAsync("81 3f");
        p.SendRpc("81 40 02 00");await p.WaitForRpcAsync("81 42 2a 06 00 22");
        var tutorial=(await context.AddPlayersInWorldAsync(new CharacterSeed("CloseWardrobe"){District="tutorial",DistrictId=0,X=5800,Y=-1105,Z=-10400}))[0];
        tutorial.SendRpc("80 c8 6a 00 00 00 03 00");await tutorial.WaitForRpcAsync("81 19");
        tutorial.SendRpc("81 1a");tutorial.SendRpc("81 1f");await tutorial.WaitForRpcAsync("81 20");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={tutorial.CharId}"),"Closed wardrobe cannot grant its contents");
        tutorial.SendRpc("80 c8 6a 00 00 00 03 00");await tutorial.WaitForRpcAsync("81 19");
        tutorial.SendRpc("81 1f");await tutorial.WaitForRpcAsync("81 20");
        Check.That(await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={tutorial.CharId}")>0,"Wardrobe contents remain available after reopen");
    }
    private static async Task IgnoredActionsGm(ScenarioContext context)
    {
        var players=await context.AddPlayersInWorldAsync(new CharacterSeed("TrustedInspector"){Items=new[]{(3,7332u,2)}},new CharacterSeed("OrdinaryInspector"){X=17120});
        var gm=players[0];var ordinary=players[1];
        var self=gm.World.CreationSnapshot().First(v=>v.ViewId==2);
        gm.SendRpc($"81 57 {HandleOf(self)} 01 01 01");
        await gm.WaitForTextAsync("GM loadout TrustedInspector");await gm.WaitForTextAsync("Slot 3: 7332 x2");
        await context.Database.ExecuteAsync("INSERT INTO factions(id,name,master_player_handle,money) VALUES(9011,'SupportFaction','TrustedInspector',123);"+
            "INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank) VALUES(9012,'SupportCrew','TrustedInspector',456,1,9011,1);"+
            $"INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at) VALUES(9012,{gm.CharId},1,0,NOW())");
        byte[] group(byte op,string name)=>new byte[]{0x81,op,0,5,0}.Concat(Sized(name)).ToArray();
        gm.SendRpc(group(0x59,"SupportCrew"));await gm.WaitForTextAsync("GM crew SupportCrew");await gm.WaitForTextAsync("Member TrustedInspector");
        gm.SendRpc(group(0x5b,"SupportFaction"));await gm.WaitForTextAsync("GM faction SupportFaction");await gm.WaitForTextAsync("Crew SupportCrew");
        ordinary.SendRpc(group(0x59,"SupportCrew"));await ordinary.WaitForTextAsync("server-granted GM");
        ordinary.SendRpc(group(0x5b,"SupportFaction"));await ordinary.WaitForTextAsync("server-granted GM");
        Check.That(!ordinary.World.RpcSnapshot().Any(t=>System.Text.Encoding.UTF8.GetString(t.Bytes).Contains("bank 456")),"Ordinary player receives no private crew data");
    }
    private static byte[] PetitionWire(uint id,string text,bool cancel=false) =>
        new byte[]{0x81,0xb0}.Concat(BitConverter.GetBytes(id)).Concat(new byte[]{10,0,0,(byte)(cancel?1:0)}).Concat(Sized(text)).ToArray();
    private static async Task IgnoredActionsSupport(ScenarioContext context)
    {
        var players=await context.AddPlayersInWorldAsync(new CharacterSeed("TicketOwner"),new CharacterSeed("TicketOther"){X=17120});
        var p=players[0];var other=players[1];
        p.SendRpc("81 b7");await p.WaitForRpcAsync("81 b8 81 ad");
        p.SendRpc(PetitionWire(0,"A local petition with ' quotes"));
        var ack=await p.WaitForRpcAsync("81 b1 00 00 00 00");uint id=BitConverter.ToUInt32(ack.Bytes,6);
        Check.That(id!=0,"Server assigns a ticket id");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM local_support_tickets WHERE id={id} AND charId={p.CharId}"),"Ticket owned by authenticated player");
        other.SendRpc($"81 b4 {Le(id)}");await other.WaitForRpcAsync($"81 b5 {Le(id)} 01 00 00 00");
        other.SendRpc(PetitionWire(id,"foreign replacement"));await other.WaitForRpcAsync("81 b1 01 00 00 00");
        other.SendRpc(PetitionWire(id,"",true));await other.WaitForRpcAsync("81 b1 01 00 00 00");
        p.SendRpc(PetitionWire(id,"Updated details"));await p.WaitForRpcAsync("81 b1 00 00 00 00");
        p.SendRpc("81 b2");await p.WaitForRpcAsync("81 b3 00 00 00 00 00");
        await p.WaitForRpcAsync($"81 b3 00 {Le(id)}");
        p.SendRpc(new byte[]{0x81,0xb9,4,0}.Concat(Sized("stuck")).ToArray());await p.WaitForRpcAsync("81 ba 00 00 00 00");
        p.SendRpc(new byte[]{0x81,0xbb,4,0}.Concat(Sized("local-stuck")).ToArray());await p.WaitForRpcAsync("81 bc 00 00 00 00");
        p.SendRpc("81 bd");await p.WaitForRpcAsync("81 be 00 00 00 00");
        other.SendRpc("81 57 02 00 00 00 01 01 01");await other.WaitForTextAsync("server-granted GM");
        other.SendChat("?clientactions petition bypass");await other.WaitForTextAsync("?clientactions is a dev command");
        Check.Equal(1L,await context.Database.LongAsync("SELECT COUNT(*) FROM local_support_tickets"),"Disabled helper cannot create tickets");

        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,new Dictionary<string,string>{{"MXO_DEV_COMMANDS","0"}});
        using var relog=await HeadlessPlayer.LoginAsync(server.Address,username,p.Handle);await relog.EnterWorldAsync();
        relog.SendRpc($"81 b4 {Le(id)}");var detail=await relog.WaitForRpcAsync($"81 b5 {Le(id)} 00 00 00 00");
        Check.Equal("Updated details",StringAtOffset(detail.Bytes,10),"Ticket survives fresh server");
        relog.SendRpc(PetitionWire(id,"",true));await relog.WaitForRpcAsync("81 b1 00 00 00 00");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT cancelled FROM local_support_tickets WHERE id={id}"),"Owned cancellation persists");
        relog.SendRpc(PetitionWire(id,"reopen"));await relog.WaitForRpcAsync("81 b1 01 00 00 00");
    }
    private static async Task IgnoredActionsPresenceTradeCompile(ScenarioContext context)
    {
        var players=await context.AddPlayersInWorldAsync(new CharacterSeed("ActionOwner"){Level=50,Info=100000,Items=new[]{(3,2399u,1)}},
            new CharacterSeed("ActionObserver"){X=17120});
        var p=players[0];var observer=players[1];
        var view=await observer.WaitForViewNamedAsync(p.Handle);
        p.SendRpc("2a 01");await p.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 20 01");
        await observer.WaitForViewUpdateAsync(view.ViewId,"02 80 80 80 80 80 01 01");
        var late=(await context.AddPlayersInWorldAsync(new CharacterSeed("ActionLate"){X=17220}))[0];
        var seen=await late.WaitForViewNamedAsync(p.Handle);
        Check.Equal((byte)1,seen.Attributes["AFK"][0],"Late observer sees current away flag");
        p.SendRpc("2a 00");await p.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 20 00");
        p.SendRpc("81 4d 78 56 34 12");await p.WaitForRpcAsync("81 4d 78 56 34 12");
        p.SendRpc("80 c6 03 00");await p.WaitForRpcAsync("5f 03 00 01");
        p.SendRpc("80 c8 62 00 00 00 02 00");await p.WaitForRpcAsync("81 0d"); // fixture vendor98, tools
        p.SendRpc($"81 0e {Le(4u)}");await p.WaitForRpcAsync("81 0f");
        p.SendRpc("81 10 00 00 00 00 00"); // CloseUI=false must preserve valid session
        p.SendRpc($"81 0e {Le(4u)}");await p.WaitForRpcAsync("81 0f");
        p.SendRpc($"81 0e {Le(2399u)}");await p.WaitForTextAsync("does not sell");
        p.SendRpc("81 10 00 00 00 00 01");p.SendRpc($"81 0e {Le(4u)}");await p.WaitForTextAsync("Open a vendor");
        p.SendRpc("80 c8 ff ff ff 7f 02 00");p.SendRpc($"81 0e {Le(4u)}");await p.WaitForTextAsync("Open a vendor");
        Check.Equal(2L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=4"),"Only two listed purchases succeeded");
        // Native60: city request without a compile session cannot touch the archive.
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},400,{CodeStorage.BaseValue(7332)},0,0)");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 00 00");await p.WaitForRpcAsync("61 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");await Task.Delay(300);
        p.SendRpc($"60 {Le(7333u)} 90 01 00 00 00");await p.WaitForRpcAsync("61 01");
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},400,{AbilityGoid(17)},1,0)");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 00 00");await p.WaitForRpcAsync("61 01");
        Check.Equal(2L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot=400"),"Ambiguous archive/ability slot is refused without deleting either row");
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId} AND slot=400 AND ability_id={AbilityGoid(17)}");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 00 00");await p.WaitForRpcAsync("5f 90 01 00");await p.WaitForRpcAsync("61 00");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={p.CharId} AND goid=7332"),"Owned code materialized once");
        p.SendRpc($"60 {Le(7332u)} 90 01 00 00 00");await p.WaitForRpcAsync("61 01");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot=400 AND ability_id={CodeStorage.BaseValue(7332)}"),"Source charge depleted");
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},401,{CodeStorage.BaseValue(7458)},0,0);"+
            $"UPDATE characters SET cash=0 WHERE charId={p.CharId}");
        p.SendRpc($"60 {Le(7458u)} 91 01 00 00 00");await p.WaitForRpcAsync("61 04");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot=401 AND ability_id={CodeStorage.BaseValue(7458)}"),"Insufficient information preserves source");
        await context.Database.ExecuteAsync($"UPDATE characters SET cash=100000 WHERE charId={p.CharId};DELETE FROM inventory WHERE charId={p.CharId} AND slot<97;"+
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES "+string.Join(",",Enumerable.Range(1,96).Select(i=>$"({p.CharId},2399,{i},1,0)")));
        p.SendRpc($"60 {Le(7458u)} 91 01 00 00 00");await p.WaitForRpcAsync("61 03");
        Check.Equal(100000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Full bag rolls back cash/source/result together");
        await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=96");
        p.SendRpc($"60 {Le(7458u)} 91 01 01 00 00");await p.WaitForRpcAsync("61 01");
        p.SendRpc($"60 {Le(7458u)} 91 01 00 00 00");await p.WaitForRpcAsync("5f 91 01 00");await p.WaitForRpcAsync("61 00");
        Check.Equal(99880L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Known fragment compile charges script twelve-percent cost");
    }
}
