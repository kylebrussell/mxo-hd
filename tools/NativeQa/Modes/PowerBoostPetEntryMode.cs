using System.Diagnostics;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public class PowerBoostPetEntryMode : NativeQaMode
{
    private const int X = 97619, Y = 695, Z = 10761, CasterX = 97769, WatcherZ = 10911, Level = 18;
    private const ushort Coder = 1008, Proxy = 28, Power = 25;
    private const string NpcName = "Crossbones Tar", PetName = "Karate Simulacrum";
    // Complete PR603 Remote Proxy 2.0 and PR619 Power Boost self-use frames.
    private const string StockProxy = "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string StockBoost = "80b91900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string StockAggressive = "81 7c fe 05 01", StockPassive = "81 7c fe 05 02";
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Power).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private readonly bool useAggressiveStance;

    public PowerBoostPetEntryMode() : this(useAggressiveStance: false) { }

    protected PowerBoostPetEntryMode(bool useAggressiveStance)
    {
        this.useAggressiveStance = useAggressiveStance;
        Plan = BuildPlan(useAggressiveStance);
    }

    public override string Name => useAggressiveStance
        ? "ability-parity-power-boost-aggressive-entry"
        : "ability-parity-power-boost-pet-entry";
    public override NativeQaModePlan Plan { get; }

    private static NativeQaModePlan BuildPlan(bool aggressive) => new(Abilities, CasterX, Z,
        Array.Empty<string>(), new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            Stand(110, CasterX, Z), Stand(6111, X, WatcherZ),
            "UPDATE characters SET innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, loadAbilityAncestors: true, hotbarAbilities: new[] { Coder, Proxy, Power }, DevCommands: true,
        testMobFixture: "npc-evade-crossbones.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CoderPets.KarateProxyQa] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_MOB_CORPSE_SECONDS"] = "3", ["MXO_MOB_RESPAWN_SECONDS"] = "8"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "25", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[] { Setup }.Concat(aggressive ? new[]
        {
            "Select Crossbones Tar and click Aggressive in the Simulacra window. Save the full stock 81 7c. Expect 'Karate Simulacrum assists against Crossbones Tar' and one [PET ENTRY] force519=0, 100 vs 110, resisted shield 255->166. Press Passive within 3 s. Require one fresh meter update, no HP loss on either actor, and no repeat while Passive.",
            "Clear selection and use slot 3 (50 IS, 4 s cast, 8 s reuse). Select the same Tar and click Aggressive again. Expect force519=40, 140 vs 110, admitted with shield still 166 and reciprocal HP loss on the same NPC and pet. Click Passive. Wait at least 4 s: no later hits, meter updates or retaliation. Do not use Attack in either stage."
        } : new[]
        {
            "Select Crossbones Tar and click Attack in the Simulacra window. Retain the full stock 81 7b. At contact, [PET ENTRY] force519=0 force=100 evade=110 resisted shield=255->166. Press stock Stay within 3 s of that first line, before Attack retries. Require exactly one refusal and one meter update. Crossbones and the same pet keep their HP, and 46 is absent.",
            "Clear selection and press slot 3. Expect 50 IS, 4 s cast, 8 s reuse, +9 melee/ballistic for 300 s. Select the same Crossbones Tar and press pet Attack, then Stay once both hit directions appear. [PET ENTRY] force519=40 force=140 evade=110 admitted shield=166. No shield debit. Target HP falls and [PET RETALIATION] reduces the same pet's HP. QAFriend gets both actual view HP changes.",
            "With the buff still present, use ?weaken 1 and stock pet Attack to kill the Tar. Wait for a fresh view after eight seconds. The old order ceases and the fresh shield starts at ff. Remain in world until 300 seconds have passed from the Power Boost landing. With the same Karate pet, select the fresh Tar and click stock Attack once more. Expect one new force519=0 refusal and one 255->166 meter update, no HP damage. Press Stay within 3 s."
        }).ToArray());

    private static string Setup =>
        $"L{Level}, {Memory}/{AbilityMemory.ForLevel(Level)} with all prerequisites. CSR ({CasterX},{Y},{Z}) faces the shielded Crossbones Tar ({X},{Y},{Z}); QAFriend observes from ({X},{Y},{WatcherZ}). All stands and the Proxy spawn are on clear outdoor NAV. Page 2 slots 1 Coder, 2 Remote Proxy 2.0, 3 Power Boost. Use 1 then 2 with selection clear. The fixture selects a L7 Karate Simulacrum with 337 HP.";

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Power Boost entry requires a disposable server.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Power Boost entry requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (CasterX, Z), (CasterX + 150, Z), (X, WatcherZ) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && nav.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, x, Y, z, X, Y, Z), $"Clear outdoor NAV at ({x},{Y},{z})");
        }

        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var watchedNpc = await FindNpcAsync(observer);
        CheckShield(watchedNpc);
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank)
                && rank >= need.Value)), "All ranked prerequisites fit the legal stock loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await FindNpcAsync(actor);
        CheckShield(target);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await CastAsync(Proxy, StockProxy, 8, 650);
        var pet = await actor.WaitForViewNamedAsync(PetName);
        var watchedPet = await observer.WaitForViewNamedAsync(PetName);
        Check.That(pet.Goid == CoderPets.SimulacraGoId && pet.Attributes["Level"][0] == 7 &&
            BitConverter.ToUInt32(pet.Attributes["OwnerCharacterID"]) == actor.CharId, "Live owned L7 melee Proxy");
        await RefusedAsync(target, watchedNpc, watchedPet);

        await CastAsync(Power, StockBoost, 4, 600);
        var boostedFor = Stopwatch.StartNew();
        int hitsAt = observer.World.ViewSnapshot().Count;
        int pairAt = server.DebugLog().Length;
        Engage(target);
        await WaitLogAsync(pairAt, "force519=40 force=140 evade=110 admitted shield=166", "Power Boost admits the same pair");
        await observer.WaitUntilAsync(() => Health(observer, watchedNpc, hitsAt).Any(hp => hp < 337) &&
            Health(observer, watchedPet, hitsAt).Any(hp => hp < 337), "The observer sees reciprocal HP loss on this pair", TimeSpan.FromSeconds(24));
        await StopAsync();
        Check.Equal(0, ShieldUpdateCount(observer, watchedNpc, hitsAt), "Successful forced entry leaves the shield meter untouched");
        Check.That(server.DebugLog()[pairAt..].Contains("[PET RETALIATION] attacker=Crossbones Tar", StringComparison.Ordinal),
            "The same NPC actually returns damage to its admitted pet");
        if (useAggressiveStance)
        {
            Check.That(server.DebugLog()[pairAt..].Contains("Karate Simulacrum assists against Crossbones Tar", StringComparison.Ordinal),
                "The boosted pair uses automatic target acquisition");
            int passiveAt = server.DebugLog().Length;
            await Task.Delay(TimeSpan.FromSeconds(CoderPets.AttackPeriodSeconds + 1));
            string passiveLog = server.DebugLog()[passiveAt..];
            Check.That(!passiveLog.Contains("Karate Simulacrum hits Crossbones Tar", StringComparison.Ordinal) &&
                !passiveLog.Contains("[PET RETALIATION] attacker=Crossbones Tar", StringComparison.Ordinal),
                "Passive ends the already-admitted automatic pair");
            Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Aggressive and Passive view framing");
            Console.WriteLine("AGGRESSIVE PET ENTRY PASS: the ordinary stance respects Evade, admits the boosted pair, and Passive stops both attacks.");
            return Array.Empty<byte>();
        }

        int weakenAt = server.DebugLog().Length;
        actor.SendChat("?weaken 1");
        await WaitLogAsync(weakenAt, "[DEV] Weakened 1 NPCs to 1 health", "Only the original Tar is wounded for respawn");
        Engage(target);
        var fresh = await FindNpcAsync(actor, target.SpawnId);
        var watchedFresh = await FindNpcAsync(observer, watchedNpc.SpawnId);
        CheckShield(fresh);
        CheckShield(watchedFresh);
        Check.That(server.DebugLog()[weakenAt..].Contains("Karate Simulacrum hits Crossbones Tar", StringComparison.Ordinal),
            "The actual boosted pet killed the original NPC");

        // The native mode and this replay wait the real stored duration. No shortened buff policy is required.
        TimeSpan remaining = TimeSpan.FromSeconds(301) - boostedFor.Elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        await RefusedAsync(fresh, watchedFresh, watchedPet);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Simulacrum entry and view framing");
        Console.WriteLine("POWER BOOST PET ENTRY PASS: rejected before damage, reciprocal stock attack, unchanged shield, and expired bonus rejected on the fresh NPC.");
        return Array.Empty<byte>();

        void Engage(ViewCreation npc)
        {
            string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(npc.ViewId, npc.SpawnId)));
            string order = useAggressiveStance ? StockAggressive : $"81 7b fe 02 {handle}";
            actor.World.SendRpcs(WireFormat.FromHex($"81 51 {handle}"), WireFormat.FromHex(order));
        }

        async Task StopAsync()
        {
            int after = server.DebugLog().Length;
            actor.SendRpc(useAggressiveStance ? StockPassive : "81 7a fe 00");
            string order = useAggressiveStance ? "command Mode target 00000000 value 2" : "command Stay";
            await WaitLogAsync(after, order, "The stock stop order reaches the ordinary owned pet");
        }

        async Task RefusedAsync(ViewCreation npc, ViewCreation observerNpc, ViewCreation observerPet)
        {
            int before = server.DebugLog().Length;
            int views = observer.World.ViewSnapshot().Count;
            Engage(npc);
            await WaitLogAsync(before, "force519=0 force=100 evade=110 resisted shield=255->166", "The unboosted pet entry is resisted");
            await StopAsync();
            await EvidenceRetestWire.ViewSinceAsync(observer, views, observerNpc.ViewId, "04 80 80 80 80 10 a6");
            if (useAggressiveStance) await Task.Delay(TimeSpan.FromSeconds(CoderPets.AttackPeriodSeconds + 1));
            Check.Equal(1, ShieldUpdateCount(observer, observerNpc, views), "Exactly one fresh meter update after the refusal");
            Check.That(!Health(observer, observerNpc, views).Any() && !Health(observer, observerPet, views).Any(),
                "A refused attack sends no damage to either actor");
            string[] attempt = server.DebugLog()[before..].Split('\n');
            if (useAggressiveStance)
                Check.That(attempt.Any(line => line.Contains("Karate Simulacrum assists against Crossbones Tar", StringComparison.Ordinal)),
                    "The resisted pair uses automatic target acquisition");
            Check.Equal(1, attempt.Count(line => line.Contains("[PET ENTRY]", StringComparison.Ordinal)),
                "Exactly one entry refusal before the stock stop order");
            Check.That(!attempt.Any(line => line.Contains("[PET RETALIATION] attacker=Crossbones Tar", StringComparison.Ordinal)),
                "The target cannot retaliate against a pet whose entry was refused");
        }

        async Task CastAsync(ushort ability, string frame, int cast, ushort isAfter)
        {
            int rpcs = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(frame);
            await EvidenceRetestWire.CastBarAsync(actor, ability, cast);
            await EvidenceRetestWire.TimerAsync(actor, rpcs, ability, AbilityCooldowns.OwnTimer, 8);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(isAfter),
                $"The stock cast leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);
        }

        Task WaitLogAsync(int start, string text, string description) =>
            actor.WaitUntilAsync(() => server.DebugLog()[start..].Contains(text, StringComparison.Ordinal),
                description, HeadlessPlayer.DefaultTimeout);
    }

    private static string Stand(int charId, int x, int z) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={x},y={Y},z={z}," +
        $"rotation={MobAggro.YawTowards(x, z, X, Z)},healthC=800,healthM=800 WHERE charId={charId}";

    private static async Task<ViewCreation> FindNpcAsync(HeadlessPlayer player, ushort? previous = null)
    {
        ViewCreation? result = null;
        await player.WaitUntilAsync(() => (result = player.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == 599 && view.Name == NpcName && view.SpawnId != previous &&
            EngageFoesFixture.Position(view).SequenceEqual(new double[] { X, Y, Z }))) != null,
            "The captured Crossbones at its exact live stand", TimeSpan.FromSeconds(30));
        return result!;
    }

    private static void CheckShield(ViewCreation npc) =>
        Check.Equal(byte.MaxValue, npc.Attributes["EvadeShieldHealth"][0], "Full shield on the exact captured Tar view");

    private static int ShieldUpdateCount(HeadlessPlayer watcher, ViewCreation npc, int start)
    {
        byte[] prefix = BitConverter.GetBytes(npc.ViewId).Concat(Convert.FromHexString("048080808010")).ToArray();
        return watcher.World.ViewSnapshot().Skip(start).Count(frame => BytePattern.IndexOf(frame, prefix, 0) >= 0);
    }

    private static IEnumerable<ushort> Health(HeadlessPlayer watcher, ViewCreation entity, int start)
    {
        byte[] prefix = BitConverter.GetBytes(entity.ViewId).Concat(Convert.FromHexString("04808080")).ToArray();
        foreach (byte[] frame in watcher.World.ViewSnapshot().Skip(start))
        {
            int at = BytePattern.IndexOf(frame, prefix, 0);
            int group = at + prefix.Length;
            if (at >= 0 && group + 3 <= frame.Length && frame[group] is 0x40 or 0xc0)
                yield return BitConverter.ToUInt16(frame, group + 1);
        }
    }
}

public sealed class PowerBoostAggressivePetEntryMode : PowerBoostPetEntryMode
{
    public PowerBoostAggressivePetEntryMode() : base(useAggressiveStance: true) { }
}
