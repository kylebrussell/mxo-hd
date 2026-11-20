using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterGunSpecialAdmissionMode : NativeQaMode
{
    private const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    // PR389 stock Hindering Shot, PacketLog d3b7112643d9 lines 3204–3205.
    private const string Hindering = "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    // PR394, PR404a/b and the documented native handgun, rifle and SMG damage samples.
    private static readonly Sample[] Samples =
    {
        new(105, 147, "rifle", CombatFixtureWeapons.Rifle, 11364,
            "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
            "80b9690004000200000000000000000000000000000000000000000069000000006b0000086b0000000000"),
        new(126, 129, "handgun", CombatFixtureWeapons.Handgun, 9522,
            "80b981000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
            "80b97e000400020000000000000000000000000000000000000000007e000000006b0000086b0000000000"),
        new(176, 158, "smg", CombatFixtureWeapons.Smg, 21083,
            "80b99e000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
            "80b9b000050003000000000000000000000000000000000000000000b0000000006b0000086b0000000000")
    };

    public override string Name => "ability-parity-starter-gun-special-admission";
    public override NativeQaModePlan Plan { get; } = new(
        StockAbilityProfile.Profile(2, 17, 105, 126, 176, 130, 129, 147, 158).Order().ToArray(), 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { 105, 126, 176, 147, 129, 158, 130 },
        testMobFixture: CombatFixtureLoadout.MeleeCsv, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.WeaponMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "105,126,176,130", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Start here before any Close Combat, weapon swap, or reset. The handgun is already equipped. Select Melee Dummy, choose Handguns (slot 5) and use Hindering Shot (slot 7) at rest. Capture a 2 s cast, 8 s reuse and the slow/Force Combat records. This is the only native retest required for PR505.",
            "For the complete sweep after Hindering lands, reset, strafe and select the fresh dummy. Equip the rifle with ?combatdepth fixture-weapon rifle. Select Melee Dummy, choose Rifles (page 2 slot 4), and try Body Shot (slot 1) at rest. It should be unavailable, with no IS charge or reuse. Open Close Combat and choose Rifles again before using Body Shot. Capture move 11364.",
            "Run ?combatdepth fixture-reset, strafe slightly and select the fresh Melee Dummy. Use ?combatdepth fixture-weapon handgun. Open Close Combat, choose Handguns (slot 5), and use Disarming Shot (slot 2). Before Disarming Shot, try Hindering Shot (slot 7) in the same interlock. Stock should show outside-combat gating without a cast or IS charge. Then use slot 2 and capture move 9522.",
            "Repeat reset and selection with ?combatdepth fixture-weapon smg. Open Close Combat, select Submachine Guns (slot 6), and use Covering Fire (slot 3). Wait for the cast and move 21083. Each move must queue on the current dummy.",
            "Enter submits commands. Keep the target selected without pressing Escape. After the at-rest shot, wait for the 8 s reuse to clear before checking Hindering in interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, target);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        await SelectStyleAsync(actor, Samples[1], handle);
        int cursor = actor.World.RpcSnapshot().Count;
        int shotLog = server.DebugLog().Length;
        actor.SendRpc(WithHandle(Hindering, handle));
        await EvidenceRetestWire.CastBarAsync(actor, 130, 2);
        await EvidenceRetestWire.TimerAsync(actor, cursor, 130, AbilityCooldowns.OwnTimer, 8);
        await actor.WaitUntilAsync(() => server.DebugLog()[shotLog..].Contains($"[ABILITY] 130 lands on view {target.ViewId}"),
            "Hindering Shot still lands on the current enemy outside combat", HeadlessPlayer.DefaultTimeout);
        target = await FreshDummyAsync(actor, target);
        await EvidenceRetestWire.TimerAsync(actor, cursor, 130, AbilityCooldowns.OwnTimer, 8, ended: true);
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, "rifle", CombatFixtureWeapons.Rifle);
        handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        await SelectStyleAsync(actor, Samples[0], handle);
        await RefusedAsync(actor, 105, WithHandle(Samples[0].AbilityCapture, handle));

        foreach (var sample in Samples)
        {
            if (sample.Id != 105)
            {
                target = await FreshDummyAsync(actor, target);
                await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, sample.Weapon, sample.Goid);
            }

            handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
            string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            actor.SkipReceived();
            actor.World.SendRpcs(WithHandle(StockOpen, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
            await actor.WaitForRpcAsync($"40 {hex} 00 6b");
            await actor.WaitForRpcAsync("47");
            await SelectStyleAsync(actor, sample, handle);
            if (sample.Id == 105)
                await RefusedAsync(actor, sample.Id, WithHandle(sample.AbilityCapture, handle ^ 0x10000));
            if (sample.Id == 126)
                await RefusedAsync(actor, 130, WithHandle(Hindering, handle), AbilityRefusals.MustBeOutsideCombat);

            int logCursor = server.DebugLog().Length;
            actor.SendRpc(WithHandle(sample.AbilityCapture, handle));
            string styleMove = $"animation {sample.Animation} (";
            string queued = $"[COMBAT] {AbilityCatalog.Find(sample.Id).Name} is the next move";
            await actor.WaitUntilAsync(() =>
            {
                var log = server.DebugLog()[logCursor..];
                int start = log.IndexOf(queued, StringComparison.Ordinal);
                return start >= 0 && log[start..].Contains(styleMove, StringComparison.Ordinal);
            }, $"Stock {sample.Id} queues and uses move {sample.Animation}", TimeSpan.FromSeconds(18));
        }
        Check.Equal(0, actor.World.Problems.Count, "Gun special framing");
        return Array.Empty<byte>();
    }

    private static async Task<ViewCreation> FreshDummyAsync(HeadlessPlayer actor, ViewCreation previous)
    {
        int viewCursor = actor.World.CreationSnapshot().Count;
        actor.SendChat("?combatdepth fixture-reset");
        await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
        await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(viewCursor)
            .Any(view => view.Name == CombatFixtureLoadout.DummyName), "Fresh dummy after reset", HeadlessPlayer.DefaultTimeout);
        var fresh = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        Check.That(fresh.ViewId != previous.ViewId && fresh.SpawnId != previous.SpawnId, "Reset replaced the combat handle");
        return fresh;
    }

    private static async Task SelectStyleAsync(HeadlessPlayer actor, Sample sample, uint handle)
    {
        int cursor = actor.World.ViewSnapshot().Count;
        var change = BytePattern.Parse($"02 00 02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(sample.Style)} 00 00");
        actor.SendRpc(WithHandle(sample.StyleCapture, handle));
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor).Any(change.Occurs),
            $"Gun style {sample.Style} selected", HeadlessPlayer.DefaultTimeout);
    }

    private sealed record Sample(ushort Id, ushort Style, string Weapon, uint Goid, int Animation,
        string StyleCapture, string AbilityCapture);
}
