using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace hds
{
    public static class VendorPricing
    {
        public const UInt32 DefaultBuyPrice = 100;
        public const UInt32 DefaultBuybackPrice = 100;
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
            if (PriceData.Value.TryGetValue(itemGoId, out VendorPriceEntry entry))
            {
                return entry.NonSellable
                    ? 0
                    : entry.VendorPrice / 10;
            }

            return DefaultBuybackPrice;
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
