using System;

namespace hds
{
    /// <summary>
    /// Experience for a kill, by retail's experience.ComputeExperience (research/client-scripts/experience.py):
    /// rank multiplier x 3^((1 + NPC level - player level) / 7) x the player's share of the damage, then
    /// x 5 x (level - 1) (XPConstantMultiplier 5 and XPKnob 1, the client's defaults; level 1 counts as 2).
    ///
    /// The rank multipliers are console variables (WeaklingKillMultiplier, ToughKillMultiplier, BossKillMultiplier) and
    /// aren't in the scripts. Weakling 67 and Tough 135 are inferred from the 15 kill rewards (80 e6 .. 01) in the luizoe
    /// and crashover2k captures, which all fit them exactly after truncation: at level 2, 335, 391, 458 and 536 are 67 x 5
    /// x 3^(0, 1, 2, 3 / 7); 675 and 923 are 135 x 5 x 3^(0, 2 / 7); at level 3, 783, 917 and 1847 are the same at x 10.
    /// That also shows the exponent is not floored: the engine passed the levels as floats.
    /// </summary>
    public static class KillExperience
    {
        public const int WeaklingMultiplier = 67;
        public const int ToughMultiplier = 135;

        /// <summary>consolevar.XPConstantMultiplier and XPKnob (client defaults, as in PlayerProgression).</summary>
        public const double XPConstantMultiplier = 5, XPKnob = 1;

        /// <summary>The XP for killing an NPC of this level, truncated as retail's reward was.</summary>
        public static UInt32 ForKill(int npcLevel, int playerLevel, int rankMultiplier = WeaklingMultiplier, double damageShare = 1.0)
        {
            double exponent = (1.0 + npcLevel - playerLevel) / 7.0;
            double experience = rankMultiplier * Math.Pow(3, exponent) * damageShare;
            int level = playerLevel != 1 ? playerLevel - 1 : playerLevel;
            experience = experience + XPKnob * (level * experience * XPConstantMultiplier - experience);
            return (UInt32)Math.Max(0, Math.Min(UInt32.MaxValue, experience));
        }

        /// <summary>experience.ComputeGroupBonus: a team of 2 to 6 gets 1.2, 1.5, 1.6, 1.75 or 2.1 times as much each.</summary>
        public static double GroupBonus(int players)
        {
            switch (players)
            {
                case 2: return 1.2;
                case 3: return 1.5;
                case 4: return 1.6;
                case 5: return 1.75;
                case 6: return 2.1;
                default: return 1.0;
            }
        }
    }
}
