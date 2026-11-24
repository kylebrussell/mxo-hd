using System;
using hds.shared;

namespace hds
{
    /// <summary>generic_free_attacks.py AbilityThrowFreeAttack, called by SneakFreeDisableShield_Test.</summary>
    internal static class StealthShieldThrowContest
    {
        internal const string QaMode = "MXO_DISABLE_EVASION_QA";

        internal static FreeCastViralContest.Result Evaluate(WorldClient caster, WorldClient target, int attackDraw, int defenseDraw) =>
            FreeCastViralContest.Evaluate(AbilityStats.ThrownAccuracy(caster), AbilityStats.Player(target, 1011),
                AbilityStats.Player(caster, 637), AbilityStats.Player(target, 637), (int)caster.playerData.combatTactic,
                (int)target.playerData.combatTactic, attackDraw, defenseDraw);

        internal static bool Hit(WorldClient caster, WorldClient target)
        {
            string targetName = TimedEffects.PlayerName(target);
            string qa = Environment.GetEnvironmentVariable(QaMode);
            bool seeded = (qa == "land" || qa == "split") &&
                QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                    caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);
            bool fixtureDeflect = seeded && qa == "split" && caster.playerData.getCharID() == 110 &&
                target.playerData.getCharID() == 6113 && targetName == "QAOther";
            int a = seeded ? (fixtureDeflect ? 15000 : 31000) : Random.Shared.Next(32768);
            int d = seeded ? (fixtureDeflect ? 31000 : 15000) : Random.Shared.Next(32768);
            var outcome = Evaluate(caster, target, a, d);
            string casterName = TimedEffects.PlayerName(caster);
            uint result = outcome.Hit ? PoisonKnifeContest.HitLine : PoisonKnifeContest.DeflectLine;
            target.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(result, true, StealthShieldAbilities.DisableEvasion,
                casterName, targetName, outcome.Attack, 0, outcome.Defense, 0));
            new ServerPackets().sendSystemChatMessage(caster,
                $"Disable Evasion {(outcome.Hit ? "hit" : "was deflected by")} {targetName}. ({outcome.Attack} vs {outcome.Defense})", "BROADCAST");
            caster.FlushQueue();
            target.FlushQueue();
            Output.WriteDebugLog($"[DISABLE-EVASION CONTEST] {casterName} -> {targetName} " +
                $"inputs(1032/1011)={outcome.AttackInput}/{outcome.DefenseInput} rolls={outcome.Attack}/{outcome.Defense} hit={outcome.Hit} qa={seeded}");
            return outcome.Hit;
        }
    }
}
