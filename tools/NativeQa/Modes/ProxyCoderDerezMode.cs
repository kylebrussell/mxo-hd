using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyCoderDerezMode : NativeQaMode
{
    private const ushort Derez = 26, Coder = 1008, Daemon = 22;
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, DummyZ = 6600;
    // Complete stock Logic Daemon request from the PR593 dismissal batch.
    private const string StockDaemon = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, Coder, Daemon).Order().ToArray();
    private static readonly (string Name, int X, int Z)[] Stands =
    {
        ("QAFriend", 16400, Z), ("QAEnemy", 17300, Z), ("QAFar", 18600, Z)
    };

    public override string Name => "ability-parity-proxy-coder-derez";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, Coder, Daemon, Derez }, X, Z,
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
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Coder, Derez },
        testMobFixture: "starter-proxy-coder-derez.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "22,26,1008", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            "CSR (17000,495,6400) faces east. All stands and summon positions are verified on outdoor NAV floor 400. Three helpers enter and summon Nukers before READY. QAFriend's pet is west (16550,495,6400), QAEnemy's is 4.5 m east (17450,495,6400), and QAFar's is 17.5 m east (18750,495,6400). The Melee Dummy is north at (17000,495,6600).",
            "Use Coder Style, page 2 slot 1, outside interlock. Try slot 2 Proxy Coder with no selection, the Melee Dummy, the west friendly Nuker and the distant east Nuker. Stock can suppress a request. For requests that send, expect reasons 24, 36, 36 and 34 respectively. No cast, reuse or IS debit.",
            "Select the nearer east Nuker and press slot 2. Expect 2 s cast, 13.5 s own reuse, 200 to 160 IS and [PET] Proxy Coder derezzed Nuker Simulacrum of QAEnemy for 13000. Only that pet disappears. QAEnemy and both other pets remain alive, with no maximum player [VITALS] change or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Proxy Coder server.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy Coder needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.SelectMany(stand => new[] { (stand.X, stand.Z), (stand.X + 150, stand.Z) })
            .Append((X, Z)).Append((X, DummyZ)))
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
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var friend = context.Helpers["QAFriend"];
        var enemy = context.Helpers["QAEnemy"];
        var friendPet = await SeePetAsync(caster, friend.CharId);
        var enemyPet = await SeePetAsync(caster, enemy.CharId);
        var far = context.Helpers["QAFar"];
        var farPet = await SeePetAsync(caster, far.CharId);
        var npc = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var nonPetPlayer = await caster.WaitForViewNamedAsync(far.Handle);
        var refusals = new List<string>();
        foreach ((uint handle, uint reason) in new[]
        {
            (0u, 24u), (Handle(npc), 36u), (Handle(friendPet), 36u), (Handle(nonPetPlayer), 36u),
            (Handle(farPet), 34u), (Handle(enemyPet) ^ 0x10000, 24u)
        })
        {
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

        int enemyAt = enemy.World.RpcSnapshot().Count;
        int friendAt = friend.World.RpcSnapshot().Count;
        int farAt = far.World.RpcSnapshot().Count;
        int casterAt = caster.World.RpcSnapshot().Count;
        byte[] request = Use(Handle(enemyPet));
        caster.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(caster, Derez, 2);
        await EvidenceRetestWire.TimerAsync(caster, casterAt, Derez, 4, 13.5f);
        byte[] message = AbilityMessages.AboutAbility(0xc6000038, true, Derez, caster.Handle, enemyPet.Name);
        var derez = await StockAbilityProfile.ObserveAsync(caster, casterAt,
            packet => packet.Bytes.AsSpan().SequenceEqual(message), "sourced Proxy Coder derez caster line");
        var removed = await StockAbilityProfile.ObserveAsync(enemy, enemyAt,
            packet => BytePattern.Parse("81 77").IsMatch(packet.Bytes), "only the target owner's pet window is removed");
        await caster.WaitForViewUpdateAsync(enemyPet.ViewId, "04 80 80 80 c0 00 00 c0 0d 06 00 28");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 10 a0 00");
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Concat(far.World.RpcSnapshot().Skip(farAt))
            .Any(rpc => BytePattern.Parse("81 77").IsMatch(rpc.Bytes)), "The friendly and out-of-range pet windows remain active");
        Check.That(server.DebugLog().Contains("[PET] Proxy Coder derezzed Nuker Simulacrum of QAEnemy for 13000"),
            "The derez uses the fixed GOB amount on the selected pet");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + far.World.Problems.Count, "Proxy Coder packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "proxy-coder-derez.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), Refusals = refusals, Derez = derez.Hex, Removed = removed.Hex }));
        return Array.Empty<byte>();
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

    private static async Task<ViewCreation> SeePetAsync(HeadlessPlayer caster, uint owner)
    {
        ViewCreation? pet = null;
        await caster.WaitUntilAsync(() => (pet = caster.World.CreationSnapshot().LastOrDefault(view =>
            view.Name == "Nuker Simulacrum" && view.Attributes.TryGetValue("OwnerCharacterID", out var field) &&
            BitConverter.ToUInt32(field) == owner)) != null, $"Nuker Simulacrum owned by {owner}", HeadlessPlayer.DefaultTimeout);
        return pet!;
    }
}
