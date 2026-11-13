using HeadlessClient;
namespace NativeQa.Modes;
public sealed class DebuffRadiusMode : NativeQaMode
{
    public override string Name => "debuff-radius";
    public override NativeQaModePlan Plan {get;} = new(DebuffRadiusFixture.Abilities,23999,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,alignment=2,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,y=495,rotation=0 WHERE charId=110",
            DebuffRadiusFixture.TargetsSql(6111,6112,6113,6114)},
        readyMessages:new[]{"Direct stat177 radius diagnostic:333 base4m;345 existing captured+21 for30s gives4.84m. QAFriend3.8/QAFar4.4/QAOther5.0 fromQAEnemy. No75 link claim. See docs/ABILITY-DEBUFF-RADIUS.md."});
}
