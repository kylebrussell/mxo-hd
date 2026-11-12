namespace hds
{
    /// <summary>Native caster radius625c5500: UBF0 uses signed stat127 as a percent of template radius.
    /// Base62519100 returns world units; server area selectors accept metres. UDB0/stat177 is outside this change.</summary>
    public static class AbilityRadius
    {
        public static double Metres(WorldClient caster, ushort ability)
        {
            var entry = AbilityCatalog.Find(ability);
            double radius = entry?.Number("Ability/RadiusOfEffect") ?? 0;
            if (entry?.Number("Ability/StatusCategory") != 0x30464255) return radius;
            short bonus = unchecked((short)AbilityStats.Player(caster,127));
            // x87 stores the divided bonus to binary32 at625c558d before adding1 and multiplying.
            float fraction = (float)(bonus / 100.0);
            return radius * (1 + (double)fraction);
        }
    }
}
