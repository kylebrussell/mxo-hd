namespace NativeQa.Modes;
/// <summary>Only340 is a player action under test; stagger preparation is a separately guarded disposable fixture.</summary>
public sealed class ConditionalPowerlessMode:NativeQaMode
{
    public override string Name=>"conditional-powerless";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,340,1112},24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),DevCommands:true,
        environmentOverrides:new(){["MXO_CONDITIONAL_POWERLESS_QA"]="1"},
        extraQaMetadata:new(){["ConditionalPowerlessAbility"]=340,["ConditionalPowerlessSeconds"]=6,["ConditionalPowerlessPreparation"]="strict disposable selected-target stagger; not a player883 grant"},
        readyMessages:new[]{"Select a live visible target. With no Staggered condition, ordinary Throat Cutting Throw340 must not apply Powerless. Select target then ?combatdepth stagger 30 (fixture only); cast340 normally after its reuse ends: six-second base Powerless, no new immunity timer. Native duration may use existing server scaling. Wait removal and check action recovery. No883 player Actions grant. See docs/ABILITY-CONDITIONAL-POWERLESS.md."});
}
