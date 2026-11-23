using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class LogicBlastThreeTargetMode : NativeQaMode
{
    private const ushort Blast = 329, Style = CombatStyles.HackerStyle;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, AcrossWallX = 29150;
    internal const int Resistance = 75;
    // Stock AfterWhoruNeo proxy_003, lines 60–61. Only the selected view/spawn is rebound.
    private const string StockBlast = "80b949011200bb0000000000000000000000000000000000000000004cf212001a76377c086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Blast).Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, 8).Order().ToArray();
    private static readonly (string Handle, uint Id, int X, int Z, int Alignment)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 300, 2), ("QAFar", 6112, X + 125, Z + 300, 2),
        ("QAFriend", 6111, X + 150, Z, 1), ("QAOther", 6113, X, Z + 2001, 2)
    };

    public override string Name => "ability-parity-logic-blast-three-target";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=5000,healthM=5000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,innerStrC=700,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Defender.Select((id, i) =>
            $"(6112,{i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Alignment} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Blast }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "Page 2: slot 1 Hacker Style, slot 2 Logic Blast 3.0. Start at READY HOME facing QAEnemy 3 m ahead; no teleport needed unless displaced. Select slot 1. Press slot 2 with target cleared, /target QAFriend, then /target QAOther (20.01 m). Expect client suppression or invalid-target/out-of-range refusal, with no cast, IS debit or timer.",
            "Use /target QAEnemy and slot 2. Keep the first 80 b9 49 01 frame. Expect a 2 s cast, 11 IS, 4 s own and 4 s family 682 reuse, a [PVP] hit on QAEnemy alone, and the retained hit FX. Damage rolls within the retained 367–619 base band before profile influence.",
            """After reuse, /target QAFar and submit {"actor":"QAFar","action":"aura-resist"}. Expect [MODIFIER] char=6112 source=8 stat=157 value=75 (20 s). Cast slot 2 again. Expect [TYPED-RESISTANCE OURS] 329 type2 total75 rawN finalM with M below N, [PVP] hit for M on QAFar only, and the same cost and reuse.""",
            "After reuse, submit the printed WALL TARGET helper request and move CSR using READY WALL. /target QAOther and try slot 2 through the solid office wall, 5 m across. Expect client suppression or the sight refusal, with no cast, IS debit or reuse. Restore READY HOME afterwards."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "329", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Logic Blast requires NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Logic Blast profile fits memory");
        foreach (var stand in Stands)
        {
            InvisibilityBeltWatcherMode.Warp(stand.X, Y, stand.Z, 0, inside: false);
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, stand.X, Y, stand.Z), $"Outdoor sight to {stand.Handle}");
        }
        string wallTarget = InvisibilityBeltWatcherMode.Warp(AcrossWallX, WallY, WallZ, 192, inside: true);
        Check.That(!NavMesh.HasSightPath(1, WallX, WallY, WallZ, AcrossWallX, WallY, WallZ), "Solid office wall blocks Logic Blast");
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY WALL {InvisibilityBeltWatcherMode.Warp(WallX, WallY, WallZ, 64, inside: true)}");
        Console.WriteLine($$"""WALL TARGET {"actor":"QAOther","action":"chat","text":"{{wallTarget}}"}""");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable fixture server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var enemy = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAFriend");
        var far = await actor.WaitForViewNamedAsync("QAOther");
        var resisted = await actor.WaitForViewNamedAsync("QAFar");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Style, 172, 27, 0, false), "Hacker Style ready");
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, Blast, StockBlast, enemy, ally);
        await RefusalAsync(far, AbilityRefusals.TargetOutOfRange);
        await CastAsync(enemy, "QAEnemy");
        await Task.Delay(TimeSpan.FromSeconds(4.2));

        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(context.Helpers["QAFar"], Resistance);
        int resistStart = server.DebugLog().Length;
        int hit = await CastAsync(resisted, "QAFar");
        var reduction = Regex.Match(server.DebugLog()[resistStart..], @"329 type2 total75 raw(\d+) final(\d+)");
        Check.That(reduction.Success, "Logic Blast uses the recipient's typed Viral resistance");
        int raw = int.Parse(reduction.Groups[1].Value);
        Check.Equal((int)TypedAbilityResistance.Amount(raw, Level, Level, Resistance), hit, "Exact normalized resistance");
        Check.Equal(hit, int.Parse(reduction.Groups[2].Value), "Reported damage matches the recipient's reduction");

        await Task.Delay(TimeSpan.FromSeconds(4.2));
        await InvisibilityBeltWatcherMode.WarpAsync(context.Helpers["QAOther"], AcrossWallX, WallY, WallZ, 192, inside: true);
        await InvisibilityBeltWatcherMode.WarpAsync(actor, WallX, WallY, WallZ, 64, inside: true);
        await RefusalAsync(await actor.WaitForViewNamedAsync("QAOther"), AbilityRefusals.TargetNotInSight);
        return Array.Empty<byte>();

        async Task RefusalAsync(ViewCreation target, uint reason)
        {
            int start = actor.World.RpcSnapshot().Count;
            Send(target);
            await StockAbilityProfile.ObserveAsync(actor, start, packet => AbilityRefusals.TryDecode(packet.Bytes, out var denial) &&
                denial.AbilityID == Blast && denial.ErrorID == reason, $"Logic Blast admission refusal {reason}");
        }

        async Task<int> CastAsync(ViewCreation target, string name)
        {
            int start = actor.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            Send(target);
            await EvidenceRetestWire.CastBarAsync(actor, Blast, 2);
            await EvidenceRetestWire.TimerAsync(actor, start, Blast, AbilityCooldowns.OwnTimer, 4);
            await EvidenceRetestWire.TimerAsync(actor, start, AbilityCooldowns.VirusDamage1, AbilityCooldowns.GroupTimer, 4);
            var landed = new Regex($@"\[PVP\] CSR_Tester's .* lands on {name} for (\d+)");
            await actor.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()[logStart..]), $"Logic Blast lands on {name}", TimeSpan.FromSeconds(8));
            return int.Parse(landed.Match(server.DebugLog()[logStart..]).Groups[1].Value);
        }

        void Send(ViewCreation view) =>
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockBlast, DuelPackets.Handle(view.ViewId, view.SpawnId)));
    }
}
