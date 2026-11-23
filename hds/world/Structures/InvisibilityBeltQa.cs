using System;
using hds.shared;

namespace hds
{
    /// <summary>Applies the Blind state to the named fixture observer without starting a fight.</summary>
    public static class InvisibilityBeltQa
    {
        public const string Mode = "MXO_BELT_WATCHER_QA";

        internal static string BlindWatcher(WorldClient target)
        {
            var data = target.playerData;
            if (Environment.GetEnvironmentVariable(Mode) != "1" || data.getCharID() != 6114 ||
                !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                    6114, data.getOnWorld(), data.isDead || data.duelDeath || data.worldChangePending || data.DistrictIsLA()))
                return "Belt Blind aid requires the disposable QAEnemy fixture.";

            const ushort source = 282;
            TimedEffects.Apply(TimedEffects.ForPlayer(target), new TempMods.Effect
            {
                Ability = source,
                Mods = new[] { new TempMods.Mod { Kind = TempMods.KindState, Proc = TempMods.Proc.State,
                    Target = TempMods.BlindAbility, Seconds = 20 } }
            }, TimeUtils.GetSimTimeSeconds());
            return "QAEnemy Blinded for the Belt watcher check.";
        }
    }
}
