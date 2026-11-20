using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class GunStyleOpeningBatchMode : NativeQaMode
{
    private sealed record Style(ushort Id, string Weapon, uint Goid, uint FirstTransition, string StockRequest);

    private const ushort LogicBarrage = 330;
    // Full style uses from the PR330 stock handgun, rifle and dual-SMG PacketLogs.
    private static readonly Style[] Styles =
    {
        new(129, "handgun", CombatFixtureWeapons.Handgun, 21052,
            "80 b9 81 00 04 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 98 5b ac 00 01 00 00 00 08 6b 00 00 00 00 00"),
        new(147, "rifle", CombatFixtureWeapons.Rifle, 21068,
            "80 b9 93 00 04 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 d5 57 37 7c 20 00 00 00 08 6b 00 00 00 00 00"),
        new(158, "dual-smg", CombatFixtureWeapons.DualSmgs, 21076,
            "80 b9 9e 00 04 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 d5 57 37 7c 20 00 00 00 08 6b 00 00 00 00 00")
    };

    // Full native Logic Barrage hotbar use, PacketLog 09f69e027ea5 lines 1835–1836.
    private const string StockLogicBarrage =
        "80 b9 4a 01 0e 00 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 40 84 d7 77 41 08 6b 00 00 00 00 00";

    public override string Name => "ability-parity-gun-style-openings";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 129, 147, 158, LogicBarrage },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            CombatFixtureLoadout.SeedWeapons
        },
        DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { 129, 147, 158, LogicBarrage },
        environmentOverrides: new()
        {
            [CombatFixtureWeapons.WeaponMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "129,147,158", ["NativeCloseableRows"] = 3 },
        readyMessages: new[]
        {
            "Target the living 30,000 HP Melee Dummy 3 m west. For each gun, activate its matching style on page 2 while out of combat. Cast slot 4 Logic Barrage and allow the dummy to initiate interlock. Its fixture hit roll is fixed. The Close Combat shortcut can switch you to Self Defense.",
            "With the login revolver, use slot 1 Handguns. Capture the slot-1 opening transition 21052–21055 and one ordinary pistol exchange. Confirm the active stance renders.",
            "Use ?combatdepth fixture-disengage and wait for the HUD to clear. Use ?combatdepth fixture-weapon rifle, then slot 2 Rifles and slot 4. Capture 21068–21071 and one ordinary rifle exchange.",
            "Disengage again. Use ?combatdepth fixture-weapon dual-smg, then slot 3 Submachine Guns and slot 4. Capture 21076–21079 and one ordinary dual-SMG exchange."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected the disposable gun-style database.");
        await CombatFixtureLoadout.AssertWeaponSeedAsync(db);
        if (context.StockProbe is not { } actor)
        {
            using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
            await observer.EnterWorldAsync();
            CombatFixtureLoadout.AssertDummy(observer, await observer.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName));
            return Array.Empty<byte>();
        }

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable gun-style server.");
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        var proof = new List<object>();
        foreach (var style in Styles)
        {
            if (style.Goid != CombatFixtureWeapons.Handgun)
                await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, db, style.Weapon, style.Goid);

            actor.SkipReceived();
            SendTargeted(style.StockRequest);
            string id = StockAbilityProfile.LittleEndianHex(style.Id);
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {id} 00 00");
            int viewCursor = actor.World.ViewSnapshot().Count;
            int castCursor = actor.World.RpcSnapshot().Count;
            SendTargeted(StockLogicBarrage);
            await actor.WaitForRpcAsync("80 ac 4a 01");
            await actor.WaitUntilAsync(() =>
                actor.World.ViewSnapshot().Skip(viewCursor).Any(update => OpeningTransition(update, style.FirstTransition) != 0),
                $"The armed {style.Weapon} fight sends its matching opening transition", TimeSpan.FromSeconds(20));
            uint transition = actor.World.ViewSnapshot().Skip(viewCursor)
                .Select(update => OpeningTransition(update, style.FirstTransition)).First(id => id != 0);
            proof.Add(new { Style = style.Id, style.Goid, Transition = transition });

            int disengageCursor = actor.World.RpcSnapshot().Count;
            actor.SendChat("?combatdepth fixture-disengage");
            var closed = BytePattern.Parse($"46 {WireFormat.ToHex(BitConverter.GetBytes(handle))} 00");
            await StockAbilityProfile.ObserveAsync(actor, disengageCursor, packet => closed.IsMatch(packet.Bytes), "Interlock closes");
            await actor.WaitForTextAsync("Fixture combat ended.");
            var reuseOff = BytePattern.Parse("80 bd 01 4a 01 04 00 00 80 40 00");
            await StockAbilityProfile.ObserveAsync(actor, castCursor, packet => reuseOff.IsMatch(packet.Bytes), "Logic Barrage reuse clears");
        }

        Check.Equal(0, actor.World.Problems.Count, "Gun-style opening replay framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "gun-style-openings.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();

        void SendTargeted(string captured)
        {
            byte[] request = WireFormat.FromHex(captured);
            Check.Equal(43, request.Length, "Captured stock ability record length");
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), handle);
            actor.SendRpc(request);
        }
    }

    private static uint OpeningTransition(byte[] update, uint firstTransition)
    {
        // Combat view subview 1: view ID, 34-byte round header, then the packed opening entry.
        const int header = 36;
        for (int at = 0; at + header + CombatRoundEntry.Size <= update.Length; at++)
        {
            if (update[at + 2] != 1 || update[at + 35] != 1 ||
                BinaryPrimitives.ReadUInt32LittleEndian(update.AsSpan(at + 27)) != 1 ||
                BinaryPrimitives.ReadUInt32LittleEndian(update.AsSpan(at + 31)) != 0)
                continue;
            var entry = update.AsSpan(at + header, CombatRoundEntry.Size);
            if (BinaryPrimitives.ReadUInt16LittleEndian(entry[32..]) != 1 ||
                BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]) != 300 ||
                BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]) != 300)
                continue;
            int transitionOffset = entry[0] == 1 ? 45 : entry[1] == 1 ? 53 : -1;
            if (transitionOffset < 0)
                continue;
            ulong reference = BinaryPrimitives.ReadUInt64LittleEndian(entry[transitionOffset..]);
            uint id = (uint)reference;
            if (id >= firstTransition && id < firstTransition + 4 && reference == CombatRoundEntry.Reference(id))
                return id;
        }
        return 0;
    }
}
