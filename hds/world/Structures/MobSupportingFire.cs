using System;
using hds.shared;

namespace hds
{
    /// <summary>Ordinary NPC free fire outside the primary opponent's combat session.</summary>
    internal static class MobSupportingFire
    {
        internal static bool CanFireAt(Mob mob, double distance) =>
            MobAggro.OpensFire(mob.getWeapon(), distance) && ControlActions.CanUseWeapons(TimedEffects.ForMob(mob).Effects);

        // True while the NPC has a clear, in-range firing position, including between shots.
        internal static bool TryFire(WorldClient player, Mob mob, double distance, double now)
        {
            var handler = CombatManager.GetHandler(player);
            if (handler.CurrentNpcOpponentFor(player) == null || mob.getCombatTarget() != null ||
                !CanFireAt(mob, distance) || !CombatSight.Between(player, mob))
                return false;

            uint weapon = mob.getWeapon();
            if (mob.TryRetaliate(now, RangedWeapons.FireIntervalMs(weapon) / 1000.0))
                DelayedActions.AfterFor(player, 0, () => Fire(player, handler, mob, weapon));
            return true;
        }

        private static void Fire(WorldClient player, CombatHandler handler, Mob mob, uint weapon)
        {
            if (!player.Alive || !player.playerData.getOnWorld() || player.playerData.waitForRPCShutDown ||
                player.playerData.worldChangePending || player.playerData.isDead || player.playerData.duelDeath ||
                PvpCombat.HealthOf(player) == 0 || handler.CurrentNpcOpponentFor(player) == null)
                return;

            ClientView view = player.viewMan.FindViewForEntity(mob.getEntityId());
            if (view == null || !view.viewCreated || view.viewNeedsToBeDeleted) return;
            var target = CombatSight.Position(player);
            byte yaw = MobAggro.YawTowards(mob.getXPos(), mob.getZPos(), target.X, target.Z);
            if (yaw != (byte)mob.getRotation())
            {
                mob.setRotation(yaw);
                mob.updateAnimation((byte)Mob.Animations.STAND);
            }

            const CombatTactic tactic = CombatTactic.Speed;
            int rangedAttack = SharpshooterFreeFire.FromNpc(mob, player, weapon).AddToAttack(AbilityStats.Attack(mob, true), weapon);
            var shot = FreeAttacks.Fire(Random.Shared, mob.getLevel(), tactic, CombatTacticType.Normal,
                player.playerInstance.Level.getValue()[0], player.playerData.combatTactic, weapon, rangedAttack,
                AbilityStats.Defense(player, true), AbilityStats.Npc(mob, 1139), AbilityStats.Npc(mob, 171) / 100.0,
                AbilityStats.Toughness(player, true), () => !MobiusCode.PreventsFreeAttack(player));

            uint self = (uint)(2 | player.playerData.selfSpawnIdCounter << 16);
            new ServerPackets().SendNpcUpdateData(view.ViewID, player, ServerPackets.BuildNpcRangedShot(
                ++mob.rangedActionCounter, RangedWeapons.AttackClass(weapon), shot.Hit, self,
                (float)mob.getXPos(), (float)mob.getYPos(), (float)mob.getZPos()));
            bool died = shot.Hit && PvpCombat.Damage(player, shot.Damage);
            string playerName = StringUtils.charBytesToString_NZ(player.playerInstance.CharacterName.getValue());
            player.messageQueue.addRpcMessage(ServerPackets.BuildCombatLog(shot.Hit, shot.Damage, shot.Hit ? shot.Absorbed : (short)0,
                shot.AttackRoll, shot.DefenseRoll, false, weapon, playerName, mob.getName(), tactic));
            Output.WriteDebugLog($"[AGGRO] {mob.getName()} fires in support of the current fight (view {view.ViewID}, damage {shot.Damage})");
            if (died && !player.playerData.isDead)
            {
                handler.StopCombatIfActive(CombatSession.CombatEndReason.AttackerDied);
                CombatHandler.HandlePlayerDeath(player);
            }
            player.FlushQueue();
        }
    }
}
