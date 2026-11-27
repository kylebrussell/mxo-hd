using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task HealthBoostAntiStunAtomic(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("SpecializedPills")
        {
            X = 17000, Y = 495, Z = 2300, Level = 50, Health = 350, MaxHealth = 500,
            Items = new[] { (4, 39411u, 1), (5, 39412u, 2), (6, 39412u, 1), (7, 42763u, 2), (8, 42763u, 1) }
        });
        await context.Database.ExecuteAsync(RestorativePillConstraint(actor.CharId));
        await actor.EnterWorldAsync();
        await ProveHealthBoostAntiStunAtomicAsync(actor, context.Database);
    }

    public static async Task ProveHealthBoostAntiStunAtomicAsync(HeadlessPlayer actor, ThrowawayDatabase db)
    {
        actor.SendRpc("80 c6 04 00");
        await actor.WaitForRpcAsync("80 ac a0 02");
        await actor.WaitForRpcAsync(ModPattern(672, 106));
        await actor.WaitForRpcAsync("5f 04 00 01");
        await actor.WaitForRpcAsync(OwnTimer(672, 30));

        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("80 ac bc 00");
        await RefusedDose(5, 39412);
        Check.Equal(0, actor.Received(ModPattern(188, 106)).Count, "Failed dose installs no Health Boost 2.0");
        Check.Equal(0, actor.Received(ModPattern(672, 106, true)).Count, "Failed dose preserves the active Health Boost 1.0");
        Check.Equal(0, actor.Received(OwnTimer(188, 30)).Count, "Failed dose starts no 30 s reuse");

        HealthBoostItemUse(actor, 188, 6);
        await actor.WaitForRpcAsync("80 ac bc 00");
        await actor.WaitForRpcAsync(ModPattern(672, 106, true));
        await actor.WaitForRpcAsync(ModPattern(188, 106));
        await actor.WaitForRpcAsync("5f 06 00 01");
        await actor.WaitForRpcAsync(OwnTimer(188, 30));

        actor.SendChat("?cd stun");
        await actor.WaitForRpcAsync(ModPattern(315, 102));
        await actor.WaitForTextAsync("Combat depth level 5 stun applied");
        actor.SendRpc("80 c6 07 00");
        await actor.WaitForRpcAsync("80 ac cb 03");
        await RefusedDose(7, 42763);
        string cure = ModPattern(315, 102, true);
        string antiStunOwn = OwnTimer(971, 900);
        string antiStunFamily = WireFormat.ToHex(AbilityCooldowns.BuildTimer(973, AbilityCooldowns.GroupTimer, 600, true));
        Check.Equal(0, actor.Received(cure).Count, "Failed Anti-Stun dose keeps Stun in effect");
        Check.Equal(0, actor.Received(antiStunOwn).Count, "Failed Anti-Stun dose starts no own reuse");
        Check.Equal(0, actor.Received(antiStunFamily).Count, "Failed Anti-Stun dose starts no family reuse");

        AntiStunItemUse(actor, 971, 8);
        await actor.WaitForRpcAsync("80 ac cb 03");
        await actor.WaitForRpcAsync("5f 08 00 01");
        await actor.WaitForRpcAsync(antiStunOwn);
        await actor.WaitForRpcAsync(antiStunFamily);
        await actor.WaitForRpcAsync(cure);
        await actor.WaitUntilAsync(() => actor.Received(ModPattern(734, 681)).Count >= 2,
            "Successful cure refreshes immunity", TimeSpan.FromSeconds(5));
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND slot IN (4,6,8)"),
            "Each successful cast consumes its exact single dose");
        Check.Equal(0, actor.World.Problems.Count, "The refusal, buffs and reuse records decode");

        async Task RefusedDose(int slot, uint goid)
        {
            var message = await actor.WaitForTextAsync(RestorativePillRefusal);
            Check.Equal((byte)0xc7, message.Bytes[1], "The refusal uses visible chat");
            Check.Equal(2L, await db.LongAsync($"SELECT count FROM inventory WHERE charId={actor.CharId} AND slot={slot} AND goid={goid}"),
                "A rejected spend retains both original doses");
            Check.Equal(0, actor.Received($"69 01 {slot:x2} 00 00 00").Count, "A rejected spend emits no stack update");
            Check.Equal(0, actor.Received($"5f {slot:x2} 00 01").Count, "A rejected spend emits no bag delete");
        }

        static string OwnTimer(ushort ability, float seconds) =>
            WireFormat.ToHex(AbilityCooldowns.BuildTimer(ability, AbilityCooldowns.OwnTimer, seconds, true));
    }
}
