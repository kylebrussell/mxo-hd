using System.Globalization;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

/// <summary>
/// The Mara Item Merchant (GO 6608, static 0x0d1004d4) is drawn by the stock client, but its stock row was captured under
/// id 98. Using the visible merchant now opens that 17-item stock through vendor_items.csv's resolved npc_static_id.
/// </summary>
public sealed class WorldVendorMaraMode : NativeQaMode
{
    private const ushort Slums = 1, MerchantGoid = 6608;
    private const uint MerchantId = 0x0d1004d4, CapturedId = 98;
    private const int X = 23405, Y = 495, Z = 11995;

    public override string Name => "world-vendor-mara";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET district='slums',districtId={Slums},y={Y},rotation=64,cash=10000 WHERE charId=110;" },
        new[] { $"UPDATE characters SET x={X},y={Y},z={Z} WHERE handle='QAFriend';" },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "The Mara Item Merchant stands 1.5 m ahead of CSR at 23555,545,11995. Use the ordinary vendor interaction on it.",
            "Expect the vendor window with 17 items (the Mara tools stock) and no 'Open Vendor Dialog' popup. Close the window.",
            "Optional: the Mara Ability Merchant at 16995,545,195 (use ?tp 16845 495 195) opens its 4-item stock the same way."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing vendor data path."), "data");
        string key = Convert.ToHexString(BitConverter.GetBytes(MerchantId));
        var source = Directory.GetFiles(data, "staticObjects_slums_*.csv").SelectMany(File.ReadLines)
            .Select(line => line.Split(',')).Single(row => row.Length > 8 && row[0] == "1" &&
                string.Equals(row[2], key, StringComparison.OrdinalIgnoreCase));
        Check.Equal(MerchantGoid, BitConverter.ToUInt16(Convert.FromHexString(source[4])), "Mara Item Merchant prototype");
        double Coordinate(int field) => double.Parse(source[field], CultureInfo.InvariantCulture);
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(Slums, X, Y, Z, out var nav) && Math.Abs(Y - nav.floorY) < 100 &&
            Math.Abs(X - Coordinate(6)) == 150 && Z == Coordinate(8), "NAV stand for this merchant");

        uint[] stock = File.ReadLines(Path.Combine(data, "vendor_items.csv")).Skip(1).Select(line => line.Split(';'))
            .Single(row => row[0] == "1" && row[1] == CapturedId.ToString(CultureInfo.InvariantCulture))[2]
            .Split(',').Select(uint.Parse).ToArray();
        using var player = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await player.EnterWorldAsync();
        player.SendRpc($"80 c8 {key} 02 00");
        var opened = await player.WaitForRpcAsync("81 0d 7c ad d9 43");
        Check.Equal(stock.Length, (int)BitConverter.ToUInt16(opened.Bytes, 32), "Using the drawn merchant opens its stock");
        Check.That(opened.Bytes.AsSpan(34).SequenceEqual(stock.SelectMany(BitConverter.GetBytes).ToArray()),
            "Vendor window carries the captured Mara tools stock");
        Check.Equal(0, player.World.Problems.Count, "Vendor interaction completed without parser problems");
        player.SendRpc("80 fe 00");
        await player.WaitForRpcAsync("80 fd");
        Console.WriteLine($"WORLD VENDOR MARA PASS {MerchantId:x8} {stock.Length} items");
        return Array.Empty<byte>();
    }
}
