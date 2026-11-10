using System.Globalization;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// Leaving the Loading Area for Slums hardline 83 (Westview.Gracy Heights (Northwest)), which the client's map has but
    /// data_hardlines doesn't: the player is placed at the client network's arrival spot (booth + Offset) and saved there.
    /// Gets to the Loading Area as MarketplaceSellerInLoadingArea does, then 80 ff &lt;district u32&gt; &lt;hardline u32&gt;.
    /// </summary>
    private static async Task LoadingAreaToMissingHardline(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Traveller")))[0];

        player.SendRpc("80 fc 01");
        await player.WaitForRpcAsync("80 fd");
        player.SendRpc("80 fe 01");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08");
        await player.WaitForRpcAsync("81 09");
        player.SendRpc("05");
        await Task.Delay(500);

        player.SendRpc($"80 ff {Le(1u)} {Le(83u)}");
        await player.WaitForRpcAsync("81 07");
        await Task.Delay(500);

        double D(string value) => double.Parse(value, CultureInfo.InvariantCulture);
        string row = await context.Database.ScalarAsync($"SELECT CONCAT(districtId, ' ', x, ' ', z) FROM characters WHERE charId={player.CharId}");
        string[] parts = row.Split(' ');
        Check.Equal("1", parts[0], "District after the Loading Area");
        Check.Equal(-95100.0, D(parts[1]), "x at hardline 83");
        Check.Equal(-127850.0, D(parts[2]), "z at hardline 83");
    }
}
