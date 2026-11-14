namespace NativeQa.Modes;
public sealed class SeverArteryStatesMode:NativeQaMode
{
    public override string Name=>"sever-artery-states";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,339,341,1112},24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,exp="+hds.PlayerProgression.ExperienceForLevel(50)+",alignment=1,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "UPDATE characters SET level=50,exp="+hds.PlayerProgression.ExperienceForLevel(50)+",alignment=2,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=6114; DELETE FROM char_abilities WHERE char_id=6114"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:new(){["MXO_SEVER_STATE_QA"]="1"},
        extraQaMetadata:new(){["SeverArteryOnlyCallbackControls"]=true,["GlobalCastCombatRngOverride"]=false,["ChancePercent"]=10,["StateDuration"]=10,["Interval"]=2.5,["QaTarget"]="QAEnemy",["QaTargetCharacter"]=6114,["SeverArteryCasterOrganization"]=1,["TargetOrganization"]=2,["BothPvpFlags"]=true,["TargetDetermination"]=0},
        readyMessages:new[]{"CSR110 only. ?severstate 99 0, select QAEnemy (different org, PvP on, normalized zero determination) and use normal Actions > Sever Artery341 (Spy Style). Normal35IS/4s cast/15s reuse; a miss stays a miss. No random341 states while99, existing DOT/slow retained. Wait actual old341/710 parent OFF (30s), not merely15s reuse; then ?severstate 0 0, normal cast:341->Daze199 exactly10s. Immediately ?severstate 99 0, wait off; ?severstate 0 1, next2.5s update OffBalance20010s, then99 and wait off; ?severstate 0 2, next update Staggered20110s, then99. No callback after parent expires; last child keeps its own10s. Controls only this callback; no cast/round RNG hook. See docs/ABILITY-SEVER-ARTERY-STATES.md. Native capture and ordinary movement/action recovery remain Claude gates."});
}
