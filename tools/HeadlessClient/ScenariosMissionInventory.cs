using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Globalization;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task<HeadlessPlayer> StartFullBagMission(ScenarioContext context, string file)
    {
        string directory = Path.Combine(context.RepositoryRoot, "data", "missions");
        XElement mission = XElement.Load(Path.Combine(directory, file));
        int order = (int)mission.Attribute("order")!;
        HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("FullBag")
        {
            X = 58800, Y = 2495, Z = 31900, Level = 50,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, NpcLoot.CodeBit1, 1)).ToArray()
        });
        IEnumerable<string> prior = Directory.GetFiles(directory, "*.xml").Select(XElement.Load)
            .Where(definition => (int?)definition.Attribute("order") < order)
            .Select(definition => $"({player.CharId},{(int)definition.Attribute("id")!})");
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions " +
            "(charId INT UNSIGNED NOT NULL, missionId INT NOT NULL, completions INT NOT NULL DEFAULT 1, PRIMARY KEY (charId, missionId)); " +
            "INSERT INTO char_missions (charId, missionId) VALUES " + string.Join(",", prior));
        await player.EnterWorldAsync();
        player.SendRpc($"80 94 05 00 {(int)mission.Attribute("org")!:x2}");
        await player.WaitForTextAsync((string)mission.Attribute("title")!);
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9e 00");
        return player;
    }

    private static async Task CheckFullBagUnchanged(ScenarioContext context, HeadlessPlayer player)
    {
        Check.Equal(96L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId}"),
            "A refused grant preserves every bag item");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot=0"),
            "No mission item is written into slot zero");
        Check.Equal(0, player.Received("80 a0 ?? 06 00 01").Count, "No objective completed without its item");
    }

    private static async Task FreeLastBagSlot(HeadlessPlayer player)
    {
        player.SendRpc("5d 60 00 01 01");
        await player.WaitForRpcAsync("5f 60 00 01");
    }

    private static async Task<ViewCreation> VisitMissionNpc(ScenarioContext context, HeadlessPlayer player, string name)
    {
        string pattern = @"\[MISSION\] Spawned (?:friendly|hostile) NPC " + Regex.Escape(name) + @" at ([^,\r\n]+),([^,\r\n]+),([^\r\n]+)";
        await player.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), pattern),
            name + " placement", TimeSpan.FromSeconds(15));
        Match placement = Regex.Matches(context.Server.DebugLog(), pattern).Last();
        await MoveForMissionTest(player,
            double.Parse(placement.Groups[1].Value, CultureInfo.InvariantCulture) + 100,
            double.Parse(placement.Groups[2].Value, CultureInfo.InvariantCulture),
            double.Parse(placement.Groups[3].Value, CultureInfo.InvariantCulture));
        return await player.WaitForViewNamedAsync(name, TimeSpan.FromSeconds(30));
    }

    private static async Task FullBagMissionGet(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "machines_02_watchful_eyes.xml");
        ViewCreation herschel = await VisitMissionNpc(context, player, "Herschel");
        player.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await player.WaitForTextAsync("Make room in your inventory");
        await CheckFullBagUnchanged(context, player);
        await FreeLastBagSlot(player);
        player.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await player.WaitForRpcAsync("5e 60 00 a9 1a 00 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(6825L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=96"),
            "Retrying Get grants the item into the freed slot");
    }

    private static async Task FullBagMissionProvided(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "zion_arc_03_just_another_pretty_face.xml");
        await player.WaitForTextAsync("Make room in your inventory");
        ViewCreation maia = await VisitMissionNpc(context, player, "Maia");
        player.SendRpc($"80 c7 {MissionNpcId(maia)} 0e 00");
        byte[] position = maia.Attributes["Position"];
        await MoveForMissionTest(player, BitConverter.ToDouble(position, 0), BitConverter.ToDouble(position, 8),
            BitConverter.ToDouble(position, 16));
        await CheckFullBagUnchanged(context, player);
        await FreeLastBagSlot(player);
        await player.WaitForRpcAsync("5e 60 00 93 1a 00 00");
        Check.Equal(6803L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=96"),
            "The briefing's item is granted after making room");
        player.SendRpc($"80 c7 {MissionNpcId(maia)} 0e 00");
        await player.WaitForRpcAsync("5f 60 00 01");
        await player.WaitForRpcAsync("80 a0 02 06 00 01");
    }

    private static async Task FullBagMissionContainer(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "machines_01_the_end_of_the_beginning.xml");
        player.SendChat("?missionfloor");
        const string placement = @"\[MISSION\] Object 1 \(Safe\) is a (\d+)";
        await player.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), placement),
            "Safe placement", TimeSpan.FromSeconds(15));
        ushort goid = ushort.Parse(Regex.Matches(context.Server.DebugLog(), placement).Last().Groups[1].Value);
        await player.WaitUntilAsync(() => player.ViewsOfGoid(goid).Count > 0, "Safe view", TimeSpan.FromSeconds(10));
        ViewCreation safe = player.ViewsOfGoid(goid).Last();
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1f");
        await player.WaitForTextAsync("Make room in your inventory");
        RpcRecord reply = await player.WaitForRpcAsync("81 20");
        Check.Equal(6799u, BitConverter.ToUInt32(reply.Bytes, 18), "The container reply retains the dossier");
        await CheckFullBagUnchanged(context, player);
        await FreeLastBagSlot(player);
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 60 00 8f 1a 00 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(6799L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=96"),
            "The container keeps its item available until there is room");
    }

    private static async Task FullBagMissionComputer(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "machines_3_2_2_a_veiled_communique.xml");
        player.SendChat("?missionfloor");
        const string placement = @"\[MISSION\] Object 1 \(Computer\) is a (\d+)";
        await player.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), placement),
            "Computer placement", TimeSpan.FromSeconds(15));
        ushort goid = ushort.Parse(Regex.Matches(context.Server.DebugLog(), placement).Last().Groups[1].Value);
        await player.WaitUntilAsync(() => player.ViewsOfGoid(goid).Count > 0, "Computer view", TimeSpan.FromSeconds(10));
        ViewCreation computer = player.ViewsOfGoid(goid).First();
        player.SendRpc($"80 c7 {MissionNpcId(computer)} 03 00");
        await player.WaitForTextAsync("Make room in your inventory");
        await CheckFullBagUnchanged(context, player);
        await FreeLastBagSlot(player);
        player.SendRpc($"80 c7 {MissionNpcId(computer)} 03 00");
        await player.WaitForRpcAsync("5e 60 00 90 1a 00 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
    }

    private static async Task FullBagMissionLoot(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "merovingian_1_3_2_network_connections.xml");
        await DefeatMissionNpc(player, await VisitMissionNpc(context, player, "Binary"));
        player.SendRpc("81 1f");
        await player.WaitForTextAsync("Make room in your inventory");
        RpcRecord reply = await player.WaitForRpcAsync("81 20");
        Check.Equal(6791u, BitConverter.ToUInt32(reply.Bytes, 18), "The loot reply retains the access card");
        await CheckFullBagUnchanged(context, player);
        long infoAfterFirstAttempt = await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
        await FreeLastBagSlot(player);
        long infoAfterRecycle = await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
        Check.That(infoAfterRecycle >= infoAfterFirstAttempt, "Recycling preserves the credited loot money");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 60 00 87 1a 00 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(infoAfterRecycle, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"),
            "Retrying the mission loot does not pay its money again");
    }

    private static async Task FullBagWorldLoot(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("FullBag")
        {
            District = "dt", DistrictId = 2, X = -146900, Y = 295, Z = 50200, Level = 50,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, 2568u, 1)).ToArray()
        }))[0];
        ViewCreation function = await player.WaitForViewNamedAsync("Override Function", TimeSpan.FromSeconds(15));
        await DefeatMissionNpc(player, function);
        player.SendRpc("81 1f");
        await player.WaitForTextAsync("Your inventory is full.");
        RpcRecord reply = await player.WaitForRpcAsync("81 20");
        Check.Equal(48150u, BitConverter.ToUInt32(reply.Bytes, 18), "The loot reply retains Function Data");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot=0"),
            "No world drop is written into slot zero");
        await FreeLastBagSlot(player);
        long infoAfterRecycle = await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 60 00 16 bc 00 00");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=48150"),
            "Retrying after making room grants the retained drop");
        Check.Equal(infoAfterRecycle, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"),
            "World loot money is credited once");
    }
}
