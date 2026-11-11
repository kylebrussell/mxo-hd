using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static XElement[] MerovingianDefinitions(ScenarioContext context)
    {
        string missionDirectory = Path.Combine(context.RepositoryRoot, "data", "missions");
        return Directory.GetFiles(missionDirectory, "*.xml")
            .Select(XElement.Load)
            .Where(mission => (string?)mission.Attribute("org") == "3")
            .ToArray();
    }

    private static async Task<HeadlessPlayer> StartMerovingianStoryMission(ScenarioContext context, XElement mission,
        XElement[] definitions)
    {
        int order = (int)mission.Attribute("order")!;
        HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("Story" + (int)mission.Attribute("id")!)
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
        return player;
    }

    private static async Task MerovingianChapterSamples(ScenarioContext context)
    {
        XElement[] definitions = MerovingianDefinitions(context);
        IEnumerable<XElement> samples = definitions.Where(mission =>
        {
            int order = (int)mission.Attribute("order")!;
            return order >= 2000 && order < 12000 && order % 1000 == 101;
        }).OrderBy(mission => (int)mission.Attribute("order")!);
        foreach (XElement mission in samples)
        {
            HeadlessPlayer player = await StartMerovingianStoryMission(context, mission, definitions);
            string title = (string)mission.Attribute("title")!;
            int objectives = mission.Element("phase")!.Elements("objective")
                .Count(objective => ((int?)objective.Attribute("after") ?? -1) < 0);
            await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("[MISSION] Accepted '" + title + "'"),
                title + " staged", TimeSpan.FromSeconds(15));
            await player.WaitUntilAsync(() => player.Received("80 a0").Count >= objectives,
                title + " objectives", TimeSpan.FromSeconds(10));
            Check.Equal(objectives, player.Received("80 a0").Count, title + " first-area objectives");
            player.SendRpc("80 a6");
            await player.WaitForRpcAsync("80 a2 08");
            Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions " +
                $"WHERE charId={player.CharId} AND missionId={(int)mission.Attribute("id")!}"), "Aborted sample stays incomplete");
        }
    }

    private static async Task MerovingianChapter12(ScenarioContext context)
    {
        XElement[] definitions = MerovingianDefinitions(context);
        int[] ids = { 312101, 312102, 312103 };
        for (int index = 0; index < ids.Length; index++)
        {
            XElement mission = definitions.Single(definition => (int)definition.Attribute("id")! == ids[index]);
            HeadlessPlayer player = await StartMerovingianStoryMission(context, mission, definitions);
            await EnterMissionArea(player);

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
        await EnterMissionArea(player);
        await TalkToMissionNpc(player, "Tesarova");
        await player.WaitForTextAsync("little floating man");
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("It's nice to know");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(player);
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
        await EnterMissionArea(player);
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
        await EnterMissionArea(player);
        await DefeatMissionNpc(player, await player.WaitForViewNamedAsync("Override Executable"));
    }

    /// <summary>The operator's client-string lines (80 a1 with the operator's portrait and name).</summary>
    private const string GoalsCompleteLeaveLine = "80 a1 70 0a 00 28 14 00 00 26 14 00 0c 00 00 26";
    private const string GoalsCompleteLine = "80 a1 70 0a 00 28 14 00 00 26 14 00 04 00 00 26";

    /// <summary>
    /// Zoochemy (11.1.4): Security and the Machines arrive when Tarka has been talked to (spawnAfter, enemiesAfter), phase 3
    /// ends on leading Tarka out with "Goals for this mission phase are complete!" and phase 4 follows at once, and phase 4
    /// is leading Tarka to the mainframe (to="object:1").
    /// </summary>
    private static async Task MissionArrivalsAndEscortToObject(ScenarioContext context)
    {
        XElement[] definitions = MerovingianDefinitions(context);
        XElement mission = definitions.Single(definition => (int)definition.Attribute("id")! == 311104);
        HeadlessPlayer player = await StartMerovingianStoryMission(context, mission, definitions);

        // Phase 1: the mainframe
        await EnterMissionArea(player);
        await UseMissionObject(context, player, "Mainframe", "04");
        await player.WaitForTextAsync("Some kind of program just merged");
        await player.WaitForRpcAsync(GoalsCompleteLeaveLine);
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("Of all the idiotic");
        await player.WaitForRpcAsync("80 9e 00");

        // Phase 2: Silver
        await EnterMissionArea(player);
        await TalkToMissionNpc(player, "Silver");
        await player.WaitForTextAsync("Any discomfort?");
        await player.WaitForRpcAsync(GoalsCompleteLeaveLine);
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("rendezvous with an operative of ours named Tarka");
        await player.WaitForRpcAsync("80 9e 00");

        // Phase 3: nobody hostile until Tarka has been talked to. On a small floor Tarka stands near enough the elevator
        // door that leading Tarka out is done as soon as Tarka follows.
        await EnterMissionArea(player);
        ViewCreation tarka = await player.WaitForViewNamedAsync("Tarka");
        byte[] tarkaAt = tarka.Attributes["Position"];
        await MoveForMissionTest(player, BitConverter.ToDouble(tarkaAt, 0) + 100, BitConverter.ToDouble(tarkaAt, 8),
            BitConverter.ToDouble(tarkaAt, 16));
        await Task.Delay(1500);
        Check.That(!context.Server.DebugLog().Contains("Security arrives"), "Security arrives only after the talk");
        Check.That(!player.World.CreationSnapshot().Any(view => view.Name == "Security" || view.Name == "Machine Program"),
            "No hostiles before the talk");
        player.SendRpc($"80 c7 {MissionNpcId(tarka)} 09 00");
        await player.WaitForTextAsync("Did you feel something?");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("[MISSION] Security arrives") &&
                                          context.Server.DebugLog().Contains("[MISSION] 2 hostiles arrive"), "Arrivals", TimeSpan.FromSeconds(10));
        await player.WaitForViewNamedAsync("Security");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(view => view.Name == "Machine Program") >= 2,
            "Two Machine Programs", TimeSpan.FromSeconds(10));
        // Security's near line works from its arrival: it stands beside the player
        await player.WaitForTextAsync("You are under arrest");

        // Leading Tarka out ends the phase without a "find the exit", and phase 4's briefing follows at once. Phase 4's
        // mainframe is spawned as the phase starts.
        int seen = player.World.CreationSnapshot().Count;
        await LeaveMissionArea(player);
        await player.WaitUntilAsync(() => player.Received(GoalsCompleteLine).Count == 1, "Goals for this mission phase are complete!",
            TimeSpan.FromSeconds(15));
        await player.WaitForTextAsync("one more part to this experiment");
        Check.Equal(2, player.Received(GoalsCompleteLeaveLine).Count, "Area lines before phase 4");
        await player.WaitForRpcAsync("80 9e 00");

        // Phase 4: lead Tarka to the mainframe. Tarka follows the player up and to it.
        await EnterMissionArea(player);
        ViewCreation mainframe = await MissionObjectView(context, player, "Mainframe", seen);
        byte[] position = mainframe.Attributes["Position"];
        await MoveForMissionTest(player, BitConverter.ToDouble(position, 0) + 100, BitConverter.ToDouble(position, 8),
            BitConverter.ToDouble(position, 16));
        await player.WaitForTextAsync("tucked back into a mainframe", TimeSpan.FromSeconds(15));
        await player.WaitForRpcAsync(GoalsCompleteLeaveLine);
        await LeaveMissionArea(player);
        await player.WaitForRpcAsync("80 a2 00", TimeSpan.FromSeconds(15));
        Check.Equal(1, player.Received(GoalsCompleteLine).Count, "One mission-phase line");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT completions FROM char_missions " +
            $"WHERE charId={player.CharId} AND missionId=311104"), "Zoochemy completion recorded");
    }

    /// <summary>The current phase's newest object of this kind, as the server placed it.</summary>
    /// <param name="seen">Creation records before the phase's: an earlier phase's object of the same kind is skipped.</param>
    private static async Task<ViewCreation> MissionObjectView(ScenarioContext context, HeadlessPlayer player, string kind, int seen = 0)
    {
        string placement = @"\[MISSION\] Object 1 \(" + kind + @"\) is a (\d+)";
        await player.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), placement), kind + " placement", TimeSpan.FromSeconds(15));
        ushort goid = ushort.Parse(Regex.Matches(context.Server.DebugLog(), placement).Last().Groups[1].Value);
        List<ViewCreation> Fresh() => player.World.CreationSnapshot().Skip(seen).Where(view => view.Goid == goid).ToList();
        await player.WaitUntilAsync(() => Fresh().Count > 0, kind + " view", TimeSpan.FromSeconds(10));
        return Fresh().Last();
    }

    private static async Task UseMissionObject(ScenarioContext context, HeadlessPlayer player, string kind, string use)
    {
        ViewCreation view = await MissionObjectView(context, player, kind);
        player.SendRpc($"80 c7 {MissionNpcId(view)} {use} 00");
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

    private static Task LeaveMissionArea(HeadlessPlayer player)
    {
        byte[] waypoint = player.Received("80 9e 00").Last().Bytes;
        double x = BitConverter.ToDouble(waypoint, 3);
        double y = BitConverter.ToDouble(waypoint, 11);
        double z = BitConverter.ToDouble(waypoint, 19);
        // The phase can advance before the teleport acknowledgment. Keep its briefing in the receive cursor.
        player.SendChat(FormattableString.Invariant($"?tp {x} {y} {z} 0 0 0"));
        return Task.CompletedTask;
    }

    private static async Task EnterMissionArea(HeadlessPlayer player)
    {
        byte[] waypoint = player.Received("80 9e 00").Last().Bytes;
        await MoveForMissionTest(player, BitConverter.ToDouble(waypoint, 3), BitConverter.ToDouble(waypoint, 11),
            BitConverter.ToDouble(waypoint, 19));
        player.SendChat("?missionfloor");
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
