namespace NativeQa.Modes;
/// <summary>Explicit QA legacy-level/EXP mismatch; memory always follows EXP, not the stored level.</summary>
public sealed class AbilityMemoryMode : NativeQaMode
{
    public override string Name=>"ability-memory";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17,23,356,314,75},17000,2300,Array.Empty<string>(),new[]{
            "UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ((ability_id & 4193280)>>10) IN (314,75)"},
        new[]{"UPDATE characters SET level=50,exp=0 WHERE charId=110"},DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        readyMessages:new[]{"Ability memory: explicit QA level50/EXP0 has capacity16. Loaded parents23(10)+356(5), zero-cost2/17. In the normal Abilities/loadout UI inspect memory15/16; load314(1) if prerequisites permit, then75(2) must exceed capacity. Unload before loading another parent; relog preserves flags. Native prerequisite UI may add further owned parents; do not bypass it. See docs/ABILITY-MEMORY.md."});
}
