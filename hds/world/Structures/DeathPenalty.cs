using System;
using hds.shared;

namespace hds
{
    /// <summary>Death Effect: stringtable_client.sta c40000c7 says 15% ability-level loss fading over five minutes.
    /// PlayerCharacter's GOB default is 1.0. A duel retains an existing penalty instead of creating another one.
    /// DeathEffectUpdate adds 3 to the modifier each tick; six-second updates recover 150 points in 300 s.</summary>
    public static class DeathPenalty
    {
        public const float Loss = .15f;
        public const double RecoverySeconds = 300, TickSeconds = 6;
        public const ushort Ability = 202;
        public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        public static float Recover(float factor, double elapsed) => (float)Math.Clamp(factor + Math.Max(0, elapsed) * Loss / RecoverySeconds, 0, 1);
        public static float AfterDeath(float factor) => Math.Max(0, factor - Loss);
        public static float Current(WorldClient client) => Recover(client.playerData.deathPenalty, Now - client.playerData.deathPenaltyAt);
        public static int EffectiveLevel(WorldClient client, int level) => level <= 0 ? 0 : Math.Max(1, (int)(level * Current(client)));

        public static void OnEnter(WorldClient client)
        {
            if (!client.playerData.deathPenaltyLoaded)
            {
                var saved = client.IsBot || Store.dbManager == null ? (Factor: 1f, At: Now) :
                    Store.dbManager.WorldDbHandler.GetDeathPenalty(client.playerData.getCharID());
                client.playerData.deathPenalty = Recover(saved.Factor, Now - saved.At);
                client.playerData.deathPenaltyAt = Now;
                client.playerData.deathPenaltyLoaded = true;
            }
            client.playerInstance.DeathPenalty.setValue(Current(client));
        }

        public static void Apply(WorldClient client)
        {
            OnEnter(client);
            client.playerData.deathPenalty = AfterDeath(Current(client));
            client.playerData.deathPenaltyAt = Now;
            if (!client.IsBot) Store.dbManager?.WorldDbHandler?.SaveDeathPenalty(client.playerData.getCharID(),
                client.playerData.deathPenalty, client.playerData.deathPenaltyAt);
            Send(client);
        }

        public static void Send(WorldClient client)
        {
            float factor = Current(client);
            client.playerInstance.DeathPenalty.setValue(factor);
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfDeathPenalty,
                PlayerViewAttributes.F32(factor))), false);
            client.playerData.deathPenaltySentAt = Now;
            if (client.playerData.deathPenaltyRecord != 0)
                client.messageQueue.addRpcMessage(AbilityBuffs.BuildModifier(AbilityBuffs.KindStat,
                    client.playerData.deathPenaltyRecord, Ability, Ability, 0, true, 0));
            if (factor < 1)
            {
                if (client.playerData.deathPenaltyRecord == 0) client.playerData.deathPenaltyRecord = AbilityBuffs.NextRecordId(client.playerData);
                client.messageQueue.addRpcMessage(AbilityBuffs.BuildModifier(AbilityBuffs.KindStat,
                    client.playerData.deathPenaltyRecord, Ability, Ability, (int)Math.Round((factor - 1) * 1000), false,
                    (float)((1 - factor) * RecoverySeconds / Loss)));
            }
            else client.playerData.deathPenaltyRecord = 0;
            client.FlushQueue();
        }

        public static void Tick(WorldClient client)
        {
            if (client.playerData.deathPenaltyLoaded && client.Alive && client.playerData.getOnWorld() &&
                client.playerData.deathPenalty < 1 && Now - client.playerData.deathPenaltySentAt >= TickSeconds)
            {
                Send(client);
                if (Current(client) >= 1) { client.playerData.deathPenalty = 1; client.playerData.deathPenaltyAt = Now; }
            }
        }
    }
}
