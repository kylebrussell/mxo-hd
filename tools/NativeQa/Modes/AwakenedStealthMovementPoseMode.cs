using hds;
using HeadlessClient;
using static NativeQa.Modes.AwakenedStockStealthSpeedMode;

namespace NativeQa.Modes;

public sealed class AwakenedStealthMovementPoseMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EndZ = 8000, ObserverX = 17200;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = 1111, Belt = 1116;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Spy, 151, 175 }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";

    public override string Name => "ability-parity-awakened-stealth-movement-pose";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot=40 OR goid=460)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0," +
            "innerStrC=500,innerStrM=500,alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET x={ObserverX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111"
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy, Sneak },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "1111,1116", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR ({X},{Y},{Z}) faces +Z on verified outdoor NAV floor 400, with QAFriend ({ObserverX},{Y},{Z}). No bag clicks. Page 2 Spy, Sneak, Belt. Sprint and Speed are unloaded. Before each sample use {Home}.",
            "Pick Spy style. For each stance (Sneak first, then Belt) activate with the stock hotbar once. With the ability still active, enter ?stealth stand and run forward for 2 s. Return to the same stand. Enter ?stealth pose and run forward again for 2 s. Use the same run setting and save first movement and first stop frames.",
            "Expect [EMOTE] stance 09 for Sneak and 1a for Belt after ?stealth pose. Compare each pair. Note any outgoing 33 that clears the pose on movement. The active [MODIFIER] 115 -200/-300 stays identical. No MovementScale override, scripted displacement or damage. Clear the pose with ?stealth stand, then toggle off the stock ability before moving to the next pair."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X, EndZ), (ObserverX, Z) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verified outdoor movement stand ({x},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EndZ), "Verifier found the continuous outdoor strip");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && home.sector == 0x4e2 && home.region == 0x12 &&
            home.polygon == 4, "The printed return command matches the start polygon");

        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(!ranks.ContainsKey(12) && !ranks.ContainsKey(184) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => ranks.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete profile with neither movement boost");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        ushort observedView = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;
        actor.SendRpc(SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        await CompareAsync(Sneak, SneakUse, 9, -200, 0);
        await CompareAsync(Belt, BeltUse, 26, -300, 68);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stealth diagnostic framing");
        return Array.Empty<byte>();

        async Task CompareAsync(ushort ability, string request, byte stance, int pace, int seconds)
        {
            int begin = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await StockAbilityProfile.ObserveAsync(actor, begin,
                frame => StockAbilityProfile.Bonus(frame, ability, 115, pace, seconds, false), $"Stock {ability} sets its movement penalty");
            int own = actor.World.ViewSnapshot().Count;
            int seen = observer.World.ViewSnapshot().Count;
            actor.SendChat("?stealth pose");
            await actor.WaitForTextAsync($"Stealth pose {stance:x2} sent.");
            await EvidenceRetestWire.ViewSinceAsync(actor, own, 2, $"01 28 ?? 40 {stance:x2} 00 00 10");
            await EvidenceRetestWire.ViewSinceAsync(observer, seen, observedView, $"01 2a ?? 40 {stance:x2} 00 00 10");
            own = actor.World.ViewSnapshot().Count;
            actor.SendChat("?stealth stand");
            await actor.WaitForTextAsync("Stealth pose cleared.");
            await EvidenceRetestWire.ViewSinceAsync(actor, own, 2, "01 28 ?? 40 00 00 00 10");
            Check.That(!actor.World.RpcSnapshot().Skip(begin).Any(frame =>
                StockAbilityProfile.Bonus(frame, ability, 115, pace, seconds, true)), "Pose changes leave the penalty active");

            await Task.Delay(TimeSpan.FromSeconds(2.1));
            int off = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await StockAbilityProfile.ObserveAsync(actor, off,
                frame => StockAbilityProfile.Bonus(frame, ability, 115, pace, seconds, true), $"Stock {ability} removes its movement penalty");
        }
    }
}
