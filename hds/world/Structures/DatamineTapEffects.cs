using System.Data.Common;
using System.Runtime.CompilerServices;
using hds.databases;
using hds.shared;

namespace hds.world.Structures
{
    /// <summary>The four Datamine taps run PlayItemActivationFXIfInInv at a four-second interval.</summary>
    internal static class DatamineTapEffects
    {
        private const uint ActivationFx = 0x04000078;
        private const double Interval = 4;

        private sealed class CarryPulse
        {
            public readonly ClientData Data;
            public readonly int World;
            public readonly uint Character;

            public CarryPulse(WorldClient carrier)
            {
                Data = carrier.playerData;
                World = Data.worldGeneration;
                Character = Data.getCharID();
            }

            public bool InWorld(WorldClient client) => client.Alive && ReferenceEquals(Data, client.playerData) &&
                Data.worldGeneration == World && Data.getCharID() == Character &&
                Data.getOnWorld() && !Data.worldChangePending;
        }

        private static readonly object sync = new object();
        private static readonly ConditionalWeakTable<WorldClient, CarryPulse> pulses = new ConditionalWeakTable<WorldClient, CarryPulse>();

        internal static void OnWorldEntry(WorldClient client)
        {
            var pulse = new CarryPulse(client);
            if (HasTap(pulse) == true) Begin(client, pulse);
        }

        internal static void OnPickup(WorldClient client, ushort goid)
        {
            if (goid >= 47276 && goid <= 47279) Begin(client, new CarryPulse(client));
        }

        private static bool? HasTap(CarryPulse pulse)
        {
            try
            {
                return WorldLuggablesDb.HasDatamineTap(pulse.Character);
            }
            catch (DbException ex)
            {
                Output.WriteDebugLog($"[DATA-MINING] tap FX inventory check failed char={pulse.Character}: {ex.Message}");
                return null;
            }
        }

        private static void Begin(WorldClient client, CarryPulse pulse)
        {
            lock (sync)
            {
                if (!pulse.InWorld(client)) return;
                pulses.Remove(client);
                pulses.Add(client, pulse);
            }
            Schedule(client, pulse);
        }

        private static bool IsCurrent(WorldClient client, CarryPulse pulse) =>
            pulses.TryGetValue(client, out var current) && ReferenceEquals(current, pulse) && pulse.InWorld(client);

        private static void Schedule(WorldClient client, CarryPulse pulse)
        {
            DelayedActions.AfterFor(client, Interval, () =>
            {
                if (!IsCurrent(client, pulse)) return;

                // Read the committed bag, so drops and other inventory changes stop the pulse.
                bool? hasTap = HasTap(pulse);
                lock (sync)
                {
                    if (!IsCurrent(client, pulse)) return;
                    if (hasTap == false)
                    {
                        pulses.Remove(client);
                        return;
                    }
                }

                if (hasTap == true && !pulse.Data.isDead && !pulse.Data.duelDeath)
                {
                    ServerPackets.SendPlayerAbilityEffect(client, ActivationFx);
                    Output.WriteDebugLog($"[DATA-MINING] tap FX char={pulse.Character} effect=0x{ActivationFx:x8}");
                }
                Schedule(client, pulse);
            });
        }
    }
}
