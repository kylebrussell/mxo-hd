using System;
using System.Linq;
using System.Threading;

namespace hds
{
    /// <summary>Daemon Boost projects the shipped five-second child onto friendly simulacra.</summary>
    internal static class DaemonBoost
    {
        internal const ushort Ability = 609, Effect = 699, AccuracyAbility = 619, AccuracyEffect = 700;

        internal sealed class PulseEffect
        {
            internal readonly WorldClient Source;
            internal readonly ushort EffectId;
            internal readonly int SourceWorld, SourceLife, RecipientWorld, RecipientLife;
            internal readonly (int ViralDamage, int Healing, int MeleeAccuracy, int BallisticAccuracy, int ViralAccuracy) Values;
            internal readonly double Until;

            internal PulseEffect(WorldClient source, WorldClient recipient, ushort effect,
                (int ViralDamage, int Healing, int MeleeAccuracy, int BallisticAccuracy, int ViralAccuracy) values, double until)
            {
                Source = source;
                EffectId = effect;
                SourceWorld = source.playerData.worldGeneration;
                SourceLife = source.playerData.lifeGeneration;
                RecipientWorld = recipient.playerData.worldGeneration;
                RecipientLife = recipient.playerData.lifeGeneration;
                Values = values;
                Until = until;
            }
        }

        internal static bool Handles(ushort ability) => ability == Ability || ability == AccuracyAbility;

        internal static bool Loaded(WorldClient source, ushort ability) => AbilityPassives.IsLoaded(source, ability);

        internal static void LoadoutChanged(WorldClient source)
        {
            if (!Loaded(source, Ability)) AbilityAuras.Stop(source, Ability);
            if (!Loaded(source, AccuracyAbility)) AbilityAuras.Stop(source, AccuracyAbility);
        }

        internal static PulseEffect Project(WorldClient source, CoderPets.Pet pet, double now, ushort ability = Ability)
        {
            ushort effect = AbilityAuras.EffectOf(ability);
            var template = AbilityCatalog.Find(effect);
            if (!Loaded(source, ability) || !CoderPets.FriendlyAuraRecipient(source, pet, template.Number("Ability/RadiusOfEffect"))) return null;
            int level = source.playerInstance.Level.getValue()[0];
            (int ViralDamage, int Healing, int MeleeAccuracy, int BallisticAccuracy, int ViralAccuracy) values = default;
            double seconds = 0;
            foreach (var modifier in template.Modifiers)
            {
                if (!AbilityValues.TryValue(modifier, level, level, out int value)) continue;
                switch (modifier.Target)
                {
                    case 172: values.ViralDamage = value; break;
                    case 174: values.Healing = value; break;
                    case 522: values.MeleeAccuracy = value; break;
                    case 524: values.BallisticAccuracy = value; break;
                    case 342: values.ViralAccuracy = value; break;
                }
                seconds = Math.Max(seconds, modifier.Number("Duration"));
            }
            var pulse = new PulseEffect(source, pet.Owner, effect, values, now + seconds);
            // A later application refreshes its own child without replacing the other tier.
            Volatile.Write(ref Current(pet, effect), pulse);
            return pulse;
        }

        private static ref PulseEffect Current(CoderPets.Pet pet, ushort effect)
        {
            if (effect == AccuracyEffect) return ref pet.DaemonBoostAccuracyEffect;
            return ref pet.DaemonBoostEffect;
        }

        internal static void Publish(CoderPets.Pet pet, PulseEffect pulse)
        {
            if (!ReferenceEquals(Volatile.Read(ref Current(pet, pulse.EffectId)), pulse) || pet.Gone || pet.Mob.getIsDead()) return;
            uint activationFx = (uint)AbilityCatalog.Find(pulse.EffectId).Modifiers
                .Select(modifier => modifier.Number("ActivationFX")).FirstOrDefault(fx => fx != 0);
            CoderPets.ShowAuraEffect(pet, activationFx);
            var values = pulse.Values;
            string stats = pulse.EffectId == AccuracyEffect
                ? $"melee={values.MeleeAccuracy} ballistic={values.BallisticAccuracy} viral={values.ViralAccuracy}"
                : $"viral={values.ViralDamage} healing={values.Healing}";
            Output.WriteDebugLog($"[DAEMON BOOST] {pulse.EffectId} source={pulse.Source.playerData.getCharID()} recipient={pet.Owner.playerData.getCharID()} pet={pet.SimulacraId} {stats}");
        }

        private static bool Live(CoderPets.Pet pet, PulseEffect pulse, double now) =>
            pulse != null && now < pulse.Until && !pet.Gone && !pet.Mob.getIsDead() &&
            CoderPets.AuraOwnerReady(pet.Owner) && CoderPets.AuraOwnerReady(pulse.Source) &&
            pulse.SourceWorld == pulse.Source.playerData.worldGeneration && pulse.SourceLife == pulse.Source.playerData.lifeGeneration &&
            pulse.RecipientWorld == pet.Owner.playerData.worldGeneration && pulse.RecipientLife == pet.Owner.playerData.lifeGeneration;

        internal static (int Viral, int Healing) Values(CoderPets.Pet pet, double now)
        {
            var pulse = Volatile.Read(ref Current(pet, Effect));
            return Live(pet, pulse, now) ? (pulse.Values.ViralDamage, pulse.Values.Healing) : (0, 0);
        }

        internal static int Accuracy(CoderPets.Pet pet, double now, bool ranged)
        {
            var pulse = Volatile.Read(ref Current(pet, AccuracyEffect));
            if (!Live(pet, pulse, now)) return 0;
            if (pet.Kind == CoderPets.Kind.Nuker) return pulse.Values.ViralAccuracy;
            return pet.Kind == CoderPets.Kind.Proxy ? (ranged ? pulse.Values.BallisticAccuracy : pulse.Values.MeleeAccuracy) : 0;
        }
    }
}
