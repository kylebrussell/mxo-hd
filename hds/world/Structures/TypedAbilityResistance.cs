using System;
using System.Linq;
namespace hds
{
    /// <summary>OURS application of the script resistance curve to explicitly typed ability health damage.
    /// Normalized against zero modifiers to preserve the existing approximate unbuffed damage exactly.</summary>
    public static class TypedAbilityResistance
    {
        public static ushort Stat(int type)=>type switch {0=>180,1=>181,2=>157,7=>1033,_=>0};
        public static int StoredType(ushort ability,bool dot=false)
        {
            var entry=AbilityCatalog.Find(ability);
            if(entry==null)return -1;
            var types=entry.Modifiers.Where(m=>m.Number("WhenEnabled",-1)==2 &&
                (dot ? m.Target==TempMods.DOTPlaceHolderAbility : m.Target==108))
                .Select(m=>ModifierType(m,dot)).Distinct().ToArray();
            return types.Length==1 && Stat(types[0])!=0 ? types[0] : -1;
        }
        public static int ModifierType(AbilityCatalog.Modifier modifier,bool dot)
        {
            if(!modifier.Has("DamageType"))return -1;
            int type=(int)modifier.Number("DamageType",-1);
            if(Stat(type)==0)return -1;
            if(!dot)return modifier.Target==108 ? type : -1;
            string expected=type switch {0=>"PhysicalDOT",1=>"RangedDOT",2=>"ViralDOT",7=>"ThrownDOT",_=>""};
            return modifier.Target==TempMods.DOTPlaceHolderAbility && modifier.Text("StatModUpdateFunc")==expected ? type : -1;
        }
        public static ushort Amount(int raw,int attackerLevel,int defenderLevel,int total)
        {
            if(raw<=0)return 0;
            if(total==0)return (ushort)Math.Min(raw,ushort.MaxValue);
            double zero=CombatCalculator.DamageReduction(attackerLevel,defenderLevel,0);
            double modified=CombatCalculator.DamageReduction(attackerLevel,defenderLevel,total);
            return (ushort)Math.Clamp(Math.Floor(raw*(1-modified)/(1-zero)),0,ushort.MaxValue);
        }
        public static ushort Player(WorldClient caster,WorldClient victim,ushort ability,int raw,int type=-2)
        {
            if(type==-2)type=StoredType(ability);
            ushort stat=Stat(type);
            if(caster==null || victim==null || caster==victim || stat==0)return (ushort)Math.Clamp(raw,0,ushort.MaxValue);
            return Apply(caster.playerInstance.Level.getValue()[0],victim.playerInstance.Level.getValue()[0],
                AbilityStats.Player(victim,144)+AbilityStats.Player(victim,stat),ability,raw,type);
        }
        public static ushort Npc(WorldClient caster,Mob victim,ushort ability,int raw,int type=-2)
        {
            if(type==-2)type=StoredType(ability);
            ushort stat=Stat(type);
            if(caster==null || victim==null || stat==0)return (ushort)Math.Clamp(raw,0,ushort.MaxValue);
            return Apply(caster.playerInstance.Level.getValue()[0],victim.getLevel(),
                AbilityStats.Npc(victim,144)+AbilityStats.Npc(victim,stat),ability,raw,type);
        }
        private static ushort Apply(int attack,int defend,int total,ushort ability,int raw,int type)
        {
            ushort final=Amount(raw,attack,defend,total);
            if(total!=0 && raw>0)Output.WriteDebugLog($"[TYPED-RESISTANCE OURS] {ability} type{type} total{total} raw{raw} final{final}");
            return final;
        }
    }
}
