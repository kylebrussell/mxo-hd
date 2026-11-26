using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ClientActionTutorialMode : NativeQaMode
{
    public override string Name => "client-action-tutorial";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2 }, 7600, -16150, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110",
            "DELETE FROM char_missions WHERE charId=110 AND missionId=986",
            "UPDATE characters SET district='tutorial',districtId=0,y=3995 WHERE charId=110"
        },
        Array.Empty<string>(), loadAbilityAncestors: true,
        readyMessages: new[]
        {
            "Run this mode as a fresh fixture. Login opens the Tutorial directly at (7600,3995,-16150). It does not start in the Loading Area Equipment view.",
            "Use Skip Tutorial and confirm Yes. Expect [TUTORIAL] Skipped, the normal Loading Area transition, then 80 fa 01 00 00 00 99 00 00 00 01 and 81 07. CSR should load Richland hardline 153. Record whether the stock client sent 81 01 or used the 05 fallback. If normal Jack In needs a posted click, use the known client-local (745,570)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("An owned database is required.");
        Check.Equal(1L, await database.LongAsync("SELECT COUNT(*) FROM characters WHERE charId=110 AND districtId=0 " +
            "AND x=7600 AND y=3995 AND z=-16150"), "CSR logs directly into the Tutorial spawn");
        Check.Equal(0L, await database.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=110 AND missionId=986"),
            "The Tutorial starts without a completion receipt");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        await actor.WaitUntilAsync(() => context.ActualServer!.DebugLog().Contains("[TUTORIAL] Begins for charId 110"),
            "the Tutorial runtime at first login", TimeSpan.FromSeconds(15));
        actor.SendRpc("80 a6");
        actor.SendRpc("80 fc 01");
        await actor.WaitForRpcAsync("80 fd 00 00 00 00 01 00");
        actor.SendRpc("80 fe 01");
        await actor.WaitForRpcAsync("81 07");
        actor.SendRpc("81 08 00 00 00 00");
        await actor.WaitForLoadingAreaAsync();
        actor.SendRpc("81 01");
        await actor.WaitForRpcAsync("80 fa 01 00 00 00 99 00 00 00 01");
        await actor.WaitForRpcAsync("81 07");
        actor.SendRpc("81 08 00 00 00 00");
        await actor.WaitForRpcAsync("06 0e 00 01 00 00 00");
        actor.SendRpc("05");
        await actor.WaitUntilAsync(() => actor.World.CurrentOwnViewLife().OwnViewReady,
            "the Richland self spawn after skipping", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await database.LongAsync("SELECT districtId FROM characters WHERE charId=110"),
            "The direct tutorial fixture exits to Richland");
        Check.Equal(0, actor.World.Problems.Count, "The complete Tutorial exit decodes");
        return Array.Empty<byte>();
    }
}
