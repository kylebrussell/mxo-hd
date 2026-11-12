using System;

namespace hds
{
    /// <summary>The deterministic native modifier value functions (client.dll 62517ff0).
    /// 626a92d8 truncates toward zero; the min is added after scaling, then a positive max caps level functions.</summary>
    public static class AbilityValues
    {
        public static bool TryValue(AbilityCatalog.Modifier modifier, int abilityLevel, int characterLevel, out int value)
        {
            int function = (int)modifier.Number("ValueFunction", 0);
            int min = (short)modifier.Number("MinValue"), max = (short)modifier.Number("MaxValue");
            float scale = (float)modifier.Number("ValueScale", 1);
            if (function == 2 || function == 3)
            {
                // The x87 multiply retains precision until _ftol: e.g. binary32 .24 * 50 is just below 12.
                value = min + (int)((function == 2 ? abilityLevel : characterLevel) * (double)scale);
                if (max > 0) value = Math.Min(value, max);
                return true;
            }
            // Fixed ranges do not need either native random distribution. Random/scaled-level functions remain explicit gaps.
            if ((function == 0 || function == 1) && min == max)
            {
                value = (int)(min * (double)scale);
                return true;
            }
            value = 0;
            return false;
        }
    }
}
