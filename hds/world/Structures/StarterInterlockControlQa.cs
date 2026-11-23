using System;
using hds.shared;

namespace hds
{
    public static class StarterInterlockControlQa
    {
        public const string Mode = "MXO_STARTER_INTERLOCK_CONTROL_QA";
        public const string PistolAerialMode = "MXO_PISTOL_AERIAL_QA";
        public const string ConcealedShotMode = "MXO_CONCEALED_SHOT_STUN_QA";
        public const int AerialImmunitySeconds = 40;

        // Force only the state modifier branch. Interlock admission, hit contest, damage
        // and subsequent health-change and expiry callbacks still use the normal paths.
        internal static bool ModifierProc(WorldClient caster, WorldClient recipient, ushort ability) =>
            recipient.playerData.getCharID() == 6114 && (ability switch
            {
                145 => Allowed(caster, Mode, 110),
                176 => Allowed(caster, Mode, 110),
                AbilityConditionalControls.PistolAerial => Allowed(caster, PistolAerialMode, 110),
                CombatSpecials.ConcealedShot => Allowed(caster, ConcealedShotMode, 110),
                _ => false
            });

        internal static string HoldPistolAerialImmunity(WorldClient recipient)
        {
            if (!Allowed(recipient, PistolAerialMode, 6114))
                return "Pistol Aerial immunity is only available to the disposable QAEnemy fixture.";
            var subject = TimedEffects.ForPlayer(recipient);
            double now = TimeUtils.GetSimTimeSeconds();
            if (subject.Effects.HasControlImmunity(now))
                return "The current control immunity must expire before another fixture hold.";
            TimedEffects.Apply(subject, new TempMods.Effect
            {
                Ability = TempMods.ControlImmunityTimerAbility,
                Mods = new[] { new TempMods.Mod { Kind = TempMods.KindPlain, Proc = TempMods.Proc.State,
                    Target = TempMods.ISDrainTargetAbility, Seconds = AerialImmunitySeconds } }
            }, now);
            return $"QAEnemy control immunity held for {AerialImmunitySeconds} seconds.";
        }

        private static bool Allowed(WorldClient client, string mode, uint character) =>
            client != null && client.playerData.getCharID() == character &&
            Environment.GetEnvironmentVariable(mode) == "1" &&
            QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                character, client.playerData.getOnWorld(), client.playerData.isDead);
    }
}
