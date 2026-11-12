namespace NativeQa.Modes;
public sealed class ProtocolFamilyMode : NativeQaMode
{
    public override string Name=>"protocol-family";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17,6},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),
        extraQaMetadata:new(){["ProtocolFamilyDiagnostic"]=true,["ProtocolFamilyAbility"]=6,
            ["ProtocolFamilyWireContract"]="80bc-native22/03-schema-masks"},
        readyMessages:new[]{"Protocol diagnostic: Actions > Consistent Technique, activate on self; observe buff icon then normal20s expiry. Nearby QA helpers exercise03 creation masks. No random outcome or forced roll."});
}
