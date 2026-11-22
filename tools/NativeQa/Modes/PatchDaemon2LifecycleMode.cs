using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class PatchDaemon2LifecycleMode : NativeQaMode
{
    private const int Level = 23, X = 17000, Y = 495, Z = 6400, PetX = X + 150, FriendX = X + 400;
    private const ushort Coder = 1008, Patch1 = 602, Patch2 = 612;
    // At level 23 the client can use 612 without loading the level-30 Utility Technician discipline.
    // The seeder owns its Actions group parent and loads all actual prerequisites of both spells.
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Coder, Patch1, Patch2 }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    // PR603 PacketLog 980–981: stock 602 with no selected direct object. Its 612 sibling uses this
    // envelope with just the ability ID substituted until the first native 612 request is recorded.
    private const string Patch1Stock = "80b95a02000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string Deactivate = "81 7a fe 01";

    public override string Name => "ability-parity-patch-daemon2-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,PetX,Z)},healthC=500,healthM=500,innerStrC=500,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},healthC=200,healthM=500 WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Patch1, Patch2 }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "612", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, complete ranked profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV: CSR ({X},{Y},{Z}) faces the Healer stand ({PetX},{Y},{Z}); wounded QAFriend is at ({FriendX},{Y},{Z}). Natural HP and CSR IS regen are held.",
            "Choose Coder Style (page 2 slot 1) with world selection clear. Press slot 2 once. After 8 s, expect the 602 Healer window and body, level 22/900 max HP, [PET] summoned ability 602/simulacrum 1, and [ABILITY-COST] paid=100, IS=500->400.",
            "Press slot 3 for 612 and retain the complete first stock 80 b9 request. The older Healer stays through the 8 s cast. At landing expect one 81 77 for simulacrum 1 and one 81 76 for 2, [PET] ability 612 level 23/937 max HP, and [ABILITY-COST] paid=120 IS=400->280. Own reuse is 10 s.",
            "Type ?health 100. Expect two [VITALS] char=110 pet=2 ability=612 HP changes, about 3 s apart and each +8 to +11 in the server's current healing model. QAFriend stays wounded at 200 HP. Type /ordersimulacra All Deactivate. Its stock 81 7a fe 01 should produce one 81 77 for 2. The window and body disappear. Wait 4 s, with no further pet heals. A passing native run projects 612 to DONE."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Patch Daemon needs the repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PetX, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, x, Y, Z, PetX, Y, Z), $"Clear outdoor stand ({x},{Y},{Z})");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => archive[id].Level <= Level && AbilityPrerequisites.Current.Find(id).CharacterLevel <= Level &&
                AbilityPrerequisites.Current.Closure(id).All(need =>
                    archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Margin publishes the complete legal rank-23 loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var old = await SummonAsync(Patch1, Patch1Stock, 22, 900, 400, null);
        byte[] patch2Request = Convert.FromHexString(Patch1Stock);
        BinaryPrimitives.WriteUInt16LittleEndian(patch2Request.AsSpan(2), Patch2);
        var healer = await SummonAsync(Patch2, Convert.ToHexString(patch2Request), Level, 937, 280, old);

        var friendView = friend.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal((ushort)200, BitConverter.ToUInt16(friendView.Attributes["Health"]), "Nearby control is wounded");
        int friendVitals = friend.World.ViewSnapshot().Count;
        int ownerVitals = actor.World.ViewSnapshot().Count;
        actor.SendChat("?health 100");
        await actor.WaitUntilAsync(() => OwnerHeals().Length >= 3, "612 heals its owner twice", TimeSpan.FromSeconds(10));
        var heals = OwnerHeals().Take(3).ToArray();
        Check.That(heals.Zip(heals.Skip(1), (before, after) => after - before).All(delta => delta is >= 8 and <= 11),
            "612's first two owner heals use the existing same-level NPC roll");
        Check.That(!PvpOpponentsFixture.HealthHistory(friend.World.ViewSnapshot(), friendVitals, 2, self: true).Any(),
            "612 does not heal the nearby wounded friendly player");

        int remove = actor.World.RpcSnapshot().Count;
        int ownDelete = actor.World.ViewSnapshot().Count, friendDelete = friend.World.ViewSnapshot().Count;
        actor.SendRpc(Deactivate);
        await StockAbilityProfile.ObserveAsync(actor, remove, frame => frame.Bytes is [0x81, 0x77, var id] && id == healer.Id,
            "Stock Deactivate closes the 612 window");
        await DeletedAsync(actor, ownDelete, healer.Own.ViewId);
        await DeletedAsync(friend, friendDelete, healer.Friend.ViewId);
        int afterDismiss = actor.World.ViewSnapshot().Count;
        await Task.Delay(TimeSpan.FromSeconds(CoderPets.AttackPeriodSeconds + 0.5));
        Check.That(!PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), afterDismiss, 2, self: true).Any(),
            "No more owner healing follows stock dismissal");
        Check.Equal(1, actor.World.RpcSnapshot().Skip(remove).Count(frame => frame.Opcode == 0x177), "One dismissal");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Healer creation and teardown decode cleanly");
        Console.WriteLine("PATCH DAEMON 2 PASS: rank, cast, reuse, replacement, owner-only heal and stock dismissal.");
        return Array.Empty<byte>();

        ushort[] OwnerHeals() => PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), ownerVitals, 2, self: true)
            .SkipWhile(hp => hp != 100).Distinct().ToArray();

        async Task<(byte Id, ViewCreation Own, ViewCreation Friend)> SummonAsync(ushort ability, string request,
            byte rank, ushort maxHp, ushort after, (byte Id, ViewCreation Own, ViewCreation Friend)? oldPet)
        {
            int rpc = actor.World.RpcSnapshot().Count;
            int ownViews = actor.World.ViewSnapshot().Count, friendViews = friend.World.ViewSnapshot().Count;
            int ownCreation = actor.World.CreationSnapshot().Count, friendCreation = friend.World.CreationSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, ability, 8);
            Check.That(!actor.World.RpcSnapshot().Skip(rpc).Any(frame => frame.Opcode is 0x176 or 0x177),
                "The cast bar does not replace the pet early");
            await EvidenceRetestWire.TimerAsync(actor, rpc, ability, AbilityCooldowns.OwnTimer, 10);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpc, frame => frame.Opcode == 0x176, "Healer window");
            byte id = registration.Bytes[3];
            var own = await ViewAsync(actor, ownCreation, id);
            var remote = await ViewAsync(friend, friendCreation, id);
            Check.Equal("Healer Simulacrum", own.Name, "The shipped Subject chooses the Healer sheet");
            Check.Equal(rank, own.Attributes["Level"][0], "Healer level clamp");
            Check.Equal(maxHp, BitConverter.ToUInt16(own.Attributes["MaxHealth"]), "Modeled Healer maximum HP");
            Check.That(WorldNpcCornerFixture.Distance(EngageFoesFixture.Position(own), new double[] { PetX, Y, Z }) < 1,
                "The Healer arrives at the verified NAV stand");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(ownViews)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(after),
                $"Cast leaves {after} Inner Strength", HeadlessPlayer.DefaultTimeout);
            if (oldPet is { } oldPetViews)
            {
                await DeletedAsync(actor, ownViews, oldPetViews.Own.ViewId);
                await DeletedAsync(friend, friendViews, oldPetViews.Friend.ViewId);
            }
            var windows = actor.World.RpcSnapshot().Skip(rpc).Where(frame => frame.Opcode is 0x176 or 0x177)
                .Select(frame => (frame.Opcode, Id: frame.Bytes[frame.Opcode == 0x176 ? 3 : 2])).ToArray();
            Check.That(oldPet is { } previous
                ? previous.Id != id && windows.SequenceEqual(new[] { (0x177, previous.Id), (0x176, id) })
                : windows.SequenceEqual(new[] { (0x176, id) }), "Exactly one Healer window remains");
            return (id, own, remote);
        }

        async Task<ViewCreation> ViewAsync(HeadlessPlayer viewer, int cursor, byte id)
        {
            ViewCreation? result = null;
            await viewer.WaitUntilAsync(() => (result = viewer.World.CreationSnapshot().Skip(cursor).FirstOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == actor.CharId)) != null,
                $"Owned Healer {id} is visible to {viewer.Handle}", HeadlessPlayer.DefaultTimeout);
            return result!;
        }
    }

    private static Task DeletedAsync(HeadlessPlayer player, int cursor, ushort view)
    {
        byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(view)}");
        return player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(cursor).Any(section => section.AsSpan().IndexOf(deletion) >= 0),
            $"Healer view {view} removed for {player.Handle}", HeadlessPlayer.DefaultTimeout);
    }
}
