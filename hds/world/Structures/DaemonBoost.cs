using System;
using System.Linq;
using System.Threading;

namespace hds
{
    /// <summary>Daemon Boost 1.0 projects the shipped five-second effect 699 onto friendly simulacra.</summary>
    internal static class DaemonBoost
    {
        internal const ushort Ability = 609, Effect = 699;
        private static AbilityCatalog.Entry Template => AbilityCatalog.Find(Effect);
        private static readonly uint ActivationFx = (uint)Template.Modifiers.Select(m => m.Number("ActivationFX"))
            .FirstOrDefault(fx => fx != 0);

        internal sealed class PulseEffect
        {
            internal readonly WorldClient Source;
            internal readonly int SourceWorld, SourceLife, RecipientWorld, RecipientLife, ViralDamage, Healing;
            internal readonly double Until;

            internal PulseEffect(WorldClient source, WorldClient recipient, int viralDamage, int healing, double until)
            {
                Source = source;
                SourceWorld = source.playerData.worldGeneration;
                SourceLife = source.playerData.lifeGeneration;
                RecipientWorld = recipient.playerData.worldGeneration;
                RecipientLife = recipient.playerData.lifeGeneration;
                ViralDamage = viralDamage;
                Healing = healing;
                Until = until;
            }
        }

        internal static bool Loaded(WorldClient source) => AbilityPassives.IsLoaded(source, Ability);

        internal static void LoadoutChanged(WorldClient source)
        {
            if (!Loaded(source)) AbilityAuras.Stop(source, Ability);
        }

        internal static PulseEffect Project(WorldClient source, CoderPets.Pet pet, double now)
        {
            if (!Loaded(source) || !CoderPets.FriendlyAuraRecipient(source, pet, Template.Number("Ability/RadiusOfEffect"))) return null;
            int level = source.playerInstance.Level.getValue()[0], viral = 0, healing = 0;
            double seconds = 0;
            foreach (var modifier in Template.Modifiers)
            {
                if (!AbilityValues.TryValue(modifier, level, level, out int value)) continue;
                if (modifier.Target == 172) viral = value;
                if (modifier.Target == 174) healing = value;
                seconds = Math.Max(seconds, modifier.Number("Duration"));
            }
            var pulse = new PulseEffect(source, pet.Owner, viral, healing, now + seconds);
            // A later application of the same child refreshes/replaces the previous record.
            Volatile.Write(ref pet.DaemonBoostEffect, pulse);
            return pulse;
        }

        internal static void Publish(CoderPets.Pet pet, PulseEffect pulse)
        {
            if (!ReferenceEquals(Volatile.Read(ref pet.DaemonBoostEffect), pulse) || pet.Gone || pet.Mob.getIsDead()) return;
            CoderPets.ShowAuraEffect(pet, ActivationFx);
            Output.WriteDebugLog($"[DAEMON BOOST] 699 source={pulse.Source.playerData.getCharID()} recipient={pet.Owner.playerData.getCharID()} pet={pet.SimulacraId} viral={pulse.ViralDamage} healing={pulse.Healing}");
        }

        internal static (int Viral, int Healing) Values(CoderPets.Pet pet, double now)
        {
            var pulse = Volatile.Read(ref pet.DaemonBoostEffect);
            if (pulse == null || now >= pulse.Until || pet.Gone || pet.Mob.getIsDead() ||
                !CoderPets.AuraOwnerReady(pet.Owner) || !CoderPets.AuraOwnerReady(pulse.Source) ||
                pulse.SourceWorld != pulse.Source.playerData.worldGeneration || pulse.SourceLife != pulse.Source.playerData.lifeGeneration ||
                pulse.RecipientWorld != pet.Owner.playerData.worldGeneration || pulse.RecipientLife != pet.Owner.playerData.lifeGeneration)
                return (0, 0);
            return (pulse.ViralDamage, pulse.Healing);
        }
    }
}
