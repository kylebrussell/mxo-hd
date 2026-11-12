using System;
namespace hds
{
    /// <summary>Explicitly OURS level policy; unknown archive reference levels use an explicit50 tuning baseline.</summary>
    public static class MissionRewards
    {
        public const int DefaultArchiveReference = 50; // OURS: preserves unknown authored rewards at the retail level cap
        public readonly struct Payout
        {
            public readonly uint Experience,Info;
            public Payout(uint experience,uint info){Experience=experience;Info=info;}
        }
        private static uint Scale(int amount,int numerator,int denominator) =>
            (uint)Math.Min(int.MaxValue,Math.Max(0,(long)amount)*numerator/denominator);
        public static Payout Calculate(Mission mission,int level)
        {
            if(mission==null)throw new ArgumentNullException(nameof(mission));
            int reference=mission.rewardLevel;
            if(reference<1 || reference>PlayerProgression.MaxLevel)reference=DefaultArchiveReference;
            level=Math.Clamp(level,1,PlayerProgression.MaxLevel);
            // OURS, not a recovered retail formula: same-difficulty Welcome samples at levels2 and3 anchor these ratios.
            return new Payout(Scale(mission.exp,Math.Max(1,level-1),Math.Max(1,reference-1)),
                Scale(mission.info,level,reference));
        }
    }
}
