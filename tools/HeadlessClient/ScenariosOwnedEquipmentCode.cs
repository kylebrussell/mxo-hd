using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CompileOwnedEquipmentCode(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("OwnedEquipmentCode")
        {
            X = 17000, Z = 2300, Level = 50, Info = 500, Items = new[] { (1, 1271u, 1) },
            OwnedLoadedAbilities = new[] { ((ushort)2, (ushort)1), ((ushort)277, (ushort)50) }
        });
        await context.Database.ExecuteAsync(
            $"UPDATE char_abilities SET slot=410 WHERE char_id={actor.CharId} AND slot=400; " +
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) " +
            $"VALUES ({actor.CharId},400,{2565 << 7},2,0)");
        await actor.EnterWorldAsync();
        await ProveOwnedEquipmentCodeAsync(actor, context.Database);
    }

    public static async Task ProveOwnedEquipmentCodeAsync(HeadlessPlayer actor, ThrowawayDatabase db)
    {
        const string compile = "60 05 0a 00 00 90 01 00 01 01";
        actor.SendRpc(compile);
        await actor.WaitForRpcAsync("61 01");
        await CheckDurableState(500, 2, 0);

        CompilerTool(actor, 1, activate: true);
        await actor.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 01");
        actor.SendRpc("60 04 00 00 00 90 01 00 01 01");
        await actor.WaitForRpcAsync("61 01");
        await CheckDurableState(500, 2, 0);

        actor.SendRpc(compile);
        await actor.WaitForRpcAsync("69 00 90 01 81 02 05 00 00 00");
        await actor.WaitForRpcAsync("5e ?? ?? 05 0a 00 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("61 00");
        await CheckDurableState(499, 1, 1);

        CompilerTool(actor, 1, activate: false);
        await actor.WaitForRpcAsync("80 bd 05 c9 03 00 00 00 00 00 00");
        await ClientActionEnterLoadingArea(actor);
        const string compileInLoadingArea = "60 00 00 00 00 90 01 00 6b 00";
        actor.SendRpc(compileInLoadingArea);
        await actor.WaitForRpcAsync("5f 90 01 00");
        await actor.WaitForRpcAsync("5e ?? ?? 05 0a 00 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("61 00");
        await CheckDurableState(498, 0, 2);

        actor.SendRpc(compileInLoadingArea);
        await actor.WaitForRpcAsync("61 01");
        await CheckDurableState(498, 0, 2);
        Check.Equal(0, actor.World.Problems.Count, "Both compile routes and their refusals stay framed");

        async Task CheckDurableState(long balance, long charges, long weapons)
        {
            Check.Equal(balance, await db.LongAsync($"SELECT cash FROM characters WHERE charId={actor.CharId}"),
                "Compile costs one Info only for each success");
            Check.Equal(charges, await db.LongAsync(
                $"SELECT COALESCE(SUM(level),0) FROM char_abilities WHERE char_id={actor.CharId} AND slot=400 AND ability_id={2565 << 7}"),
                "The exact ordinary source loses one charge on a success");
            Check.Equal(charges == 0 ? 0L : 1L, await db.LongAsync(
                $"SELECT COUNT(*) FROM char_abilities WHERE char_id={actor.CharId} AND slot=400"),
                "A depleted code is deleted and a live code preserves its slot");
            Check.Equal(weapons, await db.LongAsync(
                $"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND goid=2565 AND count=1"),
                "Each success materializes one unstacked Redpill Special");
        }
    }
}
