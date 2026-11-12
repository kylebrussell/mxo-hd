using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>tempmods.ApplyStunEffectIfDazed (2368) -> ApplyStunWithTimer (1659).
    /// The fixed value is passed directly to addStateMod/addTempMod by the script.</summary>
    public static class AbilityConditionalControls
    {
        public static bool Handles(ushort ability) => AbilityCatalog.Find(ability)?.Modifiers.Any(m =>
            m.Text("StatModTermFunc") == "ApplyStunEffectIfDazed") == true;

        internal static void Term(TimedEffects.Subject subject, TimedEffects.Active marker, double now)
        {
            if (marker.Proc != TempMods.Proc.Terminal || marker.Target != marker.Parent ||
                !Handles(marker.Parent) || !subject.Alive || marker.Value <= 0) return;
            uint dazed = CombatInterlock.ConditionBit(CombatTactic.Speed);
            if (!subject.Effects.HasAt(TempMods.DazeAbility, now) && (subject.ConditionFlags & dazed) == 0) return;
            if (subject.Effects.Snapshot().Any(m => m.Parent == TempMods.ControlImmunityTimerAbility &&
                m.Target == TempMods.ISDrainTargetAbility && now < m.Expires)) return;

            var stun = new TimedEffects.Active { Kind = TempMods.KindState, Target = TempMods.StunAbility,
                Seconds = marker.Value, Proc = TempMods.Proc.State, Caster = marker.Caster,
                CasterName = marker.CasterName, TargetView = marker.TargetView };
            var immunity = new TimedEffects.Active { Kind = TempMods.KindPlain, Target = TempMods.ISDrainTargetAbility,
                Seconds = marker.Value, Proc = TempMods.Proc.State, Caster = marker.Caster,
                CasterName = marker.CasterName, TargetView = marker.TargetView };
            // addStateMod appends the stun. It must not replace this throw's root/debuff siblings.
            var added = new List<TimedEffects.Active> { stun };
            var replaced = subject.Effects.Apply(marker.Parent, added, now, preserveOtherTargets: true);
            // An expired timer may still be in the holder until the next server sweep.
            // Its old record must go off before the new timer is presented.
            replaced.AddRange(subject.Effects.Apply(TempMods.ControlImmunityTimerAbility, new[] { immunity }, now));
            TimedEffects.Track(subject);
            subject.Applied(new TempMods.Effect { Ability = marker.Parent }, added, immunity, replaced);
        }
    }
}
