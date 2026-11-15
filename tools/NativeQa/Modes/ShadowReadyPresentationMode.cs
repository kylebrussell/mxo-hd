namespace NativeQa.Modes;

public sealed class ShadowReadyPresentationMode:NativeQaMode
{
    public override string Name=>"shadow-ready-presentation";
    // Existing ordinary175/290 prerequisite closure;1116/1117 remain grants, never archive seeds.
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,113,120,121,123,130,131,142,143,151,152,155,175,185,290,291,292,1112,1125,1128},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,exp="+hds.PlayerProgression.ExperienceForLevel(50)+",x=17000,y=495,z=2300,alignment=1,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND (slot=40 OR goid=460); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"},
        environmentOverrides:new(){["MXO_PACKET_LOG"]="1",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"},
        extraQaMetadata:new(){["ShadowReadyPresentationCandidate"]=true,["Parent"]=290,["Child"]=1117,["NoOwnedShadowSeed"]=true,["NativeListingProven"]=false,["ExpectedMemory"]=46,["NoRngControls"]=true},
        readyMessages:new[]{"CSR110: on the FIRST normal world entry inspect Actions > Abilities for the actual granted Shadow1117. No reload workaround before that observation. If absent or unusable STOP/HOLD: no hidden1117 request or archive seed. If visible, put that visible action on the hotbar and activate normally; retain actual20IS debit and sourced644+3 modifier. Late80b2 plusType45 is a presentation candidate, not visibility proof. Preserve PacketLog/Debug/state.json before cleanup and separate early/late1117 bases, own2 birth and exact0c01 qualification. Existing175 belt GO460 bag40 is owned; Actions > Tools is its separate normal route. No130/133 duration claim from this package, no detection/combat/RNG/timer changes. See docs/ABILITY-SHADOW-READY-PRESENTATION.md."});
}
