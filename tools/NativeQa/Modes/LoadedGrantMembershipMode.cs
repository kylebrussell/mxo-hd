namespace NativeQa.Modes;
/// <summary>Loaded grant registration only; no stat scaling, ownership or outcome changes.</summary>
public sealed class LoadedGrantMembershipMode:NativeQaMode
{
    public override string Name=>"loaded-grant-membership";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,64,137,143,356},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new(){["GrantMembershipPairs"]=25,["GrantMembershipReady"]="0c01 after live avatar",["ScaledAndLegacyGrantsUnchanged"]=true},
        readyMessages:new[]{"CSR110: before any QA reload, inspect Actions for Network Hacker Upgrade1078, Martial Arts Initiate Upgrade1062, Operative Upgrade1060 and Howitzer Upgrade1087. All retain captured level 0 / hidden slot 520. ?qaunload 143 must remove only 1060; ?qaload 143 restores it. Quit/relog without manual reload: initial Actions entries return once. No effect-magnitude or other grant claims. See docs/LOADED-GRANT-MEMBERSHIP.md."});
}
