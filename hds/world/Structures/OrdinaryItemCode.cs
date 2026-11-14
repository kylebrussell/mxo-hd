using System;
using System.Linq;
using System.Text;

namespace hds
{
    /// <summary>Ordinary archive item codes, distinct from flagged uploaded bits/fragments.</summary>
    public static class OrdinaryItemCode
    {
        public sealed class Entry { public ushort Slot; public uint Base; public int Charges; public uint Goid => (Base >> 7) & 0x7ffff; }
        public static bool Valid(uint value, int charges) => (value & 0x8000007f) == 0 && charges >= 1 && charges <= 127;
        public static uint Packed(uint value, int charges) => (value & ~127u) | (uint)charges;
        public static bool Supported(uint goid)
        {
            var item = CraftingRules.Find(goid);
            return item != null && item.Price.HasValue && item.Price >= 0 && item.Complexity.HasValue && item.Complexity >= 0 && item.Complexity <= 255 &&
                Encoding.ASCII.GetString(BitConverter.GetBytes(item.Category)).StartsWith("IC") && Crafting.Recipes.ContainsKey(goid);
        }
        // Native6207f650: signed16 totals, explicit float32 stores, final clamp after both reductions.
        public static float Seconds(int complexity, int skill, int reduction, int consumableReduction)
        {
            float first = (float)((2 * complexity - (short)skill + 2) * (100 - (short)reduction) * .01);
            float second = (float)(first * (100 - (short)consumableReduction) * .01);
            return Math.Clamp(second, 3f, 50f);
        }
        // Native62080630 uses the float32 multiplier, unlike the existing Code Writer policy.
        // Negative modifiers increase price. Nonnegative server charge is the script's discount<=1 policy.
        public static long Cost(CraftingRules.Item item, int modifier530) => Math.Max(0, (long)(
            (float)unchecked((uint)(item.Price ?? throw new ArgumentException("Ordinary IC compilation requires a proven VendorPrice", nameof(item)))) * (double)(float)(100 - (short)modifier530) * .12f / 100));
        public static bool Succeeds(int skill, int complexity, int roll, int specialization) =>
            CraftingRules.Succeeds(skill, complexity, roll, Math.Max(0, specialization));
        public static byte[] Update(ushort slot, uint value, int charges) => new byte[] { 0x69, 0, (byte)slot, (byte)(slot >> 8) }
            .Concat(BitConverter.GetBytes(Packed(value, charges))).Concat(new byte[2]).ToArray();
    }
}
