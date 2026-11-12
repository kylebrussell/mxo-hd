using System;
using System.Collections.Generic;
using System.Linq;

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
            // Cheap Shot (luizoe old sniffer 19:45-19:53): the NPC lost 21-25 and once 67 within the next rounds, with
            // effect 0x280008cd when one was shown; Head Butt (17:11:35): 21, no effect id seen.
            { 198, new Effect { MinDamage = 21, MaxDamage = 67, HitFx = 0x280008cd } },
            { 197, new Effect { MinDamage = 21, MaxDamage = 45 } },
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

        private static readonly Random random = new Random();

        /// <summary>
        /// The ability lands on the NPC in the given view: its hit, stun and damage over time. A fight is started
        /// with the NPC (as when the client asks for close combat) if there isn't one, so the NPC fights back and
        /// its death, experience and loot go the usual way. Returns false when the view isn't a live NPC.
        /// </summary>
        public static bool Land(WorldClient client, AbilityItem ability, UInt16 targetViewId)
        {
            Mob primary = FindMob(client, targetViewId, out _);
            if (primary == null) return PvpCombat.LandAbility(client, ability, targetViewId);
            if (primary.getIsDead() || primary.isFriendly) return false;
            var definition = AbilityCatalog.Find(ability.getAbilityID());
            double range = definition?.Number("Ability/SelectionRange") ?? 0;
            if (range > 0 && CombatCalculator.CalculateDistanceMetres(client.playerInstance.Position.getValue(),
                primary.getXPos(), primary.getYPos(), primary.getZPos()) > range) return false;
            var targets = new List<(Mob Mob, UInt16 View)> { (primary, targetViewId) };
            double radius = definition?.Number("Ability/RadiusOfEffect") ?? 0;
            if (definition?.Number("Ability/CastDirectObjects") == 3 && radius > 0)
            {
                lock (WorldServer.mobs.SyncRoot)
                    foreach (Mob mob in WorldServer.mobs)
                    {
                        if (mob == primary || mob.isFriendly || mob.getIsDead() || mob.getDistrict() != primary.getDistrict()) continue;
                        var view = client.viewMan.FindViewForEntity(mob.getEntityId());
                        if (view != null && WithinArea(primary.getXPos(), primary.getYPos(), primary.getZPos(),
                            mob.getXPos(), mob.getYPos(), mob.getZPos(), radius)) targets.Add((mob, view.ViewID));
                    }
            }
            foreach (var target in targets) LandOne(client, ability, target.View);
            // Area hostile abilities also reach players around the target that PvP rules allow
            if (definition?.Number("Ability/CastDirectObjects") == 3 && radius > 0)
                foreach (WorldClient player in AbilityHostile.PlayersInArea(client, primary.getXPos(), primary.getYPos(), primary.getZPos(), radius))
                    PvpCombat.LandAbilityOn(client, ability, player);
            if (!primary.getIsDead() && primary.getHealthC() < primary.getHealthM()) MobAggro.Retaliate(client, primary);
            return true;
        }

        /// <summary>An area ability that landed on a player also reaches the NPCs and PvP-legal players around them.</summary>
        public static void LandAround(WorldClient client, AbilityItem ability, WorldClient primary)
        {
            var definition = AbilityCatalog.Find(ability.getAbilityID());
            double radius = definition?.Number("Ability/RadiusOfEffect") ?? 0;
            if (definition?.Number("Ability/CastDirectObjects") != 3 || radius <= 0) return;
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(primary.playerInstance.Position.getValue(), ref x, ref y, ref z);
            foreach (WorldClient player in AbilityHostile.PlayersInArea(client, x, y, z, radius, primary))
                PvpCombat.LandAbilityOn(client, ability, player);
            foreach (var (mob, view) in AbilityHostile.MobsInArea(client, x, y, z, radius)) LandOne(client, ability, view);
        }

        public static bool WithinArea(double x, double y, double z, double tx, double ty, double tz, double radiusMetres)
        {
            double dx = x - tx, dy = y - ty, dz = z - tz, radius = radiusMetres * 100;
            return Math.Abs(dy) < 150 && dx * dx + dy * dy + dz * dz <= radius * radius;
        }

        private static bool LandOne(WorldClient client, AbilityItem ability, UInt16 targetViewId)
        {
            UInt16 abilityId = ability.getAbilityID();
            Effect effect;
            UInt16 damage;
            UInt32 fx;
            lock (random)
            {
                if (Retail.TryGetValue(abilityId, out effect))
                {
                    damage = RollDamage(effect, random);
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
                        : AbilityHandler.AbilityDamage(ability, random);
                    fx = ability.getActivationFX();
                }
            }

            Mob mob = FindMob(client, targetViewId, out ClientView view);
            // Combat-style specials and Exile weapons have their own damage, never the generic fallback (CombatSpecials)
            damage = CombatSpecials.Damage(abilityId, mob) ?? damage;
            damage = AbilityStats.ScaleAbilityDamage(client, abilityId, damage);
            if (mob == null)
            {
                // Another player: PvpCombat decides whether they can be hit
                return PvpCombat.LandAbility(client, ability, targetViewId);
            }
            if (mob.getIsDead())
            {
                return false;
            }

            CombatHandler handler = CombatManager.GetHandler(client);
            // Timed effects first (TempMods: stuns, debuffs, damage over time): the fight may start here, and its rounds
            // must already see a stun
            TimedEffects.ApplyToMob(client, mob, targetViewId, abilityId);
            AbilityHostile.LandedOnMob(client, mob, abilityId);
            if (!handler.ApplyAbilityToNpc(mob, targetViewId, damage, fx)) return false;
            CombatSpecials.Landed(client, abilityId);
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
