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
            /// <summary>Effect id on the NPC view when the ability lands.</summary>
            public UInt32 HitFx;
            /// <summary>Damage over time: this many ticks, every TickSeconds, each MinTick-MaxTick, with TickFx.</summary>
            public int Ticks;
            public double TickSeconds;
            public UInt16 MinTick, MaxTick;
            public UInt32 TickFx;
            /// <summary>Seconds the NPC can neither attack nor move (stuns).</summary>
            public double StunSeconds;
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
            // Plague Zone 2 (actions_combat 10:27:49 on view 16): effect 0x28000162 and no damage, then ticks of
            // 67-69 with effect 0x28000430 (or just the counter) at +5, +7, +10, +12, +15 s after the use.
            { 466, new Effect { HitFx = 0x28000162, Ticks = 5, TickSeconds = 2.5, MinTick = 65, MaxTick = 69, TickFx = 0x28000430 } },
            // UI Lag 1.0: effect 0x2800059c on the NPC, no damage. On a player retail sent four 27 s debuff
            // records (stats 1032 -10, 183 -17, 1011 -12, 342 -14); the NPC side isn't visible in the captures.
            { 97, new Effect { HitFx = 0x2800059c } },
            // Stun 1 / Stun 2.0: the effect id is the ability's ActivationFX (abilityIDs.csv); no retail NPC capture
            // shows a landed stun. The 4 s is how long retail's stun effect (StunAbility, 102) stayed on a player.
            { 315, new Effect { HitFx = 0x28000904, StunSeconds = 4 } },
            { 333, new Effect { HitFx = 0x28000908, StunSeconds = 4 } },
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
                    damage = AbilityHandler.AbilityDamage(ability, random);
                    fx = ability.getActivationFX();
                }
            }

            Mob mob = FindMob(client, targetViewId, out ClientView view);
            if (mob == null || mob.getIsDead())
            {
                return false;
            }

            CombatHandler handler = CombatManager.GetHandler(client);
            if (!handler.ApplyAbilityToTarget(targetViewId, damage, fx))
            {
                StartFight(client, view);
                if (!handler.ApplyAbilityToTarget(targetViewId, damage, fx))
                {
                    return false;
                }
            }
            Output.WriteDebugLog("[ABILITY] " + abilityId + " lands on view " + targetViewId + " for " + damage);

            if (effect != null && effect.StunSeconds > 0)
            {
                mob.Stun(TimeUtils.GetSimTimeSeconds() + effect.StunSeconds);
                Output.WriteDebugLog("[ABILITY] " + mob.getName() + " stunned for " + effect.StunSeconds + " s");
            }
            if (effect != null && effect.Ticks > 0)
            {
                ScheduleTick(client, mob, targetViewId, effect, 1);
            }
            return true;
        }

        private static void ScheduleTick(WorldClient client, Mob mob, UInt16 targetViewId, Effect effect, int tick)
        {
            DelayedActions.AfterFor(client, effect.TickSeconds, () =>
            {
                if (mob.getIsDead())
                {
                    return;
                }
                UInt16 damage;
                lock (random)
                {
                    damage = (UInt16)random.Next(effect.MinTick, effect.MaxTick + 1);
                }
                // A tick only lands while the fight with this NPC goes on (it ends when the NPC dies).
                if (!CombatManager.GetHandler(client).ApplyAbilityToTarget(targetViewId, damage, effect.TickFx))
                {
                    return;
                }
                if (tick < effect.Ticks)
                {
                    ScheduleTick(client, mob, targetViewId, effect, tick + 1);
                }
            });
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

        private static void StartFight(WorldClient client, ClientView view)
        {
            byte[] request = CloseCombatRequest(view);
            CombatManager.GetHandler(client).ProcessRequestCloseCombat(ref request);
        }
    }
}
