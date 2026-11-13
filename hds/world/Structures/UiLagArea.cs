namespace hds
{
    /// <summary>Retained97-only API; shared enumeration admits only explicitly reviewed abilities.</summary>
    internal static class UiLagArea
    {
        internal const ushort Ability=97;
        internal static void Land(WorldClient caster,AbilityItem ability)
        {
            if(ability?.getAbilityID()!=Ability)return;
            CasterHostileArea.Land(caster,ability);
        }
    }
}
