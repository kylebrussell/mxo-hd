namespace NativeQa.Modes;
/// <summary>OURS linked viral percentage; seeded diagnostic never changes live combat RNG.</summary>
public sealed class ViralAccuracyBonusMode:NativeQaMode
{
    public override string Name=>"viral-accuracy-bonus";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,23,35,32,33},24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
        DevCommands:true,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",
            ["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]="17000",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="17000"},
        extraQaMetadata:new(){["ViralAccuracyPolicy"]="OURS level*1147percent/100; retail coefficient unknown",["RunProgram50Bonus"]=5},
        readyMessages:new[]{"OURS viral accuracy: ?qaunload 32; ?combatdepth specialattack 33 baseline. ?qaload 32 (Deflect Code23 and Code Shaper35 already loaded); repeat diagnostic: total +5 and more paired seeded hits. Use normal Static Blast33 against WinterFrost before/after; inspect FREECAST-VIRAL inputs. Contest draws alone fixed17000/17000; other combat randomness untouched. Unload32/relog and confirm no lingering bonus. docs/ABILITY-VIRAL-ACCURACY-BONUS.md."});
}
