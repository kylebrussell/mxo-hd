using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class LoadingAreaPreviewMode : NativeQaMode
{
    private const int StartX = 7800, StartZ = 13795;
    private const ushort Hardline = 49;
    private const string Probe = "QAFriend";

    public override string Name => "loading-area-preview";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, StartX, StartZ,
        Array.Empty<string>(), new[] { "UPDATE characters SET y=95,rotation=192 WHERE charId=110;" },
        new[] { $"""
            UPDATE characters SET x={StartX},y=95,z={StartZ},rotation=192 WHERE handle='{Probe}';
            INSERT INTO inventory (charId,goid,slot,count,purity)
                SELECT c.charId,i.goid,i.slot,i.count,i.purity FROM inventory i
                JOIN characters c ON c.handle='{Probe}'
                WHERE i.charId=110 AND i.slot BETWEEN 97 AND 104;
            """ },
        DevCommands: true, environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] { "Compare CSR's equipped outfit at Mara Northwest with both Loading Area previews after a booth exit. "
            + "Jack back in at Mara Northwest, then use ?die and Reconstruct for a second preview check." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Preview QA requires a disposable database.");
        using var probe = await HeadlessPlayer.LoginAsync(context.Address, Probe, Probe);
        byte[] profile = probe.CharacterData[0x02];
        uint[] appearance = Enumerable.Range(0, 8).Select(i => BitConverter.ToUInt32(profile, 4 * i)).ToArray();
        Check.Equal(1L, await database.LongAsync("SELECT COUNT(*) FROM rsivalues WHERE charid=110 AND sex=0"),
            "CSR has the expected male RSI");
        Check.That(appearance[1] is 106 or 103 or 101 && appearance[2] != 0 && appearance[3] != 0,
            "The cloned CSR receives a valid male body, head and hair descriptor");
        Check.Equal(299u, BitConverter.ToUInt32(profile, 32), "Character data follows the complete appearance header");

        byte[] inventory = probe.CharacterData[0x05];
        Check.Equal(0, inventory.Length % 9, "Margin equipment records are nine bytes");
        var worn = Enumerable.Range(0, inventory.Length / 9)
            .Select(i => (Slot: inventory[i * 9], Item: BitConverter.ToUInt32(inventory, i * 9 + 1)))
            .Where(row => row.Slot is >= 97 and <= 104).OrderBy(row => row.Slot).ToArray();
        string slots = string.Join(",", worn.Select(row => $"{row.Slot}={row.Item}"));
        Check.That(worn.Length > 0, "CSR has worn clothes for the visual check");
        Check.Equal(await database.ScalarAsync(
            "SELECT GROUP_CONCAT(CONCAT(slot,'=',goid) ORDER BY slot SEPARATOR ',') FROM inventory WHERE charId=110 AND slot BETWEEN 97 AND 104"),
            slots, "The preview receives the exact occupied clothing slots from CSR");

        await probe.EnterWorldAsync();
        ViewCreation? booth = null;
        await probe.WaitUntilAsync(() =>
        {
            booth = probe.ViewsOfGoid(HardlineBooths.PhoneBoothType).LastOrDefault(view =>
                view.Attributes.TryGetValue("Position", out byte[]? pos) && pos.Length == 24 &&
                Math.Abs(BitConverter.ToDouble(pos) - StartX) < 400 &&
                Math.Abs(BitConverter.ToDouble(pos,16) - StartZ) < 400);
            return booth != null;
        }, "the Mara Northwest hardline view", TimeSpan.FromSeconds(15));
        probe.SendRpc($"80 c7 {Le(booth!.ViewId)} {booth.SpawnId:x2} 00 {HardlineBooths.ActionExitToLoadingArea:x2} 00");
        await probe.WaitForRpcAsync($"80 fb {Le((uint)Hardline)} 01");
        probe.SendRpc("80 fc 01");
        await probe.WaitForRpcAsync("80 fd 00 00 00 00 01 00");
        probe.SendRpc("80 fe 01");
        await probe.WaitForRpcAsync("81 07");
        probe.SendRpc("81 08 00 00 00 00");
        await probe.WaitForLoadingAreaAsync();
        probe.World.SendRpcs(WireFormat.FromHex("6c 01 00 00 00 00 00 00 00"), WireFormat.FromHex("05"));
        await probe.WaitForRpcAsync("6d 01 00 00 00");
        var loading = probe.World.CurrentOwnViewLife();
        Check.That(!loading.OwnViewReady, "The Loading Area does not create a world player view");

        probe.SendRpc($"80 ff {Le(1u)} {Le((uint)Hardline)}");
        await probe.WaitForRpcAsync("81 07");
        probe.SendRpc("81 08 00 00 00 00");
        await probe.WaitForRpcAsync("06 0e 00 01 00 00 00");
        probe.SendRpc("05");
        await probe.WaitUntilAsync(() =>
        {
            var life = probe.World.CurrentOwnViewLife();
            return life.Life != loading.Life && life.OwnViewReady;
        }, "a new player view after Jack In", TimeSpan.FromSeconds(15));
        Check.Equal(0, probe.World.Problems.Count, "The full Loading Area round trip is parser clean");
        Check.Equal(1L, await database.LongAsync("SELECT COUNT(*) FROM characters WHERE charId=110 AND is_online=0"),
            "CSR is available for native QA");
        probe.SendRpc("80 fe 00");
        await probe.WaitForRpcAsync("80 fd");

        Console.WriteLine($"CSR preview descriptors {string.Join(",", appearance)}; worn slots {slots}");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "loading-area-preview.txt"),
            $"Appearance descriptors {string.Join(",", appearance)}{Environment.NewLine}Equipped slots {slots}{Environment.NewLine}");
        return Array.Empty<byte>();
    }

    private static string Le(uint value) => Convert.ToHexString(BitConverter.GetBytes(value));
    private static string Le(ushort value) => Convert.ToHexString(BitConverter.GetBytes(value));
}
