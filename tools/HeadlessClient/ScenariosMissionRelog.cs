using System.Text.Json;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task<HeadlessPlayer> StartRelogMission(ScenarioContext context)
    {
        HeadlessPlayer player=(await context.AddPlayersInWorldAsync(new CharacterSeed("MissionRelog"){Level=50,X=23828,Y=495,Z=13984}))[0];
        player.SendChat("?missionpersist start");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForRpcAsync("80 9e 00");
        return player;
    }
    private static async Task<MissionProgress.SavedState> MissionCheckpoint(ScenarioContext context,HeadlessPlayer player)
    {
        string json=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={player.CharId}");
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(json,new JsonSerializerOptions{IncludeFields=true})!;
    }
    private static async Task MissionRelogPersistence(ScenarioContext context)
    {
        HeadlessPlayer original=await StartRelogMission(context);
        ViewCreation herschel=await VisitMissionNpc(context,original,"Herschel");
        original.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await original.WaitForRpcAsync("80 a0 01 06 00 01");
        MissionProgress.SavedState before=await MissionCheckpoint(context,original);
        Check.That(before.Done.Contains(1),"First Get completed before relog");
        Check.Equal(1,before.Items.Count,"Exactly one owned mission item");
        Check.That(before.Mobs.Count>=3,"Placed mission NPCs and generated enemies captured");
        // Stop only this disposable harness server. The next process has no runtime/static mission state.
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.SendChat("?save");
        await original.WaitForRpcAsync("2e 07");
        original.Dispose();context.Server.Dispose();
        using ServerInstance restarted=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);
        using HeadlessPlayer player=await HeadlessPlayer.LoginAsync(restarted.Address,username,original.Handle);
        Check.That(!string.IsNullOrEmpty(await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={player.CharId}")),"Checkpoint still present before world entry");
        await player.EnterWorldAsync();
        try { await player.WaitForRpcAsync("80 9c"); }
        catch(Exception ex) {throw new ScenarioFailure(ex.Message+" RESTART OUTPUT: "+restarted.OutputTail(35));}
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        MissionProgress.SavedState after=await MissionCheckpoint(context,player);
        Check.Equal(before.Phase,after.Phase,"Same phase after process restart");
        Check.Equal(before.Instance,after.Instance,"Same mission key after restart");
        Check.Equal(before.Mobs.Count,after.Mobs.Count,"No duplicated NPC records");
        Check.Equal(before.Items[6825],after.Items[6825],"Same owned inventory slot");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6825"),"No duplicate item grant");
        herschel=await player.WaitForViewNamedAsync("Herschel",TimeSpan.FromSeconds(20));
        player.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await player.WaitForRpcAsync("80 a0 02 06 00 01");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6825"),"Second objective does not replay first grant");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6820"),"Second Get grants its own item");
        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("80 a2 08");
        Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT COALESCE(payload,'NULL') FROM char_active_missions WHERE charId={player.CharId}"),"Abandon clears checkpoint");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid IN(6825,6820)"),"Abandon removes owned mission items");
        Check.Equal(0,player.World.Problems.Count,"Restored records readable");
    }
    private static async Task MissionRelogObjectsAndRollback(ScenarioContext context)
    {
        var original=await context.AddPlayerAsync(new CharacterSeed("Checkpoint"){Level=50,Experience=100,Info=1000,Items=new[]{(20,48125u,1),(21,4u,1)}});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={original.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({original.CharId},300,{AbilityGoid(142)},10,1)");
        await original.EnterWorldAsync();
        original.SendRpc("59 14");
        await original.WaitForRpcAsync("80 9c");await original.WaitForRpcAsync("80 9e 00");
        var courier=await VisitMissionNpc(context,original,"Checkpoint Courier");
        original.SendRpc($"80 c7 {MissionNpcId(courier)} 0d 00");
        await original.WaitForRpcAsync("80 a0 01 06 00 01");
        await original.WaitForTextAsync("Unlock the safe");
        await original.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context,original);
        original.SendChat("?missionobject 1");await original.WaitForTextAsync("Object moved");
        var safe=await MissionObjectView(context,original,"Safe");
        original.SendRpc($"80 c7 {MissionNpcId(safe)} 08 00");
        await original.WaitForRpcAsync(AbilityLine(0x7a000145));
        original.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");await original.WaitForRpcAsync("81 19");
        original.SendRpc("81 1f");await original.WaitForRpcAsync("80 a0 01 06 00 01");
        original.SendChat("?missionpersist status");await original.WaitForTextAsync("phase 2");
        var before=await MissionCheckpoint(context,original);
        Check.Equal(1,before.Phase,"Second phase saved");Check.That(before.Objects.Single().Unlocked,"Actual lockpick persisted");
        Check.Equal(2,before.Items.Count,"Prior phase unused disk and current search disk owned");
        original.SendChat("?save");await original.WaitForRpcAsync("2e 07");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.Dispose();context.Server.Dispose();
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs,"mission-persistence.xml");
        using var p=await HeadlessPlayer.LoginAsync(server.Address,username,original.Handle);await p.EnterWorldAsync();
        await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 a0 01 06 00 01");
        var after=await MissionCheckpoint(context,p);
        Check.Equal(before.Identity,after.Identity,"Same acceptance identity");
        Check.Equal(1,after.Phase,"No phase restart");Check.Equal(before.Objects.Single().Spot.pos_x,after.Objects.Single().Spot.pos_x,"Same object placement");
        Check.That(after.Objects.Single().Unlocked,"Safe stays unlocked");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(before.Objects.Single().Goid).Count>0,"Restored safe",TimeSpan.FromSeconds(15));
        safe=p.ViewsOfGoid(before.Objects.Single().Goid).Single();
        Check.Equal(1,p.ViewsOfGoid(before.Objects.Single().Goid).Count,"Exactly one live safe view");
        p.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");await p.WaitForRpcAsync("81 19");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"Prior phase item not duplicated");
        Check.Equal("InnoDB",await context.Database.ScalarAsync("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters'"),"Character engine migrated");
        Check.Equal("InnoDB",await context.Database.ScalarAsync("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='char_missions'"),"Mission engine migrated");
        await context.Database.ExecuteAsync("DELIMITER $$\nCREATE TRIGGER mission_test_rollback BEFORE UPDATE ON char_active_missions FOR EACH ROW BEGIN IF NEW.payload IS NULL AND OLD.payload IS NOT NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='M1 deliberate precommit rollback'; END IF; END$$\nDELIMITER ;\n");
        int disk=after.Items[6786];p.SendRpc($"81 21 {disk:x2}");await p.WaitForRpcAsync("80 a0 02 06 00 01");
        await p.WaitUntilAsync(()=>server.OutputLines("[MISSION] Completion transaction deferred").Any(),"Deliberate completion rollback",TimeSpan.FromSeconds(10));
        Check.Equal(100L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Rolled back XP");
        Check.Equal(1000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Rolled back info");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=399905"),"Rolled back completed record");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=48125"),"Rolled back ticket consumption");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=48134"),"Rolled back ticket reward");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"Rolled back owned-item cleanup");
        Check.That((await MissionCheckpoint(context,p)).Done.Contains(2),"Completed goal remains retryable active phase");
        await context.Database.ExecuteAsync("DROP TRIGGER mission_test_rollback");
        await p.WaitForRpcAsync("80 a2 00");
        Check.Equal(150L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"XP paid once");
        Check.Equal(1060L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Info paid once");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId=399905"),"One completed record");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid IN(48125,6831,6786)"),"Owned cleanup and ticket commit");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=48134"),"Exactly one ticket reward");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NULL AND receipt IS NOT NULL"),"Durable completion receipt");
        p.Dispose();server.Dispose();
        using var last=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs,"mission-persistence.xml");
        using var again=await HeadlessPlayer.LoginAsync(last.Address,username,original.Handle);await again.EnterWorldAsync();
        again.SendChat("?missionpersist status");await again.WaitForTextAsync("Mission persistence: none");
        Check.Equal(150L,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={again.CharId}"),"Relog does not replay completed payout");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={again.CharId} AND goid=48134"),"Relog does not repeat ticket reward");
        Check.Equal(0,again.World.Problems.Count,"Postcompletion records readable");
    }

    private static async Task MissionRelogDisconnectAndMove(ScenarioContext context)
    {
        var original=await StartRelogMission(context);
        var npc=await VisitMissionNpc(context,original,"Herschel");
        original.SendRpc($"80 c7 {MissionNpcId(npc)} 0d 00");await original.WaitForRpcAsync("80 a0 01 06 00 01");
        var before=await MissionCheckpoint(context,original);ushort slot=before.Items[6825];
        original.SendRpc($"65 {Le(slot)} 0a 00");await original.WaitForRpcAsync($"65 {Le(slot)} 0a 00 00");
        Check.Equal(10L,await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={original.CharId} AND goid=6825"),"Owned physical item moved");
        Check.Equal((ushort)10,(await MissionCheckpoint(context,original)).Items[6825],"Owned checkpoint moved atomically");
        original.SendChat("?save");await original.WaitForRpcAsync("2e 07");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.SendRpc("80 fe 00");await original.WaitForRpcAsync("80 fd");original.Dispose();
        // Natural client timeout invokes production CleanDeadPlayers/Suspend and releases the margin reservation.
        await Task.Delay(TimeSpan.FromSeconds(33));
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,username,original.Handle);
        await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 a0 01 06 00 01");
        var restored=await MissionCheckpoint(context,p);
        Check.Equal(before.Identity,restored.Identity,"Same acceptance survives ordinary disconnect");
        Check.Equal((ushort)10,restored.Items[6825],"Moved slot survives disconnect");
        Check.Equal(before.Mobs.Count,restored.Mobs.Count,"Suspended actors recreated once");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"No duplicate moved item");
        p.SendRpc("80 a6");await p.WaitForRpcAsync("80 a2 08");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"Abandon cleans moved ownership");
    }

}
