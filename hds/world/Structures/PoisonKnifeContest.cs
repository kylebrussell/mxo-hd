using System;
using hds.shared;

namespace hds
{
    internal static class PoisonKnifeContest
    {
        internal const string FixtureMode = "MXO_POISON_KNIFE_QA";
        internal const uint HitLine = 0x7a0002d6, DeflectLine = 0x7a000218;

        internal static bool Applies(ushort ability, AbilityCastOrigin origin) =>
            ability == CombatSpecials.PoisonKnife && origin == AbilityCastOrigin.NormalFreeCast;

        internal static bool Player(WorldClient caster, WorldClient recipient) =>
            Resolve(caster, recipient, TimedEffects.PlayerName(recipient), AbilityStats.Player(recipient, 1010),
                AbilityStats.Player(recipient, 637), (int)recipient.playerData.combatTactic);

        internal static bool Npc(WorldClient caster, Mob recipient) =>
            Resolve(caster, null, recipient.getName(), AbilityStats.Npc(recipient, 1010),
                AbilityStats.Npc(recipient, 637), null);

        internal static byte[] Message(string caster, string recipient, FreeCastViralContest.Result result) =>
            AbilityMessages.AboutAbility(result.Hit ? HitLine : DeflectLine, true, CombatSpecials.PoisonKnife,
                caster, recipient, result.Attack, 0, result.Defense, 0);

        private static bool Resolve(WorldClient caster, WorldClient recipient, string targetName, int defense, int consistency, int? tactic)
        {
            string casterName = TimedEffects.PlayerName(caster);
            bool? scriptedHit = FixtureResult(Environment.GetEnvironmentVariable(FixtureMode), Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName, caster.playerData.getCharID(), casterName,
                caster.playerData.getOnWorld(), caster.playerData.isDead, recipient?.playerData.getCharID() ?? 0, targetName);
            int attackDraw = scriptedHit.HasValue ? (scriptedHit.Value ? 27566 : 11183) : Random.Shared.Next(32768);
            int defenseDraw = scriptedHit.HasValue ? (scriptedHit.Value ? 11183 : 27566) : Random.Shared.Next(32768);
            var result = FreeCastViralContest.Evaluate(AbilityStats.Player(caster, 524), defense,
                AbilityStats.Player(caster, 637), consistency, (int)caster.playerData.combatTactic, tactic, attackDraw, defenseDraw);
            // The captured result IDs come from the recipient channel. Use a private
            // caster notice until its separate source string IDs are identified.
            string notice = result.Hit ? $"Poison Knife hit {targetName}." : $"{targetName} deflected Poison Knife.";
            new ServerPackets().sendSystemChatMessage(caster, $"{notice} ({result.Attack} vs {result.Defense})", "BROADCAST");
            caster.FlushQueue();
            if (recipient != null)
            {
                recipient.messageQueue.addRpcMessage(Message(casterName, targetName, result));
                recipient.FlushQueue();
            }
            Output.WriteDebugLog($"[POISON KNIFE CONTEST] {casterName} -> {targetName} " +
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
