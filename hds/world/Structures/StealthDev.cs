using System;

namespace hds
{
    /// <summary>
    /// MXO_DEV_COMMANDS=1 only. <c>?stealth &lt;state&gt;</c> puts a stealth state on the caller (own view);
    /// <c>?stealthbot &lt;state&gt; [pvp]</c> summons a DuelBot next to the caller and puts it on the bot, so one real
    /// client sees a stealthed player from outside: as a friend (shimmer) or, with pvp, as an enemy (gone).
    /// </summary>
    public static class StealthDev
    {
        public static UInt16? AbilityOf(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "sneak": return Stealth.SneakGrantAbility;
                case "improved": return Stealth.ImprovedSneakAbility;
                case "evasion": return Stealth.EscapeAndEvasionAbility;
                case "conceal": return Stealth.ConcealmentGrantAbility;
                case "invis": return Stealth.InvisibilityGrantAbility;
                case "imparted": return Stealth.InvisibilityImpartedAbility;
                case "shadow": return Stealth.MasterShadowAbility;
                default: return null;
            }
        }

        public const string Usage = "<sneak|improved|evasion|conceal|invis|imparted|shadow|off>";

        public static string Run(WorldClient caller, bool bot, string[] args)
        {
            string what = args.Length > 0 ? args[0] : "";
            if (what.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                if (bot) return DuelBot.Dismiss(caller) ? "StealthBot left." : "You have no bot.";
                return Stealth.End(caller, "dev command") ? "Stealth off." : "You are not stealthed.";
            }
            UInt16? ability = AbilityOf(what);
            if (!ability.HasValue) return (bot ? "?stealthbot " + Usage + " [pvp]" : "?stealth " + Usage);
            WorldClient subject = caller;
            if (bot)
            {
                bool pvp = args.Length > 1 && args[1].Equals("pvp", StringComparison.OrdinalIgnoreCase);
                subject = DuelBot.Summon(caller, pvp ? DuelBot.Mode.Pvp : DuelBot.Mode.Wait);
                // Let the visibility pass spawn the bot first, so the caller sees it go stealthed (or vanish)
                DelayedActions.After(3, () => Stealth.Begin(subject, Stealth.Defs[ability.Value], 50));
                return "DuelBot goes " + what + (pvp ? " as your enemy" : "") + " in 3 s.";
            }
            Stealth.Begin(subject, Stealth.Defs[ability.Value], Stealth.AbilityLevel(subject, ability.Value));
            return "Stealth " + what + " on.";
        }
    }
}
