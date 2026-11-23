using System;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Ground-targeted Spy bombs (CastDirectObjects=2, CastValidTarget=1, CastWhenUsed=1):
    /// <list type="bullet">
    /// <item>Flash Bomb (107, Cutthroat): 5 m radius, Blind 12 s + 15% each Dazed/Off-Balance/Staggered 12 s.</item>
    /// <item>Smoke Bomb (364, Master Shadow): 3 m radius, Blind 20 s + SmokeBombInit (tempmods.py 1147) vanishing
    /// into Invisibility (1116) after 0.5 s when carrying an Invisibility Belt (GOID 460).</item>
    /// </list>
    /// </summary>
    public static class SpyGroundBombs
    {
        public const ushort FlashBomb = 107, SmokeBomb = 364;
        public const uint InvisibilityBeltGoid = 460;
        // SmokeBomb 0.3 s term -> SmokeBombInit schedules 0.2 s VanishInit (tempmods.py 1147-1166).
        public const double SmokeBombVanishDelaySeconds = 0.5;

        public sealed class Cast
        {
            public bool HasGround;
            public double X, Y, Z;
        }

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == FlashBomb || id == SmokeBomb;
        }

        public static bool TryReadGroundTarget(byte[] packet, out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (packet == null || packet.Length != 41) return false;
            double gx = BitConverter.ToDouble(packet, 10);
            double gy = BitConverter.ToDouble(packet, 18);
            double gz = BitConverter.ToDouble(packet, 26);
            if (!double.IsFinite(gx) || !double.IsFinite(gy) || !double.IsFinite(gz)) return false;
            // Ignore all-zero or subnormal uninitialized stack bytes from non-ground requests.
            if (Math.Abs(gx) < 1.0 && Math.Abs(gz) < 1.0) return false;
            if (Math.Abs(gx) > 1e7 || Math.Abs(gy) > 1e7 || Math.Abs(gz) > 1e7) return false;
            x = gx; y = gy; z = gz;
            return true;
        }

        public static bool TryRefuse(WorldClient caster, ushort abilityId, byte[] packet, out uint refusalReason)
        {
            refusalReason = 0;
            if (!Handles(abilityId)) return false;
            if (CombatManager.GetHandler(caster).isCombatRunning)
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (TryReadGroundTarget(packet, out double gx, out double gy, out double gz))
            {
                double range = AbilityCatalog.Find(abilityId)?.Number("Ability/SelectionRange") ?? 16;
                if (range > 0 && caster.playerInstance?.Position?.getValue() is byte[] pos && pos.Length == 24)
                {
                    double dist = CombatCalculator.CalculateDistanceMetres(pos, gx, gy, gz);
                    if (dist > range)
                    {
                        refusalReason = AbilityRefusals.TargetOutOfRange;
                        return true;
                    }
                }
            }
            return false;
        }

        public static Cast Capture(WorldClient caster, ushort abilityId, byte[] packet)
        {
            if (!Handles(abilityId)) return null;
            if (TryReadGroundTarget(packet, out double gx, out double gy, out double gz))
                return new Cast { HasGround = true, X = gx, Y = gy, Z = gz };
            return new Cast();
        }

        public static bool HasInvisibilityBelt(WorldClient caster) => HasInvisibilityBelt(caster, null);

        internal static bool HasInvisibilityBelt(WorldClient caster, ushort? slot)
        {
            if (caster?.playerData == null || Store.dbManager?.WorldDbHandler == null) return false;
            return Store.dbManager.WorldDbHandler.GetInventoryItems(caster.playerData.getCharID())
                .Any(i => i.Slot < 97 && (!slot.HasValue || i.Slot == slot.Value) && i.Goid == InvisibilityBeltGoid);
        }

        public static void Land(WorldClient caster, AbilityItem ability, Cast cast, Func<WorldClient, bool> hasBelt = null)
        {
            if (ability == null || caster == null || !Handles(ability.getAbilityID()) || !caster.Alive ||
                !caster.playerData.getOnWorld() || caster.playerData.worldChangePending ||
                caster.playerData.DistrictIsLA() || caster.playerData.isDead || caster.playerData.duelDeath ||
                !ControlActions.CanUseAbilities(caster)) return;

            ushort id = AbilityCatalog.Canonical(ability.getAbilityID());
            int world = caster.playerData.worldGeneration, life = caster.playerData.lifeGeneration;
            bool Current() => caster.Alive && caster.playerData.worldGeneration == world &&
                caster.playerData.lifeGeneration == life && caster.playerData.getOnWorld() &&
                !caster.playerData.worldChangePending && !caster.playerData.DistrictIsLA() &&
                !caster.playerData.isDead && !caster.playerData.duelDeath && ControlActions.CanUseAbilities(caster);

            double cx = 0, cy = 0, cz = 0;
            if (cast != null && cast.HasGround)
            {
                cx = cast.X; cy = cast.Y; cz = cast.Z;
            }
            else
            {
                NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(), ref cx, ref cy, ref cz);
            }

            double radius = AbilityRadius.TargetMetres(caster, id);
            if (radius > 0 && Current())
            {
                var mobs = AbilityHostile.MobsInArea(caster, cx, cy, cz, radius);
                var players = AbilityHostile.PlayersInArea(caster, cx, cy, cz, radius);
                Output.WriteDebugLog($"[BOMB] {id} center=({cx:F1},{cy:F1},{cz:F1}) radius={radius} mobs={mobs.Count} players={players.Count}");
                foreach (var (mob, view) in mobs)
                {
                    if (!Current()) return;
                    if (mob.pet == null) AbilityTargets.LandOne(caster, ability, view, origin: AbilityCastOrigin.NormalFreeCast);
                }
                foreach (var target in players)
                {
                    if (!Current()) return;
                    PvpCombat.LandAbilityOn(caster, ability, target, origin: AbilityCastOrigin.NormalFreeCast);
                }
            }

            if (id == SmokeBomb)
            {
                ScheduleSmokeBombVanish(caster, hasBelt ?? HasInvisibilityBelt);
            }
        }

        internal static void ScheduleSmokeBombVanish(WorldClient caster, Func<WorldClient, bool> hasBelt)
        {
            ClientData owner = caster.playerData;
            PlayerCharacter avatar = caster.playerInstance;
            int world = owner.worldGeneration, life = owner.lifeGeneration;
            long transport = caster.TransportFence?.Capture() ?? 0;
            DelayedActions.AfterFor(caster, SmokeBombVanishDelaySeconds, () =>
            {
                if ((caster.TransportFence != null && !caster.TransportFence.IsCurrent(transport)) || !caster.Alive ||
                    !ReferenceEquals(caster.playerData, owner) || !ReferenceEquals(caster.playerInstance, avatar) ||
                    owner.worldGeneration != world || owner.lifeGeneration != life || !owner.getOnWorld() ||
                    owner.worldChangePending || owner.isDead || owner.duelDeath) return;

                if (!hasBelt(caster))
                {
                    Output.WriteDebugLog("[SPECIAL] Smoke Bomb: char " + owner.getCharID() + " missing Invisibility Belt (460)");
                    new ServerPackets().sendSystemChatMessage(caster, "Smoke Bomb requires an Invisibility Belt to vanish.", "BROADCAST");
                    caster.FlushQueue();
                    return;
                }

                CombatManager.GetHandler(caster).StopCombatIfActive(CombatSession.CombatEndReason.AttackerFled);
                if (Stealth.Begin(caster, Stealth.Defs[Stealth.InvisibilityGrantAbility],
                    Stealth.AbilityLevel(caster, Stealth.InvisibilityGrantAbility)) == null) return;
                caster.FlushQueue();
                Output.WriteDebugLog("[SPECIAL] Smoke Bomb: char " + owner.getCharID() + " vanishes into invisibility");
            });
        }
    }
}
