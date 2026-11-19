using System;
using hds.shared;

namespace hds
{
    /// <summary>Weapon and fight setup confined to disposable NativeQa profiles.</summary>
    public static class CombatFixtureWeapons
    {
        public const uint Handgun = 2565, Rifle = 692, Smg = 698, DualSmgs = 2503;
        public const byte HandSlot = InventoryHandler.WeaponSlot;
        public const string WeaponMode = "MXO_COMBAT_FIXTURE_WEAPONS";
        public const string DisengageMode = "MXO_COMBAT_FIXTURE_DISENGAGE";

        private static bool Allowed(WorldClient caller, string mode) =>
            Environment.GetEnvironmentVariable(mode) == "1" &&
            caller.playerData.getCharID() == 110 &&
            QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName, caller.playerData.getCharID(),
                caller.playerData.getOnWorld(), caller.playerData.isDead);

        internal static string Set(WorldClient caller, string weapon)
        {
            if (!Allowed(caller, WeaponMode)) return "Fixture weapons require the disposable combat loadout.";
            if (weapon == "unarmed")
            {
                CombatManager.GetHandler(caller).LeaveOtherFights();
                if (SharpshooterFreeFire.EquippedWeapon(caller) != 0)
                    InventoryHandler.UnequipWeapon(0);
                return "Fixture equipped unarmed (0).";
            }
            uint goid = weapon switch
            {
                "handgun" => Handgun,
                "rifle" => Rifle,
                "smg" => Smg,
                "dual-smg" => DualSmgs,
                _ => 0
            };
            if (goid == 0) return "Use ?combatdepth fixture-weapon handgun, rifle, smg, dual-smg or unarmed.";
            return Equip(caller, goid)
                ? $"Fixture equipped {weapon} ({goid}) in hand slot {InventoryHandler.WeaponSlot}."
                : $"Fixture weapon {goid} is unavailable.";
        }

        internal static bool Equip(WorldClient caller, uint goid)
        {
            if (SharpshooterFreeFire.EquippedWeapon(caller) == goid) return true;
            var item = Store.dbManager.WorldDbHandler.GetInventoryItems(caller.playerData.getCharID())
                .Find(row => row.Goid == goid && row.Slot >= 1 && row.Slot <= 96);
            if (item.Slot == 0) return false;
            CombatManager.GetHandler(caller).LeaveOtherFights();
            InventoryHandler.EquipWeapon((byte)item.Slot);
            return SharpshooterFreeFire.EquippedWeapon(caller) == goid;
        }

        internal static string Disengage(WorldClient caller)
        {
            if (!Allowed(caller, DisengageMode)) return "Fixture disengage requires the disposable combat loadout.";
            CombatManager.GetHandler(caller).LeaveOtherFights();
            return "Fixture combat ended. Wait for the stock combat HUD to clear.";
        }
    }
}
