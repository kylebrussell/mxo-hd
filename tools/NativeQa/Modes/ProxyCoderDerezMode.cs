using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyCoderDerezMode : NativeQaMode
{
    private const ushort Derez = 26, Coder = 1008, Daemon = 22, Proxy = 27, Drain = 19, Boost = 25;
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, DummyZ = 6950, FarZ = 7300, EntryZ = 6550;
    // Complete stock Logic Daemon request from the PR593 dismissal batch.
    private const string StockDaemon = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string StockProxy = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockDrain = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockBoost = "80b91900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] CasterLoadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Drain, Boost, Derez).Order().ToArray();
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, Coder, Daemon).Order().ToArray();
    private const string FaceFriend = "?tp 17000 495 6400 4e2 12 4 - C0";
    private const string FaceDummy = "?tp 17000 495 6400 4e2 12 4 - 00";
    private const string FaceEnemy = "?tp 17000 495 6400 4e2 12 4 - 40";
    private const string FaceFar = "?tp 17000 495 7300 4e2 10 1 - 40";
    private static readonly (string Name, int X, int Z, int ParkZ, string ParkNav)[] Stands =
    {
        ("QAFriend", 16400, Z, 7000, "11 0"),
        ("QAEnemy", 17300, Z, 7000, "12 2"),
        ("QAFar", 18600, FarZ, 6800, "6 0")
    };

    public override string Name => "ability-parity-proxy-coder-derez";
    public override NativeQaModePlan Plan { get; } = new(CasterLoadout, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,healthC=1000,healthM=1000,innerStrC=200,innerStrM=200,pvpflag=0 WHERE charId IN (110,6111,6112,6114)",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "UPDATE characters SET alignment=1 WHERE charId IN (110,6111)",
            "UPDATE characters SET alignment=2 WHERE charId IN (6112,6114)",
            "UPDATE characters SET rotation=64 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", new[] { 6111, 6112, 6114 }.SelectMany(id => HelperLoadout.Select((ability, slot) =>
                    $"({id},{400 + slot},{0x80000000u | ((uint)ability << 10)},{Level},1)")))
        }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},z={stand.Z} WHERE handle='{stand.Name}'")).ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Coder, Derez, Proxy, Boost, Drain }, hotbarWithdraw: true,
        testMobFixture: "starter-proxy-coder-derez.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "19,25,26,27", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            "Impact-effect retest: stay at the starting stand facing east. Select Coder Style (slot 1), clear the target on empty ground, then summon your Proxy (slot 3). With selection clear, use Power Boost (slot 4) and Drain (slot 5) on your own Proxy. IS 200 to 150 to 100 to 150. Then select the isolated QAEnemy Nuker (17450,495,6400) and use slot 2. The older refusal and interlock checks below are optional.",
            "All stands and summon positions are on outdoor NAV floor 400. The helpers move at least 5 m sideways after their Nukers stay, leaving the models separated. The far pet also has its own sightline. Confirm the intended name in the target panel before using the slot.",
            $"Choose Coder Style (page 2 slot 1) outside interlock. No selection is reason 24 (already passed). Use {FaceDummy} to face the Melee Dummy at (17000,495,6950). Then {FaceFriend} to face the friendly Nuker at (16550,495,6400). Try slot 2 on each, expecting reason 36. Use {FaceFar} to face the separate far Nuker at (18750,495,7300), 17.5 m away; expect reason 34 if stock sends. Refusals do not start a cast or reuse or debit IS.",
            $"Use {FaceEnemy} to return to the same stand. Clear the target, summon your own Remote Proxy with slot 3 (200 to 150 IS), and type ?pet passive then ?pet stay. Move QAEnemy via helper chat to ?tp {X} {Y} {EntryZ} 4e2 12 4, then ask it to melee CSR_Tester. Wait for an active [PVP] round. Press slots 4, 5 and 2 during the fight. Stock may refuse locally. Each sent request should get reason 21, no cast or reuse, and neither pet should disappear.",
            "Withdraw with slot 6 and helper stop. Wait until the fight ends, move QAEnemy back via helper chat ?tp 17300 495 7000 4e2 12 2, and select Coder Style again. Clear the target. Slot 4 Power Boost spends 50 IS (150 to 100), takes 4 s, reuses in 8 s, and gives own Proxy [PET] melee=9 ballistic=9 duration=300. Slot 5 Drain takes 2 s and 58 s reuse, removes only your Proxy and restores 100 to 150 IS.",
            $"Face east ({FaceEnemy}) and select the isolated near Nuker (17450,495,6400). Slot 2 takes 2 s, uses 40 IS (150 to 110), starts 13.5 s reuse and logs [PET] Proxy Coder derezzed Nuker Simulacrum of QAEnemy for 13000. Capture the zero-HP view updates for 0x280009cd (impact) followed by 0x2800060d (death) and the visible FX. Only the enemy pet disappears. Check no new [PVP] rounds or maximum [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Proxy Coder server.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy Coder needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.SelectMany(stand => new[] { (stand.X, stand.Z), (stand.X + 150, stand.Z), (stand.X, stand.ParkZ) })
            .Append((X, Z)).Append((X + 150, Z)).Append((X, EntryZ)).Append((X, DummyZ)).Append((X, FarZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Proxy Coder stand ({x},{Y},{z}) is on outdoor NAV");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            helper.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
            await helper.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
            helper.SendRpc(StockDaemon);
            await EvidenceRetestWire.CastBarAsync(helper, Daemon, 8);
            await helper.WaitForRpcAsync("81 76");
            var pet = await SeePetAsync(helper, helper.CharId);
            Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { stand.X + 150, Y, stand.Z }),
                $"{stand.Name} has a live Nuker on the verified spawn");
            helper.SendChat("?pet passive");
            await helper.WaitForTextAsync("Passive.");
            helper.SendChat("?pet stay");
            await helper.WaitForTextAsync("Staying.");
            helper.SendChat($"?tp {stand.X} {Y} {stand.ParkZ} 4e2 {stand.ParkNav}");
            await helper.WaitForTextAsync("Teleported to 4e2/" + stand.ParkNav.Split(' ')[0]);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(CasterLoadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            CasterLoadout.All(id => loaded.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete level-18 profile fits");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var friend = context.Helpers["QAFriend"];
        var enemy = context.Helpers["QAEnemy"];
        var friendPet = await SeePetAsync(caster, friend.CharId);
        var enemyPet = await SeePetAsync(caster, enemy.CharId);
        var enemyOwnPet = await SeePetAsync(enemy, enemy.CharId);
        var far = context.Helpers["QAFar"];
        var farPet = await SeePetAsync(caster, far.CharId);
        foreach (var (stand, pet) in Stands.Zip(new[] { friendPet, enemyPet, farPet }))
        {
            var owner = await caster.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { stand.X + 150, Y, stand.Z }) &&
                EngageFoesFixture.Position(owner).SequenceEqual(new double[] { stand.X, Y, stand.ParkZ }),
                $"CSR sees {stand.Name} separated from its stationary Nuker");
        }
        var npc = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var nonPetPlayer = await caster.WaitForViewNamedAsync(far.Handle);
        var refusals = new List<string>();
        foreach ((uint handle, uint reason) in new[]
        {
            (0u, 24u), (Handle(npc), 36u), (Handle(friendPet), 36u), (Handle(nonPetPlayer), 36u),
            (Handle(farPet), 34u), (Handle(enemyPet) ^ 0x10000, 24u)
        })
        {
            if (handle == Handle(farPet))
            {
                caster.SendChat(FaceFar);
                await caster.WaitForTextAsync("Teleported to 4e2/10");
            }
            else if (reason == 24 && handle != 0)
            {
                caster.SendChat(FaceEnemy);
                await caster.WaitForTextAsync("Teleported to 4e2/12");
            }
            int start = caster.World.RpcSnapshot().Count;
            caster.SendRpc(Use(handle));
            var denial = await StockAbilityProfile.ObserveAsync(caster, start,
                packet => AbilityRefusals.TryDecode(packet.Bytes, out var refused) && refused.AbilityID == Derez && refused.ErrorID == reason,
                $"Proxy Coder native selection refusal {reason}");
            refusals.Add(denial.Hex);
            await Task.Delay(TimeSpan.FromMilliseconds(600));
            var replies = caster.World.RpcSnapshot().Skip(start).ToArray();
            Check.Equal(1, replies.Count(rpc => BytePattern.Parse("80 ba 1a 00").IsMatch(rpc.Bytes)), "One refusal record per request");
            Check.That(!replies.Any(rpc => BytePattern.Parse("80 ac 1a 00").IsMatch(rpc.Bytes) ||
                BytePattern.Parse("80 bd 01 1a 00").IsMatch(rpc.Bytes) || BytePattern.Parse("2e c7").IsMatch(rpc.Bytes) ||
                BytePattern.Parse("81 67").IsMatch(rpc.Bytes)), "Refusal sends no cast, reuse or supplemental chat");
        }

        caster.SendRpc(StockProxy);
        await caster.WaitForRpcAsync("81 76");
        var ownPet = await SeePetAsync(caster, caster.CharId, name: null);
        await ExpectIsAsync(150);
        caster.SendChat("?pet passive");
        await caster.WaitForTextAsync("Passive.");
        caster.SendChat("?pet stay");
        await caster.WaitForTextAsync("Staying.");

        enemy.SendChat($"?tp {X} {Y} {EntryZ} 4e2 12 4");
        await enemy.WaitForTextAsync("Teleported to 4e2/12");
        string casterHandle = WireFormat.ToHex(BitConverter.GetBytes(Handle(await enemy.WaitForViewNamedAsync(caster.Handle))));
        enemy.SendRpc($"40 {casterHandle} 01 6b");
        await enemy.WaitForRpcAsync($"46 {casterHandle} 01");
        await caster.WaitForRpcAsync("45");

        byte[] drain = StarterHackerControlTargetMode.WithHandle(StockDrain, Handle(ownPet));
        int combatAt = caster.World.RpcSnapshot().Count;
        foreach (var (id, bytes) in new[] { (Drain, drain), (Boost, WireFormat.FromHex(StockBoost)), (Derez, Use(Handle(enemyPet))) })
        {
            caster.SendRpc(bytes);
            await StockAbilityProfile.ObserveAsync(caster, combatAt, packet =>
                AbilityRefusals.TryDecode(packet.Bytes, out var refusal) && refusal.AbilityID == id &&
                refusal.ErrorID == AbilityRefusals.MustBeOutsideCombat, $"In-combat {id} reports reason 21");
        }
        caster.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(caster, combatAt, packet => packet.Opcode == 0x149, "After combat refusals");
        Check.That(!caster.World.RpcSnapshot().Skip(combatAt).Any(packet => packet.Opcode is 0xac or 0xbd or 0x177),
            "Combat refusals start no cast or reuse and keep the own pet");
        Check.Equal((ushort)150, ReadIs(), "All combat refusals preserve IS");
        caster.SendRpc("44 00");
        enemy.SendRpc("44 00");
        await enemy.WaitForRpcAsync($"46 {casterHandle} 00", TimeSpan.FromSeconds(20));
        enemy.SendChat("?tp 17300 495 7000 4e2 12 2");
        await enemy.WaitForTextAsync("Teleported to 4e2/12");

        caster.SkipReceived();
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        int boostAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StockBoost);
        await EvidenceRetestWire.CastBarAsync(caster, Boost, 4);
        await EvidenceRetestWire.TimerAsync(caster, boostAt, Boost, AbilityCooldowns.OwnTimer, 8);
        await ExpectIsAsync(100);
        await caster.WaitForViewUpdateAsync(ownPet.ViewId, "04 80 80 80 c0 e0 00 c0 81 08 00 28");
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("melee=9 ballistic=9 duration=300", StringComparison.Ordinal),
            "Actual level-18 Power Boost on own Proxy", HeadlessPlayer.DefaultTimeout);

        int drainAt = caster.World.RpcSnapshot().Count;
        int drainViewAt = caster.World.ViewSnapshot().Count;
        caster.SkipReceived();
        caster.SendRpc(drain);
        await EvidenceRetestWire.CastBarAsync(caster, Drain, 2);
        await EvidenceRetestWire.TimerAsync(caster, drainAt, Drain, AbilityCooldowns.OwnTimer, 58);
        await StockAbilityProfile.ObserveAsync(caster, drainAt, packet => packet.Opcode == 0x177, "Drain removes only the own Proxy");
        await ExpectIsAsync(150);
        await AssertEffectsAsync(caster, ownPet.ViewId, drainViewAt, expectImpact: false);

        int enemyAt = enemy.World.RpcSnapshot().Count;
        int friendAt = friend.World.RpcSnapshot().Count;
        int farAt = far.World.RpcSnapshot().Count;
        int casterAt = caster.World.RpcSnapshot().Count;
        int casterViewAt = caster.World.ViewSnapshot().Count, enemyViewAt = enemy.World.ViewSnapshot().Count;
        byte[] request = Use(Handle(enemyPet));
        caster.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(caster, Derez, 2);
        await EvidenceRetestWire.TimerAsync(caster, casterAt, Derez, 4, 13.5f);
        byte[] message = AbilityMessages.AboutAbility(0xc6000038, true, Derez, caster.Handle, enemyPet.Name);
        var derez = await StockAbilityProfile.ObserveAsync(caster, casterAt,
            packet => packet.Bytes.AsSpan().SequenceEqual(message), "sourced Proxy Coder derez caster line");
        var removed = await StockAbilityProfile.ObserveAsync(enemy, enemyAt,
            packet => BytePattern.Parse("81 77").IsMatch(packet.Bytes), "only the target owner's pet window is removed");
        await AssertEffectsAsync(caster, enemyPet.ViewId, casterViewAt, expectImpact: true);
        await AssertEffectsAsync(enemy, enemyOwnPet.ViewId, enemyViewAt, expectImpact: true);
        await ExpectIsAsync(110);
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Concat(far.World.RpcSnapshot().Skip(farAt))
            .Any(rpc => BytePattern.Parse("81 77").IsMatch(rpc.Bytes)), "The friendly and out-of-range pet windows remain active");
        Check.That(server.DebugLog().Contains("[PET] Proxy Coder derezzed Nuker Simulacrum of QAEnemy for 13000"),
            "The derez uses the fixed GOB amount on the selected pet");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + far.World.Problems.Count, "Proxy Coder packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "proxy-coder-derez.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), Refusals = refusals, Derez = derez.Hex, Removed = removed.Hex }));
        return Array.Empty<byte>();

        ushort ReadIs() => caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
            .LastOrDefault(BitConverter.ToUInt16(caster.World.CreationSnapshot().First(view => view.ViewId == 2)
                .Attributes["InnerStrengthAvailable"]));

        Task ExpectIsAsync(ushort value) => caster.WaitUntilAsync(() => ReadIs() == value,
            $"Current IS {value}", HeadlessPlayer.DefaultTimeout);
    }

    private static async Task AssertEffectsAsync(HeadlessPlayer viewer, ushort viewId, int start, bool expectImpact)
    {
        const string death = "04 80 80 80 c0 00 00 c0 0d 06 00 28";
        const string impact = "04 80 80 80 c0 00 00 c0 cd 09 00 28";
        await viewer.WaitForViewUpdateAsync(viewId, death);
        string handle = StockAbilityProfile.LittleEndianHex(viewId);
        var deathPattern = BytePattern.Parse($"{handle} {death}");
        var impactPattern = BytePattern.Parse($"{handle} {impact}");
        var frames = viewer.World.ViewSnapshot().Skip(start).ToList();
        Check.Equal(1, frames.Count(deathPattern.Occurs), "The zero-HP death effect appears exactly once");
        Check.Equal(expectImpact ? 1 : 0, frames.Count(impactPattern.Occurs), "Only the sourced lethal cast publishes its impact FX");
        var ordered = BytePattern.Parse($"{handle} {impact} 01 ?? {handle} {death} 01 ??");
        Check.That(!expectImpact || frames.Any(ordered.Occurs), "The lethal impact precedes death FX within one view list");
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);

    private static byte[] Use(uint selectedHandle)
    {
        // Stock Proxy Coder requests in PR401 capture 29da6f3e7d68, lines 6409 and 10443.
        byte[] request = Convert.FromHexString(selectedHandle == 0
            ? "80b91a00000000000000000000000000000000000000000000000000d557377c20000000086b0000000000"
            : "80b91a0011000f000000000000000000000000000000000000000000585cb30001000000086b0000000000");
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), selectedHandle);
        return request;
    }

    private static async Task<ViewCreation> SeePetAsync(HeadlessPlayer caster, uint owner, string? name = "Nuker Simulacrum")
    {
        ViewCreation? pet = null;
        await caster.WaitUntilAsync(() => (pet = caster.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && (name == null || view.Name == name) &&
            view.Attributes.TryGetValue("OwnerCharacterID", out var field) &&
            BitConverter.ToUInt32(field) == owner)) != null, $"{name ?? "Simulacrum"} owned by {owner}", HeadlessPlayer.DefaultTimeout);
        return pet!;
    }
}
