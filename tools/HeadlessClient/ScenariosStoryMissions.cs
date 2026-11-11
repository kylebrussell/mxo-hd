using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task MerovingianChapter12(ScenarioContext context)
    {
        string missionDirectory = Path.Combine(context.RepositoryRoot, "data", "missions");
        XElement[] definitions = Directory.GetFiles(missionDirectory, "*.xml")
            .Select(XElement.Load)
            .Where(mission => (string?)mission.Attribute("org") == "3")
            .ToArray();
        string[] files =
        {
            "merovingian_12_1_1_hothouse.xml",
            "merovingian_12_1_2_deep_cover.xml",
            "merovingian_12_1_3_courtship.xml"
        };

        for (int index = 0; index < files.Length; index++)
        {
            XElement mission = XElement.Load(Path.Combine(missionDirectory, files[index]));
            int order = (int)mission.Attribute("order")!;
            HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("ChapterProbe" + index)
            {
                X = 58800, Y = 2495, Z = 31900, Level = 50
            });
            await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions " +
                "(charId INT UNSIGNED NOT NULL, missionId INT NOT NULL, completions INT NOT NULL DEFAULT 1, PRIMARY KEY (charId, missionId))");
            IEnumerable<string> prior = definitions.Where(definition => (int)definition.Attribute("order")! < order)
                .Select(definition => $"({player.CharId},{(int)definition.Attribute("id")!})");
            await context.Database.ExecuteAsync("INSERT INTO char_missions (charId, missionId) VALUES " + string.Join(",", prior));

            await player.EnterWorldAsync();
            player.SendRpc("80 94 05 00 03");
            await player.WaitForTextAsync((string)mission.Attribute("title")!);
            await player.WaitForRpcAsync("80 97 05 00");
            player.SendRpc("80 98 06 00 00 00 01 02 00");
            await player.WaitForRpcAsync("80 99");
            player.SendRpc("80 9b 07 00 01");
            await player.WaitForRpcAsync("80 9c");
            await player.WaitForRpcAsync("80 9e 00");
            player.SendChat("?missionfloor");

            switch (index)
            {
                case 0:
                    await PlayHothouse(player);
                    break;
                case 1:
                    await PlayDeepCover(context, player);
                    break;
                case 2:
                    await PlayCourtship(player);
                    break;
            }

            await LeaveMissionArea(player);
            await player.WaitForRpcAsync("80 a2 00", TimeSpan.FromSeconds(15));
            Check.Equal(1L, await context.Database.LongAsync($"SELECT completions FROM char_missions " +
                $"WHERE charId={player.CharId} AND missionId={(int)mission.Attribute("id")!}"), "Chapter 12 completion recorded");
        }
    }

    private static async Task PlayHothouse(HeadlessPlayer player)
    {
        await TalkToMissionNpc(player, "Ookami");
        await player.WaitForTextAsync("Leave the area with Ookami");
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("She what?!");
        await player.WaitForRpcAsync("80 9e 00");
        player.SendChat("?missionfloor");
        await TalkToMissionNpc(player, "Tesarova");
        await player.WaitForTextAsync("little floating man");
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("It's nice to know");
        await player.WaitForRpcAsync("80 9e 00");
        player.SendChat("?missionfloor");
        foreach (string name in new[] { "Machine Vector", "Machine Despoiler", "Machine Karate Master" })
        {
            await DefeatMissionNpc(player, await player.WaitForViewNamedAsync(name));
        }
    }

    private static async Task PlayDeepCover(ScenarioContext context, HeadlessPlayer player)
    {
        const string placement = @"\[MISSION\] Object 1 \(Furniture\) is a (\d+)";
        await player.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), placement),
            "Furniture placement", TimeSpan.FromSeconds(15));
        Match match = Regex.Matches(context.Server.DebugLog(), placement).Last();
        ushort goid = ushort.Parse(match.Groups[1].Value);
        await player.WaitUntilAsync(() => player.ViewsOfGoid(goid).Count > 0, "Furniture view", TimeSpan.FromSeconds(10));
        ViewCreation furniture = player.ViewsOfGoid(goid).Last();
        player.SendRpc($"80 c7 {MissionNpcId(furniture)} 03 00");
        await player.WaitForTextAsync("Machines, and no Azuna");
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("our contact's source location");
        await player.WaitForRpcAsync("80 9e 00");
        player.SendChat("?missionfloor");
        await player.WaitUntilAsync(() => player.ViewsOfGoid(599).Count(view => view.Name == "Override Function") >= 3,
            "Three Override Functions", TimeSpan.FromSeconds(15));
        foreach (ViewCreation npc in player.ViewsOfGoid(599).Where(view => view.Name == "Override Function").TakeLast(3))
        {
            await DefeatMissionNpc(player, npc);
        }
    }

    private static async Task PlayCourtship(HeadlessPlayer player)
    {
        await TalkToMissionNpc(player, "Tesarova");
        await player.WaitForTextAsync("Carlyne's android body");
        await TalkToMissionNpc(player, "Persephone");
        await player.WaitForTextAsync("Only a human can be");
        await TalkToMissionNpc(player, "The Merovingian");
        await player.WaitForTextAsync("There must be a way");
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("traceback from another position");
        await player.WaitForRpcAsync("80 9e 00");
        player.SendChat("?missionfloor");
        await DefeatMissionNpc(player, await player.WaitForViewNamedAsync("Override Executable"));
    }

    private static string MissionNpcId(ViewCreation view) => $"{Le(view.ViewId)} {Le((ushort)view.SpawnId)}";

    private static async Task TalkToMissionNpc(HeadlessPlayer player, string name)
    {
        ViewCreation npc = await player.WaitForViewNamedAsync(name);
        player.SendRpc($"80 c7 {MissionNpcId(npc)} 09 00");
    }

    private static async Task MoveForMissionTest(HeadlessPlayer player, double x, double y, double z)
    {
        player.SendChat(FormattableString.Invariant($"?tp {x} {y} {z} 0 0 0"));
        await player.WaitForTextAsync("Teleported to 0/0");
    }

    private static async Task LeaveMissionArea(HeadlessPlayer player)
    {
        byte[] waypoint = player.Received("80 9e 00").Last().Bytes;
        await MoveForMissionTest(player, BitConverter.ToDouble(waypoint, 3), BitConverter.ToDouble(waypoint, 11),
            BitConverter.ToDouble(waypoint, 19));
    }

    private static async Task DefeatMissionNpc(HeadlessPlayer player, ViewCreation npc)
    {
        byte[] position = npc.Attributes["Position"];
        await MoveForMissionTest(player, BitConverter.ToDouble(position, 0) + 100, BitConverter.ToDouble(position, 8),
            BitConverter.ToDouble(position, 16));
        player.SendChat("?weaken 1");
        player.SendRpc("3d 01");
        player.SendRpc($"41 {MissionNpcId(npc)}");
        await player.WaitForRpcAsync("81 19", TimeSpan.FromSeconds(30));
    }
}
