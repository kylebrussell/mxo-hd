using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedStockStealthSpeedMode : NativeQaMode
{
    private const int Level = 50, X = 24800, Y = 495, Z = -1498;
    private const ushort Speed = 12, Sprint = 184, Spy = CombatStyles.SpyStyle, Sneak = 1111, Invisibility = 1116;
    // Full stock Spy Style and Belt from PR538 (PacketLog 2660 and 2879–2880), and stock Sneak from PR530.
    private const string SpyUse = "80b95804000000000000000000000000000000000000000000000000585cbe0001000000086b0000000000";
    private const string BeltUse = "80b95c0400000000000000000000000000000000000000000000000064f11a0090b7433d08280000000000";
    private const string SneakUse = "80b95704000000000000000000000000000000000000000000000000585cc20001000000086b0000000000";
    // Full PR658 replay forms.
    private const string SpeedUse = "80b90c00000000000000000000000000000000000000000000000000000015000000071c086b0000000000";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Speed, Sprint, Spy, 151, 175).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-awakened-stock-stealth-speed";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            $"rotation={MobAggro.YawTowards(X,Z,X,-3247)},innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
        "UPDATE characters SET x=25100,y=495,z=-1798,alignment=1,pvpflag=0 WHERE charId=6111",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, Speed, Sprint },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "12,184", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Full {Memory}/{AbilityMemory.ForLevel(Level)} stock memory for both Awakened buffs, Sneak and the Invisibility Belt. Level {Level} makes both real stance prerequisites available. CSR ({X},{Y},{Z}) faces the open street. QAFriend (25100,495,-1798) is friendly, clear of the ice FX. Page 2 is Spy/Sneak/Speed/Sprint/Belt. Nothing to click in the bag or target in world.",
            "Press Spy once. Take three two-second same-direction runs: normal with both speed buffs off, then stock Sneak, then stock Belt after turning Sneak off. Each time return to the same stand and use the same run/walk setting. For Sneak record stock 1111, stat 115 -200 and movement action. For Belt wait for the real 3s cast, 50 IS and [STEALTH] 1116 on, stat 115 -300 for 130s. Stop the Belt after sampling and allow its 30s own reuse to finish.",
            "At rest press Speed and Sprint. Take three more same-direction runs: normal, then stock Sneak, then stock Belt with Sneak off. [MODIFIER] source 12 stats 115/+45, 877/+12, 878/+12 for 600s; source 184 stats 115/877/878 +15. Observe a 25 IS drain while each stance stays on. Keep the first movement and first stop positions and report the held time. This comparison checks whether ordinary stock stance activation changes the gait/pace observed with the dev stances in PR662.",
            "End the Belt, toggle Sprint off and type ?dismissbuff 12. Expect independent removals and 5s Sprint reuse. No maximum [VITALS] or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=40 AND goid=460 AND count=1"),
            "The stock Belt request uses the seeded bag slot");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Loadout.All(id => ranks.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Both real stance grants and speed abilities have their complete loaded prerequisites");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await observer.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(Convert.FromHexString(SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int active = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(SpeedUse));
        await ModifierAsync(active, Speed, 115, 45, 600, false);
        actor.SendRpc(Convert.FromHexString(SprintUse));
        await ModifierAsync(active, Sprint, 115, 15, 0, false);

        int stance = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(SneakUse));
        await ModifierAsync(stance, Sneak, 115, -200, 0, false);
        int logAt = server.DebugLog().Length;
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal),
            "A Sprint drain occurs while ordinary Sneak is active", TimeSpan.FromSeconds(8));
        Check.That(!actor.World.RpcSnapshot().Skip(stance).Any(packet => StockAbilityProfile.Bonus(packet, Sneak, 115, -200, 0, true)),
            "Sprint drain preserves stock Sneak");

        int belt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(BeltUse));
        await EvidenceRetestWire.CastBarAsync(actor, Invisibility, 3);
        await ModifierAsync(belt, Sneak, 115, -200, 0, true);
        await ModifierAsync(belt, Invisibility, 115, -300, 130, false);
        await EvidenceRetestWire.TimerAsync(actor, belt, Invisibility, AbilityCooldowns.OwnTimer, 30);
        logAt = server.DebugLog().Length;
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal),
            "A Sprint drain occurs while ordinary Invisibility is active", TimeSpan.FromSeconds(8));
        Check.That(!actor.World.RpcSnapshot().Skip(stance).Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) &&
            record!.AddRemoveType == 1 && record.SourceId is Speed or Sprint or Invisibility), "Both ordinary stances keep the speed records");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stock stealth and speed framing");
        return Array.Empty<byte>();

        Task<RpcRecord> ModifierAsync(int from, ushort ability, ushort stat, int value, float duration, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, from, packet => StockAbilityProfile.Bonus(packet, ability, stat, value, duration, remove),
                $"Source {ability} stat {stat} value {value}, remove={remove}");
    }
}
