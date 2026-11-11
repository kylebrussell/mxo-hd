using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static string Doubles(double x, double y, double z) =>
        WireFormat.ToHex(BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(z)).ToArray());

    /// <summary>
    /// A ride on the Red Line from Tabor Park to Moriah Projects, by the platform's subway car (80 c7 ... 02 on the 6568
    /// view): 81 3a opens the map on Tabor Park's connector, the client's 81 3d starts the 81 3e countdown, the pick 81 3c
    /// 0c 00 01 00 00 00 reloads the world (81 07) and the player stands on Moriah Projects' platform.
    /// </summary>
    private static async Task SubwayRideByCar(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(
            new CharacterSeed("Rider") { X = 68300, Y = -705, Z = 47500 }))[0];

        await player.WaitUntilAsync(() => player.ViewsOfGoid(6568).Count > 0, "a subway car", TimeSpan.FromSeconds(20));
        ViewCreation car = player.ViewsOfGoid(6568).First();
        player.SendRpc($"80 c7 {Le(car.ViewId)} {car.SpawnId:x2} 00 02 00");
        await player.WaitForRpcAsync("81 3a " + Doubles(68400, -800, 47500));

        player.SendRpc("81 3d");
        await player.WaitForRpcAsync("81 3e 3c");
        await player.WaitForRpcAsync("81 3e 3b"); // a second later

        player.SendRpc($"81 3c {Le((ushort)12)} {Le((ushort)1)} {Le((ushort)0)}");
        await player.WaitForRpcAsync("81 07");
        // The client answers 81 08 and the server reloads the district (as for a hardline): the position is saved first
        player.SendRpc("81 08");
        await player.WaitForRpcAsync("81 08");

        string row = await context.Database.ScalarAsync($"SELECT CONCAT(districtId, ' ', ROUND(x), ' ', ROUND(y), ' ', ROUND(z)) FROM characters WHERE charId={player.CharId}");
        Check.Equal("1 103000 -705 13600", row, "Saved at Moriah Projects' platform, standing height");
    }

    /// <summary>
    /// Across districts (Client_AllowCrossWorldSubwayTeleport is on by default): from Richland to Downtown's South
    /// Vauxton (world 7, Yellow Line 2, connector 0). Also: a pick with no map open does nothing, and Cancel (81 3b)
    /// closes the ride.
    /// </summary>
    private static async Task SubwayAcrossDistrictsAndRefusals(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(
            new CharacterSeed("Commuter") { X = 80300, Y = -705, Z = 48500 }))[0];

        // No map open: nothing happens
        player.SendRpc($"81 3c {Le((ushort)12)} {Le((ushort)1)} {Le((ushort)0)}");
        await Task.Delay(1000);
        Check.Equal(0, player.Received("81 07").Count, "A pick without a map");

        // ?subway (dev) opens the nearest station's map: Tabor Park on the Yellow Line
        player.SendChat("?subway");
        await player.WaitForRpcAsync("81 3a " + Doubles(80400, -800, 48500));
        player.SendRpc("81 3b");
        await Task.Delay(300);
        player.SendRpc($"81 3c {Le((ushort)12)} {Le((ushort)4)} {Le((ushort)0)}");
        await Task.Delay(1000);
        Check.Equal(0, player.Received("81 07").Count, "A pick after Cancel");

        // A link connector only switches the client's map: picking it is refused
        player.SendChat("?subway");
        await player.WaitForRpcAsync("81 3a " + Doubles(80400, -800, 48500));
        player.SendRpc($"81 3c {Le((ushort)12)} {Le((ushort)4)} {Le((ushort)1)}");
        await Task.Delay(1000);
        Check.Equal(0, player.Received("81 07").Count, "A pick of the Downtown link");

        player.SendChat("?subway");
        player.SkipReceived();
        await player.WaitForRpcAsync("81 3a " + Doubles(80400, -800, 48500));
        player.SendRpc($"81 3c {Le((ushort)7)} {Le((ushort)2)} {Le((ushort)0)}");
        await player.WaitForRpcAsync("81 07");
        // The client answers 81 08 and the server reloads the district (as for a hardline): the position is saved first
        player.SendRpc("81 08");
        await player.WaitForRpcAsync("81 08");
        string row = await context.Database.ScalarAsync($"SELECT CONCAT(districtId, ' ', ROUND(x), ' ', ROUND(y), ' ', ROUND(z)) FROM characters WHERE charId={player.CharId}");
        // SavePlayer stores districtId (the world loaded at login), not the district name column
        Check.Equal("2 -178500 -705 -63500", row, "Saved at South Vauxton in Downtown");
    }
}
