using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterDrainCodeAdmissionMode : NativeQaMode
{
    private const int Level = 8, X = 17000, Y = 495, Z = 6400, FriendX = 16400, NearX = 18100, FarX = 18200;
    private const ushort Coder = 1008, Proxy = 27, Daemon = 22, Drain = 19;
    private const string FarStand = "?tp 18200 495 6400 4e2 6 0 - C0";
    private const string NearStand = "?tp 18100 495 6400 4e2 6 0 - C0";
    // PR593 stock Proxy and Daemon requests, and PR391 CSEQ 61 for Drain. Only the target view/spawn changes.
    private const string ProxyStock = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string DaemonStock = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string DrainStock = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Drain).Order().ToArray();
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, Coder, Daemon).Order().ToArray();

    public override string Name => "ability-parity-starter-drain-code-admission";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},rotation=64,innerStrC=700,innerStrM=1000 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", HelperLoadout.Select((id, slot) => $"(6111,{400 + slot},{0x80000000u | ((uint)id << 10)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Drain }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "19,22,27", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}. CSR ({X},{Y},{Z}) faces east onto its own summon stand ({X + 150},{Y},{Z}). QAFriend's live Nuker is at ({FriendX + 150},{Y},{Z}) to the west. Both players, both summons and the two test stands are on outdoor NAV floor 400.",
            "Page 2 slot 1 Coder Style, then slot 2 Proxy. On landing, IS 700 to 650. Type ?pet passive then ?pet stay. Select the west Nuker and press slot 3 Drain Code. It should stay alive. Expect 'That is not your simulacrum' if stock sends 19. No cast, reuse or IS return.",
            $"Type {FarStand}, facing west toward your own Proxy from 10.5 m. Select it afresh and press slot 3. Capture the stock range refusal 34 if sent. The Proxy stays up and IS stays 650. No cast or reuse begins.",
            $"Type {NearStand}, 9.5 m from the same Proxy. Select it and repeat slot 3. Expect 2 s cast and 58 s own reuse, IS 650 to 700, one own window and view removal, and the Nuker still present. No player maximum [VITALS] change or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Drain Code needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150, FriendX, FriendX + 150, NearX, FarX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Drain Code position ({x},{Y},{Z}) is on outdoor NAV");

        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await PrepareAsync(friend, HelperLoadout);
        friend.SendRpc(DaemonStock);
        await friend.WaitForRpcAsync("81 76");
        await PetAsync(friend, friend.CharId);
        await ParkAsync(friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PrepareAsync(actor, Loadout);
        var foreign = await PetAsync(actor, friend.CharId);
        int summonViews = actor.World.ViewSnapshot().Count;
        actor.SendRpc(ProxyStock);
        var first = await actor.WaitForRpcAsync("81 76");
        var own = await PetAsync(actor, actor.CharId);
        byte sim = first.Bytes[3];
        await IsAsync(summonViews, 650);
        await ParkAsync(actor);

        int foreignAt = friend.World.RpcSnapshot().Count;
        await RefusedAsync(foreign, AbilityRefusals.LegacyAmbiguous, "That is not your simulacrum.");
        await MoveAsync(FarStand);
        await RefusedAsync(await PetAsync(actor, actor.CharId), AbilityRefusals.TargetOutOfRange, null);
        await MoveAsync(NearStand);
        own = await PetAsync(actor, actor.CharId);
        int from = actor.World.RpcSnapshot().Count;
        int resourceAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(Selected(own));
        await EvidenceRetestWire.CastBarAsync(actor, Drain, 2);
        await EvidenceRetestWire.TimerAsync(actor, from, Drain, AbilityCooldowns.OwnTimer, 58);
        await StockAbilityProfile.ObserveAsync(actor, from, frame => frame.Bytes is [0x81, 0x77, var removed] && removed == sim,
            "Own pet window removes after the approved 9.5 m Drain");
        await IsAsync(resourceAt, 700);
        Check.That(!friend.World.RpcSnapshot().Skip(foreignAt).Any(frame => frame.Bytes is [0x81, 0x77, ..]),
            "The foreign Nuker stays alive throughout both refusals and successful own Drain");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Drain admission wire framing");
        return Array.Empty<byte>();

        async Task PrepareAsync(HeadlessPlayer player, ushort[] abilities)
        {
            var archive = AbilityMemoryUiFixture.Decode(player.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
                .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
            Check.That(abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
                abilities.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                    archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), $"Level-8 archive fits for {player.Handle}");
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
            player.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
            await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        }

        async Task RefusedAsync(ViewCreation pet, uint reason, string? chat)
        {
            int cursor = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(Selected(pet));
            await StockAbilityProfile.ObserveAsync(actor, cursor, packet => AbilityRefusals.TryDecode(packet.Bytes, out var denial) &&
                denial.AbilityID == Drain && denial.ErrorID == reason, $"Drain refusal reason {reason}");
            if (chat != null) await actor.WaitForTextAsync(chat);
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes), "After-refusal barrier");
            Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(frame => BytePattern.Parse("80 ac 13 00").IsMatch(frame.Bytes) ||
                BytePattern.Parse("80 bd 01 13 00").IsMatch(frame.Bytes) || frame.Bytes is [0x81, 0x77, ..]) &&
                !actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isNow => isNow != 650),
                "Refusal leaves pet, reuse and Inner Strength alone");
            await Task.Delay(700);
        }

        async Task MoveAsync(string command)
        {
            actor.SkipReceived();
            actor.SendChat(command);
            await actor.WaitForTextAsync("Teleported to 4e2/6");
        }

        Task IsAsync(int from, int expected) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(from)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(value => value == expected),
            $"Drain fixture IS equals {expected}", HeadlessPlayer.DefaultTimeout);
    }

    private static async Task ParkAsync(HeadlessPlayer player)
    {
        player.SendChat("?pet passive");
        await player.WaitForTextAsync("Passive.");
        player.SendChat("?pet stay");
        await player.WaitForTextAsync("Staying.");
    }

    private static async Task<ViewCreation> PetAsync(HeadlessPlayer viewer, uint owner)
    {
        ViewCreation? pet = null;
        await viewer.WaitUntilAsync(() => (pet = viewer.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == owner)) != null,
            $"Visible simulacrum for owner {owner}", HeadlessPlayer.DefaultTimeout);
        return pet!;
    }

    private static byte[] Selected(ViewCreation pet) =>
        StarterHackerControlTargetMode.WithHandle(DrainStock, DuelPackets.Handle(pet.ViewId, pet.SpawnId));
}
