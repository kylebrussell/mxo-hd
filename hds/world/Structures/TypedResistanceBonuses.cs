using System;

namespace hds
{
    /// <summary>OURS level-scaled percentage conversion for the stored viral/thrown resistance bonus links.
    /// The retail owner coefficient is unknown; this reuses DamageReduction's4*level+20 resistance scale.</summary>
    public static class TypedResistanceBonuses
    {
        public static ushort Stat(int type)=>type switch{2=>1148,7=>1146,_=>0};

        internal static int Points(int level,int percent)
        {
            decimal amount=Math.Max(0m,4m*level+20m)*percent/100m;
            return (int)Math.Clamp(amount,int.MinValue,int.MaxValue); // signed truncation toward zero
        }

        internal static int Total(int raw,int level,int percent)=>percent==0?raw:
            (int)Math.Clamp((long)raw+Points(level,percent),int.MinValue,int.MaxValue);

        public static int Player(WorldClient target,int type,int raw)
        {
            ushort stat=Stat(type);if(stat==0)return raw;
            int level=target.playerInstance.Level.getValue()[0],percent=AbilityStats.Player(target,stat);
            return Resolve(raw,level,percent,type,TimedEffects.PlayerName(target));
        }

        public static int Npc(Mob target,int type,int raw)
        {
            ushort stat=Stat(type);if(stat==0)return raw;
            return Resolve(raw,target.getLevel(),AbilityStats.Npc(target,stat),type,target.getName());
        }

        private static int Resolve(int raw,int level,int percent,int type,string name)
        {
            int total=Total(raw,level,percent);
            if(percent!=0)Output.WriteDebugLog($"[RESISTANCE-BONUS OURS] {name} type{type} level{level} percent{percent} points{Points(level,percent)} base{raw} total{total}");
            return total;
        }
    }
}
