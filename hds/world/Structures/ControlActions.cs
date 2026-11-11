using System;

namespace hds
{
    /// <summary>Action gates in tempmods.py PowerlessInit, StunMezCommonInit, DisarmedInit and EnrageInit.</summary>
    public static class ControlActions
    {
        private static bool Has(TimedEffects.Holder holder, UInt16 state) => holder.HasAt(state, TimeUtils.GetSimTimeSeconds());
        public static bool Incapacitated(TimedEffects.Holder holder) => Has(holder, TempMods.StunAbility) || Has(holder, TempMods.MezAbility);
        public static bool CanMove(TimedEffects.Holder holder) => !Incapacitated(holder) && !Has(holder, TempMods.RootedAbility);
        public static bool CanUseAbilities(TimedEffects.Holder holder) => !Incapacitated(holder) && !Has(holder, TempMods.PowerlessAbility);
        public static bool CanUseWeapons(TimedEffects.Holder holder) => !Incapacitated(holder) && !Has(holder, TempMods.DisarmedAbility);
        public static bool CanWithdraw(TimedEffects.Holder holder) => !Incapacitated(holder) && !Has(holder, TempMods.EnrageAbility);
        public static bool CanUseAbilities(WorldClient client) => !client.playerData.isDead && CanUseAbilities(TimedEffects.ForPlayer(client).Effects);
        public static bool CanMove(WorldClient client) => !client.playerData.isDead && CanMove(TimedEffects.ForPlayer(client).Effects);

        public static void CorrectPosition(WorldClient client)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            client.messageQueue.addObjectMessage(ServerPackets.BuildSelfPositionHandoff(client.playerInstance.YawInterval.getValue()[0],
                (float)x, (float)y, (float)z, (UInt32)Math.Round(TimeUtils.GetSimTimeSeconds() * 1000)), false);
            client.FlushQueue();
        }
    }
}
