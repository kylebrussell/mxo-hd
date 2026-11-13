namespace NativeQa.Modes;
/// <summary>Ordinary PvE allies can challenge; only a normal accepted duel makes them opponents.</summary>
public sealed class DuelHelperMode:NativeQaMode
{
    public override string Name=>"duel-helper";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},17000,2300,Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),
        environmentOverrides:new(){["MXO_PVP_SERVER"]="0"},
        readyMessages:new[]{"PvE duel helper: load helpers, then arm request {actor:QAEnemy,action:duel-accept,challenger:CSR_Tester} and WAIT for responses/<requeststem>.armed.json BEFORE issuing ordinary /duel on QAEnemy. The request waits up to30s for the NEXT challenge and verifies new55 plus pairedDuelIDs. Existing approve is resurrection-only. No dev/GM/RNG; no challenge timer or gameplay legality override. See docs/NATIVEQA-DUEL-HELPER.md."});
}
