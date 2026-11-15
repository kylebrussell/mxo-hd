namespace HeadlessClient;
public static class SeverArteryFixture
{
    public static CharacterSeed Actor(bool caster)=>new(caster?"SeverCaster":"SeverVictim")
    {
        X=caster?24699:24799,Y=495,Z=-1498,Level=50,Experience=checked((int)hds.PlayerProgression.ExperienceForLevel(50)),
        Organization=caster?1:2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=caster?new(ushort,ushort)[]{(2,50),(17,50),(339,50),(341,50),(1112,50)}:Array.Empty<(ushort,ushort)>()
    };
}
