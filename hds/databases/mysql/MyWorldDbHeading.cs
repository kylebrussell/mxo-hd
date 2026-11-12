using System;
using System.Globalization;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        /// <summary>characters.rotation stores the protocol yaw byte, not radians. Preserve legacy
        /// truncation for representable values; corrupt/out-of-range/non-finite data gets neutral yaw0.</summary>
        public static byte PersistedYawInterval(object stored)
        {
            if (stored == null || stored == DBNull.Value) return 0;
            decimal value;
            // Decimal conversion rejects NaN/infinities and retains the old database-reader semantics.
            try { value = Convert.ToDecimal(stored, CultureInfo.InvariantCulture); }
            catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException) { return 0; }
            value = decimal.Truncate(value);
            return value >= byte.MinValue && value <= byte.MaxValue ? (byte)value : (byte)0;
        }
    }
}
