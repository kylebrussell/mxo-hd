using System;
using hds.shared;

namespace hds
{
    public static class StarterInterlockControlQa
    {
        public const string Mode = "MXO_STARTER_INTERLOCK_CONTROL_QA";

        // Force only the state modifier branch. Interlock admission, hit contest, damage
        // and subsequent health-change and expiry callbacks still use the normal paths.
        internal static bool ModifierProc(WorldClient caster, WorldClient recipient, ushort ability) =>
            (ability == 145 || ability == 176) &&
            caster != null && recipient.playerData.getCharID() == 6114 && caster.playerData.getCharID() == 110 &&
            Environment.GetEnvironmentVariable(Mode) == "1" &&
            QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);
    }
}
