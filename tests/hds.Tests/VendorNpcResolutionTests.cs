using System.Globalization;
using System.Text.RegularExpressions;
using hds;
using Xunit;

namespace hds.Tests;

/// <summary>
/// vendor_items.csv's captured vendor ids mostly name doors, benches or nothing in the 7.6005 statics, while each row's
/// position sits on a drawn merchant. The resolved npc_static_id must be that merchant, and using it must open the
/// row's stock without disturbing rows keyed by their own static.
/// </summary>
public class VendorNpcResolutionTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "hds.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
        }
    }

    private static List<string[]> Rows() => File.ReadLines(Path.Combine(Root, "data", "vendor_items.csv")).Skip(1)
        .Where(line => line.Length > 0).Select(line => line.Split(';')).ToList();

    private static List<Vendor> Vendors() => Rows().Select(r =>
    {
        var vendor = new Vendor(ushort.Parse(r[0]), uint.Parse(r[1]), r[2].Split(',').Select(uint.Parse).ToArray());
        if (r.Length > 6 && uint.TryParse(r[6], out uint npc)) vendor.npcStaticID = npc;
        return vendor;
    }).ToList();

    [Fact]
    public void EveryResolvedVendorIsTheMerchantStaticDrawnAtItsRowPosition()
    {
        var names = Regex.Matches(File.ReadAllText(Path.Combine(Root, "hds", "resources", "gameobjects", "definitions",
                "GameObjectDefinitions.cs")), "new \\w+\\(\"([^\"]*)\", (\\d+)\\)")
            .GroupBy(m => int.Parse(m.Groups[2].Value)).ToDictionary(g => g.Key, g => g.First().Groups[1].Value);
        var statics = Directory.GetFiles(Path.Combine(Root, "data"), "staticObjects_*.csv")
            .SelectMany(f => File.ReadLines(f).Skip(1)).Select(l => l.Split(',')).Where(c => c.Length >= 9 && c[2].Length == 8 && c[4].Length >= 4)
            .Select(c => (Metr: c[0], Id: BitConverter.ToUInt32(Convert.FromHexString(c[2])),
                Goid: BitConverter.ToUInt16(Convert.FromHexString(c[4])),
                X: double.Parse(c[6], CultureInfo.InvariantCulture), Y: double.Parse(c[7], CultureInfo.InvariantCulture),
                Z: double.Parse(c[8], CultureInfo.InvariantCulture)))
            .ToLookup(s => (s.Metr, s.Id));

        int resolved = 0;
        foreach (var row in Rows().Where(r => r.Length > 6 && r[6].Length > 0))
        {
            var match = statics[(row[0], uint.Parse(row[6]))].Single();
            Assert.Matches("Merchant|Seller|Vendor|Hacker|Patcher", names[match.Goid]);
            double Coordinate(int i) => double.Parse(row[i].Replace(',', '.'), CultureInfo.InvariantCulture);
            double distance = Math.Sqrt(Math.Pow(Coordinate(3) - match.X, 2) + Math.Pow(Coordinate(4) - match.Y, 2) +
                Math.Pow(Coordinate(5) - match.Z, 2));
            Assert.True(distance <= 60, $"vendor {row[1]} resolves {row[6]} {distance:F0} units away");
            resolved++;
        }
        Assert.Equal(23, resolved);
    }

    [Fact]
    public void UsingADrawnMerchantOpensItsStockAndOwnKeyedRowsStillWin()
    {
        var vendors = Vendors();
        // Mara Item Merchant (GO 6608, static 0x0d1004d4) opens the captured Mara tools stock, still reachable by id 98.
        var mara = DataLoader.FindVendor(vendors, 0x0d1004d4, 1);
        Assert.NotNull(mara);
        Assert.Same(DataLoader.FindVendor(vendors, 98, 1), mara);
        Assert.Equal(17, mara!.items.Length);
        // Mara Ability Merchant (0x393001f0) opens the stock captured under the bench id 0x393001e8.
        Assert.Same(DataLoader.FindVendor(vendors, 0x393001e8, 1), DataLoader.FindVendor(vendors, 0x393001f0, 1));
        // Stano Ability Merchant 0x34f0089b keeps the row keyed by its own static, not a resolved neighbour's stock.
        var stano = DataLoader.FindVendor(vendors, 0x34f0089b, 1);
        Assert.Equal(0x34f0089bu, stano!.vendorStaticID);
        // The same static in another district, and an unrelated static, open nothing.
        Assert.Null(DataLoader.FindVendor(vendors, 0x0d1004d4, 2));
        Assert.Null(DataLoader.FindVendor(vendors, 0x393001e9, 1));
        Assert.Null(DataLoader.FindVendor(vendors, 0, 1));
        // Baldwin Heights (already keyed by its own static) is unchanged.
        Assert.Equal(0x11801fbeu, DataLoader.FindVendor(vendors, 0x11801fbe, 2)!.vendorStaticID);
    }
}
