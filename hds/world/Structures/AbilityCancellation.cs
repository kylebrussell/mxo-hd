using System;

namespace hds
{
    /// <summary>80 bb: dismiss a buff on oneself. The client checks CanNOTRemoveBuff (62517780) and UBF0
    /// status category (62515ef0) before sending; enforce both again on the server.</summary>
    public static class AbilityCancellation
    {
        public const uint BuffCategory = 0x30464255; // UBF0
        public static ushort? Parse(byte[] body) => body?.Length == 2 && BitConverter.ToUInt16(body, 0) < 0x1000
            ? BitConverter.ToUInt16(body, 0) : (ushort?)null;

        public static bool CanDismiss(ushort id)
        {
            var entry = AbilityCatalog.Find(id);
            return id < 0x1000 && entry != null && entry.Number("Ability/CanNOTRemoveBuff") == 0 &&
                entry.Number("Ability/StatusCategory") == BuffCategory;
        }

        public static bool Dismiss(WorldClient client, ushort id)
        {
            if (!CanDismiss(id)) return false;
            bool removed = AbilityBuffs.Remove(client, id);
            removed |= TimedEffects.RemoveParent(TimedEffects.ForPlayer(client), id);
            if (removed) client.FlushQueue();
            return removed;
        }

        public static void Process(WorldClient client, byte[] body)
        {
            ushort? id = Parse(body);
            if (id.HasValue) Dismiss(client, id.Value);
        }
    }
}
