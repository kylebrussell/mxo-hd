using System;
using System.Collections.Generic;
using System.Linq;
using hds.world.Structures;

namespace hds
{
    /// <summary>Friendly abilities resolve to players, never to the hostile damage fallback.</summary>
    public static class AbilitySupport
    {
        public static bool Handles(UInt16 id) => AbilityCatalog.Find(id)?.Healing == true || AbilityBuffs.IsBuff(id);
        public static WorldClient PlayerInView(WorldClient caster, UInt16 view)
        {
            if (view <= 2) return caster;
            var target = caster.viewMan.getViewById(view);
            return target == null ? null : SocialHandler.OnlineWhere(c => c.playerData.getEntityId() == target.entityId).FirstOrDefault();
        }

        private static bool InRange(WorldClient a, WorldClient b, double metres)
        {
            if (a.playerData.getDistrictId() != b.playerData.getDistrictId() || b.playerData.isDead) return false;
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(b.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return CombatCalculator.CalculateDistanceMetres(a.playerInstance.Position.getValue(), x, y, z) <= metres;
        }

        public static List<WorldClient> Targets(WorldClient caster, UInt16 id, UInt16 view)
        {
            var entry = AbilityCatalog.Find(id);
            bool group = entry?.Number("Ability/CastDirectObjects") == 9;
            if (group)
            {
                var team = SocialGroups.Instance.FindTeamOf(caster.playerData.getCharID());
                double radius = entry.Number("Ability/RadiusOfEffect");
                return SocialHandler.OnlineWhere(c => (c == caster || team?.HasMember(c.playerData.getCharID()) == true) &&
                    InRange(caster, c, radius)).ToList();
            }
            // Self-only buffs ignore a selected hostile, as class and Hyper buffs did before.
            bool selfOnly = entry?.Number("Ability/CastDirectObjects") == 1;
            WorldClient target = !selfOnly && (entry?.FriendlyTarget == true || entry?.Healing == true)
                ? PlayerInView(caster, view) : caster;
            return target != null && InRange(caster, target, entry?.Number("Ability/SelectionRange", 20) ?? 20)
                ? new List<WorldClient> { target } : new List<WorldClient>();
        }

        public static void Land(WorldClient caster, UInt16 id, UInt16 view)
        {
            var entry = AbilityCatalog.Find(id);
            foreach (WorldClient target in Targets(caster, id, view))
            {
                if (entry?.Healing == true)
                {
                    UInt16 max = NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1);
                    // MobiusCodeEffect is explicit; other heal scaling is native and remains audited as partial.
                    UInt16 amount = id == 967 ? (UInt16)(max / 10) : AbilityHandler.HealAmount(target);
                    var hot = entry.Modifiers.FirstOrDefault(m => m.Text("StatModUpdateFunc") == "HOT");
                    if (hot != null && hot.Number("Duration") > 0)
                    {
                        var effect = new TempMods.Effect { Ability = id, Mods = new[] { new TempMods.Mod
                        {
                            Kind = TempMods.KindLinked, Target = 108, Proc = TempMods.Proc.Hot,
                            MinValue = amount, MaxValue = amount, Seconds = (float)hot.Number("Duration"),
                            Interval = hot.Number("UpdateFuncInterval", TempMods.DotIntervalSeconds)
                        } } };
                        TimedEffects.Apply(TimedEffects.ForPlayer(target), effect, TimeUtils.GetSimTimeSeconds(), caster, TimedEffects.PlayerName(caster));
                    }
                    else AbilityHandler.HealSelf(target, amount);
                    Output.WriteDebugLog("[ABILITY] Heal " + id + " -> " + TimedEffects.PlayerName(target) + " for " + amount);
                }
                AbilityBuffs.Apply(target, id);
                target.FlushQueue();
            }
        }
    }
}
