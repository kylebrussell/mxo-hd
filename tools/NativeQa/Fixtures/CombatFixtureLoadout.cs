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

    internal static string SeedWeapons => $"DELETE FROM inventory WHERE charId=110 AND slot IN (10,{string.Join(",", Weapons.Select(weapon => weapon.Slot))}); " +
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES {string.Join(",", Weapons.Select(weapon => $"(110,{weapon.Goid},{weapon.Slot},1,0)"))}";

    internal static async Task VerifyWeaponsAsync(HeadlessPlayer player, ThrowawayDatabase database)
    {
        await player.EnterWorldAsync();
        var self = player.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(CombatFixtureWeapons.Handgun, BitConverter.ToUInt32(self.Attributes["EquippedItemID"]),
            "Stock world entry shows the already equipped fixture revolver");
        foreach (var (name, goid) in Weapons.Skip(1).Select(weapon => (weapon.Name, weapon.Goid))
            .Append(("unarmed", 0u)).Append((Weapons[0].Name, Weapons[0].Goid)))
        {
            player.SendChat($"?combatdepth fixture-weapon {name}");
            await player.WaitForTextAsync($"Fixture equipped {name} ({goid})");
            await player.WaitForViewUpdateAsync(2, $"02 80 80 80 80 08 {WireFormat.ToHex(BitConverter.GetBytes(goid))}");
            Check.Equal((long)goid, await database.LongAsync($"SELECT COALESCE(MAX(goid),0) FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"),
                $"Fixture {name} is in the stock hand slot");
        }
    }

    internal static async Task AssertWeaponSeedAsync(ThrowawayDatabase database)
    {
        string bagGoids = string.Join(",", Weapons.Skip(1).Select(weapon => weapon.Goid));
        Check.Equal((long)Weapons.Length, await database.LongAsync(
            $"SELECT COUNT(DISTINCT goid) FROM inventory WHERE charId=110 AND count=1 AND " +
            $"((slot={CombatFixtureWeapons.HandSlot} AND goid={CombatFixtureWeapons.Handgun}) OR (slot BETWEEN 1 AND 96 AND goid IN ({bagGoids})))"),
            "CSR starts with the fixture revolver in hand and the other test weapons in the bag");
    }

    internal static async Task VerifyFireAfterSwapAsync(HeadlessPlayer player, ServerInstance server)
    {
        var dummy = await player.WaitForViewNamedAsync(DummyName);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        // The stock Free Attack dropdown sets the weapon separately from the paper doll.
        // Retail Luizoé chose the new hand slot with 3f after equipping (Unknown log 11243).
        player.SendRpc("3f 69 00 10 00 00 00");
        await player.WaitUntilAsync(() => server.DebugLog().Contains("[COMBAT] Free Fire weapon: slot 105, kind 16, goid 2565"),
            "Stock Free Attack choice references the newly equipped fixture revolver", HeadlessPlayer.DefaultTimeout);
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

    internal static void AssertDummy(HeadlessPlayer observer, ViewCreation dummy)
    {
        Check.That(dummy.Goid == 599 && BitConverter.ToUInt16(dummy.Attributes["MaxHealth"]) == 30000 &&
            BitConverter.ToUInt16(dummy.Attributes["Health"]) > 0, "The fixture has a living 30,000 HP Melee Dummy");
        Check.That(observer.World.CreationSnapshot().Where(view => view.Goid == 599).All(view => view.Name == DummyName),
            "The Melee Dummy is isolated from other NPCs");
    }
}
