namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task<HeadlessPlayer> StartEncounterFixture(ScenarioContext context, string handle)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed(handle) { Level = 50 }))[0];
        player.SendRpc("80 94 05 00 03");
        await player.WaitForTextAsync("Encounter Runtime Fixture");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForRpcAsync("80 9e 00");
        return player;
    }

    private static async Task<ViewCreation> WaitForNewNpc(HeadlessPlayer player, string name, int seen)
    {
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Skip(seen).Any(view => view.Name == name),
            "New creation of " + name, TimeSpan.FromSeconds(15));
        return player.World.CreationSnapshot().Skip(seen).Last(view => view.Name == name);
    }

    private static async Task MissionWavesSidesAndTimers(ScenarioContext context)
    {
        HeadlessPlayer player = await StartEncounterFixture(context, "EncounterPass");
        await EnterMissionArea(context, player);
        ViewCreation turncoat = await player.WaitForViewNamedAsync("Turncoat");
        ViewCreation first = await player.WaitForViewNamedAsync("Machine Program");
        Check.That(!turncoat.Attributes.ContainsKey("IsEnemy"), "The turncoat is initially talkable");
        Check.That(!player.World.CreationSnapshot().Any(view => view.Name == "Zion Soldier" || view.Name == "Witness"),
            "The second wave and witness wait for their goals");
        int seen = player.World.CreationSnapshot().Count;
        player.SendRpc($"80 c7 {MissionNpcId(turncoat)} 09 00");
        ViewCreation hostile = await WaitForNewNpc(player, "Turncoat", seen);
        Check.That(hostile.Attributes.ContainsKey("IsEnemy") && !hostile.Attributes.ContainsKey("TalkDefaultable"),
            "The replacement creation offers combat after the side change");
        await DefeatMissionNpc(player, hostile);
        await DefeatMissionNpc(player, first);
        await player.WaitForTextAsync("Second wave arrives.");
        ViewCreation second = await player.WaitForViewNamedAsync("Zion Soldier");
        Check.Equal(0, player.Received("80 a0 03 06 00 01").Count, "The second wave is still required");
        await DefeatMissionNpc(player, second);
        await player.WaitForTextAsync("Hold-out completed.", TimeSpan.FromSeconds(15));
        ViewCreation witness = await player.WaitForViewNamedAsync("Witness");
        ViewCreation mainframe = await MissionObjectView(context, player, "Mainframe");
        byte[] at = witness.Attributes["Position"], target = mainframe.Attributes["Position"];
        double dx = BitConverter.ToDouble(at, 0) - BitConverter.ToDouble(target, 0);
        double dz = BitConverter.ToDouble(at, 16) - BitConverter.ToDouble(target, 16);
        // Runtime arrivals may use a site NPC spot when the object's origin has no reachable floor nearby.
        // The old fixed four-metre assertion depended on which random mission site was allocated.
        hds.SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        hds.NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        double[] placed = PositionOf(witness), reference = PositionOf(turncoat);
        Check.That(hds.NavMesh.Locate(1, placed[0], placed[1], placed[2], out var floor) &&
            Math.Abs(placed[1] - floor.floorY - 95) < 0.01, "The delayed witness stands on walkable floor");
        Check.That(hds.NavMesh.Project(1, placed[0], placed[1], placed[2], out _, 1, reference),
            "The delayed witness is reachable from the site's original NPC");
        Check.That(dx * dx + dz * dz < 800 * 800 || context.Server.DebugLog().Contains("Arrival uses a walkable site NPC spot"),
            "The delayed witness arrives near the object or uses the walkable site fallback");
        player.SendRpc($"80 c7 {MissionNpcId(witness)} 09 00");
        await player.WaitForTextAsync("Encounter fixture second area.");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context, player);
        ViewCreation defector = await player.WaitForViewNamedAsync("Defector");
        Check.That(defector.Attributes.ContainsKey("IsEnemy"), "The defector initially offers combat");
        seen = player.World.CreationSnapshot().Count;
        await TalkToMissionNpc(player, "Controller");
        ViewCreation friendly = await WaitForNewNpc(player, "Defector", seen);
        Check.That(!friendly.Attributes.ContainsKey("IsEnemy") && friendly.Attributes.ContainsKey("TalkDefaultable"),
            "The defector becomes talkable");
        player.SendRpc($"80 c7 {MissionNpcId(friendly)} 09 00");
        await player.WaitForRpcAsync("80 a2 00", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={player.CharId} AND missionId=399902"),
            "Waves, both side changes and hold-outs complete once");

        HeadlessPlayer late = await StartEncounterFixture(context, "EncounterLate");
        await Task.Delay(11000); // Longer than the ten-second limit: loading/travel must not use the goal's clock.
        Check.Equal(0, late.Received("80 a2").Count, "The deadline waits for the player to reach the generated area");
        await EnterMissionArea(context, late);
        await late.WaitForTextAsync("You ran out of time.", TimeSpan.FromSeconds(20));
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={late.CharId} AND missionId=399902"),
            "A timed-out mission records no completion");
    }
}
