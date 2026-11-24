using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Distracting Blow (1128, SneakFreeDistractingBlowAbility) stores:
    /// <list type="bullet">
    /// <item>Instant thrown damage using native ValueFunction 3: <c>min(17 + trunc(characterLevel * binary32(2.5999999)), 114)</c>
    /// (68 at level 20, 114 at level 38+).</item>
    /// <item>A caster-on-landing <c>ApplyAuraEffect</c> (tempmods.py:1308) invoking internal <c>DistractingBlowEffectAbility</c> (1129):
    /// <c>CastDirectObjects=7</c> nearby friendlies within 8 m receive +50 Contested Withdraw (stat 1005) for 12 s.</item>
    /// </list>
    /// </summary>
    public static class DistractingBlow
    {
        public const ushort Id = 1128, EffectId = 1129;

        public static bool RetainsStealthDuringCast(ushort id) => AbilityCatalog.Canonical(id) == Id;

        public static bool TryRefuse(WorldClient caster, ushort abilityId, ushort targetViewId, ushort? spawn, out uint reason)
        {
            reason = 0;
            if (AbilityCatalog.Canonical(abilityId) != Id) return false;
            if (CombatManager.GetHandler(caster).isCombatRunning)
            {
                reason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (CombatStyles.Active(caster) != CombatStyles.SpyStyle)
            {
                reason = AbilityRefusals.WrongStyle;
                return true;
            }
            if ((PvpFlag.FlagsOf(caster) & Stealth.StealthedFlag) == 0)
            {
                reason = AbilityRefusals.CasterStateRequired;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(caster, targetViewId, spawn, requireOpponent: true))
            {
                reason = AbilityRefusals.InvalidTarget;
                return true;
            }
            if (!AbilityTargets.WithinSelectionRange(caster, Id, targetViewId))
            {
                reason = AbilityRefusals.TargetOutOfRange;
                return true;
            }
            if (!AbilityTargets.HasSelectionSight(caster, AbilityCatalog.Find(Id), targetViewId))
            {
                reason = AbilityRefusals.TargetNotInSight;
                return true;
            }
            return false;
        }

        public static bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, out ushort damage)
        {
            damage = 0;
            if (source == null || source.Id != Id) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 3 ||
                !shipped.Has("MinValue") ||
                !AbilityValues.TryValue(shipped, 0, Math.Max(0, characterLevel), out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }

        public static bool TryGetEffectBuff(ushort id, out AbilityBuffs.Buff buff)
        {
            buff = null;
            if (AbilityCatalog.Canonical(id) != EffectId) return false;
            var entry = AbilityCatalog.Find(EffectId);
            var mod = entry?.Modifiers.SingleOrDefault(m => m.Number("WhenEnabled", -1) == 2 &&
                m.Target == 1005 && m.Number("DurationType", -1) == 0 && m.Number("Duration") > 0 &&
                m.Number("MinValue") == m.Number("MaxValue"));
            if (mod == null) return false;
            buff = new AbilityBuffs.Buff
            {
                Seconds = (float)mod.Number("Duration"),
                Modifiers = new[] { new AbilityBuffs.Modifier(AbilityBuffs.KindStat, mod.Target, (int)mod.Number("MinValue")) }
            };
            return true;
        }

        public static List<WorldClient> EffectRecipients(WorldClient caster)
        {
            var recipients = new List<WorldClient>();
            if (caster == null || caster.playerData.isDead || caster.playerData.duelDeath || PvpCombat.HealthOf(caster) == 0)
                return recipients;
            var effect = AbilityCatalog.Find(EffectId);
            double radius = effect?.Number("Ability/RadiusOfEffect", 8) ?? 8;
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(), ref x, ref y, ref z);

            recipients.Add(caster);
            if (WorldServer.Clients == null) return recipients;
            foreach (var candidate in SocialHandler.OnlineWhere(c => c != caster &&
                c.playerData.getDistrictId() == caster.playerData.getDistrictId() &&
                !c.playerData.isDead && !c.playerData.duelDeath && PvpCombat.HealthOf(c) > 0 &&
                PvpCombat.Check(caster, c) != PvpRules.Verdict.Allowed))
            {
                double tx = 0, ty = 0, tz = 0;
                NumericalUtils.LtVector3dToDoubles(candidate.playerInstance.Position.getValue(), ref tx, ref ty, ref tz);
                if (AbilityTargets.WithinArea(x, y, z, tx, ty, tz, radius))
                    recipients.Add(candidate);
            }
            return recipients;
        }

        public static void Landed(WorldClient caster)
        {
            var parent = AbilityCatalog.Find(Id);
            var init = parent?.Modifiers.SingleOrDefault(m => m.Number("WhenEnabled", -1) == 1 &&
                m.Text("StatModInitFunc") == "ApplyAuraEffect" && m.Number("MinValue") == EffectId);
            if (init == null) return;
            var effect = AbilityCatalog.Find(EffectId);
            uint fx = (uint)(effect?.Number("Ability/ExecutionFX") ?? 0);
            var recipients = EffectRecipients(caster);
            foreach (var target in recipients)
            {
                if (!AbilityBuffBuffer.CanApply(target, EffectId)) continue;
                if (AbilityBuffs.Apply(target, EffectId, caster))
                {
                    if (fx != 0) ServerPackets.SendPlayerAbilityEffect(target, fx);
                    target.FlushQueue();
                }
            }
            Output.WriteDebugLog("[DISTRACTING-BLOW] 1128 applied 1129 (+50 stat 1005 for 12 s) to " + recipients.Count + " friendly target(s)");
        }
    }

    internal sealed class DistractingBlowHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { DistractingBlow.Id, DistractingBlow.EffectId };
        public bool RetainsStealthDuringCast(ushort abilityId) => DistractingBlow.RetainsStealthDuringCast(abilityId);
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            if (abilityId == DistractingBlow.EffectId)
            {
                refusalReason = AbilityRefusals.NotLoaded;
                return true;
            }
            return DistractingBlow.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        }
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            DistractingBlow.TryResolveDamage(source, characterLevel, out damage);
        public void OnLanded(WorldClient caster, ushort abilityId, ulong targetEntity)
        {
            if (abilityId != DistractingBlow.Id) return;
            StealthShieldAbilities.PlayThrow(caster, targetEntity, abilityId);
            DistractingBlow.Landed(caster);
        }
    }
}
