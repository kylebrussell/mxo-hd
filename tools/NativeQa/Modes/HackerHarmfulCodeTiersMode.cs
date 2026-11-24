using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerHarmfulCodeTiersMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    internal const int Resistance = 75;
    private const ushort Style = CombatStyles.HackerStyle, BaseCode = 53, Code2 = 238, Code3 = 415, Cannon2 = 331, EmptyMind = 8;
    private static readonly ushort[] Hotbar = { Style, BaseCode, Code2, Code3, Cannon2 };
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();
    // Retained AfterWhoruNeo proxy_020, lines 48027–48030. Only the target view and spawn are rebound.
    private const string StockCannon = "80b94b010400a90000000000000000000000000000000000000000000000004000f95541086b0000000000";
    private static readonly (string Handle, uint Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 300, 2), ("QAFar", 6112, X + 125, Z + 300, 2),
        ("QAFriend", 6111, X + 150, Z, 1), ("QAOther", 6113, X, Z + 2001, 2)
    };

    public override string Name => "ability-parity-hacker-harmful-code-tiers";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=3000,healthM=3000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Z + 300)},innerStrC=700,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Defender.Select((id, i) =>
            $"(6112,{i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "331", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = 97 },
        readyMessages: new[]
        {
            "Level 50, 97/100 memory. Stay at CSR (17000,495,6400) facing QAEnemy (17000,495,6700). Verified outdoor NAV: QAFar (17125,495,6700), QAFriend (17150,495,6400), QAOther (17000,495,8401).",
            "Page 2 is Hacker Style, Harmful Code tiers 1–3, then Logic Cannon 2.0. Select Hacker Style. Try slots 4–5 without a target, with /target QAFriend, and with /target QAOther. Expect reason 24 or 34 if the client sends an activation. No cast, debit, damage or reuse on those refusals.",
            "Use /target QAEnemy and slot 4 Harmful Code 3.0. Keep the first full outgoing 415 request. Expect 4 s cast, 8 s own and 4 s family 682 reuse, 22 IS paid after Stand Off, and [PVP] lands on QAEnemy for 75. Only QAEnemy takes a hit. Optional lower-tier controls on slots 2–3 deal 17 and 48. Wait for family reuse between Harmful Code tiers.",
            """After 415's own reuse, use /target QAFar and helper request {"actor":"QAFar","action":"aura-resist"}. Expect [MODIFIER] char=6112 source=8 stat=157 +75 for 20 s. Use slot 4 again. Expect [TYPED-RESISTANCE OURS] 415 type2 total75 raw75 final61, with the same cost and timers.""",
            "For the 331 retest skip the earlier Harmful Code casts. Use /target QAEnemy and slot 5 Logic Cannon 2.0 at rest. Expect 8 s, 26 IS, 15 s own and 15 s family 684 reuse. Authored fixed base 29 plus +46 influence gives 42 damage to QAEnemy only.",
            """When reuse clears, /target QAFar. Send {"actor":"QAFar","action":"aura-resist"} and verify Empty Mind +75 stat157. Immediately cast slot 5. Expect [TYPED-RESISTANCE OURS] 331 type2 total75 raw42 final34, same cost, both timers and no splash. The cast and contest draws are independent of this damage value.""",
            "Fixture aids are seeded full profiles and HP, fixed disposable viral hit draws and stable IS. The authored base replaces the generic random fallback. The archived Corrupted hit had different signed values; its original scaling cannot be inferred from the single known hit."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Harmful Code tiers need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Harmful Code outdoor NAV and sight ({x},{Y},{z})");

        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        foreach (var stand in Stands)
            Console.WriteLine($"READY {stand.Handle} {InvisibilityBeltWatcherMode.Warp(stand.X, Y, stand.Z, 128, inside: false)}");
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing Harmful Code fixture server");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) == 97 && 97 <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "All stored ranked requirements fit the 97-memory Harmful Code profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAFriend");
        var far = await actor.WaitForViewNamedAsync("QAOther");
        await actor.WaitForViewNamedAsync("QAFar");

        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Style, 172, 27, 0, false), "Hacker Style adds +27 Viral Damage");
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, Cannon2, StockCannon, target, ally);
        var rangeCursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockCannon, DuelPackets.Handle(far.ViewId, far.SpawnId)));
        await StockAbilityProfile.ObserveAsync(actor, rangeCursor, packet => AbilityRefusals.TryDecode(packet.Bytes, out var refusal) &&
            refusal.AbilityID == Cannon2 && refusal.ErrorID == AbilityRefusals.TargetOutOfRange, "Logic Cannon rejects the 20.01 m target");

        // Keep the exact 53 and 331 stock controls. No full 415 request has been recovered.
        await CastAsync(BaseCode, HarmfulCodeMode.StockViralUse, 4, 8, AbilityCooldowns.VirusDamage1, 4, 17);
        await CastAsync(Cannon2, StockCannon, 8, 15, AbilityCooldowns.LogicCannon, 15, 42);
        byte[] ownEnd = AbilityCooldowns.BuildTimer(Cannon2, AbilityCooldowns.OwnTimer, 15, false);
        byte[] familyEnd = AbilityCooldowns.BuildTimer(AbilityCooldowns.LogicCannon, AbilityCooldowns.GroupTimer, 15, false);
        await actor.WaitUntilAsync(() =>
        {
            var frames = actor.World.RpcSnapshot();
            return frames.Any(frame => frame.Bytes.AsSpan().SequenceEqual(ownEnd)) &&
                frames.Any(frame => frame.Bytes.AsSpan().SequenceEqual(familyEnd));
        }, "Both 15-second Logic Cannon timers finish", TimeSpan.FromSeconds(20));
        var defended = context.Helpers["QAFar"];
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(defended, Resistance);
        int logAt = server.DebugLog().Length;
        await CastAsync(Cannon2, StockCannon, 8, 15, AbilityCooldowns.LogicCannon, 15, 34, defended, 8);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("331 type2 total75 raw42 final34", StringComparison.Ordinal),
            "Logic Cannon consumes the recipient's current viral resistance", HeadlessPlayer.DefaultTimeout);
        Console.WriteLine("LOGIC CANNON TWO PASS: fixed authored damage, controlled resistance, selected delivery and own/shared reuse.");
        Check.Equal(2, server.DebugLog().Split('\n').Count(line =>
            line.Contains("[ABILITY-COST] char=110 ability=331 paid=26", StringComparison.Ordinal)),
            "Only the two admitted Logic Cannon requests charge 26 IS");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count),
            "Harmful Code tier controls keep transport framing");
        return Array.Empty<byte>();

        async Task CastAsync(ushort id, string stock, float cast, float own, ushort family, float familySeconds, int damage, HeadlessPlayer? recipient = null, int resisted = 0)
        {
            int at = actor.World.RpcSnapshot().Count;
            var cursors = context.Helpers.Values.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);
            var enemy = recipient ?? context.Helpers["QAEnemy"];
            var view = await actor.WaitForViewNamedAsync(enemy.Handle);
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, DuelPackets.Handle(view.ViewId, view.SpawnId)));
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            await EvidenceRetestWire.TimerAsync(actor, at, id, AbilityCooldowns.OwnTimer, own);
            await EvidenceRetestWire.TimerAsync(actor, at, family, AbilityCooldowns.GroupTimer, familySeconds);
            byte[] expected = AbilityMessages.Damage(false, id, actor.Handle, enemy.Handle, damage, resisted);
            await StockAbilityProfile.ObserveAsync(enemy, cursors[enemy], packet => packet.Bytes.AsSpan().SequenceEqual(expected),
                $"Selected hostile receives damage {damage}, resisted {resisted}");
            foreach (var player in context.Helpers.Values.Where(player => player != enemy))
            {
                byte[] prefix = AbilityMessages.Damage(false, id, actor.Handle, player.Handle, 0, 0)[..^8];
                Check.That(!player.World.RpcSnapshot().Skip(cursors[player]).Any(packet => packet.Bytes.AsSpan().StartsWith(prefix)),
                    $"{player.Handle} received no unselected damage from {id}");
            }
        }
    }
}
