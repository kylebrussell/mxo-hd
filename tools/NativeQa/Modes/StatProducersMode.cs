namespace NativeQa.Modes;
/// <summary>Record producers only: no link/default/math or live random override.</summary>
public sealed class StatProducersMode : NativeQaMode
{
    public override string Name=>"stat-producers";
    public override NativeQaModePlan Plan { get; }=new(new ushort[]{2,17,32,75,356,314},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{"UPDATE characters SET level=50,innerStrC=1000,innerStrM=1000 WHERE charId=110"},
        DevCommands:true,
        extraQaMetadata:new(){["StatProducersDiagnostic"]=true,["StatProducersAuthority"]="records only; no75/32 conversion proof"},
        readyMessages:new[]{"CSR110 only: owned32/75/356; normal356 grants1087. ?qaunload 75 / ?qaload 75 use ordinary owned loadout; Actions Howitzer Upgrade1087 toggles on/off (no timed expiry). Normal Upgrade Attacks314 has20s expiry. No random outcomes or forced rolls. See docs/STAT-PRODUCERS.md."});
}
