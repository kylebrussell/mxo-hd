using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CoderPetsPvpMode : NativeQaMode
{
    public override string Name => "coder-pets-pvp";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 594, 616 }, PvpOpponentsFixture.X, PvpOpponentsFixture.Z,
        Array.Empty<string>(), Array.Empty<string>(), new[] { PvpOpponentsFixture.NativeSql },
        DevCommands: true,
        environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0"
        },
        readyMessages: new[]
        {
            "CSR110 only. Use Actions to summon Remote Proxy 5.0 and Logic Daemon 4.0 in turn. Check Stay, Follow and Defensive assist on a Simulacra Dummy from ?pet dummy.",
            "Same-org QAFriend and opposing-org QAEnemy both enter flagged. Only QAEnemy qualifies for ordinary player interlock. If the pet Attack button sends a player selection, it must never redirect to the dummy.",
            "See docs/NATIVEQA-CODER-PETS-PVP.md for the order of checks and expected observations."
        });

    // The existing stock world-entry helper bootstrap sends no combat or simulacra commands.
    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) =>
        new PvpOpponentsMode().AfterStartAsync(context);
}
