using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ClientActionFollowupsMode : NativeQaMode
{
    private const uint HealthPill = 2399;
    private const uint Revolver = 700;
    private const ushort Decompile = 276, MaraCentral = 152;
    private const int ArchiveSlot = 400;
    private const int X = 17000, Y = 495, Z = 2300, Rank = 18;

    public override string Name => "client-action-followups";
    public override NativeQaModePlan Plan { get; } = new(
        StockAbilityProfile.Profile(2, 17, 37, Decompile, 277).Order().ToArray(), X, Z,
        Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,613,3,1,0),(110,{HealthPill},5,2,0)",
            $"UPDATE characters SET y={Y},rotation=128,cash=250 WHERE charId=110",
            $"DELETE FROM char_abilities WHERE char_id=110 AND slot IN ({ArchiveSlot},{ArchiveSlot + 1})",
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,{ArchiveSlot},{CodeStorage.BaseValue(HealthPill)},1,0)," +
                $"(110,{ArchiveSlot + 1},{CodeStorage.BaseValue(Revolver)},1,0)"
        },
        new[] { $"UPDATE char_abilities SET level={Rank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Decompile)}" },
        DevCommands: false, GmCharacter110: false, FixedCallbackRolls: false,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        hotbarAbilities: new[] { Decompile },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(3, 613, "Item Decompiler") },
        allowManualHotbarSetup: true,
        readyMessages: new[]
        {
            $"CSR starts at the Mara Central hardline {MaraCentral} arrival stand ({X},{Y},{Z}), facing the booth. Inventory page 1, bag slot 3 holds the tool and slot 5 holds two Health Pills. Info starts at 250.",
            "Open Inventory (I) and hotbar page 2. If the stock UI template is unavailable, drag Decompile Items from Actions > Abilities to slot 1 and right-click bag slot 3 > Add Item to Hotbar for a tool shortcut.",
            "One decompile passed on f804b3f5. To repeat it, press page 2 slot 1 (Decompile Items 276), then single-click the stack in bag slot 5. If the ability does not arm an inventory target, use the Item Decompiler hotbar shortcut and single-click the pill. Expect 81 31 14 01 00 00 05 03 01, [DECOMPILE] item=2399 skill=18 success=True, 81 30 and 81 2f success. After 10 seconds repeat to clear the stack. Info stays at 250.",
            "For the archive retest, right-click the booth in front of CSR at (17000,400,2200), choose Loading Area and confirm. In Equipment > Code Archive, select Health Pill (slot 400) and Compile twice. Record the first 60 Item field. Expect 61 00 each time and [COMPILE60] Loading Area item=2399 cost=9, remaining 1 then 0, two pills added and Info 250 to 241 to 232.",
            "Compile Revolver (slot 401) once for 120 Info and one charge. At 112 Info try again. Expect one remaining charge, no extra Revolver, Info still 112 and 61 04 if the client sends the request. The separate client-action-tutorial mode starts directly in the Tutorial."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("An owned database is required.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        HardlineNetwork.Load(Path.Combine(data, "hardline_network.csv"));
        var booth = HardlineNetwork.Get(1, MaraCentral) ?? throw new InvalidOperationException("Mara Central hardline is missing.");
        Check.That(booth.spotX == X && booth.spotY == Y && booth.spotZ == Z && NavMesh.Locate(1, X, Y, Z, out _),
            "CSR's stand matches the shipped Mara Central arrival on NAV");
        Check.Equal("1 17000 495 2300 128", await db.ScalarAsync(
            "SELECT CONCAT(districtId,' ',ROUND(x),' ',ROUND(y),' ',ROUND(z),' ',rotation) FROM characters WHERE charId=110"),
            "CSR starts in Richland on the arrival stand facing the booth");
        Check.Equal(2L, await db.LongAsync($"SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid={HealthPill}"),
            "Two pills in the decompile source slot");
        Check.Equal(1L, await db.LongAsync("SELECT count FROM inventory WHERE charId=110 AND slot=3 AND goid=613"),
            "An owned Item Decompiler in slot 3");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND level=1 AND is_loaded=0 AND " +
            $"((slot={ArchiveSlot} AND ability_id={CodeStorage.BaseValue(HealthPill)}) OR " +
            $"(slot={ArchiveSlot + 1} AND ability_id={CodeStorage.BaseValue(Revolver)}))"),
            "Both Loading Area archive rows have a code flag and two charges");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        byte[] archive = actor.CharacterData[7];
        Dictionary<ushort, uint> codes = new();
        for (int offset = 0; offset + 6 <= archive.Length; offset += 6)
        {
            uint value = BitConverter.ToUInt32(archive, offset + 2);
            if (CodeStorage.IsCode(value)) codes.Add(BitConverter.ToUInt16(archive, offset), value);
        }
        Check.Equal(CodeStorage.Pack(HealthPill, 2), codes[ArchiveSlot], "Stock Margin sends the flagged Health Pill row");
        Check.Equal(CodeStorage.Pack(Revolver, 2), codes[ArchiveSlot + 1], "Stock Margin sends the flagged Revolver row");
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.Equal((ushort)Rank, loaded[Decompile], "Stock Margin loads rank 18 Decompile Items");
        await actor.EnterWorldAsync();
        actor.SendRpc("81 31 14 01 00 00 05 03 01");
        await actor.WaitForRpcAsync("81 2f 01 05 5f 09 00 00");
        Check.Equal(1L, await db.LongAsync($"SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid={HealthPill}"),
            "The stock decompile frame consumes one pill from the fixture bag");
        ViewCreation? liveBooth = null;
        await actor.WaitUntilAsync(() =>
        {
            liveBooth = actor.World.CreationSnapshot().LastOrDefault(view => view.Goid == HardlineBooths.PhoneBoothType &&
                view.Attributes.TryGetValue("Position", out var position) && position.Length == 24 &&
                BitConverter.ToDouble(position) == booth.x && BitConverter.ToDouble(position, 16) == booth.z);
            return liveBooth != null;
        }, "Mara Central hardline view at the fixture stand", TimeSpan.FromSeconds(15));
        actor.SendRpc($"80 c7 {WireFormat.ToHex(BitConverter.GetBytes(liveBooth!.ViewId))} {liveBooth.SpawnId:x2} 00 05 00");
        await actor.WaitForRpcAsync("80 fb 98 00 00 00 01");
        actor.SendRpc("80 fc 01");
        await actor.WaitForRpcAsync("80 fd 00 00 00 00 01 00");
        actor.SendRpc("80 fe 01");
        await actor.WaitForRpcAsync("81 07");
        actor.SendRpc("81 08 00 00 00 00");
        await actor.WaitForLoadingAreaAsync();
        actor.SendRpc("60 00 00 00 00 90 01 00 6b 00");
        await actor.WaitForRpcAsync("61 00");
        Check.Equal(241L, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"),
            "The captured zero-Item request resolves the flagged Health Pill row");
        Check.Equal(0, actor.World.Problems.Count, "The booth radial route enters the Loading Area");
        return Array.Empty<byte>();
    }
}
