using System.Xml.Linq;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CollectMissionTicket(HeadlessPlayer player, byte index, bool success)
    {
        player.SendRpc("80 c8 ac 05 a0 07 01 00");
        await player.WaitForRpcAsync("81 13");
        player.SendRpc($"81 16 {index:x2} 00 00");
        await player.WaitForRpcAsync(success ? "81 17 00 00 00 00 01" : "81 17 00 00 00 00 00");
    }

    private static async Task MissionTicketCollector(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Tickets")
        {
            District = "it", DistrictId = 3, X = 63150, Y = -1055, Z = -23050
        }))[0];
        uint[] tickets = { 48125, 48128, 48131 };
        player.SendRpc("80 c8 ac 05 a0 07 01 00");
        await player.WaitForRpcAsync("81 13");
        for (byte index = 0; index < tickets.Length; index++)
        {
            RpcRecord offer = await player.WaitForRpcAsync("81 14");
            Check.Equal(index, offer.Bytes[18], "Collector item-set index");
            Check.Equal(tickets[index], BitConverter.ToUInt32(offer.Bytes, 27), "Collector issues the usable mission ticket");
            Check.Equal(0x040006e2u + index, BitConverter.ToUInt32(offer.Bytes, 36), "Per-offer retail dialogue");
        }
        await player.WaitForRpcAsync("81 14");
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 01");
        await CollectMissionTicket(player, 1, true);
        await CollectMissionTicket(player, 2, true);
        await CollectMissionTicket(player, 0, false);
        foreach (uint ticket in tickets)
        {
            Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid={ticket}"),
                "A collector ticket is singleton");
        }
        player.SendRpc("65 01 00 61 00");
        await player.WaitForRpcAsync("65 01 00 61 00");
        await CollectMissionTicket(player, 0, false);
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=48125"),
            "Moving a ticket out of the bag cannot bypass singleton issuance");

        HeadlessPlayer full = (await context.AddPlayersInWorldAsync(new CharacterSeed("FullTickets")
        {
            District = "it", DistrictId = 3, X = 63150, Y = -1055, Z = -23050,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, 2568u, 1)).ToArray()
        }))[0];
        await CollectMissionTicket(full, 0, false);
        Check.Equal(96L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={full.CharId}"),
            "A refused full-bag grant preserves the bag");
        await FreeLastBagSlot(full);
        await CollectMissionTicket(full, 0, true);
        Check.Equal(48125L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={full.CharId} AND slot=96"),
            "Issuance can be retried after making room");
    }

    private static async Task MissionTicketGates(ScenarioContext context)
    {
        XElement[] missions = MerovingianDefinitions(context).Where(mission => mission.Attribute("entryItem") != null).ToArray();
        int character = 0;
        foreach (XElement mission in missions)
        {
            uint ticket = (uint)mission.Attribute("entryItem")!;
            int level = (int)mission.Attribute("minLevel")!;
            int reputation = (int)mission.Attribute("minReputation")!;
            int district = (int)mission.Attribute("district")!;
            foreach (string gate in new[] { "level", "reputation", "district", "valid" })
            {
                int playerDistrict = gate == "district" ? (district == 1 ? 2 : 1) : district;
                HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Gate" + ++character)
                {
                    Level = gate == "level" ? level - 1 : level,
                    DistrictId = playerDistrict, District = playerDistrict == 2 ? "dt" : "slums",
                    X = 58800, Y = 2495, Z = 31900, Items = new[] { (1, ticket, 1) }
                }))[0];
                player.SendChat("?rep mero " + (gate == "reputation" ? reputation - 1 : reputation));
                await player.WaitForRpcAsync("80 ab 03");
                player.SendRpc("59 01");
                if (gate == "valid")
                {
                    await player.WaitForRpcAsync("80 9c");
                    await player.WaitForRpcAsync("80 9e 00");
                    player.SendRpc("59 01");
                    await player.WaitForTextAsync("Finish or abort your current mission");
                    Check.Equal(1, player.Received("80 9c").Count, "A second use cannot replace the running mission");
                    player.SendRpc("80 a6");
                    await player.WaitForRpcAsync("80 a2 08");
                    player.SendRpc("59 01");
                    await player.WaitForRpcAsync("80 9c");
                    await player.WaitForRpcAsync("80 9e 00");
                    player.SendRpc("80 a6");
                    await player.WaitForRpcAsync("80 a2 08");
                }
                else
                {
                    string expected = gate switch
                    {
                        "level" => "requires level " + level,
                        "reputation" => "requires at least " + reputation + " reputation",
                        _ => "Use this mission ticket in"
                    };
                    await player.WaitForTextAsync(expected);
                    Check.Equal(0, player.Received("80 9c").Count, "A refused ticket starts no mission");
                }
                Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid={ticket}"),
                    "Gates and aborts preserve the ticket");
            }
        }
    }

    private static Task FillTicketCompletionBag(ScenarioContext context, HeadlessPlayer player)
    {
        string rows = string.Join(",", Enumerable.Range(1, 95).Select(slot => $"({player.CharId},2568,{slot},1)"));
        return context.Database.ExecuteAsync("INSERT INTO inventory (charId,goid,slot,count) VALUES " + rows);
    }

    private static async Task MissionTicketCompletionRetry(ScenarioContext context)
    {
        XElement[] definitions = MerovingianDefinitions(context);
        XElement mission = definitions.Single(definition => (int)definition.Attribute("id")! == 312101);
        HeadlessPlayer player = await StartMerovingianStoryMission(context, mission, definitions);
        player.SendRpc("65 01 00 60 00");
        await player.WaitForRpcAsync("65 01 00 60 00");
        await EnterMissionArea(context, player);
        await PlayHothouse(context, player);
        await FillTicketCompletionBag(context, player);
        await context.Database.ExecuteAsync($"UPDATE inventory SET count=2 WHERE charId={player.CharId} AND slot=96");
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("make room for the completion reward");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=96"),
            "A failed completion grant rolls ticket consumption back");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={player.CharId} AND missionId=312101"),
            "A blocked reward leaves the mission incomplete");
        player.SendRpc("5d 5f 00 01 01");
        await player.WaitForRpcAsync("5f 5f 00 01");
        await player.WaitForRpcAsync("80 a2 00", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND goid=48125"),
            "Retry consumes exactly one legacy ticket");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=48134"),
            "Retry grants exactly one verification token");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={player.CharId} AND missionId=312101"),
            "Retry completes the mission once");
    }
}
