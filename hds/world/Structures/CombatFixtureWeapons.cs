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
        public const string ResetMode = "MXO_COMBAT_FIXTURE_RESET";

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
            if (SharpshooterFreeFire.EquippedWeapon(caller) == goid)
                return $"Fixture already has {weapon} ({goid}) in hand slot {HandSlot}. No inventory move was needed.";
            return Equip(caller, goid)
                ? $"Fixture equipped {weapon} ({goid}) in hand slot {HandSlot}. Choose the gun in the Free Attack dropdown for ranged Fire!. Use Close Combat for interlock specials. Re-enter the world to verify it in the login spawn."
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

        internal static string ResetDummy(WorldClient caller)
        {
            if (!Allowed(caller, ResetMode)) return "Fixture reset requires the disposable combat loadout.";

            Mob dummy = null;
            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob.getDistrict() != caller.playerData.getDistrictId() || !MobMotion.Ordinary(mob) ||
                        mob.isFriendly || mob.getHealthM() != 30000 || mob.getName() != "Melee Dummy")
                        continue;
                    if (dummy != null) return "Fixture reset requires exactly one living Melee Dummy.";
                    dummy = mob;
                }
            }
            if (dummy == null) return "Fixture reset could not find the living 30,000 HP Melee Dummy.";

            CombatManager.GetHandler(caller).LeaveOtherFights();
            dummy.setIsSpawned(false);
            try
            {
                dummy.ResetForRespawn();
                dummy.SetInCombat(false);
                dummy.setRotation(0);
                MobMotion.ResetFixtureMotion(dummy);
                TimedEffects.Clear(TimedEffects.ForMob(dummy));
                // Give every viewer a standard new hostile creation with a fresh spawn ID after reset.
                Store.world.DeleteMobViews(dummy);

                float x = (float)dummy.xBase, y = (float)dummy.yBase, z = (float)dummy.zBase - 252;
                byte yaw = MobAggro.YawTowards(x, z, dummy.xBase, dummy.zBase);
                byte teleport = (caller.playerData.teleportCounter = (byte)(caller.playerData.teleportCounter % 255 + 1));
                caller.playerData.currentSelectedTargetViewId = 2;
                caller.playerData.currentSelectedTargetSpawnId = 0;
                caller.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
                caller.playerInstance.YawInterval.setValue(yaw);
                caller.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket(x, y, z, 0, 0, 0, teleport, yaw), false);
                caller.FlushQueue();
            }
            finally
            {
                dummy.setIsSpawned(true);
            }
            return "Fixture reset: Melee Dummy restored. Select its new view before casting.";
        }
    }
}
