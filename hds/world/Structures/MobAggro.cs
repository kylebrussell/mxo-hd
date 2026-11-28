using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Hostile NPCs attack players who come near. Retail started such a fight from the server alone: with no
    /// close-combat request from the client it sent <c>40 &lt;target&gt; 00 6b</c> and <c>46 &lt;target&gt; 01</c>
    /// (actions_combat.log.txt line 12319, research/packet-dumps/community-intake/...combat-ability-selected),
    /// the same messages our server sends when it accepts a player's close-combat request. So an aggro runs
    /// the normal close-combat start for the player, after moving the NPC next to them.
    ///
    /// MXO_AGGRO=0 turns it off, MXO_AGGRO_RANGE sets the radius in metres (default 12) and MXO_AGGRO_LEVEL_GAP
    /// how many levels above an NPC a player can be before it leaves them alone (default 10).
    /// </summary>
    public static class MobAggro
    {
        internal static bool UsesEnragePursuit(ushort ability) => ability == 462 || ability == 588 || ability == 597;
        private const double EnrageRangeUnits = 1000;
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("MXO_AGGRO") != "0";
        public static readonly double RangeUnits = ReadDouble("MXO_AGGRO_RANGE", 12) * 100; // 100 units per metre
        public static readonly int LevelGap = (int)ReadDouble("MXO_AGGRO_LEVEL_GAP", 10);

        /// <summary>Seconds a player is left alone after an aggro (so a fight that fails to start isn't retried every pass).</summary>
        public const double RetrySeconds = 5;

        /// <summary>How far from the player (units) the NPC stops: arm's length.</summary>
        public const double MeleeDistanceUnits = 150;
        private const double MeleeArrivalDistance = MeleeDistanceUnits + .01;

        // StopPoint can round just outside arm's length, too close for a further movement step.
        internal static bool ReachedMeleeDistance(double squaredDistance) =>
            squaredDistance <= MeleeArrivalDistance * MeleeArrivalDistance;

        private static double ReadDouble(string name, double fallback)
        {
            return double.TryParse(Environment.GetEnvironmentVariable(name),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out double value) ? value : fallback;
        }

        /// <summary>Building floors are 400 units apart: an NPC on the floor above or below can't see the player.</summary>
        public const double FloorHeightUnits = 350;

        public static bool SameFloor(double mobY, double playerY) => Math.Abs(mobY - playerY) <= FloorHeightUnits;

        /// <summary>Whether this NPC would attack a player of this level at this distance (in units).</summary>
        public static bool WouldAttack(Mob mob, double distanceUnits, int playerLevel)
        {
            if (mob == null || mob.PassiveTarget || mob.isFriendly || mob.getIsDead() || !mob.getIsSpawned() || mob.getCombatTarget() != null || MobMotion.Chasing(mob))
            {
                return false;
            }

            return distanceUnits <= RangeUnits && playerLevel - mob.getLevel() <= LevelGap &&
                   MobBehaviour.WillingAtLevels(mob.aggroKind, mob.getLevel(), playerLevel);
        }

        /// <summary>
        /// An NPC with a gun opens fire from where it stands when the player is within the gun's reach (Tool/Range);
        /// the others run up for close combat. Retail: Corrupted fired at entilsar without any request from the client
        /// (entilsar lastday2, 21:48:40-41).
        /// </summary>
        public static bool OpensFire(UInt32 weaponGoid, double distanceUnits)
        {
            return RangedWeapons.IsGun(weaponGoid) && OpensFire(RangedWeapons.RangeMetres(weaponGoid), distanceUnits);
        }

        public static bool OpensFire(float gunRangeMetres, double distanceUnits)
        {
            return distanceUnits <= gunRangeMetres * CombatCalculator.WorldUnitsPerMetre;
        }

        /// <summary>Yaw byte (256 steps a turn) for looking from one point to another on the ground plane.</summary>
        public static byte YawTowards(double fromX, double fromZ, double toX, double toZ)
        {
            double angle = Math.Atan2(toX - fromX, toZ - fromZ); // radians, 0 = +z
            int steps = (int)Math.Round(angle / (2 * Math.PI) * 256);
            return (byte)((steps % 256 + 256) % 256);
        }

        /// <summary>The point <paramref name="distance"/> units from the player towards the NPC.</summary>
        public static void StopPoint(double playerX, double playerZ, double mobX, double mobZ, double distance,
            out double x, out double z)
        {
            double dx = mobX - playerX;
            double dz = mobZ - playerZ;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length <= distance || length < 0.001)
            {
                x = mobX;
                z = mobZ;
                return;
            }

            x = playerX + dx / length * distance;
            z = playerZ + dz / length * distance;
        }

        /// <summary>Starts the nearest opponent and lets other public NPCs support an existing NPC fight.</summary>
        internal static void Check(List<WorldClient> clients, Dictionary<ushort, SpatialGrid<Mob>> mobGrids)
        {
            if (!Enabled)
            {
                return;
            }

            double now = TimeUtils.GetSimTimeSeconds();
            foreach (WorldClient client in clients)
            {
                try
                {
                    CheckClient(client, mobGrids, now);
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("[AGGRO] Check failed: " + ex.Message);
                }
            }
        }

        private static void CheckClient(WorldClient client, Dictionary<ushort, SpatialGrid<Mob>> mobGrids, double now)
        {
            ClientData data = client?.playerData;
            if (data == null || data.isBot || !client.Alive || !data.getOnWorld() || data.waitForRPCShutDown || data.isDead || data.duelDeath ||
                data.worldChangePending || data.DistrictIsLA())
            {
                return;
            }

            if (!mobGrids.TryGetValue((ushort)data.getDistrictId(), out SpatialGrid<Mob> grid))
            {
                return;
            }

            CombatHandler handler = CombatManager.GetHandler(client);
            bool supporting = HasActiveNpcFight(client);
            if (!supporting && (!handler.CanStartNpcAggro || !RetryReady(client, now))) return;

            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            byte[] levelBytes = client.playerInstance.Level.getValue();
            int playerLevel = levelBytes != null && levelBytes.Length > 0 ? levelBytes[0] : 1;

            // Stealth (docs/STEALTH.md): invisible players are ignored; a sneaker is tested by the NPCs that would attack
            if (Stealth.Current(client) != null)
            {
                var willing = new List<(Mob, double)>();
                foreach (List<Mob> cell in grid.GetNeighborCells((float)px, (float)pz, (float)RangeUnits))
                    foreach (Mob mob in cell)
                    {
                        double d = Math.Sqrt((mob.getXPos() - px) * (mob.getXPos() - px) + (mob.getZPos() - pz) * (mob.getZPos() - pz));
                        if (SameFloor(mob.getYPos(), py) && WouldAttack(mob, d, playerLevel)) willing.Add((mob, d));
                    }
                if (Stealth.IgnoredByNpcs(client, willing, now)) return;
            }

            Mob attacker = null;
            ClientView attackerView = null;
            double best = double.MaxValue;
            foreach (List<Mob> cell in grid.GetNeighborCells((float)px, (float)pz, (float)RangeUnits))
            {
                foreach (Mob mob in cell)
                {
                    double dx = mob.getXPos() - px;
                    double dz = mob.getZPos() - pz;
                    double distance = Math.Sqrt(dx * dx + dz * dz);
                    if (!SameFloor(mob.getYPos(), py)) continue;
                    if (!supporting && distance <= RangeUnits && MobMotion.HoldingFor(mob, client)) return;
                    if ((!supporting && distance >= best) || !WouldAttack(mob, distance, playerLevel))
                    {
                        continue;
                    }

                    ClientView view = client.viewMan.FindViewForEntity(mob.getEntityId());
                    if (view == null || !view.viewCreated || view.viewNeedsToBeDeleted)
                    {
                        continue; // the player hasn't got it spawned yet
                    }

                    if (supporting)
                    {
                        if (MobMotion.Ordinary(mob) && !ControlActions.Incapacitated(TimedEffects.ForMob(mob).Effects))
                        {
                            bool firing = MobSupportingFire.TryFire(client, mob, distance, now);
                            if ((firing || ControlActions.CanMove(TimedEffects.ForMob(mob).Effects) && !MobiusCode.PreventsNpcInterlock(client)) &&
                                MobMotion.BeginChase(client, mob) && !firing)
                                Output.WriteDebugLog($"[AGGRO] {mob.getName()} pursues {data.getCharID()} as a second aggressor");
                        }
                        continue;
                    }

                    attacker = mob;
                    attackerView = view;
                    best = distance;
                }
            }

            if (attacker == null)
            {
                return;
            }

            data.lastAggroAt = now;
            Mob mobToAttack = attacker;
            ClientView viewToAttack = attackerView;
            DelayedActions.AfterFor(client, 0, () => Attack(client, mobToAttack, viewToAttack, px, py, pz));
        }

        internal static bool RetryReady(WorldClient client, double now) => now - client.playerData.lastAggroAt >= RetrySeconds;

        internal static bool HasActiveNpcFight(WorldClient client) =>
            CombatManager.GetHandler(client).CurrentNpcOpponentFor(client) != null;

        public static void Retaliate(WorldClient client, Mob mob)
        {
            if (client == null || mob.PassiveTarget || mob.getIsDead() || mob.isFriendly || client.playerData.isDead ||
                CombatManager.GetHandler(client).isCombatRunning || ControlActions.Incapacitated(TimedEffects.ForMob(mob).Effects)) return;
            ClientView view = client.viewMan.FindViewForEntity(mob.getEntityId());
            if (view == null || !view.viewCreated) return;
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            Attack(client, mob, view, x, y, z);
        }

        // GenericEnrageUpdate and GetClosestEnemyInSight. Use the normal
        // public-world player/NPC eligibility and the ordinary close-combat entry.
        internal static void EnragePulse(Mob mob, TimedEffects.Active control)
        {
            if (!CanStartEnrage(mob, control)) return;
            WorldClient nearest = null;
            double nearestDistance = double.MaxValue;
            List<WorldClient> players = WorldServer.RentClientSnapshot();
            try
            {
                for (int i = 0; i < players.Count; i++)
                {
                    WorldClient player = players[i];
                    if (!TryEnrageTarget(mob, player, out _, out _, out double distance) || distance >= nearestDistance)
                        continue;
                    nearest = player;
                    nearestDistance = distance;
                }
            }
            finally
            {
                WorldServer.ReturnClientSnapshot(players);
            }
            if (nearest == null) return;

            DelayedActions.AfterFor(nearest, 0, () =>
            {
                if (!CanStartEnrage(mob, control) ||
                    !TryEnrageTarget(mob, nearest, out ClientView view, out var position, out _)) return;
                Output.WriteDebugLog($"[ENRAGE] source={control.Parent} entity={mob.getEntityId()} seeks char={nearest.playerData.getCharID()}");
                Attack(nearest, mob, view, position.X, position.Y, position.Z, control);
            });
        }

        private static bool CanStartEnrage(Mob mob, TimedEffects.Active control) =>
            MobMotion.Ordinary(mob) && mob.getHealthC() > 0 && !mob.isFriendly && !mob.PassiveTarget &&
            mob.CombatantCount == 0 && !MobMotion.Chasing(mob) &&
            TimedEffects.ForMob(mob).Effects.IsCurrent(control, TimeUtils.GetSimTimeSeconds());

        private static bool TryEnrageTarget(Mob mob, WorldClient player, out ClientView view,
            out (double X, double Y, double Z) position, out double distanceSquared)
        {
            view = null;
            position = default;
            distanceSquared = 0;
            var data = player?.playerData;
            if (data == null || !player.Alive || !data.getOnWorld() || data.waitForRPCShutDown ||
                data.isDead || data.duelDeath || data.DistrictIsLA() || data.getDistrictId() != mob.getDistrict() ||
                Stealth.Current(player) != null)
                return false;
            position = CombatSight.Position(player);
            double dx = mob.getXPos() - position.X, dy = mob.getYPos() - position.Y, dz = mob.getZPos() - position.Z;
            distanceSquared = dx * dx + dy * dy + dz * dz;
            if (!(distanceSquared <= EnrageRangeUnits * EnrageRangeUnits) || !SameFloor(mob.getYPos(), position.Y)) return false;
            view = player.viewMan.FindViewForEntity(mob.getEntityId());
            return view != null && view.viewCreated && !view.viewNeedsToBeDeleted &&
                !CombatManager.GetHandler(player).isCombatRunning && CombatSight.Between(player, mob);
        }

        internal static void Attack(WorldClient client, Mob mob, ClientView view, double px, double py, double pz,
            TimedEffects.Active enrage = null)
        {
            CombatHandler handler = CombatManager.GetHandler(client);
            if (!handler.CanStartNpcAggro || !client.Alive || !client.playerData.getOnWorld() ||
                client.playerData.worldChangePending || client.playerData.isDead || client.playerData.duelDeath ||
                mob.getIsDead() || mob.getCombatTarget() != null || view.viewNeedsToBeDeleted) return;

            var effects = TimedEffects.ForMob(mob).Effects;
            if (ControlActions.Incapacitated(effects) || enrage != null &&
                (!effects.IsCurrent(enrage, TimeUtils.GetSimTimeSeconds()) || mob.CombatantCount != 0 ||
                 !TryEnrageTarget(mob, client, out _, out _, out _))) return;
            double dx = mob.getXPos() - px, dz = mob.getZPos() - pz;
            if (enrage == null && OpensFire(mob.getWeapon(), Math.Sqrt(dx * dx + dz * dz)) && ControlActions.CanUseWeapons(effects))
            {
                Output.WriteDebugLog("[AGGRO] " + mob.getName() + " opens fire on " + client.playerData.getCharID() +
                                     " (view " + view.ViewID + ")");
                handler.StartNpcGunfight(mob, view);
                client.FlushQueue();
                return;
            }

            if (!ControlActions.CanMove(TimedEffects.ForMob(mob).Effects) || MobiusCode.PreventsNpcInterlock(client)) return;

            if (mob.missionInstance == 0 && (!ReachedMeleeDistance(dx * dx + dz * dz) ||
                !NavMesh.HasSightPath(mob.getDistrict(), mob.getXPos(), mob.getYPos(), mob.getZPos(), px, py, pz)))
            {
                if(MobMotion.BeginChase(client,mob,enrage))Output.WriteDebugLog("[AGGRO] "+mob.getName()+" pursues "+client.playerData.getCharID());
                return;
            }

            // The NPC closes in and faces the player
            StopPoint(px, pz, mob.getXPos(), mob.getZPos(), MeleeDistanceUnits, out double x, out double z);
            mob.setXPos(x);
            mob.setZPos(z);
            mob.setRotation(YawTowards(x, z, px, pz));
            mob.updateAnimation((byte)Mob.Animations.STAND);

            // The client's close-combat request would be 40 <view u16> <spawn u16> 01 6b
            byte[] request =
            {
                (byte)view.ViewID, (byte)(view.ViewID >> 8),
                (byte)view.spawnId, (byte)(view.spawnId >> 8),
                0x01, 0x6b
            };
            Output.WriteDebugLog("[AGGRO] " + mob.getName() + " attacks " + client.playerData.getCharID() +
                                 " (view " + view.ViewID + ")");
            handler.ProcessRequestCloseCombat(ref request, npcStarted: true);
            client.FlushQueue();
        }

    }
}
