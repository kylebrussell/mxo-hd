using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// What attack abilities do to the NPC they target, from the retail captures under research/packet-dumps.
    /// Retail showed an ability hit as an NPC view update carrying the health and the ability's own effect
    /// (<c>04 80 80 80 c0 &lt;health&gt; c0 &lt;effect u32&gt; 01 &lt;counter&gt;</c>, see Mob.BuildHitUpdate), 3-8 s after the
    /// client's <c>80 b9</c> (the cast, then retail's interlock RPCs 45/47, which this server doesn't send).
    /// Damage is the health lost in that update; hits with no health lost (misses) are left out.
    /// Example, Code Nuke (actions_combat_marketplace_abilitychangecoder 10:31:48-53): 80 b9 67 01 05 00, then
    /// NPC view 5 Health 0x129 -&gt; 0 with EffectID 0x28000a11.
    /// </summary>
    public static class AbilityTargets
    {
        public class Effect
        {
            /// <summary>Damage range of the hit (0 = no direct damage).</summary>
            public UInt16 MinDamage, MaxDamage;
            public bool UseAttachedDamage;
            /// <summary>Effect id on the NPC view when the ability lands.</summary>
            public UInt32 HitFx;
        }

        /// <summary>Ability id to its effect on the target.</summary>
        public static readonly Dictionary<UInt16, Effect> Retail = new Dictionary<UInt16, Effect>
        {
            // Logic Blast 3.0: 367, 619, 619 (and a 461 kill), effect 0x280001c1
            { 329, new Effect { MinDamage = 367, MaxDamage = 619, HitFx = 0x280001c1 } },
            // Logic Barrage 4.0: 424, 424, effect 0x28000a02
            { 330, new Effect { MinDamage = 424, MaxDamage = 424, HitFx = 0x28000a02 } },
            // Logic Bomb 2: 266-323 (five hits of 269), effect 0x280001b7
            { 336, new Effect { MinDamage = 266, MaxDamage = 323, HitFx = 0x280001b7 } },
            // Logic Bomb 3: 283-346 (four of 346), effect 0x28000a0d
            { 337, new Effect { MinDamage = 283, MaxDamage = 346, HitFx = 0x28000a0d } },
            // Logic Bomb 4: 305-392 (three of 392), effect 0x280001b8
            { 357, new Effect { MinDamage = 305, MaxDamage = 392, HitFx = 0x280001b8 } },
            // Code Nuke: 645, 1183 (and kills of 297 and 434), effect 0x28000a11
            { 359, new Effect { MinDamage = 645, MaxDamage = 1183, HitFx = 0x28000a11 } },
            // Plague Zone 2 (actions_combat 10:27:49 on view 16): effect 0x28000162 and no damage; its damage over time
            // (ticks of 67-69 with effect 0x28000430 every 2.5 s) is a tempmod (TempMods.Retail, TimedEffects).
            { 466, new Effect { HitFx = 0x28000162 } },
            // UI Lag 1.0: effect 0x2800059c on the NPC, no damage. On a player retail sent four 27 s debuff
            // records (stats 1032 -10, 183 -17, 1011 -12, 342 -14); the NPC side isn't visible in the captures.
            { 97, new Effect { HitFx = 0x2800059c } },
            // Stun 1 / Stun 2.0: the effect id is the ability's ActivationFX (abilityIDs.csv); no retail NPC capture
            // shows a landed stun. The stun itself is a tempmod (TempMods.Retail: 4 s, as retail's StunAbility on players).
            { 315, new Effect { HitFx = 0x28000904 } },
            // Captured landed damage includes influence and must not replace the shipped base.
            { 198, new Effect { UseAttachedDamage = true, HitFx = 0x280008cd } },
            { 197, new Effect { UseAttachedDamage = true } },
            { 333, new Effect { HitFx = 0x28000908 } },
        };

        public static bool Has(UInt16 abilityId)
        {
            return Retail.ContainsKey(abilityId);
        }

        public static UInt16 RollDamage(Effect effect, Random random)
        {
            return effect.MaxDamage == 0 ? (UInt16)0 : (UInt16)random.Next(effect.MinDamage, effect.MaxDamage + 1);
        }

        internal static UInt16 RollDamage(Effect effect, AbilityItem ability, Random random) =>
            effect.UseAttachedDamage ? AbilityHandler.AbilityDamage(ability, random) : RollDamage(effect, random);

        private static readonly Random random = new Random();

        /// <summary>
        /// The ability lands on the NPC in the given view: its hit, stun and damage over time. A fight is started
        /// with the NPC (as when the client asks for close combat) if there isn't one, so the NPC fights back and
        /// its death, experience and loot go the usual way. Returns false when the view isn't a live NPC.
        /// </summary>
        public static bool Land(WorldClient client, AbilityItem ability, UInt16 targetViewId, bool sneakOpener = false, AbilityCastOrigin origin = AbilityCastOrigin.AlreadyResolved, AbilityEngageFoes.Cast engageFoes=null, AbilityReverseBulldog.Cast reverseBulldog=null, GhostStrikeCallbacks.Ticket ghostStrike=null)
        {
            if(engageFoes!=null && !engageFoes.Current)return false;
            if(reverseBulldog!=null && !reverseBulldog.Current)return false;
            Mob primary = FindMob(client, targetViewId, out var primaryView);
            if (primary == null) return PvpCombat.LandAbility(client, ability, targetViewId, sneakOpener,origin,engageFoes:engageFoes,reverseBulldog:reverseBulldog,ghostStrike:ghostStrike);
            // A real deflection can still affect an area secondary. A stale/unspawned target
            // is invalid before the contest and cannot supply the centre of that area.
            if (!primaryView.viewCreated || primaryView.viewNeedsToBeDeleted || primary.getIsDead() || primary.isFriendly)
                return false;
            if (!CombatSight.Between(client, primary))
            {
                Output.WriteDebugLog($"[ABILITY] Sight blocked ability {ability.getAbilityID()} on view {targetViewId}");
                return false;
            }
            var definition = AbilityCatalog.Find(ability.getAbilityID());
            double range = definition?.Number("Ability/SelectionRange") ?? 0;
            if (range > 0 && CombatCalculator.CalculateDistanceMetres(client.playerInstance.Position.getValue(),
                primary.getXPos(), primary.getYPos(), primary.getZPos()) > range) return false;
            var center = (primary.getXPos(), primary.getYPos(), primary.getZPos());
            LandOne(client, ability, targetViewId, sneakOpener, origin, engageFoes, reverseBulldog, ghostStrike);
            LandAround(client, ability, center, origin, engageFoes,
                skipMob: primary);
            // Enrage's zero-damage cast starts pursuit through its timed callback, even on a previously wounded NPC.
            if (ability.getAbilityID() != MobAggro.EnrageAbility && !primary.getIsDead() && primary.getHealthC() < primary.getHealthM())
                MobAggro.Retaliate(client, primary);
            return true;
        }

        // Range, sight, PvP permission and the contest still run when the cast lands.
        internal static bool HasLiveAttackSelection(WorldClient caster, ushort viewId, ushort? spawn, bool requireOpponent = false)
        {
            if (viewId <= 2) return false;
            var mob = FindMob(caster, viewId, out var view);
            if (view == null || !view.viewCreated || view.viewNeedsToBeDeleted ||
                spawn.HasValue && view.spawnId != spawn.Value)
                return false;

            if (mob != null) return !mob.isFriendly && !mob.getIsDead() && mob.getHealthC() > 0;
            var player = PvpCombat.PlayerInView(caster, viewId);
            return player != null && !player.playerData.isDead && !player.playerData.duelDeath &&
                PvpCombat.HealthOf(player) > 0 && (!requireOpponent || PvpCombat.Check(caster, player) == PvpRules.Verdict.Allowed);
        }

        internal static bool WithinSelectionRange(WorldClient caster, ushort ability, ushort targetView)
        {
            double range = AbilityCatalog.Find(ability)?.Number("Ability/SelectionRange") ?? 0;
            if (range <= 0) return true;
            var mob = FindMob(caster, targetView, out _);
            var player = mob == null ? PvpCombat.PlayerInView(caster, targetView) : null;
            if (mob == null && player == null) return false;
            var (x, y, z) = mob != null ? (mob.getXPos(), mob.getYPos(), mob.getZPos()) : CombatSight.Position(player);
            return CombatCalculator.CalculateDistanceMetres(caster.playerInstance.Position.getValue(), x, y, z) <= range;
        }

        internal static bool HasSelectionSight(WorldClient caster, AbilityCatalog.Entry definition, ushort targetView)
        {
            if (definition.Number("Ability/DoesNOTRequireLOS") != 0) return true;
            var mob = FindMob(caster, targetView, out _);
            if (mob != null) return CombatSight.Between(caster, mob);
            var player = PvpCombat.PlayerInView(caster, targetView);
            return player != null && CombatSight.Between(caster, player);
        }

        /// <summary>Expand a landed primary into the template's hostile area.</summary>
        internal static void LandAround(WorldClient client, AbilityItem ability, (double X, double Y, double Z) primary,
            AbilityCastOrigin origin, AbilityEngageFoes.Cast engageFoes, WorldClient skipPlayer = null, Mob skipMob = null)
        {
            var definition = AbilityCatalog.Find(ability.getAbilityID());
            double radius = AbilityRadius.TargetMetres(client, ability.getAbilityID());
            double selector = definition?.Number("Ability/CastDirectObjects") ?? 0;
            double angle = definition?.Number("Ability/AngleOfEffect") ?? 0;
            if (radius <= 0 || (selector != 3 && selector != 13) || (selector == 13 && (angle <= 0 || angle > 180))) return;

            var center = selector == 13 ? CombatSight.Position(client) : primary;
            bool Included(double x, double z) => selector != 13 ||
                InCone(primary.X - center.X, primary.Z - center.Z, x - center.X, z - center.Z, angle);

            // Snapshot both sets before landing any secondary effects.
            var mobs = AbilityHostile.MobsInArea(client, center.X, center.Y, center.Z, radius, skipMob);
            var players = AbilityHostile.PlayersInArea(client, center.X, center.Y, center.Z, radius, skipPlayer);
            foreach (var (mob, view) in mobs)
                if (Included(mob.getXPos(), mob.getZPos()))
                    LandOne(client, ability, view, origin: origin, engageFoes: engageFoes);
            foreach (var player in players)
            {
                var p = CombatSight.Position(player);
                if (Included(p.X, p.Z))
                    PvpCombat.LandAbilityOn(client, ability, player, origin: origin);
            }
        }

        /// <summary>Selector 13 uses the selected enemy as its aim axis. The stored angle is a full horizontal arc.</summary>
        internal static bool InCone(double aimX, double aimZ, double dx, double dz, double angle)
        {
            double aimLength = Math.Sqrt(aimX * aimX + aimZ * aimZ);
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (aimLength == 0 || length == 0) return false;
            double alignment = (aimX * dx + aimZ * dz) / (aimLength * length);
            return alignment >= Math.Cos(angle * Math.PI / 360);
        }

        public static bool WithinArea(double x, double y, double z, double tx, double ty, double tz, double radiusMetres)
        {
            double dx = x - tx, dy = y - ty, dz = z - tz, radius = radiusMetres * 100;
            return Math.Abs(dy) < 150 && dx * dx + dy * dy + dz * dz <= radius * radius;
        }

        internal static bool LandOne(WorldClient client, AbilityItem ability, UInt16 targetViewId, bool sneakOpener = false, AbilityCastOrigin origin = AbilityCastOrigin.AlreadyResolved, AbilityEngageFoes.Cast engageFoes=null, AbilityReverseBulldog.Cast reverseBulldog=null, GhostStrikeCallbacks.Ticket ghostStrike=null)
        {
            UInt16 abilityId = ability.getAbilityID();
            bool contest=FreeCastViralContest.Applies(abilityId,origin);
            Mob mob=null;ClientView view=null;
            if(contest)
            {
                mob=FindMob(client,targetViewId,out view);
                if(mob==null)return PvpCombat.LandAbility(client,ability,targetViewId,sneakOpener,origin,ghostStrike:ghostStrike);
                if(!FreeCastViralContest.Npc(client,ability,mob,view))return true;
            }
            Effect effect;
            UInt16 damage;
            UInt32 fx;
            lock (random)
            {
                if (Retail.TryGetValue(abilityId, out effect))
                {
                    damage = RollDamage(effect, ability, random);
                    fx = effect.HitFx;
                }
                else
                {
                    // Viruses do their damage over time only (retail's Code Infection and Infect Area: "infects you",
                    // then the ticks; TempMods.Retail)
                    var source = AbilityCatalog.Find(abilityId);
                    damage = TempMods.TryGet(abilityId, out TempMods.Effect timed) && timed.Virus ||
                        source != null && !source.DirectDamage && (source.DamageOverTime || TempMods.Has(abilityId))
                        ? (UInt16)0
                        : abilityId == AwakenedPowerShot.Id ? AwakenedPowerShot.RollDamage(client, random) :
                            AbilityHandler.AbilityDamage(ability, random, allowTypedResistanceQa:true, characterLevel:client.playerInstance.Level.getValue()[0]);
                    fx = ability.getActivationFX();
                }
            }

            if(!contest)mob = FindMob(client, targetViewId, out view);
            // Combat-style specials and Exile weapons have their own damage, never the generic fallback (CombatSpecials)
            damage = CombatSpecials.Damage(abilityId, mob) ?? damage;
            damage = AbilityStats.ScaleAbilityDamage(client, abilityId, SneakAttacks.Add(damage, abilityId, sneakOpener));
            if (mob == null)
            {
                // Another player: PvpCombat decides whether they can be hit
                return PvpCombat.LandAbility(client, ability, targetViewId, sneakOpener,origin,ghostStrike:ghostStrike);
            }
            if (mob.getIsDead())
            {
                return false;
            }

            if(!GhostStrikeCallbacks.BeforeHit(ghostStrike,client,mob,targetViewId,view.spawnId))return false;
            var engageRecipient=AbilityEngageFoes.Before(engageFoes,client,mob,view??client.viewMan.getViewById(targetViewId),abilityId);

            var conditionalFence=new ConditionalThrownDamage.Fence(client,npcSpawn:view.spawnId);
            CombatHandler handler = CombatManager.GetHandler(client);
            // Timed effects first (TempMods: stuns, debuffs, damage over time): the fight may start here, and its rounds
            // must already see a stun
            damage = PunishingBlows.Npc(client, mob, damage);
            damage = TypedAbilityResistance.Npc(client, mob, abilityId, damage);
            TimedEffects.ApplyToMob(client, mob, targetViewId, abilityId,reverseBulldog:reverseBulldog);
            AbilityHostile.LandedOnMob(client, mob, abilityId);
            if (!handler.ApplyAbilityToNpc(mob, targetViewId, damage, fx)) return false;
            AbilityEngageFoes.After(engageRecipient);
            ConditionalThrownDamage.Npc(client,mob,targetViewId,abilityId,fx,conditionalFence);
            GhostStrikeCallbacks.Accepted(ghostStrike);
            CombatSpecials.Landed(client, abilityId);
            SpyAbilityRegistry.Landed(client, abilityId);
            Output.WriteDebugLog("[ABILITY] " + abilityId + " lands on view " + targetViewId + " for " + damage);
            return true;
        }

        /// <summary>The live NPC in one of the client's views, or null.</summary>
        public static Mob FindMob(WorldClient client, UInt16 viewId, out ClientView view)
        {
            view = client.viewMan.getViewById(viewId);
            if (view == null)
            {
                return null;
            }
            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob.getEntityId() == view.entityId)
                    {
                        return mob;
                    }
                }
            }
            return null;
        }

        /// <summary>The client's close-combat request for the view (40 &lt;view u16&gt; &lt;spawn u16&gt; 01 6b), as MobAggro builds it.</summary>
        public static byte[] CloseCombatRequest(ClientView view)
        {
            return new byte[]
            {
                (byte)view.ViewID, (byte)(view.ViewID >> 8),
                (byte)view.spawnId, (byte)(view.spawnId >> 8),
                0x01, 0x6b
            };
        }

    }
}
