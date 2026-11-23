using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>tempmods.ApplyStunEffectIfDazed (2368) and reviewed combat-special calls to ApplyStunWithTimer (1659).
    /// The fixed value is passed directly to addStateMod/addTempMod by the script.</summary>
    public static class AbilityConditionalControls
    {
        // Proxy_003 login, 20131-20178 and 20689-20694, records the Sniper Shot victim's linked Stun stack.
        internal const ushort PistolAerial = 243;
        internal const ushort DevastationField = 468;
        internal const ushort CodeShock = 482;

        // This authored callback has no target attribute or state. Only its resulting 734 record is published.
        internal static bool IsCodeShockTimer(TimedEffects.Active modifier) => modifier.Parent == CodeShock &&
            modifier.Proc == TempMods.Proc.Terminal && modifier.Target == 0;

        internal static bool HasCapturedStunStack(ushort ability) =>
            ability == PistolAerial || ability == CombatSpecials.SniperShot || ability == CombatSpecials.DeadlyShot;

        internal static bool IsCapturedStunMarker(TimedEffects.Active modifier) =>
            HasCapturedStunStack(modifier.Parent) && modifier.Proc == TempMods.Proc.Terminal;

        internal static bool UsesStunCallbackPresentation(ushort source) =>
            HasCapturedStunStack(source) || source == DevastationField || source == CombatSpecials.ConcealedShot;

        private static bool UsesStunCombatTactics(ushort source) =>
            UsesStunCallbackPresentation(source) || source == CombatSpecials.ThreeRoundBurst;

        internal static bool IsLinkedStunTactics(TimedEffects.Active mod) =>
            UsesStunCombatTactics(mod.Parent) && mod.Proc == TempMods.Proc.Stat && mod.Target == TempMods.CombatTacticsAbility;
        // tempmods2362: callback is initialization-only and has no explicit immunity timer.
        public static bool HandlesInitial(ushort ability) => ability == 340 && AbilityCatalog.Find(ability)?.Modifiers.Any(
            m => m.Text("StatModInitFunc") == "ApplyPowerlessIfStaggered") == true;
        internal static bool InitialPowerless(ushort ability, ushort target) => ability == 340 && target == TempMods.PowerlessAbility;
        internal static void Init(TimedEffects.Subject subject, TempMods.Effect effect, List<TimedEffects.Active> mods, double now)
        {
            if (!HandlesInitial(effect.Ability) || !subject.Alive ||
                (!subject.Effects.HasAt(TempMods.StaggeredAbility,now) &&
                 (subject.ConditionFlags & CombatInterlock.ConditionBit(CombatTactic.Power)) == 0)) return;
            var source = AbilityCatalog.Find(effect.Ability)?.Modifiers.SingleOrDefault(m =>
                m.Text("StatModInitFunc") == "ApplyPowerlessIfStaggered" && m.Number("WhenEnabled",-1) == 2 &&
                m.Number("DurationType",-1) == 1 && m.Number("MinValue") == m.Number("MaxValue") &&
                !m.Has("ValueScale") && m.Number("ValueFunction") == 0);
            if (source == null || source.Number("MinValue") <= 0) return;
            mods.Add(new TimedEffects.Active { Kind=TempMods.KindState, Target=TempMods.PowerlessAbility,
                Proc=TempMods.Proc.State, Seconds=(float)source.Number("MinValue") });
        }

        public static bool Handles(ushort ability) => AbilityCatalog.Find(ability)?.Modifiers.Any(m =>
            m.Text("StatModTermFunc") == "ApplyStunEffectIfDazed") == true;

        internal static bool TerminalStun(ushort ability, AbilityCatalog.Modifier modifier) =>
            modifier.Text("StatModTermFunc") == "ApplyStunEffectIfDazed" ||
            ((ability == PistolAerial || ability == 253 || ability == 256 || ability == 265 || ability == 429 || ability == DevastationField || ability == CombatSpecials.DeadlyShot || ability == 504 || ability == 505 || ability == 532 || ability == 534) &&
                modifier.Text("StatModTermFunc") == "ApplyStunWithTimer");

        internal static void Term(TimedEffects.Subject subject, TimedEffects.Active marker, double now)
        {
            if (IsCodeShockTimer(marker))
            {
                var timer = new TimedEffects.Active { Kind = TempMods.KindPlain, Target = TempMods.ISDrainTargetAbility,
                    Seconds = marker.Value, Proc = TempMods.Proc.State, Caster = marker.Caster,
                    CasterName = marker.CasterName, TargetView = marker.TargetView };
                if (!subject.Effects.AppendCallback(TempMods.ControlImmunityTimerAbility, timer, now,
                    () => subject.Alive && !subject.Effects.HasControlImmunity(now, timer), out var retired)) return;
                TimedEffects.Track(subject);
                subject.Applied(new TempMods.Effect { Ability = CodeShock }, new List<TimedEffects.Active>(), timer, retired);
                return;
            }
            if (marker.Proc != TempMods.Proc.Terminal || marker.Target != marker.Parent ||
                !subject.Alive || marker.Value <= 0) return;
            var callback = AbilityCatalog.Find(marker.Parent)?.Modifiers.FirstOrDefault(m => TerminalStun(marker.Parent, m));
            if (callback == null) return;
            if (callback.Text("StatModTermFunc") == "ApplyStunEffectIfDazed" &&
                !subject.Effects.HasAt(TempMods.DazeAbility, now) &&
                (subject.ConditionFlags & CombatInterlock.ConditionBit(CombatTactic.Speed)) == 0) return;
            if (subject.Effects.HasControlImmunity(now))
            {
                if ((marker.Parent == PistolAerial || marker.Parent == DevastationField) && subject is PlayerEffects player)
                {
                    string targetName = TimedEffects.PlayerName(player.Context);
                    string abilityName = marker.Parent == PistolAerial ? "Pistol Aerial" : "Devastation Field";
                    Output.WriteDebugLog($"[EFFECTS] {marker.Parent} on {targetName}: Stun suppressed by active control immunity");
                    // ApplyStunWithTimer reports lower-buff stacking to the caster. Its localized ID is unresolved.
                    if (marker.Caster is WorldClient source && source != player.Context && source.Alive &&
                        source.playerData.getOnWorld() && !source.playerData.worldChangePending)
                        new ServerPackets().sendSystemChatMessage(source, $"{targetName} is immune to {abilityName}'s Stun.", "BROADCAST");
                }
                return;
            }

            var stun = new TimedEffects.Active { Kind = TempMods.KindState, Target = TempMods.StunAbility,
                Seconds = marker.Value, Proc = TempMods.Proc.State, Caster = marker.Caster,
                CasterName = marker.CasterName, TargetView = marker.TargetView };
            var immunity = new TimedEffects.Active { Kind = TempMods.KindPlain, Target = TempMods.ISDrainTargetAbility,
                Seconds = marker.Value, Proc = TempMods.Proc.State, Caster = marker.Caster,
                CasterName = marker.CasterName, TargetView = marker.TargetView };
            // addStateMod appends the stun. It must not replace this throw's root/debuff siblings.
            var added = new List<TimedEffects.Active> { stun };
            if (UsesStunCombatTactics(marker.Parent))
            {
                // ApplyStunWithTimer adds StunAbility, whose CombatTactics modifier lasts with the state.
                var tactics = AbilityCatalog.Find(TempMods.StunAbility).Modifiers.Single(m =>
                    m.Component == "CombatTacticsMod" && m.Target == TempMods.CombatTacticsAbility &&
                    m.Number("WhenEnabled") == 2 && m.Number("DurationType") == 2);
                if (AbilityValues.TryValue(tactics, 0, 0, out int penalty))
                {
                    added.Add(new TimedEffects.Active { Kind = TempMods.KindStat, Target = tactics.Target,
                        Value = penalty, Seconds = stun.Seconds, Proc = TempMods.Proc.Stat, Caster = marker.Caster,
                        CasterName = marker.CasterName, TargetView = marker.TargetView });
                }
            }
            var replaced = subject.Effects.Apply(marker.Parent, added, now, preserveOtherTargets: true);
            // An expired timer may still be in the holder until the next server sweep.
            // Its old record must go off before the new timer is presented.
            replaced.AddRange(subject.Effects.Apply(TempMods.ControlImmunityTimerAbility, new[] { immunity }, now));
            TimedEffects.Track(subject);
            subject.Applied(new TempMods.Effect { Ability = marker.Parent }, added, immunity, replaced);
        }
    }
}
