using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// research/client-scripts/augments.py: augment tables by equipment category. All ten are empty in the 7.6005
    /// client and nothing imports the module (dead code, compiled 2006-03-30), so there is nothing to apply; this keeps
    /// its shape and lookup so code that asks gets retail's answer: no augments.
    /// </summary>
    public static class Augments
    {
        /// <summary>
        /// MasterAugmentTable's categories in order (augments.py 18): 0 none, 1 hat, 2 glasses, 3 shirt, 4 gloves,
        /// 5 coat, 6 pants, 7 none, 8 shoes, 9 weapon, 10 hacker tool.
        /// </summary>
        public const int Hat = 1, Glasses = 2, Shirt = 3, Gloves = 4, Coat = 5, Pants = 6, Shoes = 8, Weapon = 9, HackerTool = 10;

        private static readonly IReadOnlyList<UInt16> Empty = Array.Empty<UInt16>();

        public static readonly IReadOnlyList<UInt16>[] MasterAugmentTable =
        {
            Empty, Empty /* Hat */, Empty /* Glasses */, Empty /* Shirt */, Empty /* Gloves */, Empty /* Coat */,
            Empty /* Pants */, Empty, Empty /* Shoes */, Empty /* Weapon */, Empty /* HackerTool */
        };

        /// <summary>GetAugmentTable (augments.py 20-24): the category's table, empty past the end.</summary>
        public static IReadOnlyList<UInt16> GetAugmentTable(int category)
        {
            if (category < 0 || category >= MasterAugmentTable.Length)
            {
                return Empty;
            }
            return MasterAugmentTable[category];
        }
    }
}
