namespace NativeQa.Modes;
/// <summary>Ordinary77 prerequisite closure plus fixed display-only refusal examples.</summary>
public sealed class AbilityRefusalFeedbackMode:NativeQaMode
{
    public override string Name=>"ability-refusal-feedback";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,45,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{$"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},healthC=100,healthM=1000,innerStrC=1000,innerStrM=1000,y=495,rotation=0 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){["MXO_ABILITY_REFUSAL_QA"]="1"},
        readyMessages:new[]{"Clear your target, use existing ?health 100 if regen filled health, then normally cast Actions Repair RSI 1.0 on yourself. Observe ordinary cast/heal/cost/timer separately. Fixed ?abilityrefusal reuse / inner / unloaded are DISPLAY ONLY: reuse text reads the client-local timer/fallback; inner says 15 more IS; unloaded renders even though 77 remains loaded. No probe changes resources/timers/loaded state or proves native UI generated a refusal request. No GM/RNG. See docs/ABILITY-REFUSAL-FEEDBACK.md."});
}
