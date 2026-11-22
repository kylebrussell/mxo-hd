using hds;
using HeadlessClient;
using static NativeQa.Modes.AwakenedStockStealthSpeedMode;

namespace NativeQa.Modes;

public sealed class AwakenedIsolatedSprintMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EndZ = 8000, ObserverX = 17200;
    private const ushort Sprint = 184, Spy = CombatStyles.SpyStyle, Sneak = 1111, Invisibility = 1116;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Sprint, Spy, 151, 175 }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-awakened-isolated-sprint";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            $"rotation={MobAggro.YawTowards(X,Z,X,EndZ)},innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET x={ObserverX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, Sprint },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "184,1111,1116", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "184" },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, complete prerequisites. CSR ({X},{Y},{Z}) faces the clear street towards z={EndZ}. QAFriend ({ObserverX},{Y},{Z}) observes from the side. Both stands and the path are verified outdoor NAV floor 400. No Hyper-Speed, world targeting or bag clicks.",
            "Use slot 1 Spy first. Page 2 also has Sneak in slot 2, Sprint in slot 3 and Invisibility Belt in slot 4. Use the printed SPRINT HOME command before each run. Keep the same movement direction, run setting and held time, about 2 seconds. Save the first movement and stop positions.",
            "Normal pair: use Sprint, run once, toggle Sprint off, return to the stand and run again. Each on costs 25 IS with +15 on source 184 stats 115/877/878 and movement scale 0.15. Each off removes all three, clears the scale and starts 5 s reuse.",
            "Sneak pair: after Sprint is ready, turn it on, then use Sneak. Run with Sneak and Sprint. Turn off Sprint but stay in Sneak, return and repeat. Turn Sneak off before the next pair. The +15 scale and 25 IS per five-second drain should preserve Sneak until you toggle it off.",
            "Invisibility pair: turn on Sprint, then use the Belt. Wait for the stock 3 s cast and do the same two runs without ending Invisibility between them. Observe a 25 IS drain and verify the Belt's 115 -300 remains after Sprint off. Compare the Sprint-on and Sprint-off distance within each stance. Expect about +15% at matched duration. No max [VITALS] or [PVP] change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X, EndZ), (ObserverX, Z) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor Sprint stand ({x},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EndZ), "Continuous outdoor sprint strip");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home), "Sprint home polygon");
        byte yaw = MobAggro.YawTowards(X, Z, X, EndZ);
        Console.WriteLine($"SPRINT HOME: ?tp {X} {Y} {Z} {home.sector:x} {home.region:x} {home.polygon:x} - {yaw:x2}");

        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(!ranks.ContainsKey(12) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => ranks.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                ranks.TryGetValue(need.Key, out var rank) && rank >= need.Value)), "Standalone Sprint profile fits with valid stance prerequisites");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        ushort view = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;
        actor.SendRpc(Convert.FromHexString(SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        await CompareAsync(null, 0, 0);
        await CompareAsync(SneakUse, Sneak, Stealth.Sneak);
        await CompareAsync(BeltUse, Invisibility, Stealth.Invisible);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Standalone Sprint framing");
        return Array.Empty<byte>();

        async Task CompareAsync(string? stanceRequest, ushort stance, ushort bits)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int observerAt = observer.World.ViewSnapshot().Count;
            actor.SendRpc(Convert.FromHexString(SprintUse));
            foreach (ushort stat in new ushort[] { 115, 877, 878 })
                await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => StockAbilityProfile.Bonus(frame, Sprint, stat, 15, 0, false),
                    $"Sprint adds stat {stat}");
            await AwakenedHyperSprintMode.Observed(observer, view, observerAt, .15f, 0x0400);
            if (stanceRequest != null)
            {
                actor.SendRpc(Convert.FromHexString(stanceRequest));
                await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame =>
                    frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(stance, true)), $"The stock stance {stance} starts");
            }
            int logAt = server.DebugLog().Length;
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal),
                $"Sprint drains in stance {stance}", TimeSpan.FromSeconds(8));

            int stopAt = actor.World.RpcSnapshot().Count;
            observerAt = observer.World.ViewSnapshot().Count;
            actor.SendRpc(Convert.FromHexString(SprintUse));
            foreach (ushort stat in new ushort[] { 115, 877, 878 })
                await StockAbilityProfile.ObserveAsync(actor, stopAt, frame => StockAbilityProfile.Bonus(frame, Sprint, stat, 15, 0, true),
                    $"Sprint removes stat {stat}");
            await AwakenedHyperSprintMode.Observed(observer, view, observerAt, 0, bits);
            if (stanceRequest != null)
            {
                byte[] stanceOff = AbilityBuffs.BuildExclusive(stance, false);
                Check.That(!actor.World.RpcSnapshot().Skip(rpcAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(stanceOff)),
                    "Sprint stop and drain preserve the stock stance");
                actor.SendRpc(Convert.FromHexString(stanceRequest));
                await StockAbilityProfile.ObserveAsync(actor, stopAt, frame => frame.Bytes.AsSpan().SequenceEqual(stanceOff),
                    $"The stock stance {stance} switches off when requested");
            }
            byte[] ready = AbilityCooldowns.BuildTimer(Sprint, AbilityCooldowns.OwnTimer, 5, false);
            await StockAbilityProfile.ObserveAsync(actor, stopAt, frame => frame.Bytes.AsSpan().SequenceEqual(ready), "Sprint's five-second reuse ends");
        }
    }
}
