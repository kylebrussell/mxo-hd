using hds.shared;

namespace hds
{
    /// <summary>Detection_Test and Detection_DirectObject in ability/general.py.</summary>
    public static class ConcealmentDetection
    {
        public const ushort Ability = 298, Detection = 638;
        private const ushort Invisibility = 175, Disguise = 120, DisguiseDuration = 639;
        private const uint CounterEffect = 0x2800059d;

        internal static bool Detects(int roll, int detection, int duration, int ability) =>
            roll + detection > 50 + 8 * duration + 4 * ability;

        internal static void Land(WorldClient caster)
        {
            if (!caster.Alive || !caster.playerData.getOnWorld() || caster.playerData.worldChangePending ||
                caster.playerData.DistrictIsLA() || caster.playerData.isDead || caster.playerData.duelDeath ||
                AbilityPassives.Level(caster, Ability) == 0) return;

            var (x, y, z) = Position(caster);
            uint district = caster.playerData.getDistrictId();
            var radius = AbilityRadius.TargetMetres(caster, Ability);
            int detection = AbilityStats.Player(caster, Detection);
            int roll = System.Random.Shared.Next(100);

            // The stored caster-area enemy selector has to admit invisible targets independently of view creation.
            foreach (var target in SocialHandler.OnlineWhere(p => p != caster && p.playerData.getDistrictId() == district))
            {
                if (!target.Alive || !target.playerData.getOnWorld() || target.playerData.worldChangePending ||
                    target.playerData.DistrictIsLA() || PvpCombat.Check(caster, target) != PvpRules.Verdict.Allowed)
                    continue;

                var state = Stealth.Current(target);
                if (state == null) continue;
                var (tx, ty, tz) = Position(target);
                if (!AbilityTargets.WithinArea(x, y, z, tx, ty, tz, radius)) continue;

                var (duration, ability) = state.Def.Kind switch
                {
                    Stealth.Kind.Disguise => (DisguiseDuration, Disguise),
                    Stealth.Kind.Invisibility => (Stealth.InvisDurationAbility, Invisibility),
                    Stealth.Kind.Imparted => (Stealth.InvisDurationAbility, Invisibility),
                    _ => ((ushort)0, (ushort)0)
                };
                if (ability == 0) continue;
                int durationBonus = AbilityStats.Player(target, duration);
                int targetRank = AbilityPassives.Level(target, ability);
                bool revealed = Detects(roll, detection, durationBonus, targetRank);
                Output.WriteDebugLog($"[DETECTION] {TimedEffects.PlayerName(caster)} -> {TimedEffects.PlayerName(target)} " +
                    $"roll={roll} detect={detection} duration={durationBonus} rank={targetRank} revealed={revealed}");
                if (!revealed) continue;

                if (Stealth.End(target, "revealed by Detection", true, state.Generation))
                    ServerPackets.SendPlayerAbilityEffect(target, CounterEffect);
            }
        }

        private static (double X, double Y, double Z) Position(WorldClient client)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return (x, y, z);
        }
    }
}
