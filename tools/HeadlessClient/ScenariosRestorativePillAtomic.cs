using System.Text.RegularExpressions;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const string RestorativePillRefusal = "This item could not be used. Your item was kept.";
    public static string RestorativePillConstraint(uint character) =>
        "SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION'; " +
        $"ALTER TABLE inventory ADD CONSTRAINT qa_refuse_restorative_dose " +
        $"CHECK (charId<>{character} OR slot NOT IN (5,7) OR count>=2)";

    private static async Task RestorativePillAtomic(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("RestorativePill")
        {
            X = 17000, Y = 495, Z = 2300, Level = 50, Health = 100, MaxHealth = 2000,
            Items = new[] { (5, 2399u, 2), (6, 2399u, 1), (7, 39410u, 2), (8, 39410u, 1) }
        });
        await context.Database.ExecuteAsync(RestorativePillConstraint(actor.CharId));
        await actor.EnterWorldAsync();
        await ProveRestorativePillAtomicAsync(actor, context.Database, context.Server);
    }

    public static async Task ProveRestorativePillAtomicAsync(HeadlessPlayer actor, ThrowawayDatabase db, ServerInstance server)
    {
        string healTimer = Timer(2399, 10);
        string healFamily = Timer(AbilityCooldowns.HealPill, 10);
        string antibioticTimer = Timer(39410, 8);
        string antibioticFamily = Timer(AbilityCooldowns.AntibioticPill, 8);
        await Refuse("80 c6 05 00", "80 ac be 00", 5, 2399, healTimer, healFamily);
        Check.That(!server.DebugLog().Contains("[ABILITY] Healed ", StringComparison.Ordinal),
            "The failed Health Pill does not heal");
        await Refuse("80 c6 07 00", "80 ac a6 02", 7, 39410, antibioticTimer, antibioticFamily);
        Check.Equal(0, actor.Received(ModPattern(678,678)).Count, "The failed Antibiotic starts no sweep");

        // The two retries use the other native item sender and a different slot of the same GOID.
        // Both must be admitted immediately despite the failed cast's provisional family/item timers.
        CastSweepItem(actor, 190, 6);
        await actor.WaitForRpcAsync("80 ac be 00");
        await actor.WaitForRpcAsync(healTimer);
        await actor.WaitForRpcAsync(healFamily);
        await actor.WaitForRpcAsync("5f 06 00 01");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[ABILITY] Healed ", StringComparison.Ordinal),
            "Health Pill heals after one committed dose", TimeSpan.FromSeconds(5));
        var heal = Regex.Match(server.DebugLog(), @"\[ABILITY\] Healed (\d+) -> (\d+)");
        Check.That(heal.Success, "The health delta is logged");
        Check.Equal(30, int.Parse(heal.Groups[2].Value) - int.Parse(heal.Groups[1].Value), "The stock Health Pill heals 30");

        CastSweepItem(actor, 678, 8);
        await actor.WaitForRpcAsync("80 ac a6 02");
        await actor.WaitForRpcAsync(antibioticTimer);
        await actor.WaitForRpcAsync(antibioticFamily);
        await actor.WaitForRpcAsync("5f 08 00 01");
        await actor.WaitForRpcAsync(ModPattern(678,678));
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND slot IN (6,8)"),
            "Exactly one dose was committed for each accepted cast");
        Check.Equal(2L, await Count(5, 2399), "The original Health Pill stack stays intact");
        Check.Equal(2L, await Count(7, 39410), "The original Antibiotic stack stays intact");
        Check.Equal(0, actor.World.Problems.Count, "Cast, refusal, timers, inventory and effect decode");

        async Task Refuse(string request, string cast, int slot, uint goid, string ownTimer, string familyTimer)
        {
            actor.SendRpc(request);
            await actor.WaitForRpcAsync(cast);
            var notice = await actor.WaitForTextAsync(RestorativePillRefusal);
            Check.Equal((byte)0xc7, notice.Bytes[1], "The refusal uses a stock-visible notice type");
            Check.Equal(2L, await Count(slot, goid), "A rejected inventory update leaves the exact stack");
            Check.Equal(0, actor.Received($"69 01 {slot:x2} 00 00 00").Count, "No false stack update");
            Check.Equal(0, actor.Received($"5f {slot:x2} 00 01").Count, "No false bag deletion");
            Check.Equal(0, actor.Received(ownTimer).Count, "No item reuse for an uncommitted dose");
            Check.Equal(0, actor.Received(familyTimer).Count, "No family reuse for an uncommitted dose");
        }

        Task<long> Count(int slot, uint goid) =>
            db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={actor.CharId} AND slot={slot} AND goid={goid}");
        static string Timer(ushort id, float seconds) =>
            WireFormat.ToHex(AbilityCooldowns.BuildTimer(id, AbilityCooldowns.GroupTimer, seconds, true));
    }
}
