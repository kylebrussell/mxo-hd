using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace hds
{
    public static class VendorPricing
    {
        public const UInt32 DefaultBuyPrice = 100;
        public const UInt32 DefaultVendorPrice = 1000;
        public const UInt32 DefaultBuybackPrice = DefaultVendorPrice / 10;
        public const UInt32 NoBuyBackPrice = 1;
        private const UInt32 AbilityCodeFlag = 0x80000000;
        private const UInt32 AbilityCodeMask = 0xfffffc00;
        private const UInt32 AbilityLevelMask = 0x000003ff;
        private static readonly HashSet<UInt32> FullPriceAbilityCodes = new HashSet<UInt32>
        {
            2148365312, // BalanceAbility
            2147490816, // DetectVulnerabilityAbility
            2147605504, // DisarmTrapsAbility
            2147492864, // EnergizedAttacksAbility
            2147496960, // HyperStrengthAbility
            2147684352, // IgnorePainAbility
            2147858432, // ImpartInvisibilityAbility
            2147497984, // PowerShotAbility
            2147499008, // PreciseBlowAbility
            2147500032, // PunishingBlowsAbility
            2148372480, // PurityAbility
            2148374528, // SpinningFlowerAbility
            2148373504, // SweepTheFloorAbility
            2148364288, // TechniqueAbility
            2148375552  // ZenMasterAbility
        };
        private static readonly Lazy<IReadOnlyDictionary<UInt32, VendorPriceEntry>> PriceData =
            new Lazy<IReadOnlyDictionary<UInt32, VendorPriceEntry>>(() => LoadPriceData(DataLoader.DataPath("vendor_prices.csv")));

        public static UInt32 GetBuyPrice(UInt32 itemGoId)
        {
            if (PriceData.Value.TryGetValue(itemGoId, out VendorPriceEntry entry) && entry.VendorPrice > 0)
            {
                return entry.VendorPrice;
            }

            return DefaultBuyPrice;
        }

        public static UInt32 GetSellPrice(UInt32 itemGoId)
        {
            if (itemGoId == 0)
            {
                return NoBuyBackPrice;
            }

            if ((itemGoId & AbilityCodeFlag) != 0)
            {
                return GetAbilityBuybackPrice(itemGoId);
            }

            if (PriceData.Value.TryGetValue(itemGoId, out VendorPriceEntry entry))
            {
                return entry.NonSellable
                    ? 0
                    : entry.VendorPrice / 10;
            }

            return DefaultBuybackPrice;
        }

        private static UInt32 GetAbilityBuybackPrice(UInt32 itemGoId)
        {
            UInt32 abilityCodeData = itemGoId & AbilityCodeMask;
            UInt32 level = itemGoId & AbilityLevelMask;
            UInt64 buyback = DefaultVendorPrice;
            bool hasDecodedPrice = false;

            if (PriceData.Value.TryGetValue(abilityCodeData, out VendorPriceEntry entry))
            {
                if (entry.VendorPrice > 0)
                {
                    buyback = entry.VendorPrice;
                    hasDecodedPrice = true;
                }
            }

            if (hasDecodedPrice)
            {
                for (UInt32 i = 1; i < level; i++)
                {
                    UInt64 nextLevel = i + 1;
                    buyback += nextLevel * nextLevel * 200;
                }
            }

            return FullPriceAbilityCodes.Contains(abilityCodeData)
                ? ClampPrice(buyback)
                : ClampPrice(buyback / 10);
        }

        private static UInt32 ClampPrice(UInt64 price)
        {
            return price > UInt32.MaxValue
                ? UInt32.MaxValue
                : (UInt32)price;
        }

        public static bool TryApplyBuy(long currentCash, UInt32 itemGoId, out UInt32 newCash, out UInt32 price)
        {
            price = GetBuyPrice(itemGoId);
            if (currentCash < price)
            {
                newCash = currentCash <= 0
                    ? 0
                    : (UInt32)Math.Min(currentCash, UInt32.MaxValue);
                return false;
            }

            newCash = (UInt32)Math.Min(currentCash - price, UInt32.MaxValue);
            return true;
        }

        // Pure, unit-testable sell helper mirroring TryApplyBuy. No Store/DB access.
        //
        // Rule for non-sellable items: GetSellPrice returns 0 for items flagged NonSellable in
        // the decoded csv (see VendorPricingTests.SellPriceIsZeroForNonSellableItems). A buyback
        // value of 0 is therefore the explicit "this item cannot be sold" signal, so we reject the
        // sell (return false) and leave cash unchanged. NoBuyBackPrice (1) is only produced for an
        // empty/zero itemGoId; that is treated as a normal (if trivial) sale for 1, mirroring how
        // TryApplyBuy still proceeds on its DefaultBuyPrice fallback path. newCash is clamped to
        // UInt32.MaxValue so a huge buyback can never overflow the cash field.
        public static bool TryApplySell(long currentCash, UInt32 itemGoId, out UInt32 newCash, out UInt32 price)
        {
            price = GetSellPrice(itemGoId);
            if (price == 0)
            {
                newCash = currentCash <= 0
                    ? 0
                    : (UInt32)Math.Min(currentCash, UInt32.MaxValue);
                return false;
            }

            long baseCash = currentCash < 0 ? 0 : currentCash;
            newCash = (UInt32)Math.Min(baseCash + (long)price, (long)UInt32.MaxValue);
            return true;
        }

        internal static IReadOnlyDictionary<UInt32, VendorPriceEntry> LoadPriceData(string path)
        {
            Dictionary<UInt32, VendorPriceEntry> prices = new Dictionary<UInt32, VendorPriceEntry>();
            if (!File.Exists(path))
            {
                return prices;
            }

            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                bool firstLine = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (firstLine)
                    {
                        firstLine = false;
                        continue;
                    }

                    if (line.Length == 0)
                    {
                        continue;
                    }

                    string[] columns = line.Split(',');
                    if (columns.Length < 4
                        || !UInt32.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out UInt32 goId)
                        || !UInt32.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out UInt32 price))
                    {
                        continue;
                    }

                    bool nonSellable = columns[3].Equals("true", StringComparison.OrdinalIgnoreCase);
                    prices[goId] = new VendorPriceEntry(columns[1], price, nonSellable);
                }
            }

            return prices;
        }
    }

    public sealed class VendorPriceEntry
    {
        public VendorPriceEntry(string codeName, UInt32 vendorPrice, bool nonSellable)
        {
            CodeName = codeName;
            VendorPrice = vendorPrice;
            NonSellable = nonSellable;
        }

        public string CodeName { get; }
        public UInt32 VendorPrice { get; }
        public bool NonSellable { get; }
    }
}
