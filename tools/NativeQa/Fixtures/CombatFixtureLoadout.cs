using HeadlessClient;
using hds;

namespace NativeQa;

internal static class CombatFixtureLoadout
{
    internal const string MeleeCsv = "ability-sweep-melee.csv";
    internal const string DummyName = "Melee Dummy";
    internal const int CasterX = 24800, GroundY = 495, CasterZ = -1498;
    private static readonly (string Name, uint Goid, int Slot)[] Weapons =
    {
        ("handgun", CombatFixtureWeapons.Handgun, CombatFixtureWeapons.HandSlot),
        ("rifle", CombatFixtureWeapons.Rifle, 11),
        ("smg", CombatFixtureWeapons.Smg, 12),
        ("dual-smg", CombatFixtureWeapons.DualSmgs, 13)
    };

    internal static string SeedWeapons => SeedWeaponsFor(CombatFixtureWeapons.Handgun);

    internal static string SeedWeaponsFor(uint equipped) =>
        $"DELETE FROM inventory WHERE charId=110 AND slot IN (10,{string.Join(",", Weapons.Select(weapon => weapon.Slot))}); " +
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES {string.Join(",", Weapons.Select(weapon =>
            $"(110,{weapon.Goid},{SeedSlot(weapon.Goid, weapon.Slot, equipped)},1,0)"))}";

    private static int SeedSlot(uint goid, int bagSlot, uint equipped) =>
        goid == equipped ? CombatFixtureWeapons.HandSlot : goid == CombatFixtureWeapons.Handgun ? 10 : bagSlot;

    internal static async Task VerifyWeaponAtLoginAsync(HeadlessPlayer player, uint expected)
    {
        await player.EnterWorldAsync();
        var self = player.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(expected, BitConverter.ToUInt32(self.Attributes["EquippedItemID"]),
            "The first stock own view contains the configured weapon in hand");
        Check.Equal((byte)0x22, self.Attributes["CombatantMode"][0], "The armed own view spawns at rest");
    }

    internal static async Task VerifyWeaponsAsync(HeadlessPlayer player, ThrowawayDatabase database)
    {
        await VerifyWeaponAtLoginAsync(player, CombatFixtureWeapons.Handgun);
        foreach (var (name, goid) in Weapons.Skip(1).Select(weapon => (weapon.Name, weapon.Goid))
            .Append(("unarmed", 0u)).Append((Weapons[0].Name, Weapons[0].Goid)))
            await VerifyWeaponSwapAsync(player, database, name, goid);
    }

    internal static async Task VerifyWeaponSwapAsync(HeadlessPlayer player, ThrowawayDatabase database, string name, uint goid)
    {
        int sourceSlot = goid == 0 ? CombatFixtureWeapons.HandSlot :
            (int)await database.LongAsync($"SELECT slot FROM inventory WHERE charId=110 AND goid={goid} AND slot BETWEEN 1 AND 96 ORDER BY slot LIMIT 1");
        player.SkipReceived();
        player.SendChat($"?combatdepth fixture-weapon {name}");
        string move = goid == 0 ? "65 69 00 ?? 00 00" : $"65 {sourceSlot:x2} 00 69 00 00";
        await player.WaitForRpcAsync(move);
        await player.WaitForTextAsync($"Fixture equipped {name} ({goid})");
        await player.WaitForViewUpdateAsync(2, $"02 80 80 80 80 08 {WireFormat.ToHex(BitConverter.GetBytes(goid))}");
        Check.Equal((long)goid, await database.LongAsync($"SELECT COALESCE(MAX(goid),0) FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"),
            $"Fixture {name} is in the stock hand slot");
    }

    internal static async Task AssertWeaponSeedAsync(ThrowawayDatabase database, uint equipped = CombatFixtureWeapons.Handgun)
    {
        string bagGoids = string.Join(",", Weapons.Where(weapon => weapon.Goid != equipped).Select(weapon => weapon.Goid));
        Check.Equal((long)Weapons.Length, await database.LongAsync(
            $"SELECT COUNT(DISTINCT goid) FROM inventory WHERE charId=110 AND count=1 AND " +
            $"((slot={CombatFixtureWeapons.HandSlot} AND goid={equipped}) OR (slot BETWEEN 1 AND 96 AND goid IN ({bagGoids})))"),
            "CSR starts with the configured gun in hand and the other fixture weapons in the bag");
    }

    internal static async Task VerifyFireAfterSwapAsync(HeadlessPlayer player, ServerInstance server, uint goid)
    {
        var dummy = await player.WaitForViewNamedAsync(DummyName);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        // The stock Free Attack dropdown sets the weapon separately from the paper doll.
        // Retail Luizoé chose the new hand slot with 3f after equipping (Unknown log 11243).
        player.SendRpc("3f 69 00 10 00 00 00");
        await player.WaitUntilAsync(() => server.DebugLog().Contains($"[COMBAT] Free Fire weapon: slot 105, kind 16, goid {goid}"),
            "Stock Free Attack choice references the newly equipped fixture weapon", HeadlessPlayer.DefaultTimeout);
        // Retail Uriah first attack: draw, shot, companion 40 with the same target and weapon slot.
        player.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"),
            WireFormat.FromHex($"40 {handle} 00 69"));
        await player.WaitForRpcAsync("81 68");
        await player.WaitUntilAsync(() => server.DebugLog().Contains("Close combat request ignored: firing at this target"),
            "The Fire! pair starts ranged fire, preserving the distinct close-combat action", HeadlessPlayer.DefaultTimeout);
        player.SendRpc("3d 00");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 22");
        player.SendRpc($"40 {handle} 01 6b");
        await player.WaitForRpcAsync("47");
        player.SendRpc("44 00");
        await player.WaitForRpcAsync("46 ?? ?? ?? ?? 00");
    }

    internal static async Task VerifyGunChoreographyAsync(HeadlessPlayer player, ServerInstance server, Modes.GunStyleChoreographyMode mode)
    {
        await VerifyWeaponAtLoginAsync(player, mode.Weapon);
        var dummy = await player.WaitForViewNamedAsync(DummyName);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        string style = WireFormat.ToHex(BitConverter.GetBytes(mode.Style));
        string ability = WireFormat.ToHex(BitConverter.GetBytes(mode.Special));
        string view = WireFormat.ToHex(BitConverter.GetBytes(dummy.ViewId));

        // Set the initial style through the toolbar RPC so the later Actions request is a distinct use.
        player.SendRpc($"3e 6b {style} 00 00 00 00");
        await player.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {style} 00 00");

        // PR330's stock Close Combat slot sends this full Self Defense selection before 40 in the same packet.
        player.SkipReceived();
        player.World.SendRpcs(
            WireFormat.FromHex($"80 b9 11 00 {handle} 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 34 09 7f 00 00 00 00 ff 08 6b 00 00 00 00 00"),
            WireFormat.FromHex($"40 {handle} 01 6b"));
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 11 00 00 00");
        await player.WaitForRpcAsync("4e 01");
        await player.WaitForRpcAsync($"40 {handle} 00 6b");
        await player.WaitForRpcAsync("47");

        var reselected = BytePattern.Parse($"02 00 02 80 80 80 80 80 10 {style} 00 00");
        int styleCursor = player.World.ViewSnapshot().Count;
        player.SendRpc($"80 b9 {style} {view}");
        await player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(styleCursor).Any(reselected.Occurs),
            $"Gun style {mode.Style} is reselected during interlock", HeadlessPlayer.DefaultTimeout);

        if (mode.RequiresOffBalance)
        {
            byte[] conditionHeader = WireFormat.FromHex($"{view} 04 80 80 80 80 80 80 02");
            const uint offBalanceInCombat = CombatInterlock.InCombatFlag | 0x04;
            int conditionCursor = player.World.ViewSnapshot().Count;
            int grabLog = server.DebugLog().Length;
            player.SendRpc("42 02 00 00 00 00 00");
            await player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(conditionCursor).Any(update =>
            {
                int header = update.AsSpan().IndexOf(conditionHeader);
                int flagsOffset = header + conditionHeader.Length;
                return header >= 0 && flagsOffset + sizeof(uint) <= update.Length &&
                    (BitConverter.ToUInt32(update, flagsOffset) & offBalanceInCombat) == offBalanceInCombat;
            }), "Grab sets the Off Balance bit 04 on Melee Dummy's interlock view", TimeSpan.FromSeconds(100));
            Check.That(server.DebugLog()[grabLog..].Contains("Melee Dummy suffers condition 04 from Grab", StringComparison.Ordinal),
                "Off Balance comes from a natural Grab round");
            Console.WriteLine($"  {mode.Name}: Self Defense opened interlock, {mode.Style} reselected, Melee Dummy mask 04 from Grab");
        }

        int castLog = server.DebugLog().Length;
        string cast = mode.RequiresOffBalance
            ? $"80 b9 f1 00 {handle} 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 f1 00 00 00 00 6b 00 00 08 6b 00 00 00 00 00"
            : $"80 b9 {ability} {view}";
        player.SendRpc(cast);
        await player.WaitUntilAsync(
            () => server.DebugLog()[castLog..].Contains($"animation {mode.Animation} (", StringComparison.Ordinal),
            $"{mode.Special} lands with interlock animation {mode.Animation}", TimeSpan.FromSeconds(35));
        Console.WriteLine($"  {mode.Name}: {mode.Special} landed with interlock animation {mode.Animation}");

        player.SendRpc("44 00");
        await player.WaitForRpcAsync($"46 {handle} 00");
    }

    internal static void AssertDummy(HeadlessPlayer observer, ViewCreation dummy)
    {
        Check.That(dummy.Goid == 599 && BitConverter.ToUInt16(dummy.Attributes["MaxHealth"]) == 30000 &&
            BitConverter.ToUInt16(dummy.Attributes["Health"]) > 0, "The fixture has a living 30,000 HP Melee Dummy");
        Check.That(observer.World.CreationSnapshot().Where(view => view.Goid == 599).All(view => view.Name == DummyName),
            "The Melee Dummy is isolated from other NPCs");
    }
}
