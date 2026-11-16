using HeadlessClient;
namespace NativeQa.Modes;
public sealed class BuffDrainLifetimeMode:NativeQaMode
{
    public override string Name=>"buff-drain-lifetime";
    public override NativeQaModePlan Plan {get;}=new(BuffDrainLifetimeFixture.Abilities,24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,healthC=20000,healthM=20000,innerStrC=2000,innerStrM=2000,y=495 WHERE charId=110;"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        extraQaMetadata:new(){["DrainLifecycle"]="Exact activation + current owner/world/life/transport; confirmed normal Quit cancels old drain"},
        readyMessages:new[]{"Normal Actions: toggle Punishing Blows16 on and observe1IS/s. Normal Quit, reconnect SAME CSR110 without re-preparing fixture, toggle16 on then off once. Observe no continuing1IS/s loss. No forced disconnect/reset, combat/RNG override or modified drain timers. See docs/BUFF-DRAIN-LIFETIME.md."});
}
