using System;

namespace hds
{
    /// <summary>OURS approximation: active DamageAgainstVulnerability improves damage against
    /// current Daze, Stun or OffBalance. The retail exploited-vulnerability counter is unrecovered.</summary>
    public static class PunishingBlows
    {
        internal static bool Vulnerable(TimedEffects.Subject target, double now) => target != null && target.Alive &&
            ((target.ConditionFlags & 0x06) != 0 || target.Effects.HasAt(TempMods.DazeAbility, now) ||
             target.Effects.HasAt(TempMods.StunAbility, now) || target.Effects.HasAt(TempMods.OffBalanceAbility, now));

        internal static ushort Amount(int damage, int percent, bool vulnerable) => (ushort)Math.Clamp(
            (long)Math.Max(0, damage) * (100L + (vulnerable ? Math.Max(0, percent) : 0)) / 100, 0, ushort.MaxValue);

        private static bool Current(WorldClient client) => client?.playerData != null && client.Alive &&
            client.playerData.getOnWorld() && !client.playerData.worldChangePending &&
            !client.playerData.isDead && !client.playerData.duelDeath;

        private static ushort Resolve(WorldClient source, TimedEffects.Subject target, string name, int damage)
        {
            int percent=AbilityStats.Player(source,212);
            bool vulnerable=Vulnerable(target,TimeUtils.GetSimTimeSeconds());
            ushort result=Amount(damage,percent,vulnerable);
            if(percent>0 || Environment.GetEnvironmentVariable("MXO_PUNISHING_BLOWS_QA")=="1")
                Output.WriteDebugLog($"[PUNISHING-BLOWS OURS] {TimedEffects.PlayerName(source)} -> {name} raw{damage} stat{percent} vulnerable{vulnerable} final{result}");
            return result;
        }

        public static ushort Player(WorldClient source, WorldClient target, int damage)
        {
            if (!Current(source) || !Current(target) || source == target ||
                source.playerData.getDistrictId() != target.playerData.getDistrictId()) return Amount(damage, 0, false);
            return Resolve(source,TimedEffects.ForPlayer(target),TimedEffects.PlayerName(target),damage);
        }

        public static ushort Npc(WorldClient source, Mob target, int damage)
        {
            if (!Current(source) || target == null || target.getIsDead() || target.getHealthC() == 0 ||
                source.playerData.getDistrictId() != target.getDistrict()) return Amount(damage, 0, false);
            return Resolve(source,TimedEffects.ForMob(target),target.getName(),damage);
        }
    }
}
