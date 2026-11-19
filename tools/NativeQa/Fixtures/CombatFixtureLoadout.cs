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

    internal static void AssertDummy(HeadlessPlayer observer, ViewCreation dummy)
    {
        Check.That(dummy.Goid == 599 && BitConverter.ToUInt16(dummy.Attributes["MaxHealth"]) == 30000 &&
            BitConverter.ToUInt16(dummy.Attributes["Health"]) > 0, "The fixture has a living 30,000 HP Melee Dummy");
        Check.That(observer.World.CreationSnapshot().Where(view => view.Goid == 599).All(view => view.Name == DummyName),
            "The Melee Dummy is isolated from other NPCs");
    }
}
