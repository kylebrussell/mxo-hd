using System;
using System.Linq;

namespace hds
{
    /// <summary>Power Shot's two independent instant modifiers from GOB 14.</summary>
    internal static class AwakenedPowerShot
    {
        internal const ushort Id = 14;
        private const ushort Health = 108, InnerStrength = 111;
        private static readonly Random restoreRandom = new Random();

        private static readonly Lazy<AbilityCatalog.Modifier> Damage = new Lazy<AbilityCatalog.Modifier>(
            () => FindModifier(Health, 2));
        private static readonly Lazy<AbilityCatalog.Modifier> Restore = new Lazy<AbilityCatalog.Modifier>(
            () => FindModifier(InnerStrength, 1));

        private static AbilityCatalog.Modifier FindModifier(ushort stat, int target) =>
            AbilityCatalog.Find(Id).Modifiers.Single(m => m.Target == stat &&
                m.Number("WhenEnabled", -1) == target && m.Number("DurationType", -1) == 1 &&
                m.Number("ValueFunction", -1) == 4);

        internal static ushort RollDamage(WorldClient caster, Random random) =>
            (ushort)Sample(Damage.Value, caster.playerInstance.Level.getValue()[0], random);

        internal static int Sample(AbilityCatalog.Modifier modifier, int level, Random random)
        {
            var entry = AbilityCatalog.Find(Id);
            int min = (short)modifier.Number("MinValue"), max = (short)modifier.Number("MaxValue");
            int minLevel = (int)entry.Number("Ability/MinValueScaleLevel");
            int maxLevel = (int)entry.Number("Ability/MaxValueScaleLevel");
            if (minLevel == 0 || maxLevel == 0 || minLevel == maxLevel) return min;

            // 625180c5: keep the stored binary32 skew and intermediate floats until the mean is truncated.
            float midpoint = (min + max) / 2f;
            float topLevelCenter = (float)((max - (double)midpoint) * 0.8f + midpoint);
            float progress = Math.Clamp((float)(1.0 - (double)(maxLevel - level) / (maxLevel - minLevel)), 0, 1);
            int center = (int)((topLevelCenter - (double)midpoint) * progress + midpoint);
            int roll = CombatCalculator.GaussianClamped(random, center - min, (max - min) / 6, 0, max - min);
            return (int)((min + roll) * (double)(float)modifier.Number("ValueScale", 1));
        }

        internal static void Landed(WorldClient caster)
        {
            int amount;
            lock (restoreRandom)
                amount = Sample(Restore.Value, caster.playerInstance.Level.getValue()[0], restoreRandom);
            ushort restored = RestoreInnerStrength(caster, amount);
            if (restored == 0) return;
            string name = TimedEffects.PlayerName(caster);
            caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(
                (uint)Restore.Value.Number("ActivationStringOnSelf"), true, Id, name, name, restored));
            new ServerPackets().sendISCurrent(caster, AbilityHandler.CurrentInnerStrength(caster));
            ServerPackets.SendPlayerAbilityEffect(caster, (uint)Restore.Value.Number("ActivationFX"));
        }

        internal static ushort RestoreInnerStrength(WorldClient caster, int amount)
        {
            lock (caster.playerData.lifeSync)
            {
                if (caster.playerData.isDead || caster.playerData.duelDeath) return 0;
                ushort before = AbilityHandler.CurrentInnerStrength(caster);
                ushort after = (ushort)Math.Min(AbilityHandler.MaxInnerStrength(caster), before + amount);
                if (after <= before) return 0;
                caster.playerInstance.InnerStrengthAvailable.setValue(after);
                return (ushort)(after - before);
            }
        }
    }
}
