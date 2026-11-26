namespace HeadlessClient;

public static partial class Scenarios
{
    private const string RichlandTutorialHardline = "80 fa 01 00 00 00 99 00 00 00 01";

    private static async Task ClientActionEnterLoadingArea(HeadlessPlayer player)
    {
        player.SkipReceived();
        player.SendRpc("80 fc 01");
        await player.WaitForRpcAsync("80 fd 00 00 00 00 01 00");
        player.SendRpc("80 fe 01");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08 00 00 00 00");
        await WaitForLoadingAreaSetupBatchAsync(player);
    }

    private static async Task TutorialRichlandHandoff(ScenarioContext context)
    {
        var player = (await context.AddPlayersInWorldAsync(new CharacterSeed("TutorialHandoff")
            { X = 23828, Y = 495, Z = 13984 }))[0];
        await ClientActionEnterLoadingArea(player);
        player.SendRpc("81 01");
        player.SendRpc("6c 01 00 00 00 00 00 00 00");
        await player.WaitForRpcAsync("6d 01 00 00 00");
        Check.Equal(0, player.Received(RichlandTutorialHardline).Count, "An ordinary LA entry grants no tutorial handoff");

        player.SendRpc("81 00");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08 00 00 00 00");
        await player.WaitForRpcAsync("06 0e 00 20 03 00 00");
        player.SendRpc("05");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("[TUTORIAL] Begins"),
            "tutorial entry", TimeSpan.FromSeconds(15));

        player.SendRpc("80 a6");
        await ClientActionEnterLoadingArea(player);
        int spawns = player.World.CreationSnapshot().Count(view => view.ViewId == 2);
        player.SendRpc("81 01 00");
        player.SendRpc("6c 01 00 00 00 00 00 00 00");
        await player.WaitForRpcAsync("6d 01 00 00 00");
        Check.Equal(0, player.Received(RichlandTutorialHardline).Count, "Malformed 81 01 does not consume tutorial eligibility");

        // norajob_create_female_char_mxoemu2, client RPC 81 01 precedes 80 fa and the world reset.
        player.World.SendRpcs(WireFormat.FromHex("81 01"), WireFormat.FromHex("05"), WireFormat.FromHex("81 01"));
        await player.WaitForRpcAsync(RichlandTutorialHardline);
        await player.WaitForRpcAsync("81 07");
        Check.Equal(1, player.Received(RichlandTutorialHardline).Count, "Only one tutorial handoff");
        Check.Equal(spawns, player.World.CreationSnapshot().Count(view => view.ViewId == 2), "No stale 05 spawn during the reset");

        player.SendRpc("81 08 00 00 00 00");
        await player.WaitForRpcAsync("06 0e 00 01 00 00 00");
        player.SendRpc("05");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(view => view.ViewId == 2) > spawns,
            "Richland self spawn", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={player.CharId}"),
            "The server-owned destination persists");
        Check.Equal(0, player.World.Problems.Count, "Tutorial transition packets decode");
    }

    private static async Task LoadingAreaOrdinaryArchiveCompile(ScenarioContext context)
    {
        const uint item = 2399, encoded = item << 7;
        const uint revolver = 700;
        var player = await context.AddPlayerAsync(new CharacterSeed("ArchiveCompile") { Info = 250 });
        await context.Database.ExecuteAsync("INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES" +
            $"({player.CharId},400,{encoded},2,0),({player.CharId},401,{revolver << 7},2,0)");
        await player.EnterWorldAsync();
        const string request = "60 5f 09 00 00 90 01 00 6b 00";
        player.SendRpc(request);
        await player.WaitForRpcAsync("61 01");
        await ClientActionEnterLoadingArea(player);

        player.SendRpc("60 5f 09 00 00 90 01 00 01 01");
        await player.WaitForRpcAsync("61 01");
        player.SendRpc(request);
        await player.WaitForRpcAsync("61 00");
        Check.Equal(1, player.Received("69 00 90 01 81 af 04 00 00 00").Count, "A direct charge remains in archive");
        player.SendRpc(request);
        await player.WaitForRpcAsync("61 00");
        Check.Equal(1, player.Received("5f 90 01 00").Count, "Last direct charge removes the archive row");
        player.SendRpc(request);
        await player.WaitForRpcAsync("61 01");
        player.SendRpc("60 bc 02 00 00 91 01 00 6b 00");
        await player.WaitForRpcAsync("61 00");
        player.SendRpc("60 bc 02 00 00 91 01 00 6b 00");
        await player.WaitForRpcAsync("61 04");
        Check.Equal(112L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"),
            "Two 9 Info debits and one 120 Info debit");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={player.CharId} AND goid={item}"),
            "Two bag items from two charged successes");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={player.CharId} AND slot=400"),
            "Replaying a depleted source cannot add another item");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT level FROM char_abilities WHERE char_id={player.CharId} AND slot=401"),
            "Insufficient Info preserves the remaining revolver charge");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid={revolver}"),
            "Exactly one ordinary weapon materialized");
        Check.Equal(0, player.World.Problems.Count, "Archive compile packets decode");
    }
}
