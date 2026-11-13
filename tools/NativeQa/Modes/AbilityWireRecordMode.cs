namespace NativeQa.Modes;
/// <summary>Ordinary77 with its sourced prerequisite closure; dev health preparation only.</summary>
public sealed class AbilityWireRecordMode:NativeQaMode
{
    public override string Name=>"ability-wire-record";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,45,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{$"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},healthC=100,healthM=1000,innerStrC=1000,innerStrM=1000 WHERE charId=110"},DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        readyMessages:new[]{"Ability wire record: ordinary2/17/77 plus sourced45/1006 prerequisites. Self health100/1000, level50 with matching EXP. Cast ability77 on yourself; retain actual full43-byte80b9 and normal cast/heal replies. Existing ?health 100 preparation only, no GM/RNG. Native printer names do not establish activation/target semantics. See docs/ABILITY-USE-RECORD.md."});
}
