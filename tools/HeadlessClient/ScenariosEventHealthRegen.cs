using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private const ushort AnniversaryCookie = 1258, PatchersPill = 1263;

    private static async Task EventHealthRegenItemsUse(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("EventHealthRegen")
        {
            X = 17000, Y = 495, Z = 2300, Level = 50, Health = 100, MaxHealth = 1000,
            Items = new[] { (5, 46995u, 2), (6, 47013u, 1) }
        });
        await actor.EnterWorldAsync();
        var (cookieId, patcherId) = await ProveEventHealthRegenAsync(actor, context.Database);

        var expired = await actor.WaitForRpcAsync(RegenModifier(AnniversaryCookie, 75, 60, remove: true), TimeSpan.FromSeconds(67));
        Check.Equal(cookieId, BitConverter.ToUInt16(expired.Bytes, 4), "Natural expiry clears the exact Cookie modifier");
        Check.Equal(0, actor.Received(RegenModifier(PatchersPill, 30, 300, remove: true)).Count,
            "The Cookie expiry preserves the longer Patcher's Pill");
        actor.SendChat("?health 100");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 40 64 00");
        await RequireRegen(actor, 26, "Patcher alone after Cookie expiry");

        actor.SendChat("?die");
        var cleared = await actor.WaitForRpcAsync(RegenModifier(PatchersPill, 30, 300, remove: true));
        Check.Equal(patcherId, BitConverter.ToUInt16(cleared.Bytes, 4), "Death removes the surviving Patcher's Pill modifier");
        Check.Equal(0, actor.World.Problems.Count, "The item, effect, and cleanup records decode");
    }

    public static async Task<(ushort CookieId, ushort PatcherId)> ProveEventHealthRegenAsync(HeadlessPlayer actor, ThrowawayDatabase db)
    {
        await RequireRegen(actor, 20, "Ordinary regen at 1000 max HP");

        CastSweepItem(actor, PatchersPill, 5);
        var denied = await actor.WaitForRpcAsync($"80 ba {Le(PatchersPill)}");
        Check.That(AbilityRefusals.TryDecode(denied.Bytes, out var refusal) &&
            refusal.ErrorID == AbilityRefusals.RequiredItemCategory, "A mismatched item cannot activate the Patcher's Pill ability");
        Check.Equal(2L, await Count(5, 46995), "The wrong-ability request retains both Cookies");

        actor.SendRpc("80 c6 05 00");
        await RequireCast(AnniversaryCookie);
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        var cookie = await actor.WaitForRpcAsync(RegenModifier(AnniversaryCookie, 75, 60));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 27 00 00 04");
        await RequireRegen(actor, 35, "Cookie adds 75 percent to natural healing");
        Check.Equal(1L, await Count(5, 46995), "Only one Cookie is spent");

        CastSweepItem(actor, PatchersPill, 6);
        await RequireCast(PatchersPill);
        await actor.WaitForRpcAsync("5f 06 00 01");
        var patcher = await actor.WaitForRpcAsync(RegenModifier(PatchersPill, 30, 300));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 2a 00 00 04");
        await RequireRegen(actor, 41, "The two shipped modifiers add to 105 percent");
        Check.Equal(0L, await Count(6, 47013), "The single Patcher's Pill is spent");
        Check.Equal(0, actor.Received("80 bd 01").Count, "Neither item starts a separate reuse timer");
        Check.Equal(0, actor.World.Problems.Count, "The two successful item routes are readable");
        return (BitConverter.ToUInt16(cookie.Bytes, 4), BitConverter.ToUInt16(patcher.Bytes, 4));

        async Task RequireCast(ushort ability)
        {
            var start = await actor.WaitForRpcAsync($"80 ac {Le(ability)}");
            Check.Equal(1.5f, BitConverter.ToSingle(start.Bytes, 14), "The event consumable casts for 1.5 seconds");
        }

        Task<long> Count(ushort slot, uint goid) =>
            db.LongAsync($"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={actor.CharId} AND slot={slot} AND goid={goid}");
    }

    private static string RegenModifier(ushort source, uint value, float duration, bool remove = false) =>
        $"80 bc 15 00 ?? ?? 00 {Le((uint)source)} 70 00 {Le(value)} {(remove ? "01" : "00")} {WireFormat.ToHex(BitConverter.GetBytes(duration))}";

    private static async Task RequireRegen(HeadlessPlayer actor, int expected, string stage)
    {
        int start = actor.World.ViewSnapshot().Count;
        var values = new List<ushort>();
        await actor.WaitUntilAsync(() =>
        {
            values = ViewUpdateValues(actor, 2, "02 80 80 80 40", start);
            return values.Count >= 3;
        }, stage, TimeSpan.FromSeconds(9));
        Check.Equal(expected, values[1] - values[0], stage);
        Check.Equal(expected, values[2] - values[1], stage);
    }
}
