using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class VectorPlayerPowerlessMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, Level = 50;
    private const ushort Vector = 355, Stun = 315, Style = CombatStyles.HackerStyle;
    // Stock PR997 f61d60d8, pr997-PacketLog.txt. Only the live recipient view/spawn is rebound.
    private const string VectorStock = "80b963010500030000000000000000000000000000000000000000001060a90001000000086b0000000000";
    private static readonly ushort[] VectorLoadout = StockAbilityProfile.Profile(2, 17, Style, Vector).Order().ToArray();
    private static readonly ushort[] StunLoadout = StockAbilityProfile.Profile(2, 17, Style, Stun).Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z)[] Helpers =
    {
        ("QAEnemy", 6114, X, 6600), ("QAOther", 6113, X + 200, 6600)
    };

    public override string Name => "ability-parity-vector-player-powerless";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 12, 17 }, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,innerStrC=700,innerStrM=900 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6113,6114)",
            Seed(6114, VectorLoadout), Seed(6113, StunLoadout)
        }.Concat(Helpers.Select(s =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={s.X},y={Y},z={s.Z},alignment=2,innerStrC=700,innerStrM=900 WHERE charId={s.Id}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new ushort[] { 12 }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "355", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at verified HOME. If displaced, type the printed warp in chunks of up to 16 characters on one line before Enter. No selection, no NPCs, no active Hyper-Speed. The disposable fixture pins helper Viral hit admission only and holds CSR's IS regen.",
            """Use helper request {"actor":"QAEnemy","action":"vector-lifecycle"}. QAEnemy casts the retained Vector packet and QAOther tries ordinary Stun once immunity is up.""",
            "At VECTOR ACTIVE, press page 2 slot 1 (Hyper-Speed) once. Expect a locally disabled action or 'You cannot use abilities while powerless' if sent, with no debit. Client receives Vector's normal infection presentation plus state 96 8.94 s, stat 183 -60 for 30 s, and independent 734/681 immunity 8 s. The follow-up Stun should report immunity and give no state 102. The shipped 6 s/-35 and retained 8.94 s/-60 differ; this fixture measures the retained values and their lifecycle.",
            "After 355/96 removes, press Hyper-Speed once more. Expect 40 IS (700 to 660) and normal active bonuses. State 96 and immunity have ended, while the 183 record stays up to its own expiry. Keep the 30-second removal."
        });

    private static string Seed(int id, ushort[] profile) => "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
        string.Join(",", profile.Select((ability, i) => $"({id},{i},{AbilityMessages.AbilityGameObject(ability)},{Level},1)"));

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Repository NAV required.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        var server = context.ActualServer ?? throw new InvalidOperationException("Disposable server required.");
        foreach (var helper in Helpers)
        {
            Console.WriteLine($"READY {helper.Name} {InvisibilityBeltWatcherMode.Warp(helper.X, Y, helper.Z, 128, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, helper.X, Y, helper.Z, X, Y, Z), "Helper has clear targeted sight");
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Name, player);
            DisablingShotMode.CheckProfile(player, helper.Name == "QAEnemy" ? VectorLoadout : StunLoadout, Level);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            player.SendRpc(StarterHackerViralSightMode.StockStyle);
            await StockAbilityProfile.ObserveAsync(player, 0, frame => StockAbilityProfile.Bonus(frame, Style, 172, 27, 0, false), "Stock Hacker Style");
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        int start = actor.World.RpcSnapshot().Count;
        int log = server.DebugLog().Length;
        var probe = RunAsync(context.Helpers["QAEnemy"], context.Helpers["QAOther"], server);
        await BonusAsync(Vector, TempMods.PowerlessAbility, 0, 8.94f, false);
        await BonusAsync(Vector, 183, -60, 30, false);
        await BonusAsync(TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, 8, false);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpeedUse);
        await actor.WaitForTextAsync("You cannot use abilities while powerless.");
        Check.That(!server.DebugLog()[log..].Contains("[ABILITY-COST] char=110 ability=12 "), "Powerless costs no IS");
        await probe;
        await BonusAsync(TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, 8, true);
        await BonusAsync(Vector, TempMods.PowerlessAbility, 0, 8.94f, true);
        Check.That(!actor.World.RpcSnapshot().Skip(start).Any(record =>
            StockAbilityProfile.Bonus(record, Stun, TempMods.StunAbility, 0, 7.35f, false) ||
            StockAbilityProfile.Bonus(record, Vector, 183, -60, 30, true)), "Stun suppressed; independent defense record still active");

        actor.SendRpc(AwakenedStockStealthSpeedMode.SpeedUse);
        await StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, 12, 877, 12, 600, false), "Normal Hyper-Speed after Powerless");
        Check.That(server.DebugLog()[log..].Contains("[ABILITY-COST] char=110 ability=12 paid=40 IS=700->660"), "Only recovered activation debits IS");
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(start).Any(frame =>
            StockAbilityProfile.Bonus(frame, Vector, 183, -60, 30, true)), "30-second defense removal", TimeSpan.FromSeconds(25));
        Check.Equal(0, actor.World.Problems.Count, "Valid retained control and self-cast framing");
        return Array.Empty<byte>();

        Task<RpcRecord> BonusAsync(ushort source, ushort stat, int value, float seconds, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, source, stat, value, seconds, remove),
                $"Vector recipient source={source} stat={stat} remove={remove}");
    }

    internal static async Task<string> RunAsync(HeadlessPlayer caster, HeadlessPlayer stun, ServerInstance server)
    {
        if (caster.Handle != "QAEnemy" || stun.Handle != "QAOther")
            throw new InvalidOperationException("The Vector fixture accepts QAEnemy and QAOther only.");
        var target = await caster.WaitForViewNamedAsync("CSR_Tester");
        var stunTarget = await stun.WaitForViewNamedAsync("CSR_Tester");
        int log = server.DebugLog().Length;
        int cast = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(VectorStock, DuelPackets.Handle(target.ViewId, target.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(caster, Vector, 2);
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("char=110 source=355 stat=96 add"),
            "Vector lands on CSR", HeadlessPlayer.DefaultTimeout);
        Console.WriteLine("VECTOR ACTIVE: press the native Hyper-Speed slot now.");
        stun.SendRpc(StarterHackerControlTargetMode.WithHandle(EarlyHackerControlRecoveryMode.StunRequest,
            DuelPackets.Handle(stunTarget.ViewId, stunTarget.SpawnId)));
        await EvidenceRetestWire.TimerAsync(caster, cast, Vector, AbilityCooldowns.OwnTimer, 60);
        await stun.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[EFFECTS] 315: control immunity, state refused"),
            "The independent Stun is refused by Vector immunity", HeadlessPlayer.DefaultTimeout);
        return "Vector landed; its immunity refused the separate Stun before the state was added.";
    }
}
