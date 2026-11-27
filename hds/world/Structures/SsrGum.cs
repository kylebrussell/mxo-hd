using System.Collections.Generic;
using System.Linq;

namespace hds
{
    internal static class SsrGum
    {
        internal const uint Item = 47360;
        internal const ushort Ability = 1325;
        private const int RecordsPerBatch = 6;

        internal static void QueueRecords(WorldClient client, IReadOnlyList<byte[]> records)
        {
            // Native QA eventually acknowledged both large blocks, after 7 and 34 sends.
            // Publish the same records in smaller batches while the buff itself changes atomically.
            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0 && i % RecordsPerBatch == 0)
                    client.FlushQueue(force: true);
                client.messageQueue.addRpcMessage(records[i]);
            }
        }

        internal static AbilityBuffs.Buff Definition()
        {
            var mods = AbilityCatalog.Find(Ability).Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
            var pulse = mods.Single(mod => mod.Text("StatModUpdateFunc") == "SSRGumFunc");
            return new AbilityBuffs.Buff
            {
                Seconds = (float)pulse.Number("Duration"),
                Modifiers = mods.Select(mod => new AbilityBuffs.Modifier(AbilityBuffs.KindStat,
                    mod.Target, (int)mod.Number("MinValue"))).ToArray(),
                PeriodicFx = 67108996, // tempmods.py: SSRGumFunc
                PeriodicFxSeconds = pulse.Number("UpdateFuncInterval")
            };
        }
    }
}
