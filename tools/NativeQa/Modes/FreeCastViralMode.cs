namespace NativeQa.Modes;
/// <summary>Contest draws only; modeled totals and NPC neutral tactic remain explicit server policy.</summary>
public abstract class FreeCastViralMode : NativeQaMode
{
    protected static NativeQaModePlan Fixture(bool hit)=>new(
        new ushort[]{2,17,1006,33,57,314,272},24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]=hit?"32766":"0",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]=hit?"0":"32766",["MXO_PVP_SERVER"]="1"},
        extraQaMetadata:new(){["FreeCastViralContestQa"]=true,["FreeCastViralAttackRand15"]=hit?32766:0,["FreeCastViralDefenseRand15"]=hit?0:32766,["FreeCastViralPolicy"]="PARTIAL modeled342/183/637; NPC neutral1; modern flags and 64-significand OURS"},
        readyMessages:new[]{"Select WinterFrost, use normal Static Blast33. Do not enter interlock. "+(hit?"Expect viral hit then damage.":"Expect viral deflect, unchanged NPC HP and no debuff.")+" Contest-only draws; all combat, health and callback RNG unchanged. Do not activate314 for the baseline deflect check. See docs/ABILITY-FREECAST-VIRAL.md."});
}
public sealed class FreeCastViralHitMode : FreeCastViralMode
{
    public override string Name=>"freecast-viral-hit";
    public override NativeQaModePlan Plan {get;}=Fixture(true);
}
public sealed class FreeCastViralDeflectMode : FreeCastViralMode
{
    public override string Name=>"freecast-viral-deflect";
    public override NativeQaModePlan Plan {get;}=Fixture(false);
}
