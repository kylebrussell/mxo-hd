using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// generic_free_attacks.AbilityRangeFreeAttack for selected at-rest specials with the ranged TestFunction 1: Poison Knife
    /// (146), Hindering Shot (130) and Deadly Shot (454). Attack 524 against defense 1010 with consistency 637 and the tactic
    /// factors; a hit needs attack above defense. The retained TestFunction 1 Deadly Shot victim trace supplies the hit
    /// (0x7a0002d6) and deflection (0x7a000218) recipient lines (docs/COMBAT-STYLES.md).
    /// </summary>
    internal static class PoisonKnifeContest
    {
        internal const string FixtureMode = "MXO_POISON_KNIFE_QA", RangedFixtureMode = "MXO_RANGED_CONTEST_QA", Command = "?rangedcontest";
        internal const uint HitLine = 0x7a0002d6, DeflectLine = 0x7a000218;
        private static volatile int forced; // 0 off, 1 hit, 2 deflect (QA fixture only)

        internal static bool Applies(ushort ability, AbilityCastOrigin origin) =>
            origin == AbilityCastOrigin.NormalFreeCast && (ability == CombatSpecials.PoisonKnife ||
                ability == CombatSpecials.HinderingShot || ability == CombatSpecials.DeadlyShot);

        internal static string Name(ushort ability) => ability switch
        {
            CombatSpecials.HinderingShot => "Hindering Shot",
            CombatSpecials.DeadlyShot => "Deadly Shot",
            _ => "Poison Knife"
        };

        internal static bool Player(WorldClient caster, WorldClient recipient, ushort ability = CombatSpecials.PoisonKnife) =>
            Resolve(caster, recipient, TimedEffects.PlayerName(recipient), AbilityStats.Player(recipient, 1010),
                AbilityStats.Player(recipient, 637), (int)recipient.playerData.combatTactic, ability);

        internal static bool Npc(WorldClient caster, Mob recipient, ushort ability = CombatSpecials.PoisonKnife) =>
            Resolve(caster, null, recipient.getName(), AbilityStats.Npc(recipient, 1010),
                AbilityStats.Npc(recipient, 637), null, ability);

        internal static byte[] Message(string caster, string recipient, FreeCastViralContest.Result result, ushort ability = CombatSpecials.PoisonKnife) =>
            AbilityMessages.AboutAbility(result.Hit ? HitLine : DeflectLine, true, ability,
                caster, recipient, result.Attack, 0, result.Defense, 0);

        private static bool RangedQaAllowed(WorldClient caster) =>
            Environment.GetEnvironmentVariable(RangedFixtureMode) == "1" && caster.playerData.getCharID() == 110 &&
            QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        /// <summary>QA fixture only: force the next Hindering Shot / Deadly Shot contests to hit or deflect.</summary>
        internal static string Dev(WorldClient caller, string[] args)
        {
            if (!RangedQaAllowed(caller)) return "Ranged contest control requires the disposable QA fixture.";
            if (args.Length != 1 || (args[0] != "hit" && args[0] != "deflect" && args[0] != "off")) return "Use ?rangedcontest hit|deflect|off.";
            forced = args[0] == "hit" ? 1 : args[0] == "deflect" ? 2 : 0;
            return $"Ranged contest {args[0]}.";
        }

        /// <summary>Unit tests only: a fixed outcome for the 130/454 contest (never Poison Knife).</summary>
        internal static bool? TestOutcome;

        internal static bool? ForcedResult(WorldClient caster, ushort ability) =>
            ability == CombatSpecials.PoisonKnife ? null : TestOutcome ?? (forced != 0 && RangedQaAllowed(caster) ? (bool?)(forced == 1) : null);

        private static bool Resolve(WorldClient caster, WorldClient recipient, string targetName, int defense, int consistency, int? tactic,
            ushort ability)
        {
            string casterName = TimedEffects.PlayerName(caster);
            bool? scriptedHit = ForcedResult(caster, ability) ?? FixtureResult(Environment.GetEnvironmentVariable(FixtureMode), Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName, caster.playerData.getCharID(), casterName,
                caster.playerData.getOnWorld(), caster.playerData.isDead, recipient?.playerData.getCharID() ?? 0, targetName);
            int attackDraw = scriptedHit.HasValue ? (scriptedHit.Value ? 27566 : 11183) : Random.Shared.Next(32768);
            int defenseDraw = scriptedHit.HasValue ? (scriptedHit.Value ? 11183 : 27566) : Random.Shared.Next(32768);
            var result = FreeCastViralContest.Evaluate(AbilityStats.Player(caster, 524), defense,
                AbilityStats.Player(caster, 637), consistency, (int)caster.playerData.combatTactic, tactic, attackDraw, defenseDraw);
            // The captured result IDs come from the recipient channel. Use a private
            // caster notice until its separate source string IDs are identified.
            string name = Name(ability);
            string notice = result.Hit ? $"{name} hit {targetName}." : $"{targetName} deflected {name}.";
            new ServerPackets().sendSystemChatMessage(caster, $"{notice} ({result.Attack} vs {result.Defense})", "BROADCAST");
            caster.FlushQueue();
            if (recipient != null)
            {
                recipient.messageQueue.addRpcMessage(Message(casterName, targetName, result, ability));
                recipient.FlushQueue();
            }
            Output.WriteDebugLog($"[{(ability == CombatSpecials.PoisonKnife ? "POISON KNIFE" : "RANGED")} CONTEST] {ability} {casterName} -> {targetName} " +
                $"inputs(524/1010)={result.AttackInput}/{result.DefenseInput} rolls={result.Attack}/{result.Defense} hit={result.Hit} qa={scriptedHit.HasValue}");
            return result.Hit;
        }

        internal static bool? FixtureResult(string mode, string dev, string database, uint caster, string casterName,
            bool live, bool dead, uint recipient, string targetName)
        {
            if (mode == null || !QaLoadout.Allowed(dev, database, caster, live, dead))
                return null;
            return mode switch
            {
                "players" when caster == 110 && recipient == 6114 && targetName == "QAEnemy" => true,
                "players" when caster == 110 && recipient == 6112 && targetName == "QAFar" => false,
                "dummy" when caster == 110 && recipient == 0 && targetName == "Melee Dummy" => true,
                "combat-style-regression" when casterName == "Knifer" && recipient == 0 && targetName == "WinterFrost" => true,
                _ => null
            };
        }
    }
}
