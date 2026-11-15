using HeadlessClient;
namespace NativeQa.Modes;

public sealed class SneakScaledGrantMode:NativeQaMode
{
    public override string Name=>SneakScaledGrantFixture.Name;
    public override NativeQaModePlan Plan {get;}=new(SneakScaledGrantFixture.Parents,17000,2300,Array.Empty<string>(),
        new[]{"UPDATE char_abilities SET level=17 WHERE char_id=110 AND ability_id=2147638272"},
        Array.Empty<string>(),DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        extraQaMetadata:new(){["SneakParent"]=151,["SneakChild"]=1111,["SneakLearnedLevel"]=17,["SneakCharacterLevel"]=50,
            ["SneakScope"]="exact stored151->1111 only; capture observations at50 only; low levels use sourced function2",
            ["SneakEnemyFixture"]="test NPC list; nearest authored row8405.94 units from17k/2300; no enemy helper login/detection-rule override",
            ["SneakNativeActionsProven"]=false},
        readyMessages:new[]{"CSR110 character50; normally owned/loaded Sneak151 learned17 plus real prerequisites,17/100 memory. No1111 archive seed. BEFORE any QA reload, open Actions > Abilities and locate Sneak in its actual group; shipped grouping resolves1111 to Spy155, but native listing is UNPROVED. If absent, stop and preserve the screenshot/packets; do not load hidden children or manufacture a folder. Activate normally: actual1111 cast and Stealth209=17, NOT character50; toggle off. Only afterward may existing owned ?qaunload 151 / ?qaload 151 controls check removal/re-addition. Quit/relog normally and repeat initial availability without a reload workaround. Stay near17k/2300, away from the disclosed test NPC list; no detection/noise/movement/RNG or active-buff-refresh parity claim. See docs/SNEAK-SCALED-GRANT.md. Claude alone uses the native client/lock/reset; ordinary CSR110 reset remains required."});
}
