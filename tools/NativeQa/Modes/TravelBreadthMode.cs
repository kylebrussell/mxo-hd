using System.Text.Json;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class TravelBreadthMode : NativeQaMode
{
    private const ushort Mara = 49, SouthVauxton = 7, Tabor = 120;
    private const int StartX = 7800, StartZ = 13795;
    private const string Rider = "QAFriend";
    private static readonly TimeSpan ViewTimeout = TimeSpan.FromSeconds(20);

    public override string Name => "travel-breadth";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, StartX, StartZ,
        Array.Empty<string>(), new[] { "UPDATE characters SET y=95,rotation=192 WHERE charId=110" },
        new[] { $"UPDATE characters SET x={StartX},y=95,z={StartZ} WHERE handle='{Rider}'" },
        DevCommands: true, environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] { "Travel: Mara Northwest hardline to Downtown South Vauxton Central. Ride Yellow Line to Tabor Park, "
            + "open the arrival car's map, then leave from Tabor Park Central to Loading Area and jack back into Mara Northwest. "
            + "Use the printed NAV approaches if needed. See docs/GAMEPLAY-BREADTH-SESSIONS.md." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Travel QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Travel QA requires a live server.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing travel data path."), "data");
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        HardlineNetwork.Load(Path.Combine(data, "hardline_network.csv"));
        SubwayNetwork.Load(Path.Combine(data, "subway_network.csv"));
        var mara = HardlineNetwork.Get(1, Mara);
        var downtown = HardlineNetwork.Get(2, SouthVauxton);
        var tabor = HardlineNetwork.Get(1, Tabor);
        var departure = SubwayNetwork.Get(7, 2, 0);
        var arrival = SubwayNetwork.Get(12, 4, 3);
        Check.That(mara != null && downtown != null && tabor != null && departure?.IsStation == true && arrival?.IsStation == true,
            "The travel fixture has its three booth destinations and both Yellow Line stations");
        var platform = Approach(2, departure!.x + 100, departure.y + 95, departure.z, departure.x - 500, departure.z);
        var exit = Approach(1, tabor!.spotX, tabor.spotY, tabor.spotZ, tabor.x, tabor.z);

        // The probe uses the same route on a disposable helper. CSR remains at the starting booth for native login.
        using var player = await HeadlessPlayer.LoginAsync(context.Address, Rider, Rider);
        await player.EnterWorldAsync();
        var startBooth = await BoothAsync(player, mara!);
        Use(player, startBooth, HardlineBooths.ActionSynchronize);
        await player.WaitForRpcAsync($"80 fa {Le(1u)} {Le((uint)Mara)} 01");

        player.SendRpc($"81 8e {Le((uint)Mara)} {Le(1u)} {Le((uint)SouthVauxton)} {Le(2u)}");
        await LoadCityAsync(player, 2);
        await RequirePositionAsync(database, player, 2, downtown!.spotX, downtown.spotY, downtown.spotZ);
        await BoothAsync(player, downtown);
        await MoveAsync(player, platform);
        await OpenSubwayAsync(player, departure);
        player.SendRpc("81 3d");
        await player.WaitForRpcAsync("81 3e 3c");

        player.SendRpc($"81 3c {Le(arrival!.worldId)} {Le(arrival.network)} {Le(arrival.connector)}");
        await LoadCityAsync(player, 1);
        double[] arrivalPosition = SubwayHandler.Arrival(arrival);
        await RequirePositionAsync(database, player, 1, arrivalPosition[0], arrivalPosition[1], arrivalPosition[2]);
        await OpenSubwayAsync(player, arrival);
        player.SendRpc("81 3b");

        await MoveAsync(player, exit);
        Use(player, await BoothAsync(player, tabor), HardlineBooths.ActionExitToLoadingArea);
        await player.WaitForRpcAsync($"80 fb {Le((uint)Tabor)} 01");
        player.SendRpc("80 fc 01");
        await player.WaitForRpcAsync("80 fd 00 00 00 00 01 00");
        player.SendRpc("80 fe 01");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08 00 00 00 00");
        await player.WaitForLoadingAreaAsync();
        player.World.SendRpcs(WireFormat.FromHex($"6c {Le(1u)} {Le(0u)}"), WireFormat.FromHex("05"));
        await player.WaitForRpcAsync($"6d {Le(1u)}");
        Check.That(!player.World.CurrentOwnViewLife().OwnViewReady, "Loading Area has no in-world player view");

        player.SendRpc($"80 ff {Le(1u)} {Le((uint)Mara)}");
        await LoadCityAsync(player, 1);
        await RequirePositionAsync(database, player, 1, mara!.spotX, mara.spotY, mara.spotZ);
        Use(player, await BoothAsync(player, mara), HardlineBooths.ActionSynchronize);
        await player.WaitForRpcAsync($"80 fa {Le(1u)} {Le((uint)Mara)} 01");
        NativeQaReadiness.RequireNoAmbientAggro(server, player);
        Check.Equal(0, player.World.Problems.Count, "Travel completed without wire errors");
        Check.Equal("1 7800 95 13795", await database.ScalarAsync(
            "SELECT CONCAT(districtId,' ',ROUND(x),' ',ROUND(y),' ',ROUND(z)) FROM characters WHERE charId=110 AND is_online=0"),
            "Native character remains offline at Mara");
        player.SendRpc("80 fe 00");
        await player.WaitForRpcAsync("80 fd");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "travel-bootstrap.json"),
            JsonSerializer.Serialize(new { Hardline = downtown.name, Ride = arrival.name, Returned = mara.name,
                PlatformApproach = platform.Command, ExitApproach = exit.Command }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Travel approach after Downtown arrival: {platform.Command}");
        Console.WriteLine($"Travel approach from Tabor Park platform to the exit booth: {exit.Command}");
        return Array.Empty<byte>();
    }

    private static async Task LoadCityAsync(HeadlessPlayer player, uint logical)
    {
        long oldLife = player.World.CurrentOwnViewLife().Life;
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08 00 00 00 00");
        await player.WaitForRpcAsync($"06 0e 00 {Le(logical)}");
        player.SendRpc("05");
        await player.WaitUntilAsync(() =>
        {
            var life = player.World.CurrentOwnViewLife();
            return life.Life != oldLife && life.OwnViewReady;
        }, "fresh own view after city load", ViewTimeout);
        player.SendRpc("0c 01");
    }

    private static async Task RequirePositionAsync(ThrowawayDatabase database, HeadlessPlayer player, ushort world,
        double x, double y, double z)
    {
        string row = await database.ScalarAsync($"SELECT CONCAT(districtId,' ',ROUND(x),' ',ROUND(y),' ',ROUND(z)) FROM characters WHERE charId={player.CharId}");
        Check.Equal(FormattableString.Invariant($"{world} {Math.Round(x)} {Math.Round(y)} {Math.Round(z)}"), row,
            "Saved arrival district and position");
    }

    private static Task<ViewCreation> BoothAsync(HeadlessPlayer player, Hardline booth) =>
        NearbyAsync(player, HardlineBooths.PhoneBoothType, booth.x, booth.z, 80);

    private static async Task OpenSubwayAsync(HeadlessPlayer player, SubwayStation station)
    {
        var car = await NearbyAsync(player, 6568, station.x, station.z, 800);
        Use(player, car, 2);
        await player.WaitForRpcAsync("81 3a " + WireFormat.ToHex(new[] { station.x, station.y, station.z }
            .SelectMany(BitConverter.GetBytes).ToArray()));
    }

    private static async Task<ViewCreation> NearbyAsync(HeadlessPlayer player, ushort goid, double x, double z, double radius)
    {
        ViewCreation? view = null;
        await player.WaitUntilAsync(() =>
        {
            var life = player.World.CurrentOwnViewLife();
            view = life.Events.SelectMany(e => WireFormat.FindCreations(e.Bytes)).LastOrDefault(v =>
                v.Goid == goid && v.Attributes.TryGetValue("Position", out var position) && position.Length == 24 &&
                Math.Abs(BitConverter.ToDouble(position) - x) <= radius &&
                Math.Abs(BitConverter.ToDouble(position, 16) - z) <= radius);
            return life.OwnViewReady && view != null;
        }, $"current view of GO {goid} near {x},{z}", ViewTimeout);
        return view!;
    }

    private static void Use(HeadlessPlayer player, ViewCreation view, byte action) =>
        player.SendRpc($"80 c7 {Le(view.ViewId)} {view.SpawnId:x2} 00 {action:x2} 00");

    private sealed record Stand(string Command, string Confirmation);

    private static Stand Approach(ushort world, double x, double y, double z, double faceX, double faceZ)
    {
        Check.That(NavMesh.Project(world, x, y, z, out var point, maxDistance: 180), "Travel approach lands on NAV");
        byte yaw = MobAggro.YawTowards(point.x, point.z, faceX, faceZ);
        var nav = point.location;
        string command = FormattableString.Invariant($"?tp {point.x:0.###} {point.y:0.###} {point.z:0.###} {nav.sector:X} {nav.region:X} {nav.polygon:X} - {yaw:X2}");
        return new Stand(command, $"Teleported to {nav.sector:X}/{nav.region:X}");
    }

    private static async Task MoveAsync(HeadlessPlayer player, Stand stand)
    {
        player.SendChat(stand.Command);
        await player.WaitForTextAsync(stand.Confirmation);
    }

    private static string Le(uint value) => Convert.ToHexString(BitConverter.GetBytes(value));
    private static string Le(ushort value) => Convert.ToHexString(BitConverter.GetBytes(value));
}
