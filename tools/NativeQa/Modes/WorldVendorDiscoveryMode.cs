using System.Globalization;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class WorldVendorDiscoveryMode : NativeQaMode
{
    private const ushort Downtown = 2, MerchantGoid = 7105;
    private const uint VendorId = 293609406, Shirt = 11666;
    private const int X = -11550, Y = 1895, Z = 57400;
    private const byte Slot = 11;

    public override string Name => "world-vendor-discovery";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"""
            UPDATE characters SET district='downtown',districtId={Downtown},y={Y},rotation=64,cash=10000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot={Slot};
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{Shirt},{Slot},1,0);
            """ },
        new[] { $"""
            UPDATE characters SET x={X},y={Y},z={Z} WHERE handle='QAFriend';
            INSERT INTO inventory(charId,goid,slot,count,purity)
                SELECT charId,{Shirt},{Slot},1,0 FROM characters WHERE handle='QAFriend';
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] { "Find the Baldwin Heights Clothing Merchant ahead of CSR at -11400,1945,57400. "
            + "Use the ordinary vendor interaction and appraise the shirt in slot 11 for 680 Info." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Vendor QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Vendor QA requires a live server.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing vendor data path."), "data");
        string key = Convert.ToHexString(BitConverter.GetBytes(VendorId));
        var source = File.ReadLines(Path.Combine(data, "staticObjects_dt_1.csv"))
            .Where(line => line.StartsWith("2,280,", StringComparison.Ordinal))
            .Select(line => line.Split(','))
            .Single(row => string.Equals(row[2], key, StringComparison.OrdinalIgnoreCase));
        Check.Equal(MerchantGoid, BitConverter.ToUInt16(Convert.FromHexString(source[4])), "Baldwin Heights merchant prototype");
        double Coordinate(int field) => double.Parse(source[field], CultureInfo.InvariantCulture);
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(Downtown, X, Y, Z, out var nav) && Math.Abs(Y - nav.floorY - 95) < 1 &&
            nav.sector == 280 && Math.Abs(X - Coordinate(6)) == 150 &&
            Math.Abs(Y - Coordinate(7)) == 50 && Z == Coordinate(8), "NAV stand for this merchant");

        uint[] stock = File.ReadLines(Path.Combine(data, "vendor_items.csv")).Skip(1)
            .Select(line => line.Split(';')).Single(row => row[0] == "2" && row[1] == VendorId.ToString(CultureInfo.InvariantCulture))[2]
            .Split(',').Select(uint.Parse).ToArray();
        using var player = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await player.EnterWorldAsync();
        player.SendRpc($"80 c8 {key} 02 00");
        var opened = await player.WaitForRpcAsync("81 0d 7c ad d9 43");
        Check.Equal(stock.Length, (int)BitConverter.ToUInt16(opened.Bytes, 32), "Ordinary vendor use opens its stock");
        Check.That(opened.Bytes.AsSpan(34).SequenceEqual(stock.SelectMany(BitConverter.GetBytes).ToArray()),
            "Vendor window carries the exact Baldwin Heights stock");

        player.SendRpc($"81 11 01 {Slot:x2}");
        await player.WaitForRpcAsync($"81 12 01 {Convert.ToHexString(BitConverter.GetBytes(Shirt))} ?? ?? ?? ?? a8 02 00 00");
        Check.Equal(10000L, await database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"), "Appraise preserves Info");
        Check.Equal(1L, await database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot={Slot} AND goid={Shirt}"),
            "Appraise preserves the shirt");
        NativeQaReadiness.RequireNoAmbientAggro(server, player);
        Check.Equal(0, player.World.Problems.Count, "Vendor interaction completed without parser problems");
        player.SendRpc("80 fe 00");
        await player.WaitForRpcAsync("80 fd");
        return Array.Empty<byte>();
    }
}
