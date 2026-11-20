using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class GunmanRifleOpenerScopeMode : NativeQaMode
{
    private const ushort Rifles = 147, Handguns = 129;
    private const string StockRifles = "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockHandguns = "80b981000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    // PR328 stock 4242512d1e6c and PR389 d3b7112643d9.
    private const string Immobilizing = "80b9fe00060004000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string Disabling = "80b9ff00070005000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string Hindering = "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-gunman-rifle-opener-scope";
    public override NativeQaModePlan Plan { get; } = new(Profile(2, 17, 130, 254, 255, Rifles, Handguns).Order().ToArray(),
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=0 WHERE charId IN (110,6113,6114)",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6114",
            "UPDATE characters SET x=24800,z=-1798 WHERE charId=6113"
        }, DevCommands: true, hotbarAbilities: new ushort[] { Rifles, 254, 255, Handguns, 130 }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            [CombatFixtureWeapons.WeaponMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "254,255,130", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Perform all openers at rest. Use ?combatdepth fixture-weapon rifle, then page 2 slot 1 Rifles. Try slots 2 and 3 with empty selection, then with friendly QAOther. If a request is sent, each should refuse before IS, cast bar or reuse. Enter submits chat. Keep the selected target without Escape.",
            "Select hostile QAEnemy. Slots 2 and 3 are Immobilizing Shot and Disabling Shot. Both cast in 2 s and have 10 s own reuse. Wait for the cast to end before using the next slot. The stored ballistic bases are 18 and 16 respectively. Compare the landed damage records after normal style influence and record any conditional state. Conditional rolls are ordinary.",
            "After shared reuse clears, use ?combatdepth fixture-weapon handgun then slot 4 Handguns. Keep or reselect QAEnemy. Slot 5 Hindering Shot is the outside-combat control, with 2 s cast and 8 s own reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (string name in new[] { "QAEnemy", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var db = context.OwnedDatabase!;
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, db, "rifle", CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StockRifles);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");
        var hostile = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAOther");
        uint target = Handle(hostile);

        foreach (var (id, request) in new[] { ((ushort)254, Immobilizing), ((ushort)255, Disabling) })
        {
            await RefusedAsync(actor, id, WithHandle(request, 0));
            await RefusedAsync(actor, id, WithHandle(request, Handle(ally)));
            await RefusedAsync(actor, id, WithHandle(request, target ^ 0x10000));
        }
        await LandAsync(actor, context.Helpers["QAEnemy"], hostile, 254, Immobilizing, 22, 10);
        await LandAsync(actor, context.Helpers["QAEnemy"], hostile, 255, Disabling, 19, 10);
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, db, "handgun", CombatFixtureWeapons.Handgun);
        actor.SendRpc(StockHandguns);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 81 00 00 00");
        await LandAsync(actor, context.Helpers["QAEnemy"], hostile, 130, Hindering, 11, 8);
        Check.Equal(0, actor.World.Problems.Count, "Rifle opener framing");
        return Array.Empty<byte>();
    }

    private static async Task LandAsync(HeadlessPlayer actor, HeadlessPlayer enemy, ViewCreation target,
        ushort id, string request, int damage, int reuse)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        await StarterFixedDamageMode.Land(actor, enemy, target, id, damage, 2, reuse, request);
        if (AbilityCooldowns.TryGet(id, out var cooldown) && cooldown.Group != 0)
            await EvidenceRetestWire.TimerAsync(actor, cursor, cooldown.Group, AbilityCooldowns.GroupTimer, cooldown.GroupSeconds, ended: true);
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
