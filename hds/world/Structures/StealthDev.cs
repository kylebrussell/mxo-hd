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

        /// <summary>The bot's ability level: low, so a player facing it within 21 m detects it on a 5 s pass about 40% of the time.</summary>
        public const int BotLevel = 5;

        /// <summary>Why <paramref name="observer"/> could not attack <paramref name="bot"/>, or null when they could.</summary>
        public static string EnemyProblem(WorldClient observer, WorldClient bot)
        {
            PvpRules.Verdict verdict = PvpCombat.Check(observer, bot);
            if (verdict == PvpRules.Verdict.Allowed) return null;
            var self = PvpCombat.SubjectOf(observer);
            var target = PvpCombat.SubjectOf(bot);
            if (self.Organization == 0)
                return "you have no organisation (0); set one with ?org 1, 2 or 3 and summon it again.";
            if (self.DuelId != 0) return "you are in a duel.";
            return verdict + " (mode " + PvpFlag.ModeFor(observer) + ", organisations " + self.Organization + "/" + target.Organization +
                ", bot flagged " + target.PvpFlag + ").";
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
                DelayedActions.After(3, () => Stealth.Begin(subject, Stealth.Defs[ability.Value], BotLevel));
                if (!pvp) return "DuelBot goes " + what + " in 3 s.";
                // Only an observer who could attack the bot loses its view. A flagged bot of another organisation is
                // not enough when the caller has no organisation of their own (PvpRules.IsOpponent), so say so.
                string why = EnemyProblem(caller, subject);
                Output.WriteDebugLog("[STEALTH] ?stealthbot pvp for " + TimedEffects.PlayerName(caller) + ": " + (why ?? "the bot is their enemy"));
                return why == null
                    ? "DuelBot goes " + what + " in 3 s as your enemy: it should disappear for you."
                    : "DuelBot goes " + what + " in 3 s but is NOT your enemy, so you keep seeing it: " + why;
            }
            Stealth.Begin(subject, Stealth.Defs[ability.Value], Stealth.AbilityLevel(subject, ability.Value));
            return "Stealth " + what + " on.";
        }
    }
}
