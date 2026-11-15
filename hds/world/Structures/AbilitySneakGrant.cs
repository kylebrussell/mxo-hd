using System;
using System.Linq;

namespace hds
{
    // One stored/function-qualified scaled grant. This is not a generic scaled-grant admission rule.
    internal static class AbilitySneakGrant
    {
        internal const ushort Parent=151,Child=1111;
        private static AbilityCatalog.Modifier Modifier(AbilityCatalog.Entry entry)
        {
            if(entry?.Id!=Parent||entry.Number("Ability/AbilityLevelCap",-1)!=50)return null;
            // GrantLevel's existing API is parent-based: never apply this value to another grant.
            var grants=entry.Modifiers.Where(m=>m.Has("TargetAbilityGrant")).ToArray();
            if(grants.Length!=1)return null;
            var grant=grants[0];
            return AbilityCatalog.IdOf((uint)grant.Number("TargetAbilityGrant"))==Child&&
                grant.Number("WhenEnabled",-1)==0&&grant.Number("DurationType",-1)==2&&grant.Number("ValueFunction",-1)==2&&
                !grant.Has("MinValue")&&!grant.Has("MaxValue")&&!grant.Has("ValueScale")?grant:null;
        }
        internal static bool Matches(ushort parent,ushort child)=>parent==Parent&&child==Child&&Modifier(AbilityCatalog.Find(parent))!=null;
        internal static bool TryLevel(AbilityCatalog.Entry entry,ushort learned,out ushort level)
        {
            level=0;var modifier=Modifier(entry);if(modifier==null)return false;
            int source=Math.Clamp((int)learned,0,(int)entry.Number("Ability/AbilityLevelCap"));
            if(!AbilityValues.TryValue(modifier,source,0,out int value)||value<0||value>50)return false;
            level=(ushort)value;return true;
        }
        internal static bool TryLevel(ushort parent,ushort child,ushort learned,out ushort level)
        {
            level=0;return parent==Parent&&child==Child&&TryLevel(AbilityCatalog.Find(parent),learned,out level);
        }
    }
}
