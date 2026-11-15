using HeadlessClient;
namespace NativeQa.Modes;

public sealed class PunishingBlowsMode:NativeQaMode
{
    public override string Name=>"punishing-blows";
    public override NativeQaModePlan Plan {get;}=new(PunishingBlowsFixture.Abilities,24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=20000,healthM=20000,innerStrC=2000,innerStrM=2000,y=495 WHERE charId IN (110,6111,6112,6113,6114); DELETE FROM inventory WHERE charId=110 AND slot IN (1,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,692,1,1,0);"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_CONDITIONAL_THROW_QA"]="1",["MXO_PUNISHING_BLOWS_QA"]="1"},
        extraQaMetadata:new(){["PunishingBlowsPolicy"]="OURS: active212 percent against current Daze/Stun/OffBalance; retail counter unrecovered",
            ["PreparationOnly"]="Existing selected-current-target offbalance885 state; no RNG or damage override"},
        readyMessages:new[]{"OURS Punishing Blows16: use Actions to toggle normally. Select QAEnemy, ordinary rifle fire before/after ?combatdepth offbalance 30; active212=15 adds15% only while vulnerable. Healthy/root-only targets get no bonus. Stop fire, let state expire, then repeat. Observe normal1IS/s drain and toggle off. No retail exploited-counter parity. See docs/ABILITY-PUNISHING-BLOWS.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",enemy);await enemy.EnterWorldAsync();await ConcentrationFixture.PrepareEnemyAsync(enemy);
        enemy.SendChat("?pvpflag on");return Array.Empty<byte>();
    }
}
