namespace NativeQa.Modes;
/// <summary>OURS queued-special accuracy; no live RNG override. Paired diagnostics are read-only.</summary>
public sealed class TypedAttackMode : NativeQaMode
{
    public override string Name => "typed-attack";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[]{2,17,1112,134,129,105,1006,97,314,383},24800,-1498,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "UPDATE characters SET level=50,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        readyMessages:new[]{"OURS typed queued accuracy: normal instant134 uses1032; single-client PvP: ?duelbot wait, select DuelBot, /duel, then normal134. Incoming97 modifier/expiry is headless-covered; native pending helper cast control. ?combatdepth specialattack 134 is read-only paired seed731, no live RNG/HP override. 314/33 viral diagnostic is NOT normal queued viral proof. See docs/ABILITY-TYPED-ATTACK.md."});
}
