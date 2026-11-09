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
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("MXO_AGGRO") != "0";
        public static readonly double RangeUnits = ReadDouble("MXO_AGGRO_RANGE", 12) * 100; // 100 units per metre
        public static readonly int LevelGap = (int)ReadDouble("MXO_AGGRO_LEVEL_GAP", 10);

        /// <summary>Seconds a player is left alone after an aggro (so a fight that fails to start isn't retried every pass).</summary>
        public const double RetrySeconds = 5;

        /// <summary>How far from the player (units) the NPC stops: arm's length.</summary>
        public const double MeleeDistanceUnits = 150;

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
            if (mob == null || mob.isFriendly || mob.getIsDead() || !mob.getIsSpawned() || mob.getCombatTarget() != null)
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

        /// <summary>Called every visibility pass: lets the nearest willing NPC attack each idle player.</summary>
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
            if (data == null || !client.Alive || !data.getOnWorld() || data.waitForRPCShutDown || data.isDead ||
                data.DistrictIsLA() || now - data.lastAggroAt < RetrySeconds)
            {
                return;
            }

            if (!mobGrids.TryGetValue((ushort)data.getDistrictId(), out SpatialGrid<Mob> grid))
            {
                return;
            }

            CombatHandler handler = CombatManager.GetHandler(client);
            if (handler.isCombatRunning)
            {
                return;
            }

            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            byte[] levelBytes = client.playerInstance.Level.getValue();
            int playerLevel = levelBytes != null && levelBytes.Length > 0 ? levelBytes[0] : 1;

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
                    if (distance >= best || !SameFloor(mob.getYPos(), py) || !WouldAttack(mob, distance, playerLevel))
                    {
                        continue;
                    }

                    ClientView view = client.viewMan.FindViewForEntity(mob.getEntityId());
                    if (view == null || !view.viewCreated)
                    {
                        continue; // the player hasn't got it spawned yet
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
            DelayedActions.AfterFor(client, 0, () => Attack(client, mobToAttack, viewToAttack, px, pz));
        }

        private static void Attack(WorldClient client, Mob mob, ClientView view, double px, double pz)
        {
            double dx = mob.getXPos() - px, dz = mob.getZPos() - pz;
            if (OpensFire(mob.getWeapon(), Math.Sqrt(dx * dx + dz * dz)))
            {
                Output.WriteDebugLog("[AGGRO] " + mob.getName() + " opens fire on " + client.playerData.getCharID() +
                                     " (view " + view.ViewID + ")");
                CombatManager.GetHandler(client).StartNpcGunfight(mob, view);
                client.FlushQueue();
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
            CombatManager.GetHandler(client).ProcessRequestCloseCombat(ref request);
            client.FlushQueue();
        }
    }
}
