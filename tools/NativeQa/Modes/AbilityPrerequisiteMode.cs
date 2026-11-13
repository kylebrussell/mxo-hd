using HeadlessClient;
namespace NativeQa.Modes;
public sealed class AbilityPrerequisiteMode : NativeQaMode
{
    public override string Name=>"ability-load-prerequisites";
    public override NativeQaModePlan Plan {get;}=new(AbilityPrerequisiteFixture.Known.ToArray(),17000,2300,Array.Empty<string>(),
        new[]{AbilityPrerequisiteFixture.ArchiveSql(110)},new[]{AbilityPrerequisiteFixture.CharacterSql(110)},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        extraQaMetadata:new(){["PrerequisiteAuthorityQa"]=true,["ExplicitOursBatchPolicy"]=true},
        readyMessages:new[]{"CSR110 alone, level1/EXP0. Normal hardline radial > Abilities: Awakened and Self Defense are loaded. Double-click archived Hindering Shot; the native client should also load its owned Combat Insight parent, reaching4/16. Unload/reload through the normal UI, load Evade Combat and Concentration, then relog and verify the same loaded flags. No developer command or random outcome is required. Invalid child-only RPC cases are headless authority proof, not actions the native UI should send. See docs/ABILITY-LOAD-PREREQUISITES.md."});
}
