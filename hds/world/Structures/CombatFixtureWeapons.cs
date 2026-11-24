using System;
using hds.shared;

namespace hds
{
    /// <summary>Weapon and fight setup confined to disposable NativeQa profiles.</summary>
    public static class CombatFixtureWeapons
    {
        public const uint Handgun = 2565, DualHandguns = 2496, Rifle = 692, Smg = 698, DualSmgs = 2503;
        public const byte HandSlot = InventoryHandler.WeaponSlot;
        public const string WeaponMode = "MXO_COMBAT_FIXTURE_WEAPONS";
        public const string DisengageMode = "MXO_COMBAT_FIXTURE_DISENGAGE";
        public const string ResetMode = "MXO_COMBAT_FIXTURE_RESET";
        public const string LiveResetMode = "MXO_COMBAT_FIXTURE_LIVE_RESET";
        public const string VanishWinMode = "MXO_VANISH_WIN_QA";
        public const string RifleEscapeWinMode = "MXO_RIFLE_ESCAPE_WIN_QA";
        public const string PistolWhipLossMode = "MXO_PISTOL_WHIP_LOSS_QA";
        public const int DummyPhysicalResistance = 75;
        public const string MartialTacticMode = "MXO_MARTIAL_TACTIC_DAMAGE_QA";
        public const string KungFuTacticMode = "MXO_KUNG_FU_TACTIC_DAMAGE_QA";
        public const string MartialFixedDamageMode = "MXO_MARTIAL_FIXED_DAMAGE_QA";

        internal static string PrepareMartialDummy(WorldClient caller, ushort ability, bool armor)
        {
            bool starter = (armor || ability == 138 || ability == 139) && Allowed(caller, MartialTacticMode);
            bool kungFu = (armor || ability == 259 || ability == 260 || ability == 262) && Allowed(caller, KungFuTacticMode);
            bool karate = (armor || ability == 265 || ability == 266 || ability == 267) &&
                Allowed(caller, MartialFixedDamageMode, "karate");
            bool aikido = (armor || ability == 273 || ability == 274 || ability == 296) &&
                Allowed(caller, MartialFixedDamageMode, "aikido");
            bool grandmaster = (armor || ability == 427 || ability == 428 || ability == 429 || ability == 430) &&
                Allowed(caller, MartialFixedDamageMode, "kungfu-grandmaster");
            if (!starter && !kungFu && !karate && !aikido && !grandmaster)
                return "Martial preparation requires the matching disposable fixture.";
            var handler = CombatManager.GetHandler(caller);
            var dummy = handler.CurrentNpcTarget;
            ushort selected = caller.playerData.currentSelectedTargetViewId;
            if (dummy == null || dummy.getIsDead() || dummy.getName() != "Melee Dummy" ||
                selected <= 2 || AbilityTargets.FindMob(caller, selected, out _) != dummy)
                return "Select the fixture Melee Dummy and enter Close Combat first.";
            if (armor)
            {
                ResetDummyEffects(dummy, 180);
                return "Martial starter dummy resistance +75.";
            }
            if (ability == 138 || ability == 273 || ability == 296 || ability == 427)
                CombatDepthOffBalance.Apply(TimedEffects.ForMob(dummy), 60, caller, selected);
            if (ability == 259 || ability == 430)
                CombatDepthDaze.Apply(TimedEffects.ForMob(dummy), 60, caller, selected);
            if (ability == 262 || ability == 265)
                CombatDepthStagger.Apply(TimedEffects.ForMob(dummy), 60, caller, selected);
            handler.ArmMartialFixtureWin(dummy, ability);
            return $"Martial starter fixture armed {ability}.";
        }

        private static bool Allowed(WorldClient caller, string mode, string value = "1") =>
            Environment.GetEnvironmentVariable(mode) == value &&
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
                "dual-handgun" => DualHandguns,
                "rifle" => Rifle,
                "smg" => Smg,
                "dual-smg" => DualSmgs,
                _ => 0
            };
            if (goid == 0) return "Use ?combatdepth fixture-weapon handgun, dual-handgun, rifle, smg, dual-smg or unarmed.";
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

        internal static bool RoundAidAllowed(WorldClient caller, ushort ability) => ability switch
        {
            CombatSpecials.RifleButtSmash => Allowed(caller, RifleEscapeWinMode),
            Stealth.VanishAbility => Allowed(caller, VanishWinMode),
            CombatSpecials.PistolWhip => Allowed(caller, PistolWhipLossMode),
            _ => false
        };

        internal static string ArmRifleEscapeWin(WorldClient caller)
        {
            if (!RoundAidAllowed(caller, CombatSpecials.RifleButtSmash))
                return "Rifle Butt Smash win aid requires the disposable rifle fixture.";
            return CombatManager.GetHandler(caller).ActiveInterlockIdentity is PvpFight fight &&
                fight.ArmRoundAid(caller, CombatSpecials.RifleButtSmash)
                ? "Next Rifle Butt Smash against QAEnemy is armed for one winning withdrawal roll."
                : "Rifle Butt Smash win aid requires the active interlock with QAEnemy.";
        }

        internal static string ArmVanishWin(WorldClient caller)
        {
            if (!RoundAidAllowed(caller, Stealth.VanishAbility))
                return "Vanish win aid requires the disposable Vanish fixture.";
            return CombatManager.GetHandler(caller).ActiveInterlockIdentity is PvpFight fight &&
                fight.ArmRoundAid(caller, Stealth.VanishAbility)
                ? "Next Vanish against QAEnemy is armed for one winning withdrawal roll."
                : "Vanish win aid requires the active interlock with QAEnemy.";
        }

        internal static string ArmPistolWhipLoss(WorldClient caller)
        {
            if (!RoundAidAllowed(caller, CombatSpecials.PistolWhip))
                return "Pistol Whip loss aid requires the disposable dual-pistol fixture.";
            return CombatManager.GetHandler(caller).ActiveInterlockIdentity is PvpFight fight &&
                fight.ArmRoundAid(caller, CombatSpecials.PistolWhip)
                ? "Next Pistol Whip against QAEnemy is armed for one ordinary loss."
                : "Pistol Whip loss aid requires the active interlock with QAEnemy.";
        }

        internal static string ResetDummy(WorldClient caller, bool addPhysicalResistance = false, ushort resistanceStat = 0)
        {
            if (!Allowed(caller, ResetMode)) return "Fixture reset requires the disposable combat loadout.";
            if (resistanceStat == 0 && addPhysicalResistance) resistanceStat = 180;

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

            if (Environment.GetEnvironmentVariable(LiveResetMode) == "1")
                return ResetLiveDummy(caller, dummy, resistanceStat);

            CombatManager.GetHandler(caller).LeaveOtherFights();
            dummy.setIsSpawned(false);
            try
            {
                dummy.ResetForRespawn();
                dummy.SetInCombat(false);
                dummy.setRotation(0);
                MobMotion.ResetFixtureMotion(dummy);
                ResetDummyEffects(dummy, resistanceStat);
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
            return resistanceStat == 180
                ? $"Fixture reset: Melee Dummy restored with +{DummyPhysicalResistance} Melee Damage Resistance. Select its new view."
                : resistanceStat == 1033
                ? $"Fixture reset: Melee Dummy restored with +{DummyPhysicalResistance} Thrown Damage Resistance. Select its new view."
                : "Fixture reset: Melee Dummy restored. Select its new view before casting.";
        }
        private static string ResetLiveDummy(WorldClient caller, Mob dummy, ushort resistanceStat)
        {
            var view = caller.viewMan.FindViewForEntity(dummy.getEntityId());
            if (view == null || !view.viewCreated || view.viewNeedsToBeDeleted || view.spawnId == 0 ||
                !dummy.getIsSpawned() || dummy.getIsDead() || dummy.getHealthC() == 0)
                return "Fixture reset needs the living Melee Dummy in view.";

            // The stock client can retain the old target when an interlock ends and the same NPC is
            // immediately deleted and recreated. This soak fixture only needs a fresh combat state.
            dummy.PassiveTarget = true;
            CombatManager.GetHandler(caller).LeaveOtherFights();
            dummy.SetInCombat(false);
            MobMotion.ResetFixtureMotion(dummy);
            ResetDummyEffects(dummy, resistanceStat);
            return $"Fixture ready: Melee Dummy view {view.ViewID}, spawn {view.spawnId}. Select the same dummy.";
        }

        private static void ResetDummyEffects(Mob dummy, ushort resistanceStat)
        {
            var effects = TimedEffects.ForMob(dummy);
            TimedEffects.Clear(effects);
            if (resistanceStat != 0)
                TimedEffects.Apply(effects, new TempMods.Effect
                {
                    Mods = new[] { new TempMods.Mod
                    {
                        Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = resistanceStat,
                        MinValue = DummyPhysicalResistance, MaxValue = DummyPhysicalResistance, Seconds = 300
                    } }
                }, TimeUtils.GetSimTimeSeconds());
        }

    }
}
