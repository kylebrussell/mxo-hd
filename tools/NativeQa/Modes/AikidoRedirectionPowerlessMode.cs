using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class AikidoRedirectionPowerlessMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Home = 6400, Enemy = 6600;
    private const ushort Redirection = 405, Throat = 340, Spy = CombatStyles.SpyStyle;
    // First complete stock 340 from the Master Knife Thrower fixture.
    private const string StockThroat = "80b9540104000200000000000000000000000000000000000000000054010000006b0000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Throat, Redirection);

    public override string Name => "ability-parity-aikido-redirection-powerless";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Home, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Home},rotation=0 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={Enemy},rotation=128 WHERE charId=6114",
        "UPDATE char_abilities SET level=50 WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id=6114"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Throat }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "405", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"96/100 memory. No initial tp. CSR faces the verified player QAEnemy ({X},{Y},{Enemy}). Stay out of combat, /target QAEnemy, select Spy slot 1.",
            "With 405 loaded, type ?cd stagger 8 and press slot 2. Expect [MODIFIER] source340 stat96 6.6 s and one matching removal. Only Stagger is prepared by the disposable fixture. Wait out 8 s reuse.",
            "Type ?qaunload 405 and check stat1042 +10 remove. Prepare and cast again for 6 s. After reuse ?qaload 405, prepare and cast for 6.6 s. The same unmodified target is used each time. No new immunity timer."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Home, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, Enemy, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Home, X, Y, Enemy), "Clear outdoor cast path");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.Equal(96, Loadout.Sum(AbilityMemory.Cost), "Both disciplines fit Archive memory");
        DisablingShotMode.CheckProfile(actor, Loadout, 50);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await ObserveAsync(actor, 0, frame => Bonus(frame, Spy, 1034, 29, 0, false), "Live Spy Style");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await MeasureAsync(6.6f);
        int off = await ChangeLoadoutAsync(actor, archive, new[] { Redirection }, false);
        await ObserveAsync(actor, off, frame => Bonus(frame, Redirection, 1042, 10, 0, true), "Powerless source removed");
        await MeasureAsync(6f);
        int on = await ChangeLoadoutAsync(actor, archive, new[] { Redirection }, true);
        await ObserveAsync(actor, on, frame => Bonus(frame, Redirection, 1042, 10, 0, false), "Powerless source restored");
        await MeasureAsync(6.6f);
        Console.WriteLine("REDIRECTION POWERLESS PASS: 6.6/6/6.6 s from the same ordinary cast.");
        return Array.Empty<byte>();

        async Task MeasureAsync(float duration)
        {
            actor.SendChat("?cd stagger 8");
            await actor.WaitForTextAsync("Stagger fixture applied");
            int recipient = enemy.World.RpcSnapshot().Count, caster = actor.World.RpcSnapshot().Count;
            actor.SendRpc(WithHandle(StockThroat, handle));
            await EvidenceRetestWire.CastBarAsync(actor, Throat, 1.5f);
            var add = await ObserveAsync(enemy, recipient, frame =>
                ManageBonusRecord.TryDecode(frame.Bytes, out var v) && v is { SourceId: Throat, TargetId: TempMods.PowerlessAbility, AddRemoveType: 0 } &&
                Math.Abs(v.Duration - duration) < .001f, "Exact Powerless duration");
            ManageBonusRecord.TryDecode(add.Bytes, out var record);
            await ObserveAsync(enemy, recipient, frame =>
                ManageBonusRecord.TryDecode(frame.Bytes, out var v) && v == record with { AddRemoveType = 1 }, "Matching Powerless removal");
            await EvidenceRetestWire.TimerAsync(actor, caster, Throat, AbilityCooldowns.OwnTimer, 8, ended: true);
        }
    }
}
