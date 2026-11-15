using HeadlessClient;
namespace NativeQa.Modes;
/// <summary>Level/EXP-consistent and prerequisite-closed ordinary UI memory profile.</summary>
public sealed class AbilityMemoryMode : NativeQaMode
{
    public override string Name=>"ability-memory";
    public override NativeQaModePlan Plan {get;}=new(
        AbilityMemoryUiFixture.Known.ToArray(),17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{AbilityMemoryUiFixture.SeedSql(110)},DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        readyMessages:new[]{"Ability memory: level1/EXP0, all training1, closed loaded cost15/16. Hardline Abilities: archived Awakened > Evade Combat217 loads to16. Concentration628 would be17 and must give MEMORY, not prerequisites. Unload217, load628, relog flags. Three discipline folders are owned/unloaded; do not load them. No developer bridge. See docs/ABILITY-MEMORY.md."});
}
