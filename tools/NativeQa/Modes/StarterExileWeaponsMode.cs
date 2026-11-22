using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterExileWeaponsMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    // Complete PR746 stock frames. Only the current receiver-local view/spawn is substituted.
    private static readonly (ushort Id, string Capture)[] StockSpecials =
    {
        (207, "80b9cf00040002000000000000000000000000000000000000000000cf000000006b0000086b0000000000"),
        (1022, "80b9fe03040002000000000000000000000000000000000000000000fe030000006b0000086b0000000000"),
        (208, "80b9d000040002000000000000000000000000000000000000000000d0000000006b0000086b0000000000"),
        (1024, "80b9000404000200000000000000000000000000000000000000000000040000006b0000086b0000000000"),
    };
    private static readonly ushort[] Specials = StockSpecials.Select(sample => sample.Id).ToArray();
    private static readonly ushort[] Loadout = new ushort[] { 2, 17 }.Concat(Specials)
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-exile-weapon-requests";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND (slot<97 OR slot IN (105,107)); " +
                "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,2504,1,4,0),(110,43035,2,4,0),(110,2505,3,4,0),(110,43033,4,4,0)"
        }, DevCommands: true, hotbarAbilities: Specials, hotbarWithdraw: true, testMobFixture: "awakened-power-shot-cap.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "207,1022,208,1024", ["NativeCloseableRows"] = 4 },
        readyMessages: new[]
        {
            $"Level {Level}. Verified outdoor NAV: CSR ({X},{Y},{Z}) faces the unarmed Melee Dummy at ({DummyX},{Y},{Z}). Page 2 slots 1–4 are Wooden Stake, Enhanced Wooden Stake, Silver Bullet and Enhanced Silver Bullet. Slot 5 is Withdraw. The corresponding real bag items are in slots 1–4.",
            "Repeat the PR746 stock admission sample. At rest, select Melee Dummy and try page 2 slots 1–4. Expect the stock local combat-only gate, or reason 24 without [ABILITY-COST] if a request reaches the server. Open Close Combat via the dummy's right-click menu. With the current opponent selected, each eligible move should enter the next round for 20 IS with no own reuse. Capture the actual first requests and outcomes.",
            "With ?exileweakness none, the callback's raw damage is 10 for each landed move, before influence and resistance. Then ?exileweakness wood for slots 1/2 and ?exileweakness silver for slots 3/4. Their matching raw outcomes are 100/750. Record the queued or direct move and result. Natural round misses can happen. Let pending rounds finish or use ?cd stop between passes. No helper, no PvP and no max vitals changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor weapon test stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "The attack stand has a clear path to Melee Dummy");
        var db = context.OwnedDatabase!;
        Check.Equal(4L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 4 AND count=4"),
            "All four stock weapon items are in the disclosed bag slots");

        foreach (var id in Specials)
        {
            var fields = AbilityCatalog.Find(id);
            ushort matching = id == CombatSpecials.WoodenStake || id == CombatSpecials.WoodenStakePlus
                ? CombatSpecials.KilledOnlyByWood : CombatSpecials.KilledOnlyBySilver;
            Check.That(fields.Number("Ability/CastWhenUsed") == 3 && fields.Number("Ability/CastValidTarget") == 3,
                $"The shipped {id} definition declares an enemy interlock target");
            Check.Equal((ushort)10, CombatSpecials.ExileDamage(id, 0), $"Neutral raw damage for {id}");
            Check.Equal((ushort)(id == CombatSpecials.WoodenStakePlus || id == CombatSpecials.SilverBulletPlus ? 750 : 100),
                CombatSpecials.ExileDamage(id, matching), $"Matching raw damage for {id}");
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "The stock login has all four loaded actions");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        foreach (var (weakness, expected) in new[] { ("wood", 464), ("silver", 463), ("none", 0) })
        {
            actor.SendChat($"?exileweakness {weakness}");
            await actor.WaitForTextAsync($"Melee Dummy now has Exile weakness {expected}");
        }
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        foreach (var sample in StockSpecials)
            await StarterHackerControlTargetMode.RefusedAsync(actor, sample.Id,
                StarterHackerControlTargetMode.WithHandle(sample.Capture, handle));
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
            WireFormat.FromHex($"40 {hex} 01 6b"));
        await actor.WaitForRpcAsync($"40 {hex} 00 6b");
        await actor.WaitForRpcAsync("47");
        foreach (var sample in StockSpecials)
        {
            await StarterHackerControlTargetMode.RefusedAsync(actor, sample.Id,
                StarterHackerControlTargetMode.WithHandle(sample.Capture, handle ^ 0x10000));
            int from = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, handle));
            string name = AbilityCatalog.Find(sample.Id).Name;
            await actor.WaitUntilAsync(() =>
            {
                string log = server.DebugLog()[from..];
                int queued = log.IndexOf($"[COMBAT] {name} is the next move", StringComparison.Ordinal);
                return queued >= 0 && log[queued..].Contains("[COMBAT] Round ", StringComparison.Ordinal);
            }, $"Stock {sample.Id} queues for the current opponent and reaches the ordinary round", HeadlessPlayer.DefaultTimeout);
        }
        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended.");
        Check.Equal(0, actor.World.Problems.Count, "Stock entry and fixture proof frame cleanly");
        return Array.Empty<byte>();
    }
}
