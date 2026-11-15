using HeadlessClient;
namespace NativeQa.Modes;

public sealed class InvisibilityToolRegistrationMode:NativeQaMode
{
    public override string Name=>"invisibility-tool-registration";
    public override NativeQaModePlan Plan {get;}=new(InvisibilityToolRegistrationFixture.Abilities,24800,-1498,
        Array.Empty<string>(),new[]{"DELETE FROM inventory WHERE charId=110 AND slot=40; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"},
        Array.Empty<string>(),DevCommands:true,environmentOverrides:new(){["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["RegistrationSource"]=175,["RegistrationChild"]=1116,["NativeEntry"]="Actions > Tools > double-click Invisibility Belt; hidden child is not archived"},
        readyMessages:new[]{"Before QA reload controls, activate Spy Style from Actions. Actions > Tools > double-click Invisibility Belt (bag40). Require no local missing-Invisibility refusal, actual1116,50IS debit and stealth. Toggle off normally, quit/relog and repeat before any reload. Then ?qaunload 175 must remove1116; ?qaload 175 must restore it once. No random outcomes are exercised. See docs/INVISIBILITY-TOOL-REGISTRATION.md."});
}
