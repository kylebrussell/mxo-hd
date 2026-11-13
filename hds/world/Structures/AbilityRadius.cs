namespace hds
{
    /// <summary>Native owner radius625c5500; stored metres, signed percentage and binary32 fraction.</summary>
    public static class AbilityRadius
    {
        // Existing support/friendly callback contract stays UBF0-only.
        public static double Metres(WorldClient caster, ushort ability) => ForCategory(caster,ability,0x30464255,127);

        // Only AbilityTargets' target-centred hostile selectors opt into the UDB0 branch.
        public static double TargetMetres(WorldClient caster, ushort ability) => ForCategory(caster,ability,0x30424455,177);

        private static double ForCategory(WorldClient caster,ushort ability,uint category,ushort stat)
        {
            var entry=AbilityCatalog.Find(ability);
            double radius=entry?.Number("Ability/RadiusOfEffect")??0;
            if(entry?.Number("Ability/StatusCategory")!=category)return radius;
            short bonus=unchecked((short)AbilityStats.Player(caster,stat));
            // Native625c558d stores /100 as binary32 before adding1 and multiplying by the base.
            float fraction=(float)(bonus/100.0);
            return radius*(1+(double)fraction);
        }
    }
}
