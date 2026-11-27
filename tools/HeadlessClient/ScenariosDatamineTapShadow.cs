using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    public static ushort[] DatamineShadowLoadout()
    {
        ushort[] roots = { 2, 17, CombatStyles.SpyStyle, 367 };
        return roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().Order().ToArray();
    }

    private static async Task DatamineTapShadow(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("DatamineShadow")
        {
            X = 17000, Y = 495, Z = 2300, Level = 50, InnerStrength = 1000, MaxInnerStrength = 1000,
            OwnedLoadedAbilities = DatamineShadowLoadout().Select(id => (id, (ushort)50)).ToArray(),
            Items = new[] { (5, 47276u, 1), (6, 596u, 1) }
        });
        await actor.EnterWorldAsync();
        await ProveDatamineTapShadowAsync(actor, context.Database, context.Server);
    }

    public static async Task ProveDatamineTapShadowAsync(HeadlessPlayer actor, ThrowawayDatabase db, ServerInstance server)
    {
        const ushort shadow = Stealth.MasterShadowAbility;
        string off = WireFormat.ToHex(AbilityBuffs.BuildExclusive(shadow, false));
        string on = WireFormat.ToHex(AbilityBuffs.BuildExclusive(shadow, true));
        string add = ModPattern(shadow, 681);
        string remove = ModPattern(shadow, 681, true);
        string reuse = WireFormat.ToHex(AbilityCooldowns.BuildTimer(shadow, AbilityCooldowns.OwnTimer, 30, true));

        actor.SendRpc(new AbilityUseRecord(CombatStyles.SpyStyle, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        byte[] cast = new AbilityUseRecord(shadow, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        actor.SendRpc(cast);
        await actor.WaitForRpcAsync("80 ac 5e 04");
        await actor.WaitForRpcAsync(on);
        var active = await actor.WaitForRpcAsync(add);
        var removed = await actor.WaitForRpcAsync(remove, TimeSpan.FromSeconds(8));
        Check.Equal(BitConverter.ToUInt16(active.Bytes, 4), BitConverter.ToUInt16(removed.Bytes, 4),
            "The Datamine tap ends the exact active Shadow modifier");
        await actor.WaitForRpcAsync(off);
        await actor.WaitForRpcAsync(reuse);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[STEALTH] {actor.Handle} 1118 off (carrying a Datamine tap)"),
            "The Datamine tap is the reason for termination", TimeSpan.FromSeconds(3));
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND slot=5 AND goid=47276"),
            "Ending Shadow does not consume the tap");

        actor.SendRpc("5c 05 00 01");
        await actor.WaitForRpcAsync("5f 05 00 01");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM world_luggables WHERE goid=47276 AND district=1"),
            "The ordinary inventory Drop places the tap on the departure floor");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND goid=47276"),
            "The tap is no longer carried");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND slot=6 AND goid=596"),
            "A different luggable remains in the bag as a negative control");
        await actor.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(shadow, AbilityCooldowns.OwnTimer, 30, false)),
            TimeSpan.FromSeconds(34));

        int reuseCount = actor.Received(reuse).Count;
        int offCount = actor.Received(off).Count;
        actor.SendRpc(cast);
        await actor.WaitForRpcAsync(on);
        await actor.WaitForRpcAsync(add);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"[SHADOW DRAIN] {actor.Handle} "),
            "Shadow has passed the tap check and reached its first 25 IS drain", TimeSpan.FromSeconds(9));
        Check.Equal(offCount, actor.Received(off).Count, "A ground tap and an unrelated held luggable keep Shadow active");
        Check.Equal(reuseCount, actor.Received(reuse).Count, "No termination timer on the control cast");
        actor.SendRpc(cast);
        await actor.WaitForRpcAsync(off);
        await actor.WaitForRpcAsync(reuse);
        Check.Equal(0, actor.World.Problems.Count, "The cast, automatic termination, item drop and retry decode");
    }
}
