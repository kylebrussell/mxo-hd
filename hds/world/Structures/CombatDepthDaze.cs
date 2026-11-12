using System;
using System.Globalization;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Disposable QA preparation only; applies the existing NPCDaze884 state, never grants an ability.</summary>
    public static class CombatDepthDaze
    {
        public static bool Allowed(string dev, string database, uint character, bool world, bool dead, string name) =>
            QaLoadout.Allowed(dev, database, character, world, dead) &&
            !string.Equals(name, "TheNeo", StringComparison.OrdinalIgnoreCase);

        public static bool Parse(string[] args, out float seconds)
        {
            seconds = 30; // NPCDaze884's stored duration; explicit override is QA-only.
            return args != null && args.Length >= 1 && args.Length <= 2 && args[0] == "daze" &&
                (args.Length == 1 || float.TryParse(args[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out seconds)) &&
                float.IsFinite(seconds) && seconds >= 1 && seconds <= 60;
        }

        private static bool Ready(WorldClient client) => client != null && client.Alive &&
            client.playerData.getOnWorld() && !client.playerData.worldChangePending && !client.playerData.DistrictIsLA() &&
            !client.playerData.isDead && !client.playerData.duelDeath;

        internal static TempMods.Effect Effect(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds < 1 || seconds > 60 || !TempMods.TryGet(884, out var source)) return null;
            var daze = source.Mods.Single(m => m.Proc == TempMods.Proc.State && m.Target == TempMods.DazeAbility);
            return new TempMods.Effect { Ability = source.Ability, Mods = new[] { new TempMods.Mod {
                Kind = daze.Kind, Proc = daze.Proc, Target = daze.Target, Seconds = seconds
            } } };
        }

        internal static void Apply(TimedEffects.Subject subject, float seconds, WorldClient caller = null, ushort view = 0)
        {
            // The requested QA duration is exact. No caster scaling/target mitigation or production cast is invoked.
            // Attach caster context after instantiation so existing NPC/player presentation still uses the selected view.
            TimedEffects.Apply(subject, Effect(seconds), TimeUtils.GetSimTimeSeconds(),
                casterName: caller == null ? "QA" : TimedEffects.PlayerName(caller), targetView: view,
                configure: m => m.Caster = caller);
        }

        public static string Run(WorldClient caller, string[] args)
        {
            if (caller == null || !ReferenceEquals(caller, Store.currentClient) || !Ready(caller) ||
                !Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                    caller.playerData.getCharID(), caller.playerData.getOnWorld(), caller.playerData.isDead,
                    TimedEffects.PlayerName(caller)))
                return "Daze fixture requires MXO_DEV_COMMANDS=1, a strict mxotest_ database and a live in-world character other than 2/TheNeo.";
            if (!Parse(args, out float seconds)) return "Usage: ?combatdepth daze [1..60 seconds]; select a live visible NPC or QA player.";
            ushort view = caller.playerData.currentSelectedTargetViewId;
            if (view <= 2) return "Select a live visible NPC or QA player for the daze fixture.";
            var selected = caller.viewMan.getViewById(view);
            if (selected == null || !selected.viewCreated || selected.spawnId != caller.playerData.currentSelectedTargetSpawnId)
                return "Select a live visible NPC or QA player for the daze fixture.";
            var target = PvpCombat.PlayerInView(caller, view);
            if (target != null)
            {
                if (!Ready(target) || target.playerData.getDistrictId() != caller.playerData.getDistrictId() ||
                    !Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName, target.playerData.getCharID(),
                        target.playerData.getOnWorld(), target.playerData.isDead, TimedEffects.PlayerName(target)))
                    return "Daze fixture target must be a live visible QA player other than 2/TheNeo.";
                Apply(TimedEffects.ForPlayer(target), seconds, caller, view);
                return "Daze fixture applied884/199 to " + TimedEffects.PlayerName(target) + " for " + seconds.ToString(CultureInfo.InvariantCulture) + " seconds; immunity unchanged.";
            }
            var mob = AbilityTargets.FindMob(caller, view, out var visible);
            if (mob == null || visible == null || !visible.viewCreated || mob.getIsDead() ||
                mob.getDistrict() != caller.playerData.getDistrictId() || string.Equals(mob.getName(), "TheNeo", StringComparison.OrdinalIgnoreCase))
                return "Select a live visible NPC or QA player for the daze fixture.";
            Apply(TimedEffects.ForMob(mob), seconds, caller, view);
            return "Daze fixture applied884/199 to " + mob.getName() + " for " + seconds.ToString(CultureInfo.InvariantCulture) + " seconds; immunity unchanged.";
        }
    }
}
